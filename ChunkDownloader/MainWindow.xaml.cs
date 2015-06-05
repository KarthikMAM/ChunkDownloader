using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace ChunkDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {//Default Variables
        static string SAVE_LOCATION_TEXT = "Enter the Save Location here";
        static string URL_TEXT = "Enter the URL here";
        static string PROXY_TEXT = "Enter the Proxy e.g: proxy.ssn.net:8080";
        static string CHUNK_SIZE_TEXT = "Limit Size";
        static string EMPTY_STRING = "";
        static int CHUNK_PART_SIZE = 256;

        string fileURL;
        string saveLocation;
        long chunkSize = 0;
        long fileSize, downloadedSize;
        HttpWebRequest dwnlReq;
        BackgroundWorker worker;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On clicking the download button, all the preliminary conditions are set for the download to start asynchronously
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            //If all the required fields are filled
            if (saveLocation != EMPTY_STRING && saveLocation != SAVE_LOCATION_TEXT && ChunkSize.Text != EMPTY_STRING && ChunkSize.Text != CHUNK_SIZE_TEXT && Url.Text != EMPTY_STRING && Url.Text != URL_TEXT)
            {
                //Creation of a new Worker for download process
                worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.WorkerSupportsCancellation = true;
                worker.ProgressChanged += worker_ProgressChanged;
                worker.RunWorkerCompleted += worker_RunWorkerCompleted;
                worker.DoWork += worker_DoWork;

                //Get the data regarding the download
                fileURL = Url.Text;
                saveLocation = SaveLocation.Text;
                chunkSize = (long)Convert.ToDouble(ChunkSize.Text);

                //Prepare for starting the download
                dwnlReq = (HttpWebRequest)WebRequest.Create(fileURL);
                try { File.Delete(saveLocation); }
                catch { }

                //Start the download process
                worker.RunWorkerAsync();
            }
            else
            {
                MessageBox.Show("Please Enter all the Details \n\t1. URL \n\t2. Save \n\t3.Limit", "Incomplete Fields");
            }
        }

        /// <summary>
        /// This is where the logic for the download is implemented
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            //Set the parameters for splitting the download into fragments
            fileSize = dwnlReq.GetResponse().ContentLength;
            downloadedSize = 0;

            //Iterate the chunk download
            while (downloadedSize < fileSize)
            {
                downloadChunk(downloadedSize, chunkSize);
            }

            worker.ReportProgress(100);
        }

        /// <summary>
        /// The logic for downloading and writing the chunks
        /// The chunks are downloaded as sub-chunks or parts of size 256 bytes
        /// </summary>
        /// <param name="startPos"></param>
        /// <param name="chunkSize"></param>
        void downloadChunk(long startPos, long chunkSize)
        {
            //Select the range to be requested for the download
            dwnlReq.AddRange(startPos, startPos + chunkSize);

            //Create the input and the output streams
            Stream dwnlRes = dwnlReq.GetResponse().GetResponseStream();
            FileStream dwnlStream = new FileStream(saveLocation, FileMode.Append, FileAccess.Write);

            //Download and write the chunk in parts each of size CHUNK_PART_SIZE 
            byte[] partData = new byte[CHUNK_PART_SIZE];
            int partlen;
            while ((partlen = dwnlRes.Read(partData, 0, CHUNK_PART_SIZE)) > 0)
            {
                downloadedSize += partlen;
                dwnlStream.Write(partData, 0, partlen);
                worker.ReportProgress(Convert.ToInt32((downloadedSize * 100) / fileSize));
            }

            dwnlStream.Close();
            dwnlRes.Close();
        }

        /// <summary>
        /// Displays the message that the download has completed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show("Download Completed", "Complete");
        }

        /// <summary>
        /// Used to update the progress bar with the download content value
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Progress.Value = e.ProgressPercentage;
        }

        /// <summary>
        /// Used to get the path for saving our downloaded file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.OverwritePrompt = true;
            saveDialog.ShowDialog();
            saveLocation = SaveLocation.Text = saveDialog.FileName;
        }

        /// <summary>
        /// Used to remove the default text and ease the wor of the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            switch (textBox.Name)
            {
                case "Url":
                    if (textBox.Text == URL_TEXT) { textBox.Text = EMPTY_STRING; }
                    break;
                case "SaveLocation":
                    if (textBox.Text == SAVE_LOCATION_TEXT) { textBox.Text = EMPTY_STRING; }
                    break;
                case "Proxy":
                    if (textBox.Text == PROXY_TEXT) { textBox.Text = EMPTY_STRING; }
                    break;
                case "ChunkSize":
                    if (textBox.Text == CHUNK_SIZE_TEXT) { textBox.Text = EMPTY_STRING; }
                    break;
            }
            textBox.FontStyle = FontStyles.Normal;
        }

        /// <summary>
        /// Used to test whether the file is allowed by the router to be downloaded in parts
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TestLink_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url.Text);
                request.AddRange(0, Convert.ToInt32(ChunkSize.Text));

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.Headers[HttpResponseHeader.AcceptRanges] == "bytes")
                {
                    MessageBox.Show("Download Supported");
                }
                else
                {
                    MessageBox.Show("Download Not Supported");
                }
            }
            catch
            {
                MessageBox.Show("Downloader Error");
            }
        }

        /// <summary>
        /// Used to test whether the file can be downloaded completely
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TestLink_Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url.Text);

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                if (response.Headers[HttpResponseHeader.AcceptRanges] == "bytes")
                {
                    MessageBox.Show("Download Supported");
                }
                else
                {
                    MessageBox.Show("Download Not Supported");
                }
            }
            catch
            {
                MessageBox.Show("Downloader Error");
            }
        }

        /*void startDownload()
        {
            //Set the parameters for splitting the download into fragments
            long actualSize = dwnlReq.GetResponse().ContentLength;
            long remainingSize = actualSize, startPosition = 0;

            //Iterate the chunk download
            while (remainingSize > 0)
            {
                long downloadedSize = downloadChunk(startPosition, chunkSize);
                startPosition += downloadedSize;
                remainingSize -= downloadedSize;
            }
        }

        long downloadChunk(long startPos, long chunkSize)
        {
            dwnlReq.AddRange(startPos, startPos + chunkSize);

            Stream dwnlRes = dwnlReq.GetResponse().GetResponseStream();
            FileStream dwnlStream = new FileStream(saveLocation, FileMode.Append, FileAccess.Write);

            byte[] partData = new byte[CHUNK_PART_SIZE];
            int partlen;
            long dwnlLen = 0;
            while ((partlen = dwnlRes.Read(partData, 0, CHUNK_PART_SIZE)) > 0)
            {
                dwnlLen += partlen;
                dwnlStream.Write(partData, 0, partlen);
            }

            dwnlStream.Close();
            dwnlRes.Close();

            return dwnlLen;
        }*/
    }
}
