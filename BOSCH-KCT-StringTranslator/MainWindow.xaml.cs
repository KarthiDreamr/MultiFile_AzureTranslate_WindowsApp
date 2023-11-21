using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using System.Data.SqlClient;
using System.Windows;
using System.Data;
using Microsoft.Data.SqlClient;

namespace BOSCH_KCT_StringTranslator
{
    public sealed partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void myButton_Click(object sender, EventArgs e)
        {
            string connectionString = "Data Source=(local);Initial Catalog=master;Integrated Security=SSPI;Encrypt=false";
            string query = @"
            CREATE TABLE BOSCH_Translation_Table
            (
                id INT,
                language_id VARCHAR(10),
                string VARCHAR(255),
                CreatedDate DATETIME,
                UpdatedDate DATETIME
            );

            INSERT INTO BOSCH_Translation_Table (id, language_id, string, CreatedDate, UpdatedDate)
            VALUES
                (1, 'en', 'good morning', GETDATE(), GETDATE()),
                (1, 'fr', 'bonjour', GETDATE(), GETDATE()),
                (1, 'de', 'Guten Morgen', GETDATE(), GETDATE()),
                (2, 'en', 'good afternoon', GETDATE(), GETDATE()),
                (2, 'fr', 'bon après-midi', GETDATE(), GETDATE()),
                (2, 'de', 'Guten Tag', GETDATE(), GETDATE()),
                (3, 'en', 'good evening', GETDATE(), GETDATE()),
                (3, 'fr', 'bonsoir', GETDATE(), GETDATE()),
                (3, 'de', 'Guten Abend', GETDATE(), GETDATE());

            SELECT * FROM BOSCH_Translation_Table;";

            try
            {
                ExecuteQuery(query, connectionString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing query: {ex.Message}");
            }
        }

        private static void ExecuteQuery(string queryString, string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                SqlCommand command = new SqlCommand(queryString, connection);
                connection.Open();

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string languageId = reader.GetString(1);
                        string translation = reader.GetString(2);
                        DateTime createdDate = reader.GetDateTime(3);
                        DateTime updatedDate = reader.GetDateTime(4);

                        Console.WriteLine($"ID: {id}, Language: {languageId}, Translation: {translation}, Created: {createdDate}, Updated: {updatedDate}");
                    }
                }
            }
        }
    }
}
