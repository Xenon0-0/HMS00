using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Windows.Forms;
using iText.Html2pdf;
using iText.Kernel.Pdf;
using Microsoft.Data.SqlClient;

namespace SimpleHMS
{
    public partial class BillingForm : Form
    {
        // Fields to track current bill and patient
        private int currentBillId = 0;
        private int currentPatientId = 0;
        private string patientEmail = "";
        private DataTable billItemsTable;

        public BillingForm()
        {
            InitializeComponent();
            InitializeBillItemsTable();
        }

        /// <summary>
        /// Initialize the DataTable for bill items
        /// </summary>
        private void InitializeBillItemsTable()
        {
            billItemsTable = new DataTable();
            billItemsTable.Columns.Add("ItemName", typeof(string));
            billItemsTable.Columns.Add("Price", typeof(decimal));
            billItemsTable.Columns.Add("Quantity", typeof(int));
            billItemsTable.Columns.Add("Total", typeof(decimal));
            
            dgvBillItems.DataSource = billItemsTable;
        }

        /// <summary>
        /// Load event for the BillingForm
        /// </summary>
        private void BillingForm_Load(object sender, EventArgs e)
        {
            try
            {
                LoadPatients();
                LoadDoctors();
                LoadBills();
                ClearBillItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading form data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Load patients from the database into the patient combo box
        /// </summary>
        private void LoadPatients()
        {
            try
            {
                string query = "SELECT PatientID, Name + ' (' + Phone + ')' AS PatientInfo, Phone FROM Patients";
                DataTable dt = DB.GetData(query);
                
                cmbPatient.DataSource = dt;
                cmbPatient.DisplayMember = "PatientInfo";
                cmbPatient.ValueMember = "PatientID";
                
                // Store patient phone for later use
                if (dt.Rows.Count > 0)
                {
                    currentPatientId = Convert.ToInt32(dt.Rows[0]["PatientID"]);
                    patientEmail = dt.Rows[0]["Phone"].ToString();
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load patients: {ex.Message}");
            }
        }

        /// <summary>
        /// Load doctors from the database into the doctor combo box
        /// </summary>
        private void LoadDoctors()
        {
            try
            {
                string query = "SELECT DoctorID, Name + ' (' + Specialization + ')' AS DoctorInfo FROM Doctors";
                DataTable dt = DB.GetData(query);
                
                cmbDoctor.DataSource = dt;
                cmbDoctor.DisplayMember = "DoctorInfo";
                cmbDoctor.ValueMember = "DoctorID";
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load doctors: {ex.Message}");
            }
        }

        /// <summary>
        /// Load bills from the database into the bills data grid view
        /// </summary>
        private void LoadBills()
        {
            try
            {
                string query = @"
                    SELECT b.BillID, p.Name AS Patient, d.Name AS Doctor, 
                           b.BillDate, SUM(bi.TotalPrice) AS TotalAmount
                    FROM Bills b
                    JOIN Patients p ON b.PatientID = p.PatientID
                    JOIN Doctors d ON b.DoctorID = d.DoctorID
                    JOIN BillItems bi ON b.BillID = bi.BillID
                    GROUP BY b.BillID, p.Name, d.Name, b.BillDate
                    ORDER BY b.BillDate DESC";
                
                DataTable dt = DB.GetData(query);
                dgvBills.DataSource = dt;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load bills: {ex.Message}");
            }
        }

        /// <summary>
        /// Load bill items for a specific bill
        /// </summary>
        private void LoadBillItems(int billId)
        {
            try
            {
                string query = $"SELECT ItemName, UnitPrice, Quantity, TotalPrice AS Total FROM BillItems WHERE BillID = {billId}";
                DataTable dt = DB.GetData(query);
                
                // Clear existing items
                billItemsTable.Rows.Clear();
                
                // Add items from database
                foreach (DataRow row in dt.Rows)
                {
                    billItemsTable.Rows.Add(
                        row["ItemName"],
                        row["UnitPrice"],
                        row["Quantity"],
                        row["Total"]
                    );
                }
                
                CalculateTotalAmount();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load bill items: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear bill items and reset form
        /// </summary>
        private void ClearBillItems()
        {
            billItemsTable.Rows.Clear();
            txtItemName.Clear();
            txtItemPrice.Clear();
            txtQuantity.Text = "1";
            txtTotalAmount.Text = "0.00";
            currentBillId = 0;
        }

        /// <summary>
        /// Calculate the total amount of all bill items
        /// </summary>
        private void CalculateTotalAmount()
        {
            decimal total = 0;
            
            foreach (DataRow row in billItemsTable.Rows)
            {
                total += Convert.ToDecimal(row["Total"]);
            }
            
            txtTotalAmount.Text = total.ToString("0.00");
        }

        /// <summary>
        /// Event handler for the New Bill button
        /// </summary>
        private void btnNewBill_Click(object sender, EventArgs e)
        {
            try
            {
                ClearBillItems();
                dtpBillDate.Value = DateTime.Now;
                currentBillId = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating new bill: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Event handler for the Save Bill button
        /// </summary>
        private void btnSaveBill_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate bill has items
                if (billItemsTable.Rows.Count == 0)
                {
                    MessageBox.Show("Please add at least one item to the bill.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Get selected patient and doctor
                int patientId = Convert.ToInt32(cmbPatient.SelectedValue);
                int doctorId = Convert.ToInt32(cmbDoctor.SelectedValue);
                
                // Format date for SQL
                string billDate = dtpBillDate.Value.ToString("yyyy-MM-dd");
                
                // Begin transaction
                using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["HMSDB"].ConnectionString))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();
                    
                    try
                    {
                        // Insert or update bill
                        string billQuery;
                        if (currentBillId == 0)
                        {
                            // Insert new bill
                            billQuery = $"INSERT INTO Bills (PatientID, DoctorID, BillDate) VALUES ({patientId}, {doctorId}, '{billDate}'); SELECT SCOPE_IDENTITY();";
                            
                            // Execute query and get new bill ID
                            SqlCommand cmd = new SqlCommand(billQuery, conn, transaction);
                            currentBillId = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        else
                        {
                            // Update existing bill
                            billQuery = $"UPDATE Bills SET PatientID = {patientId}, DoctorID = {doctorId}, BillDate = '{billDate}' WHERE BillID = {currentBillId}";
                            
                            // Execute update query
                            SqlCommand cmd = new SqlCommand(billQuery, conn, transaction);
                            cmd.ExecuteNonQuery();
                            
                            // Delete existing bill items
                            string deleteItemsQuery = $"DELETE FROM BillItems WHERE BillID = {currentBillId}";
                            SqlCommand deleteCmd = new SqlCommand(deleteItemsQuery, conn, transaction);
                            deleteCmd.ExecuteNonQuery();
                        }
                        
                        // Insert bill items
                        foreach (DataRow row in billItemsTable.Rows)
                        {
                            string itemName = row["ItemName"].ToString();
                            decimal itemPrice = Convert.ToDecimal(row["Price"]);
                            int itemQuantity = Convert.ToInt32(row["Quantity"]);
                            
                            string itemQuery = $"INSERT INTO BillItems (BillID, ItemName, ItemType, UnitPrice, Quantity, TotalPrice) VALUES ({currentBillId}, '{itemName}', 'Service', {itemPrice}, {itemQuantity}, {itemPrice * itemQuantity})";
                            SqlCommand itemCmd = new SqlCommand(itemQuery, conn, transaction);
                            itemCmd.ExecuteNonQuery();
                        }
                        
                        // Commit transaction
                        transaction.Commit();
                        
                        MessageBox.Show("Bill saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        // Refresh bills list
                        LoadBills();
                    }
                    catch (Exception ex)
                    {
                        // Rollback transaction on error
                        transaction.Rollback();
                        throw new Exception($"Failed to save bill: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving bill: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Event handler for the Add Item button
        /// </summary>
        private void btnAddItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(txtItemName.Text))
                {
                    MessageBox.Show("Please enter an item name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                if (!decimal.TryParse(txtItemPrice.Text, out decimal price) || price <= 0)
                {
                    MessageBox.Show("Please enter a valid price.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                if (!int.TryParse(txtQuantity.Text, out int quantity) || quantity <= 0)
                {
                    MessageBox.Show("Please enter a valid quantity.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Calculate total
                decimal total = price * quantity;
                
                // Add to bill items table
                billItemsTable.Rows.Add(txtItemName.Text, price, quantity, total);
                
                // Clear inputs
                txtItemName.Clear();
                txtItemPrice.Clear();
                txtQuantity.Text = "1";
                
                // Update total amount
                CalculateTotalAmount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Event handler for the Remove Item button
        /// </summary>
        private void btnRemoveItem_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if an item is selected
                if (dgvBillItems.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Please select an item to remove.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Remove selected item
                int selectedIndex = dgvBillItems.SelectedRows[0].Index;
                billItemsTable.Rows.RemoveAt(selectedIndex);
                
                // Update total amount
                CalculateTotalAmount();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Event handler for the Delete Bill button
        /// </summary>
        private void btnDeleteBill_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if a bill is selected
                if (currentBillId == 0)
                {
                    MessageBox.Show("Please select a bill to delete.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Confirm deletion
                DialogResult result = MessageBox.Show("Are you sure you want to delete this bill?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    // Delete bill items first (foreign key constraint)
                    string deleteItemsQuery = $"DELETE FROM BillItems WHERE BillID = {currentBillId}";
                    DB.SetData(deleteItemsQuery);
                    
                    // Delete bill
                    string deleteBillQuery = $"DELETE FROM Bills WHERE BillID = {currentBillId}";
                    DB.SetData(deleteBillQuery);
                    
                    MessageBox.Show("Bill deleted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Clear form and refresh bills
                    ClearBillItems();
                    LoadBills();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting bill: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Event handler for the Bills DataGridView cell click
        /// </summary>
        private void dgvBills_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex >= 0)
                {
                    // Get selected bill ID
                    currentBillId = Convert.ToInt32(dgvBills.Rows[e.RowIndex].Cells["BillID"].Value);
                    
                    // Load bill details
                    string query = $"SELECT * FROM Bills WHERE BillID = {currentBillId}";
                    DataTable dt = DB.GetData(query);
                    
                    if (dt.Rows.Count > 0)
                    {
                        // Set patient and doctor
                        int patId = Convert.ToInt32(dt.Rows[0]["PatientID"]);
                        int docId = Convert.ToInt32(dt.Rows[0]["DoctorID"]);
                        
                        cmbPatient.SelectedValue = patId;
                        cmbDoctor.SelectedValue = docId;
                        
                        // Set bill date
                        dtpBillDate.Value = Convert.ToDateTime(dt.Rows[0]["BillDate"]);
                        
                        // Get patient phone
                        string phoneQuery = $"SELECT Phone FROM Patients WHERE PatientID = {patId}";
                        DataTable phoneDt = DB.GetData(phoneQuery);
                        
                        if (phoneDt.Rows.Count > 0)
                        {
                            patientEmail = phoneDt.Rows[0]["Phone"].ToString();
                        }
                        
                        // Load bill items
                        LoadBillItems(currentBillId);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading bill details: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Event handler for the Generate PDF button
        /// </summary>
        private void btnGeneratePDF_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if a bill is selected
                if (currentBillId == 0)
                {
                    MessageBox.Show("Please select a bill to generate PDF.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Generate PDF receipt
                string pdfPath = GeneratePDFReceipt();
                
                // Open the PDF
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pdfPath,
                    UseShellExecute = true
                });
                
                MessageBox.Show($"PDF receipt generated successfully at: {pdfPath}", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating PDF: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Generate a PDF receipt for the current bill
        /// </summary>
        private string GeneratePDFReceipt()
        {
            try
            {
                // Get bill information
                string patientName = cmbPatient.Text.Split('(')[0].Trim();
                string doctorName = cmbDoctor.Text.Split('(')[0].Trim();
                string billDate = dtpBillDate.Value.ToString("yyyy-MM-dd");
                string totalAmount = txtTotalAmount.Text;
                
                // Create HTML content for the receipt
                string htmlContent = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Hospital Bill Receipt</title>
                    <style>
                        body {{
                            font-family: 'Segoe UI', Arial, sans-serif;
                            margin: 0;
                            padding: 20px;
                            color: #333;
                        }}
                        .header {{
                            text-align: center;
                            margin-bottom: 30px;
                        }}
                        .hospital-name {{
                            font-size: 24px;
                            font-weight: bold;
                            color: #2c3e50;
                            margin-bottom: 5px;
                        }}
                        .receipt-title {{
                            font-size: 18px;
                            color: #3498db;
                            margin-bottom: 20px;
                        }}
                        .info-section {{
                            margin-bottom: 20px;
                        }}
                        .info-row {{
                            display: flex;
                            margin-bottom: 10px;
                        }}
                        .info-label {{
                            font-weight: bold;
                            width: 150px;
                        }}
                        .info-value {{
                            flex: 1;
                        }}
                        table {{
                            width: 100%;
                            border-collapse: collapse;
                            margin-bottom: 20px;
                        }}
                        th, td {{
                            border: 1px solid #ddd;
                            padding: 10px;
                            text-align: left;
                        }}
                        th {{
                            background-color: #f2f2f2;
                        }}
                        .total-row {{
                            font-weight: bold;
                        }}
                        .footer {{
                            margin-top: 40px;
                            text-align: center;
                            font-size: 14px;
                            color: #7f8c8d;
                        }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <div class='hospital-name'>Simple Hospital Management System</div>
                        <div class='receipt-title'>BILL RECEIPT</div>
                    </div>
                    
                    <div class='info-section'>
                        <div class='info-row'>
                            <div class='info-label'>Receipt No:</div>
                            <div class='info-value'>BILL-{currentBillId}</div>
                        </div>
                        <div class='info-row'>
                            <div class='info-label'>Date:</div>
                            <div class='info-value'>{billDate}</div>
                        </div>
                        <div class='info-row'>
                            <div class='info-label'>Patient Name:</div>
                            <div class='info-value'>{patientName}</div>
                        </div>
                        <div class='info-row'>
                            <div class='info-label'>Doctor Name:</div>
                            <div class='info-value'>{doctorName}</div>
                        </div>
                    </div>
                    
                    <table>
                        <thead>
                            <tr>
                                <th>Item</th>
                                <th>Price</th>
                                <th>Quantity</th>
                                <th>Total</th>
                            </tr>
                        </thead>
                        <tbody>";
                
                    // Add bill items to the table
                    foreach (DataRow row in billItemsTable.Rows)
                    {
                        htmlContent += $@"
                                <tr>
                                    <td>{row["ItemName"]}</td>
                                    <td>${Convert.ToDecimal(row["Price"]):0.00}</td>
                                    <td>{row["Quantity"]}</td>
                                    <td>${Convert.ToDecimal(row["Total"]):0.00}</td>
                                </tr>";
                    }
                    
                    // Add total row and close HTML
                    htmlContent += $@"
                        </tbody>
                        <tfoot>
                            <tr class='total-row'>
                                <td colspan='3' style='text-align: right;'>Total Amount:</td>
                                <td>${totalAmount}</td>
                            </tr>
                        </tfoot>
                    </table>
                    
                    <div class='footer'>
                        <p>Thank you for choosing our hospital. We wish you a speedy recovery!</p>
                        <p>For any queries, please contact us at: support@simplehospital.com</p>
                    </div>
                </body>
                </html>";
                
                // Create directory for receipts if it doesn't exist
                string receiptDirectory = Path.Combine(Application.StartupPath, "Receipts");
                if (!Directory.Exists(receiptDirectory))
                {
                    Directory.CreateDirectory(receiptDirectory);
                }
                
                // Create PDF file path
                string pdfFileName = $"Receipt_Bill_{currentBillId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                string pdfPath = Path.Combine(receiptDirectory, pdfFileName);
                
                // Convert HTML to PDF
                using (FileStream pdfDest = new FileStream(pdfPath, FileMode.Create))
                {
                    try
                    {
                        ConverterProperties converterProperties = new ConverterProperties();
                        // Create a temporary HTML file to ensure proper conversion
                        string tempHtmlPath = Path.Combine(Path.GetTempPath(), $"temp_bill_{currentBillId}.html");
                        File.WriteAllText(tempHtmlPath, htmlContent);
                        
                        // Convert from file instead of string
                        HtmlConverter.ConvertToPdf(new FileInfo(tempHtmlPath), new FileInfo(pdfPath));
                        
                        // Clean up temp file
                        if (File.Exists(tempHtmlPath))
                        {
                            File.Delete(tempHtmlPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"PDF conversion error: {ex.Message}", ex);
                    }
                }
                
                return pdfPath;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to generate PDF receipt: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for the Send Email button
        /// </summary>
        private void btnSendEmail_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if a bill is selected
                if (currentBillId == 0)
                {
                    MessageBox.Show("Please select a bill to send via email.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Check if patient has an email
                if (string.IsNullOrWhiteSpace(patientEmail))
                {
                    MessageBox.Show("The selected patient does not have an email address.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                
                // Generate PDF receipt
                string pdfPath = GeneratePDFReceipt();
                
                // Send email with PDF attachment
                SendEmailWithReceipt(pdfPath);
                
                MessageBox.Show($"Email sent successfully to {patientEmail}!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending email: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Send an email with the PDF receipt attachment
        /// </summary>
        private void SendEmailWithReceipt(string pdfPath)
        {
            try
            {
                // Verify the PDF file exists
                if (!File.Exists(pdfPath))
                {
                    throw new FileNotFoundException("PDF receipt file not found", pdfPath);
                }
                
                // Get email credentials from app settings
                // Note: In a real application, these should be stored securely
                string senderEmail = "hospital@example.com"; // Replace with actual email
                string senderPassword = "password123"; // Replace with actual password
                string smtpServer = "smtp.example.com"; // Replace with actual SMTP server
                int smtpPort = 587; // Replace with actual SMTP port
                
                // Create mail message
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(senderEmail);
                mail.To.Add(patientEmail);
                mail.Subject = $"Hospital Bill Receipt - {DateTime.Now:yyyy-MM-dd}";
                mail.Body = $"Dear {cmbPatient.Text.Split('(')[0].Trim()},\n\nPlease find attached your hospital bill receipt.\n\nThank you for choosing our hospital.\n\nBest regards,\nSimple Hospital Management System";
                
                // Add PDF attachment with proper content type
                Attachment attachment = new Attachment(pdfPath, "application/pdf");
                mail.Attachments.Add(attachment);
                
                // Configure SMTP client
                SmtpClient smtpClient = new SmtpClient(smtpServer);
                smtpClient.Port = smtpPort;
                smtpClient.Credentials = new NetworkCredential(senderEmail, senderPassword);
                smtpClient.EnableSsl = true;
                
                // Send email
                smtpClient.Send(mail);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to send email: {ex.Message}");
            }
        }
    }
}