using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace SchoolManagementSystem.Models
{
    public class CommonFn
    {
        public class Commonfnx
        {
            Connection conn = new Connection();
            public void Query(string query)
            {
                SqlCommand cmd = new SqlCommand(query, conn.Connect());
                cmd.ExecuteNonQuery();
                conn.Close();

            }

            public DataTable Fetch(string query)
            {
                SqlCommand cmd = new SqlCommand(query, conn.Connect());
                SqlDataAdapter sda = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                sda.Fill(dt);
                conn.Close();
                return dt;
            }
        }
    }
}