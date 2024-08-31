using System;
using System.Data;
using System.Data.SqlClient;

namespace SchoolManagementSystem.Models
{
    public class CommonFn
    {
        public class Commonfnx
        {
            Connection conn = new Connection();
            public void Query(string query)
            {
                try
                {
                    SqlCommand cmd = new SqlCommand(query, conn.Connect());
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            public DataTable Fetch(string query)
            {
                try
                {
                    SqlCommand cmd = new SqlCommand(query, conn.Connect());
                    SqlDataAdapter sda = new SqlDataAdapter(cmd);
                    DataTable dt = new DataTable();
                    sda.Fill(dt);
                    conn.Close();
                    return dt;
                }
                catch (Exception ex)
                {
                    throw;
                }
                
            }
        }
    }
}