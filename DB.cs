using System.Data;
using Microsoft.Data.SqlClient;
using System.Configuration;

namespace SimpleHMS
{
    // Database helper for .NET 9 — reads connection string from App.config
    public static class DB
    {
        private static readonly string conn =
            ConfigurationManager.ConnectionStrings["HMSDB"].ConnectionString;

        // SELECT queries
        public static DataTable GetData(string query)
        {
            DataTable dt = new DataTable();

            using (SqlConnection con = new SqlConnection(conn))
            using (SqlDataAdapter da = new SqlDataAdapter(query, con))
            {
                da.Fill(dt);
            }

            return dt;
        }

        // INSERT / UPDATE / DELETE queries
        public static int SetData(string query)
        {
            using (SqlConnection con = new SqlConnection(conn))
            using (SqlCommand cmd = new SqlCommand(query, con))
            {
                con.Open();
                return cmd.ExecuteNonQuery();
            }
        }
    }
}
