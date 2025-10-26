using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SimpleHMS
{
    public partial class LoginForm : Form
    {
        private MacOSButton closeButton;
        private MacOSButton minimizeButton;
        private MacOSButton maximizeButton;

        public LoginForm()
        {
            InitializeComponent();
            AddMacOSButton(); // Add macOS buttons
        }

        private void AddMacOSButton()
        {
            // *** CUSTOMIZE BUTTON SIZE HERE ***
            int buttonSize = 14;    // Change this value (12, 14, 16, 18, etc.)

            // macOS exact positioning
            int yPosition = 23;      // Distance from top
            int startX = 25;         // Distance from left
            int spacing = 8;        // Space between buttons

            // Create Close button (Red)
            closeButton = new MacOSButton(MacOSButton.ButtonType.Close)
            {
                Location = new Point(startX, yPosition),
                Size = new Size(buttonSize, buttonSize)  // Custom size
            };
            closeButton.Click += (s, e) => this.Close();

            // Create Minimize button (Yellow)
            minimizeButton = new MacOSButton(MacOSButton.ButtonType.Minimize)
            {
                Location = new Point(startX + buttonSize + spacing, yPosition),
                Size = new Size(buttonSize, buttonSize)  // Custom size
            };
            minimizeButton.Click += (s, e) => this.WindowState = FormWindowState.Minimized;

            // Create Maximize button (Green)
            maximizeButton = new MacOSButton(MacOSButton.ButtonType.Maximize)
            {
                Location = new Point(startX + (buttonSize + spacing) * 2, yPosition),
                Size = new Size(buttonSize, buttonSize)  // Custom size
            };
            maximizeButton.Click += MaximizeButton_Click;

            // Add buttons to form
            this.Controls.Add(closeButton);
            this.Controls.Add(minimizeButton);
            this.Controls.Add(maximizeButton);

            // Bring to front to ensure visibility
            closeButton.BringToFront();
            minimizeButton.BringToFront();
            maximizeButton.BringToFront();
        }

        private void MaximizeButton_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
                this.WindowState = FormWindowState.Maximized;
            else
                this.WindowState = FormWindowState.Normal;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string eamil = txtemail.Text;
            string pass = txtpassword.Text;
            if (eamil == "admin" && pass == "admin")
            {
                MessageBox.Show("Welcome, Admin!", "Login Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
                MainForm mainForm = new();
                mainForm.Show();
                this.Hide();
            }
            else
            {
                MessageBox.Show("Invalid email or password. Please try again.", "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
        }

        private void button2_Click(object sender, EventArgs e)
        {
        }

        private void label2_Click(object sender, EventArgs e)
        {
        }

        private void label3_Click(object sender, EventArgs e)
        {
        }

        private void label4_Click(object sender, EventArgs e)
        {
        }

        private void button3_Click(object sender, EventArgs e)
        {
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
        }

        private void label5_Click(object sender, EventArgs e)
        {
        }

        private void pictureBox1_Click_1(object sender, EventArgs e)
        {
        }

        private void panel2_Paint(object sender, PaintEventArgs e)
        {
        }

        private void pictureBox1_Click_2(object sender, EventArgs e)
        {
        }

        private void panel2_Paint_1(object sender, PaintEventArgs e)
        {

        }

        private void pictureBox2_Click_1(object sender, EventArgs e)
        {

        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox4_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox5_Click(object sender, EventArgs e)
        {

        }

        private void pictureBox5_Click_1(object sender, EventArgs e)
        {

        }
    }
}