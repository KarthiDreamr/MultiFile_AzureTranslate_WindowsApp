using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel;
using System.Drawing;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NPOI.XWPF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Org.BouncyCastle.Bcpg;
using MathNet.Numerics;
using NPOI.SS.Formula.Functions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using Windows.Storage.Pickers;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using DocumentFormat.OpenXml.Office2010.Ink;
using DocumentFormat.OpenXml.Spreadsheet;
using Azure;


namespace BOSCH_KCT_StringTranslator
{
    public sealed partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
        }
        private static void ExecuteQuery(string queryString, string connectionString)
        {
            using SqlConnection connection = new(connectionString);
            SqlCommand command = new(queryString, connection);
            connection.Open();

            using SqlDataReader reader = command.ExecuteReader();
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

        private async void ExportFile(object sender, RoutedEventArgs e)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            savePicker.FileTypeChoices.Add("Excel", new List<string>() { ".xlsx" });
            savePicker.SuggestedFileName = "TestFile_Translated";

            Windows.Storage.StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                // Prevent updates to the remote version of the file until we finish making changes and call CompleteUpdatesAsync.
                Windows.Storage.CachedFileManager.DeferUpdates(file);
                // write to file
                using (var fileStream = await file.OpenStreamForWriteAsync())
                {

                    List<string> sourceList = new();
                    List<string> destinationList = new();
                    IWorkbook workbook = new XSSFWorkbook();
                    workbook.Write(fileStream);
                }

                // Let Windows know that we're finished changing the file so the other app can update the remote version of the file.
                Windows.Storage.Provider.FileUpdateStatus status = await Windows.Storage.CachedFileManager.CompleteUpdatesAsync(file);
                if (status == Windows.Storage.Provider.FileUpdateStatus.Complete)
                {
                    System.Diagnostics.Debug.WriteLine("File " + file.Name + " was saved.");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("File " + file.Name + " couldn't be saved.");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Operation cancelled.");
            }
        }

        public async void TranslateText(object sender, RoutedEventArgs e)
        {
            if (SourceComboBox.SelectedItem != null && DestinationComboBox.SelectedItem != null && SourceTextBox.Text != null)
            {
                string SourceLanguage = ((ComboBoxItem)SourceComboBox.SelectedItem)?.Tag?.ToString() ?? SourceComboBox.Items[2].ToString();
                string DestinationLanguage = ((ComboBoxItem)DestinationComboBox.SelectedItem)?.Tag?.ToString() ?? DestinationComboBox.Items[2].ToString();
                string textToTranslate = SourceTextBox.Text;

                string textValue = await TranslateString(textToTranslate);

                DestinationTextBox.Text = textValue;
            }
            else
            {
                // Handle the case where the variables are null
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "One or more required fields are null.",
                    CloseButtonText = "Ok"
                };

                await dialog.ShowAsync();
            }
        }

        private static readonly string key = "547af7f18d404062a17aa0636811691c";
        private static readonly string endpoint = "https://api.cognitive.microsofttranslator.com";
        private static readonly string location = "global";

        public void DatabaseCreate()
        {

            string connectionString = "Data Source=(local);Initial Catalog=master;Integrated Security=SSPI;Encrypt=false";

            using SqlConnection connection = new(connectionString);

            connection.Open();

            // Check if the table exists
            string checkTableQuery = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'TXT01_TRANSLATED_TEXTS')
                CREATE TABLE TXT01_TRANSLATED_TEXTS
                (
                    TXT01_TRANSLATION_TEXT_ID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TXT01_TEXT_ID INT NOT NULL,
                    TXT01_LANG_ID VARCHAR(20) NOT NULL,
                    TXT01_TEXT NVARCHAR(3000) NOT NULL,
                    TXT01_CREATED_DATE DATETIME NOT NULL,
                    CONSTRAINT UC_Id_Language UNIQUE (TXT01_TEXT_ID, TXT01_LANG_ID)
                );
            ";

            using SqlCommand checkTableCommand = new(checkTableQuery, connection);
            checkTableCommand.ExecuteNonQuery();
        }

        public async Task<string> TranslateString(string stringValue)
        {
            string sourceLanguage = ((ComboBoxItem)SourceComboBox.SelectedItem)?.Tag?.ToString() ?? SourceComboBox.Items[2].ToString();

            string destinationLanguage = ((ComboBoxItem)DestinationComboBox.SelectedItem)?.Tag?.ToString() ?? DestinationComboBox.Items[2].ToString();

            try
            {

                // Trim the empty space
                stringValue = stringValue.Trim();

                // Remove any <p> tags.
                stringValue = Regex.Replace(stringValue, @"<p>", "");

                // Remove any </p> tags.
                stringValue = Regex.Replace(stringValue, @"</p>", "");

                // Trim the empty space
                stringValue = stringValue.Trim();

                DatabaseCreate();

                string CheckedTranslation = CheckTranslationExists(stringValue,sourceLanguage, destinationLanguage);

                if (!CheckedTranslation.Equals("-1") && !CheckedTranslation.StartsWith("{"))
                {
                    Debug.WriteLine("Miracle Happened");
                    return CheckedTranslation;                   
                }                
                else
                {
                    string route;
                    // The route for the translation API after 

                    if (sourceLanguage == "")
                    {
                        route = "/translate?api-version=3.0" + "&to=" + destinationLanguage;
                    }
                    else
                    {
                        route = "/translate?api-version=3.0&from=" + sourceLanguage + "&to=" + destinationLanguage;
                    }

                    // The body of the request
                    object[] body = new object[] { new { Text = stringValue } };
                    var requestBody = JsonConvert.SerializeObject(body);

                    // Create a new HTTP client and request
                    using var client = new HttpClient();
                    using var request = new HttpRequestMessage();
                    // Set the method, URI, content, and headers of the request
                    request.Method = HttpMethod.Post;
                    request.RequestUri = new Uri(endpoint + route);
                    request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    request.Headers.Add("Ocp-Apim-Subscription-Key", key);
                    request.Headers.Add("Ocp-Apim-Subscription-Region", location);

                    // Send the request and get the response
                    HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);


                    string result = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrEmpty(result))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            // Parse the JSON array and get the translated text
                            JArray jsonArray = JArray.Parse(result);
                            string translatedText = jsonArray[0]["translations"][0]["text"].ToString();

                            if (CheckedTranslation.StartsWith("{"))
                            {

                                Debug.WriteLine("(CheckedTranslation.StartsWith(\"{\")");
                                // Parse the textId from the CheckedTranslation string
                                int textId = int.Parse(CheckedTranslation.TrimStart('{').TrimEnd('}'));
                                // Add only the destination string to the database
                                AddTranslation(sourceLanguage, destinationLanguage, stringValue, translatedText, textId);
                            }
                            else
                            {
                                Debug.WriteLine(" Else (CheckedTranslation.StartsWith(\"{\")");
                                AddTranslation(sourceLanguage, destinationLanguage, stringValue, translatedText, -1);
                            }


                            // Return the translated text
                            return translatedText;

                        }
                        else
                        {
                            // Parse the JSON object and get the error message
                            JObject jsonObject = JObject.Parse(result);
                            string errorMessage = jsonObject["error"]["message"].ToString();

                            Debug.WriteLine("An error occurred: " + errorMessage);
                            return stringValue;
                        }
                    }
                    else
                    {
                        Debug.WriteLine("The response is empty.");
                        return stringValue;
                    }
                }
            }

            catch (Exception ex)
            {
                // Handle the exception
                Debug.WriteLine("An error occurred: " + ex.Message);
                return stringValue;
            }
        }

        public string CheckTranslationExists(string stringValue, string sourceLanguage, string destinationLanguage)
        {
            string connectionString = "Data Source=(local);Initial Catalog=master;Integrated Security=SSPI;Encrypt=false";

            using SqlConnection connection = new(connectionString);
            connection.Open();

            string query = @"
                DECLARE @textId INT;
                SELECT @textId = TXT01_TEXT_ID
                FROM TXT01_TRANSLATED_TEXTS
                WHERE TXT01_TEXT = @stringValue AND TXT01_LANG_ID = @sourceLanguage;

                IF @textId IS NULL
                BEGIN
                    SELECT '-1' AS TXT01_TEXT;
                END
                ELSE IF EXISTS (
                    SELECT 1
                    FROM TXT01_TRANSLATED_TEXTS
                    WHERE TXT01_TEXT_ID = @textId AND TXT01_LANG_ID = @destinationLanguage
                )
                BEGIN
                    SELECT TXT01_TEXT
                    FROM TXT01_TRANSLATED_TEXTS
                    WHERE TXT01_TEXT_ID = @textId AND TXT01_LANG_ID = @destinationLanguage;
                END
                ELSE
                BEGIN
                    SELECT '{' + CAST(@textId AS VARCHAR(10)) + '}' AS TXT01_TEXT;
                END
                ";

            using SqlCommand command = new(query, connection);
            command.Parameters.AddWithValue("@stringValue", stringValue);
            command.Parameters.AddWithValue("@sourceLanguage", sourceLanguage);
            command.Parameters.AddWithValue("@destinationLanguage", destinationLanguage);

            object result = command.ExecuteScalar();
            return Convert.ToString(result);
        }


        public void AddTranslation(String sourceLanguage, String destinationLanguage, String originalText, String translatedText, int textId)
        {

            string connectionString = "Data Source=(local);Initial Catalog=master;Integrated Security=SSPI;Encrypt=false";

            using SqlConnection connection = new(connectionString);
            connection.Open();

            // Check if the TextIdSequence exists
            string checkTextIdSequenceQuery = @"
                IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name = 'TextIdSequence')
                BEGIN
                    CREATE SEQUENCE dbo.TextIdSequence
                        AS INT
                        START WITH 1
                        INCREMENT BY 1;
                END
            ";

            using SqlCommand checkTextIdSequenceCommand = new(checkTextIdSequenceQuery, connection);
            checkTextIdSequenceCommand.ExecuteNonQuery();

            if ( textId != -1 )
            {
                               // Add the destination language text to the database
                string insertDestinationQuery = @"
                INSERT INTO TXT01_TRANSLATED_TEXTS (TXT01_TEXT_ID, TXT01_LANG_ID, TXT01_TEXT, TXT01_CREATED_DATE)
                VALUES (@texts_id, @language_id, @txt, GETDATE()); ";
                using SqlCommand insertDestinationCommand = new(insertDestinationQuery, connection);
                insertDestinationCommand.Parameters.AddWithValue("@texts_id", textId);
                insertDestinationCommand.Parameters.AddWithValue("@language_id", destinationLanguage);
                insertDestinationCommand.Parameters.AddWithValue("@txt", translatedText);
                insertDestinationCommand.ExecuteNonQuery();
            }
            else
            {
                // Get the next value from the sequence
                string sequenceQuery = "SELECT NEXT VALUE FOR dbo.TextIdSequence;";
                using SqlCommand sequenceCommand = new(sequenceQuery, connection);
                textId = (int)sequenceCommand.ExecuteScalar();

                // Add the source language text to the database
                string insertSourceQuery = @"
                INSERT INTO TXT01_TRANSLATED_TEXTS (TXT01_TEXT_ID, TXT01_LANG_ID, TXT01_TEXT, TXT01_CREATED_DATE)
                VALUES (@text_id, @lang_id, @text, GETDATE()); ";
                using SqlCommand insertSourceCommand = new(insertSourceQuery, connection);
                insertSourceCommand.Parameters.AddWithValue("@text_id", textId);
                insertSourceCommand.Parameters.AddWithValue("@lang_id", sourceLanguage);
                insertSourceCommand.Parameters.AddWithValue("@text", originalText);
                insertSourceCommand.ExecuteNonQuery();

                // Add the destination language text to the database
                string insertDestinationQuery = @"
                INSERT INTO TXT01_TRANSLATED_TEXTS (TXT01_TEXT_ID, TXT01_LANG_ID, TXT01_TEXT, TXT01_CREATED_DATE)
                VALUES (@texts_id, @language_id, @txt, GETDATE()); ";
                using SqlCommand insertDestinationCommand = new(insertDestinationQuery, connection);
                insertDestinationCommand.Parameters.AddWithValue("@texts_id", textId);
                insertDestinationCommand.Parameters.AddWithValue("@language_id", destinationLanguage);
                insertDestinationCommand.Parameters.AddWithValue("@txt", translatedText);
                insertDestinationCommand.ExecuteNonQuery();
            }          

        }

        public async void TypeHandler(object sender, RoutedEventArgs e)
        {
            var window = this;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            filePicker.FileTypeFilter.Add(".xlsx");
            filePicker.FileTypeFilter.Add(".docx");
            filePicker.FileTypeFilter.Add(".pdf");
            filePicker.FileTypeFilter.Add(".txt");
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);
            var storageFile = await filePicker.PickSingleFileAsync();


            if (storageFile != null)
            {

                /*
                var dialog = new ContentDialog
                {
                    Title = "File Upload",
                    Content = "File Upload Successful!",
                    CloseButtonText = "Ok"
                };

                await dialog.ShowAsync(); */

                UploadedFileName.Text = storageFile.Name;


                // Get the file extension
                string fileExtension = storageFile.FileType;

                // Handle different file types
                switch (fileExtension)
                {
                    case ".xlsx":
                        Debug.WriteLine("Excel file uploaded");
                        ExcelUpload(storageFile);
                        break;
                    case ".docx":
                        Debug.WriteLine("Word file uploaded");
                        WordUpload(storageFile);
                        break;
                    case ".txt":
                        Debug.WriteLine("Text file uploaded");
                        TextUpload(storageFile);
                        break;
                    case ".pdf":
                        Debug.WriteLine("Pdf file uploaded");
                        PdfUpload(storageFile);
                        break;
                    default:
                        Debug.WriteLine("Other type file uploaded");
                        break;
                        
                }
            }
        }

        private async void ExcelUpload(StorageFile storageFile)
        {      
            List<string> sourceList = new();
            List<string> destinationList = new();
            IWorkbook workbook = new XSSFWorkbook();

            string SourceLanguage, DestinationLanguage;

            // IWorkbook workbook = new XSSFWorkbook(rstream);
            ISheet sheet = workbook.GetSheetAt(0); // Get the first sheet

            using (Stream rstream = await storageFile.OpenStreamForReadAsync())
                {

                SourceLanguage = ((ComboBoxItem)SourceComboBox.SelectedItem)?.Tag?.ToString() ?? SourceComboBox.Items[2].ToString();

                DestinationLanguage = ((ComboBoxItem)DestinationComboBox.SelectedItem)?.Tag?.ToString() ?? DestinationComboBox.Items[2].ToString();

                // Iterate through each row in the sheet
                for (int i = 1; i <= sheet.LastRowNum; i++)
                    {
                        IRow row = sheet.GetRow(i);

                        if (row != null)
                        {
                            NPOI.SS.UserModel.ICell cell = row.GetCell(1); // Get the first cell in the row

                            if (cell != null)
                            {
                                string textToTranslate = cell.ToString();
                                sourceList.Add(textToTranslate);

                                string textValue = await TranslateString(textToTranslate);
                                destinationList.Add(textValue);

                                // Place the translated text in the next column
                                // ICell nextCell = row.GetCell(2) ?? row.CreateCell(2);
                                // nextCell.SetCellValue(textValue);

                                // Print the value of the cell
                                // System.Diagnostics.Debug.WriteLine(textValue);
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine("Source");
                for (int i = 0; i < sourceList.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(sourceList[i]);
                }

                System.Diagnostics.Debug.WriteLine("Destination");
                for (int i = 0; i < destinationList.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(destinationList[i]);
                }

                // Get the app's installed location.
                Windows.Storage.StorageFolder installedLocation = ApplicationData.Current.LocalFolder;

                Debug.WriteLine(installedLocation.Path.ToString());

                // Create a new FileStream object to write the file to.
                using (FileStream fs = new(installedLocation.Path + "\\TestFile_Copy.xlsx", FileMode.Create, FileAccess.ReadWrite))
                {

                    ISheet sheet1 = workbook.CreateSheet("Sheet1");

                    // Iterate through each row in the sheet
                    for (int i = 0; i < sourceList.Count; i++)
                    {
                        IRow row = sheet1.GetRow(i) ?? sheet1.CreateRow(i);

                        NPOI.SS.UserModel.ICell cell = row.GetCell(1) ?? row.CreateCell(1);

                        if (i == 0)
                        {
                            cell.SetCellValue(SourceLanguage);
                        }
                        else
                        {
                            cell.SetCellValue(sourceList[i]);
                            System.Diagnostics.Debug.WriteLine(sourceList[i]);
                        }
                    }

                    for (int i = 0; i < destinationList.Count; i++)
                    {
                        IRow row = sheet1.GetRow(i) ?? sheet1.CreateRow(i);

                        NPOI.SS.UserModel.ICell nextCell = row.GetCell(2) ?? row.CreateCell(2);

                        if (i == 0)
                        {
                            nextCell.SetCellValue(DestinationLanguage);
                        }
                        else
                        {
                            nextCell.SetCellValue(destinationList[i]);
                            System.Diagnostics.Debug.WriteLine(destinationList[i]);
                        }

                    }
                    workbook.Write(fs);
                }
            ExportFile(null, null);
        }
         

        private async void TextUpload( StorageFile storageFile)
        {                      
            // Read the input file
            string textToTranslate = await Windows.Storage.FileIO.ReadTextAsync(storageFile);

            // Translate the text (replace "en" and "fr" with the source and destination languages)
            string translatedText = await TranslateString(textToTranslate);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

            // Create a FileSavePicker to select the output file
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = "NewText_Translated";

            // Get the window handle and initialize the picker
            InitializeWithWindow.Initialize(savePicker,hwnd);

            // Pick the output file
            Windows.Storage.StorageFile outputFile = await savePicker.PickSaveFileAsync();
            if (outputFile != null)
            {
                // Write the translated text to the output file
                await Windows.Storage.FileIO.WriteTextAsync(outputFile, translatedText);
            }            

        }

        private async void WordUpload(StorageFile storageFile)
        {

            var picker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.List,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".docx");

            Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                using WordprocessingDocument doc = WordprocessingDocument.Open(file.Path, false);
                string text = doc.MainDocumentPart.Document.Body.InnerText;
                // Application now has read access to the picked file
                Debug.WriteLine("Word File Name is --> " + file.Name);
                Debug.WriteLine("Word Content is --> " + text);
            }
            else
            {
                // Operation cancelled.
            }

        }

        private async void PdfUpload(StorageFile storageFile)
        {

        }

    }
}
