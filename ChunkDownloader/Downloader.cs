using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows;

namespace ChunkDownloader
{
    /// <summary>
    /// Implementation of the downloader module
    /// </summary>
    class Downloader
    {
        //Units in bytes
        public const int MEGA_BYTE = 1048576;
        public const int KILO_BYTE = 1024;
        public const int CHUNK_BUFFER = 512;

        //Sources and Destination
        private string SourceURL { get; set; }
        private string DestinationPath { get; set; }

        //Data for tracking the download progress and status
        private enum DownloadChunkResults { DownloadComplete, ContinueDownload, AbortDownload, DownloadError }
        public long DownloadedSize { get; private set; }
        public int TempPercentage { get; private set; }
        private long TempFileSize { get; set; }
        private int MaxLimitSize { get; set; }
        private bool abortFlag;

        //Data for tracking download speed
        private DateTime timeMills;
        private long lastDownloadSize;

        //The thread that downloads the file
        private Thread DownloadThread;

        /// <summary>
        /// Initializes the download
        /// </summary>
        /// <param name="sourceUrl"></param>
        /// <param name="destinationPath"></param>
        /// <param name="maxLimitSize"></param>
        public Downloader(string sourceUrl, string destinationPath, int maxLimitSize)
        {
            //Set the variables based on user inputs
            this.SourceURL = sourceUrl;
            this.DestinationPath = destinationPath;
            this.MaxLimitSize = maxLimitSize * MEGA_BYTE;
            abortFlag = false;

            //Initialize the downloader
            DownloadThread = new Thread(new ThreadStart(DownloadFile));
            DownloadThread.IsBackground = false;
        }

        /// <summary>
        /// Starts the downloader by starting the downloader thread
        /// and also sets the initial state
        /// </summary>
        public void StartDownload() { DownloadThread.Start(); timeMills = DateTime.Now; lastDownloadSize = 0; }

        /// <summary>
        /// Aborts the thread by setting the flag variable to true
        /// </summary>
        public void AbortDownload() { abortFlag = true; }

        /// <summary>
        /// This will download the file in chunks
        /// </summary>
        private void DownloadFile()
        {
            //Assume the parameters for the download
            TempFileSize = MaxLimitSize;
            DownloadedSize = 0;

            //Create the file and close it
            //Doing so deletes any previous copies existing
            File.Create(DestinationPath).Close();

            while (true)
            {
                //Download one chunk at a time and get its results
                //Notify the user of the final results
                DownloadChunkResults results = DownloadChunk(DownloadedSize);
                if (results == DownloadChunkResults.DownloadComplete)
                {
                    TempPercentage = 100;
                    MessageBox.Show("Download Completed \n  - File Name : " + Path.GetFileNameWithoutExtension(DestinationPath) + " \n  - File Size    : " + ((float)DownloadedSize / MEGA_BYTE).ToString("0.## MB"), "Download Complete");
                    break;
                }
                else if (results == DownloadChunkResults.AbortDownload)
                {
                    //If a download is aborted then delete it
                    MessageBox.Show("The download has been aborted by the user", "Download Aborted");
                    try { File.Delete(DestinationPath); }
                    catch { }
                    break;
                }   //If there was an error in the download then abort                    
                else if (results == DownloadChunkResults.DownloadError) { break; }
            }
        }

