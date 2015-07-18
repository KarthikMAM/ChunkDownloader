using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

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

        //Animation variables
        Storyboard progressStoryboard;
        DoubleAnimation propertyAnimation;

        //Data for tracking the download
        Downloader downloader;
        DispatcherTimer progressUpdateTimer;

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

            //Create a new instance of the timer to check and update the progress report periodically
            progressUpdateTimer = new DispatcherTimer();
            progressUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
            progressUpdateTimer.Tick += ProgressUpdateTimer_Tick;
        }

        /// <summary>
        /// Used to get the path for saving our downloaded file
        /// </summary>
        /// <param name="sender">The svaeAs button that got clicked</param>
        /// <param name="e">The event args for this event</param>
        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            //Prepare the SaveAs Dialog Box
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.OverwritePrompt = true;

            //Get the download file name from the url
            if (Path.GetExtension(Url.Text).Length <= 5)
            {
                saveDialog.FileName = Path.GetFileNameWithoutExtension(Url.Text);
                saveDialog.DefaultExt = Path.GetExtension(Url.Text);
                saveDialog.Filter = saveDialog.DefaultExt + " | *" + saveDialog.DefaultExt;
            }

            //Get the saveAs location from the user
            saveDialog.ShowDialog();
            SaveLocation.Text = saveDialog.FileName;
            SaveLocation.FontStyle = FontStyles.Normal;
        }

        /// <summary>
        /// On clicking the download button, all the preliminary conditions are set for the download to start asynchronously
        /// </summary>
        /// <param name="sender">The Ok button that got clicked</param>
        /// <param name="e">The Event args for this event</param>
        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (Ok.Content.ToString() == "Go")
            {
                //Check if all the fields are Ok
                if (SaveLocation.Text != EMPTY_STRING && SaveLocation.Text != SAVE_LOCATION_TEXT && ChunkSize.Text != EMPTY_STRING && ChunkSize.Text != CHUNK_SIZE_TEXT && Url.Text != EMPTY_STRING && Url.Text != URL_TEXT)
                {
                    //Create a new instance of the Downloader and start the download
                    downloader = new Downloader(Url.Text, SaveLocation.Text, Convert.ToInt32(ChunkSize.Text));
                    downloader.StartDownload();

                    //Start the downlaod
                    progressUpdateTimer.Start();

                    //Change Ok to Cancel
                    Ok.Content = "Cancel";
                }
                else { MessageBox.Show("All Details are to be furnished", "Incomplete Fields"); }
            }
            else
            {
                //Change Cancel button to Ok Button
                Ok.Content = "Go";

                //Call abort download and reset the window
                downloader.AbortDownload();
                ResetWindow();
            }

        }

        /// <summary>
        /// Resets the window to its original state
        /// </summary>
        private void ResetWindow()
        {
            //Updating data fields
            Url.Text = URL_TEXT;
            SaveLocation.Text = SAVE_LOCATION_TEXT;
            DownloadedSize.Text = "";

            //Stop the progress update timer
            if (progressUpdateTimer != null) { progressUpdateTimer.Stop(); }

            //Updating the visual fields
            progressStoryboard.Stop();
                propertyAnimation.From = Progress.Value;
                propertyAnimation.To = 0;   
            progressStoryboard.Begin();

            //Update visual styles of the labels
            Url.FontStyle = SaveLocation.FontStyle = ChunkSize.FontStyle = FontStyles.Italic;
        }

        /// <summary>
        /// Will update the progress report once in every few milliseconds
        /// </summary>
        /// <param name="sender">The timer that is triggering this event</param>
        /// <param name="e">EventArgs</param>
        void ProgressUpdateTimer_Tick(object sender, EventArgs e)
        {
            //Stop and update the From and to values of the StoryBoardAnimation of the progress bar
            progressStoryboard.Stop();
                propertyAnimation.From = Progress.Value;
                propertyAnimation.To = downloader.TempPercentage;
            progressStoryboard.Begin();

            //Update the downloaded file size in the progress bar
            DownloadedSize.Text = "Downloaded : " + downloader.SizeDownloaded() + " (" + downloader.DownloadSpeed() + ")";

            //Download is complete, so stop and reset the window
            if (downloader.TempPercentage == 100) { progressUpdateTimer.Stop(); ResetWindow(); }
        }

        /// <summary>
        /// Used to remove the default text and ease the work of the user
        /// </summary>
        /// <param name="sender">TextBox which got focussed</param>
        /// <param name="e">EventArgs</param>
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
        /// <param name="sender">Button pressed</param>
        /// <param name="e">EventArgs</param>
        private void TestLink_Click(object sender, RoutedEventArgs e)
        {
            //Creates a new instance of the testDownload class to perform a non blocking download
            if (ChunkSize.Text != EMPTY_STRING && ChunkSize.Text != CHUNK_SIZE_TEXT && Url.Text != EMPTY_STRING && Url.Text != URL_TEXT) { new TestDownload(Url.Text, Convert.ToInt32(ChunkSize.Text)); }
            else { MessageBox.Show("All Details are to be furnished", "Incomplete Fields"); }
        }

        /// <summary>
        /// This is used to create a new downloader window for another download
        /// </summary>
        /// <param name="sender">Button Pressed</param>
        /// <param name="e">Event Args</param>
        private void New_Click(object sender, RoutedEventArgs e) { new MainWindow().Show(); }

        /// <summary>
        /// Stops the thread that is running when the app is about to close
        /// If there is any instance of the downloader call abort on it
        /// </summary>
        /// <param name="sender">The application that is about to close</param>
        /// <param name="e">CancelEventArgs</param>
        private void Window_Closing(object sender, CancelEventArgs e) { if (downloader != null) { downloader.AbortDownload(); } }
    }
}