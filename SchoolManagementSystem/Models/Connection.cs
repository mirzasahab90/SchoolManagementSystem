using System.Configuration;
using System.Data.SqlClient;

namespace SchoolManagementSystem.Models
{
    public class Connection
    {
        public string ConnectionString = ConfigurationManager.ConnectionStrings["SchoolCS"].ConnectionString.ToString();
        public SqlConnection conn = null;

        public Connection()
        {
            conn = new SqlConnection(ConnectionString);
        }

        public SqlConnection Connect()
        {
            if (conn != null)
            {
                conn?.Open();
            }

            return conn;
        }

        public void Close()
        {
            conn?.Close();
        }
    }
}