        /// <summary>
        /// Downloads one chunk worth of data from the Internet
        /// </summary>
        /// <param name="startPos">The position where we want to start the download</param>
        /// <returns>The enumerated results which shows the state of this chunk</returns>
        DownloadChunkResults DownloadChunk(long startPos)
        {
            //IO streams
            Stream dwnlRes = null;
            FileStream dwnlStream = null;

            try
            {
                //Set the request for downloading the new chunk
                HttpWebRequest dwnlReq = (HttpWebRequest)WebRequest.Create(SourceURL);
                dwnlReq.AddRange(startPos, startPos + MaxLimitSize);
                dwnlReq.AllowAutoRedirect = true;

                //Get the response stream and open an output stream
                dwnlRes = dwnlReq.GetResponse().GetResponseStream();
                dwnlStream = new FileStream(DestinationPath, FileMode.Append, FileAccess.Write);

                //while there is data or the download is not aborted download the current chunk
                int partlen;
                byte[] chunkBuffer = new byte[CHUNK_BUFFER];
                while ((partlen = dwnlRes.Read(chunkBuffer, 0, CHUNK_BUFFER)) > 0 && !abortFlag)
                {
                    //write the contents to the output file
                    dwnlStream.Write(chunkBuffer, 0, partlen);

                    //Updae the progress parameters
                    DownloadedSize += partlen;
                    if (DownloadedSize >= TempFileSize) { TempFileSize += MaxLimitSize * 2; }
                    TempPercentage = Convert.ToInt32((DownloadedSize * 100) / TempFileSize);
                }

                //Close all the streams
                dwnlRes.Close();
                dwnlStream.Flush();
                dwnlStream.Close();

                //Message the callee by returning an enumerated result
                //The message depends on the download state
                if (abortFlag) { return DownloadChunkResults.AbortDownload; }
                else if (dwnlReq.GetResponse().ContentLength != MaxLimitSize + 1) { return DownloadChunkResults.DownloadComplete; }
                else { return DownloadChunkResults.ContinueDownload; }
            }
            catch (Exception e)
            {
                //Initmate the user of the reason and release all the resources
                //Also delete the partial download file
                //Message the callee about the error
                MessageBox.Show(e.Message, "Download Failed");
                try { dwnlRes.Close(); dwnlStream.Close(); File.Delete(DestinationPath); }
                catch { }
                return DownloadChunkResults.DownloadError;
            }
        }

        /// <summary>
        /// Calculates and returns the download speed with units either in MBps or KBps
        /// </summary>
        /// <returns>The formatted string with download speed</returns>
        public string DownloadSpeed()
        {
            //Calculate the download speed
            long sizeDiff = lastDownloadSize - (lastDownloadSize = DownloadedSize);
            int timeDiff = (timeMills - (timeMills = DateTime.Now)).Milliseconds;

            //If download speed is greater than 1MB then display it in MBps else in KBps
            float dwnlSpeed = (float)sizeDiff / timeDiff;
            if (dwnlSpeed > KILO_BYTE) { return (dwnlSpeed / KILO_BYTE).ToString("0.00 MBps"); }
            else { return (dwnlSpeed).ToString("0.00 KBps"); }
        }

        /// <summary>
        /// Calculates the downloaded file size in MB or KB and returns it
        /// </summary>
        /// <returns>The formatted string with the downloaded size</returns>
        public string SizeDownloaded()
        {
            if (DownloadedSize > MEGA_BYTE) { return ((float)DownloadedSize / MEGA_BYTE).ToString("0.00 MB"); }
            else { return ((float)DownloadedSize / KILO_BYTE).ToString("0.00 KB"); }
        }
    }

    /// <summary>
    /// Implementation of the module to test whether the download is supported or not
    /// </summary>
    class TestDownload
    {
        //Parameters
        string SourceURL;
        int MaxLimitSize;

        /// <summary>
        /// Initializes the download
        /// </summary>
        /// <param name="sourceURL">The URL to test</param>
        /// <param name="maxLimitSize">The maximum file size</param>
        public TestDownload(string sourceURL, int maxLimitSize)
        {
            //Assign the data for the local variables
            SourceURL = sourceURL;
            MaxLimitSize = maxLimitSize * Downloader.MEGA_BYTE;

            //Start the download tester
            new Thread(new ThreadStart(Test)).Start();
        }

        /// <summary>
        /// This will test the link whether it accepts byte range or not
        /// </summary>
        private void Test()
        {
            try
            {
                //Create request to download one chunk of data
                //By applying a range to it
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(SourceURL);
                request.AddRange(0, MaxLimitSize);

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
    }
}