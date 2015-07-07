using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace ChunkDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Static fields
        static string SAVE_LOCATION_TEXT = "Enter the Save Location here";
        static string URL_TEXT = "Enter the URL here";
        static string CHUNK_SIZE_TEXT = "Limit Size";
        static string EMPTY_STRING = "";
        static int CHUNK_PART_SIZE = 512;
        static int DEFAULT_UNIT_SIZE_MB = 1048576;

        //Default store of text fields
        string fileURL;
        string saveLocation;
        long chunkSize = 0;

        //Download helpers and parameters
        long fileSize, downloadedSize;
        FileStream dwnlStream;
        Stream dwnlRes;
        BackgroundWorker worker;

        //Animation variables
        Storyboard progressStoryboard;
        DoubleAnimation propertyAnimation;

        /// <summary>
        /// Initializes the MainWindow and animation properties
        /// </summary>
        public MainWindow()
        {

            InitializeComponent();

            //Initialize the ProgressBar Animation requirements
            progressStoryboard = new Storyboard();
            propertyAnimation = new DoubleAnimation();
            progressStoryboard.Children.Add(propertyAnimation);
            Storyboard.SetTarget(progressStoryboard, Progress);
            Storyboard.SetTargetProperty(progressStoryboard, new PropertyPath(ProgressBar.ValueProperty));
        }

        /// <summary>
        /// Used to get the path for saving our downloaded file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            //Prepare the SaveAs Dialog Box
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.OverwritePrompt = true;

            //Get the download file name from the url
            saveDialog.FileName = Path.GetFileNameWithoutExtension(Url.Text);
            saveDialog.DefaultExt = Path.GetExtension(Url.Text);
            saveDialog.Filter = saveDialog.DefaultExt + " | *" + saveDialog.DefaultExt;

            //Get the saveAs location from the user
            saveDialog.ShowDialog();
            saveLocation = SaveLocation.Text = saveDialog.FileName;
            SaveLocation.FontStyle = FontStyles.Normal;
        }

        /// <summary>
        /// On clicking the download button, all the preliminary conditions are set for the download to start asynchronously
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            //If all the required fields are filled properly
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
                chunkSize = (long)Convert.ToDouble(ChunkSize.Text) * DEFAULT_UNIT_SIZE_MB;

                //Start the download process
                worker.RunWorkerAsync();

                //Disable the Ok button
                Ok.IsEnabled = false;
            }
            else
            {
                MessageBox.Show("All the text fields are mandatory.", "Incomplete Fields");
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
            fileSize = chunkSize;
            downloadedSize = 0;

            //Try to delete the files with the same name as the download
            try { File.Delete(saveLocation); }
            catch { }

            //Since the number of chunks are unknown try and download as many chunks as possible
            while (true)
            {
                //try and download each chunk of the file. If it fails release the resources.
                try { downloadChunk(downloadedSize, chunkSize); }
                catch (Exception ex)
                {
                    //Close the unclosed streams to avoid unnecessary holding of resources
                    dwnlRes.Close();
                    dwnlStream.Close();

                    //If there is any exception thrown check if the range was out-of-bounds
                    if (ex.Message == "The remote server returned an error: (416) Requested Range Not Satisfiable.")
                    {
                        //If so, the possibility that the download is complete is very high
                        //So stop the download and close the application
                        worker.ReportProgress(100);
                        MessageBox.Show("Download Completed \n  - File Name : " + Path.GetFileNameWithoutExtension(saveLocation) + " \n  - File Size    : " + ((float)downloadedSize / DEFAULT_UNIT_SIZE_MB).ToString("0.## MB"), "Download Complete");
                        break;
                    }
                    else
                    {
                        //If there is anyother error then the file is not downloaded completely
                        //So stop the download and roll-back the changes
                        MessageBox.Show(ex.Message, "Download Failed");
                        try { File.Delete(fileURL); }
                        catch { }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// The logic for downloading and writing the chunks.
        /// The chunks are downloaded as sub-chunks or parts of size CHUNK_PART_SIZE bytes.
        /// The errors must be handled in the calling function.
        /// </summary>
        /// <param name="startPos">Starting position of the chunk of download</param>
        /// <param name="chunkSize">The size of the download</param>
        void downloadChunk(long startPos, long chunkSize)
        {
            //Create a HttpWebRequest to the download server and
            //Select the range to be requested for the download (less than or equal to the chunk size)
            HttpWebRequest dwnlReq = (HttpWebRequest)WebRequest.Create(fileURL);
            dwnlReq.AddRange(startPos, startPos + chunkSize);
            dwnlReq.AllowAutoRedirect = true;

            //Create the input and the output streams
            //Input Stream : Downloaded response stream
            //Output Stream : The required file
            dwnlRes = dwnlReq.GetResponse().GetResponseStream();
            dwnlStream = new FileStream(saveLocation, FileMode.Append, FileAccess.Write);

            //Download and write the chunk in parts each of size CHUNK_PART_SIZE 
            byte[] partData = new byte[CHUNK_PART_SIZE];
            int partlen;
            while ((partlen = dwnlRes.Read(partData, 0, CHUNK_PART_SIZE)) > 0)
            {
                //write the downloaded data to the save location
                dwnlStream.Write(partData, 0, partlen);

                //Update the download parameters and the progress bar
                downloadedSize += partlen;
                if (downloadedSize >= fileSize) { fileSize += chunkSize; }
                worker.ReportProgress(Convert.ToInt32((downloadedSize * 100) / fileSize));
            }

            //Close the streams and return success
            dwnlStream.Close();
            dwnlRes.Close();
        }

        /// <summary>
        /// Used to update the progress bar with the download content value
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            //Stop and update the From and to values of the StoryBoardAnimation of the progress bar
            progressStoryboard.Stop();
            propertyAnimation.From = Progress.Value;
            propertyAnimation.To = e.ProgressPercentage;
            progressStoryboard.Begin();

            //Update the downloaded file size in the progress bar
            DownloadedSize.Text = "Downloaded : " + ((float)downloadedSize / DEFAULT_UNIT_SIZE_MB).ToString("0.## MB");
        }

        /// <summary>
        /// This is where the Window is restored to its original state
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //Reenable the ok button
            Ok.IsEnabled = true;

            //Reset the text field to their original contents
            Url.Text = URL_TEXT;
            SaveLocation.Text = SAVE_LOCATION_TEXT;
            ChunkSize.Text = CHUNK_SIZE_TEXT;
            Url.FontStyle = SaveLocation.FontStyle = ChunkSize.FontStyle = FontStyles.Italic;
        }

        /// <summary>
        /// Used to remove the default text and ease the work of the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            //Change the textbox field attributes to enable easy editing of the text
            TextBox textBox = sender as TextBox;
            if (textBox.Text == URL_TEXT || textBox.Text == SAVE_LOCATION_TEXT || textBox.Text == CHUNK_SIZE_TEXT) { textBox.Text = EMPTY_STRING; }
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
                //Create request to download one chunk of data
                //By applying a range to it
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Url.Text);
                request.AddRange(0, Convert.ToInt32(ChunkSize.Text) * DEFAULT_UNIT_SIZE_MB);

                //Get the response from the server
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                //Check to see if the AcceptRanges field is "bytes"
                //If so the downloads are byte addressed and can be downloaded by this downloader
                if (response.Headers[HttpResponseHeader.AcceptRanges] == "bytes") { MessageBox.Show("Download Supported"); }
                else { MessageBox.Show("Download Not Supported"); }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Network Error");
            }
        }

        /// <summary>
        /// This is used to create a new downloader window for another download
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void New_Click(object sender, RoutedEventArgs e)
        {
            //Creates a new Window
            new MainWindow().Show();
        }
    }
}