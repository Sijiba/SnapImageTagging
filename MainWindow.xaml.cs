﻿using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ExifLibrary;
using OpenQA.Selenium;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using OpenQA.Selenium.Support.UI;
using Tags = Tagging.Tagging;
using OpenQA.Selenium.Firefox;

namespace SnapImageTaggingWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var sourcePath = Properties.Settings.Default.SourceFolder;
            if (sourcePath.Length > 0)
            {
                SourceDirPath = Properties.Settings.Default.SourceFolder;
                UpdateDirectoryInfo();
            }
            ShowEventTagOptionsBox.IsChecked = false;
            HideBrowserSide();
        }

        private void UpdateDirectoryInfo()
        {
            int imgCount = 0;
            int untagged = 0;
            int skipped = 0;

            if (Directory.Exists(SourceDirPath))
            {
                //Count jpg files
                var files = Directory.EnumerateFiles(SourceDirPath, "*.jpg", SearchOption.AllDirectories);
                imgCount = files.Count();

                foreach (var file in files)
                {
                    var img = ExifLibrary.JPEGFile.FromFile(file);
                    if (img != null)
                    {
                        var tagsMaybe = img.Properties.Get(ExifTag.WindowsKeywords);
                        if (tagsMaybe != null)
                        {
                            if (((string)tagsMaybe.Value).Contains(Tags.nameless))
                            {
                                skipped++;
                            }
                            else
                            {
                                var tagList = Tags.GetTagStringList((string)tagsMaybe.Value);
                                if (!Tags.HasMetadataSocials(tagList))
                                {
                                    untagged++;
                                }
                            }
                        }
                        else
                        {
                            untagged++;
                        }
                    }
                }
            }
            FileCountLabel.Content = String.Format("{0} images: {1} without names, {2} skipped, {3} completed.",
                imgCount, untagged, skipped, imgCount - (untagged + skipped));
        }

        private void FileBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog1 = new();
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                SourceDirPath = folderBrowserDialog1.SelectedPath;
                UpdateDirectoryInfo();
            }
        }

        private OpenQA.Selenium.Firefox.FirefoxDriver _driver;
        private IEnumerable<string> _fileList;
        private int _browserImgIndex;

        private GridLength _1gridLength = new(1, GridUnitType.Star);
        private GridLength _0gridLength = new(0, GridUnitType.Star);

        private string _SDirPath = "";
        public string SourceDirPath
        {
            get => _SDirPath;
            set
            {
                _SDirPath = value;
                PathLabel.Content = value;
                PathLabel.ToolTip = value;
                Properties.Settings.Default.SourceFolder = value;
            }
        }

        public bool ShouldTagMissingDates
        {
            get => Properties.Settings.Default.TagMissingDates;
            set { Properties.Settings.Default.TagMissingDates = value; }
        }
        public bool ShouldTagColors
        {
            get => Properties.Settings.Default.TagColors;
            set { Properties.Settings.Default.TagColors = value; }
        }
        public bool ShouldTagEvents
        {
            get => Properties.Settings.Default.TagEvents;
            set { Properties.Settings.Default.TagEvents = value; }
        }
        public bool ShowBrowserWindow
        {
            get => Properties.Settings.Default.ShowBrowserWindow;
            set { Properties.Settings.Default.ShowBrowserWindow = value; }
        }
        public bool ShouldOpenBrowser
        {
            get => Properties.Settings.Default.ShouldReverseImageSearch;
            set { Properties.Settings.Default.ShouldReverseImageSearch = value; }
        }

        public bool ShouldBrowseSkipped
        {
            get => Properties.Settings.Default.ShouldBrowseSkipped;
            set { Properties.Settings.Default.ShouldBrowseSkipped = value; }
        }

        public bool ShouldUseCustomTags
        {
            get => Properties.Settings.Default.ShouldUseCustomTags;
            set { Properties.Settings.Default.ShouldUseCustomTags = value; }
        }
        public string CustomTagString
        {
            get => Properties.Settings.Default.CustomTagString;
            set { Properties.Settings.Default.CustomTagString = value; }
        }
        public string EventStartTerminator
        {
            get => Properties.Settings.Default.EventStartTerminator;
            set { Properties.Settings.Default.EventStartTerminator = value; }
        }
        public string EventEndTerminator
        {
            get => Properties.Settings.Default.EventEndTerminator;
            set { Properties.Settings.Default.EventEndTerminator = value; }
        }
        public string EventDelimiter
        {
            get => Properties.Settings.Default.EventDelimiter;
            set {
                Properties.Settings.Default.EventDelimiter = value.Length == 0 ? " " : value;
            }
        }

        private void SaveSettings()
        {
            
            Properties.Settings.Default.Save();
        }

        private bool editTags(ref ImageFile img, ref List<string> tagSet)
        {
            bool changed = false;
            string fname = _fileList.ElementAt(_browserImgIndex);
            if (ShouldTagMissingDates)
            {
                //If "Date Taken" isn't set, set it from the Last Modified Date
                var givenDate = img.Properties.Get<ExifDateTime>(ExifTag.DateTimeOriginal);
                if (givenDate == null)
                {
                    DateTime dt = File.GetLastWriteTime(fname);
                    img.Properties.Set(ExifTag.DateTimeOriginal, dt);
                    changed = true;
                }
            }

            if (ShouldTagEvents)
            {
                //TODO Establish a plan to get the event name from the file name
                var parentFolder = new DirectoryInfo(Path.GetDirectoryName(fname)!).Name;
                int nameLen = parentFolder.Length;
                int startCutoff = 0;
                int endCutoff = nameLen;
                if (EventStartTerminator.Length > 0)
                    startCutoff = parentFolder.IndexOf(EventStartTerminator) + EventStartTerminator.Length;
                if (EventEndTerminator.Length > 0)
                {
                    int foundIndex = parentFolder.IndexOf(EventEndTerminator);
                    if (foundIndex != 0)
                        endCutoff = foundIndex;
                }
                if (EventDelimiter.Length == 0)
                    EventDelimiter = " ";

                string eventString = parentFolder[startCutoff..endCutoff].Trim().ToLower();
                string[] wordList = eventString.Split(EventDelimiter);

                string eventYearString = "";

                if (wordList.Length > 0)
                {
                    if (int.TryParse(wordList[^1], out _)) {
                        eventYearString = wordList[^1];
                        wordList = wordList.SkipLast(1).ToArray();
                    }
                }
                string eventNameOnly = string.Join(EventDelimiter, wordList);
                string eventNameAndYear = String.Concat(eventNameOnly, eventYearString);

                string eventPrefix = "event:";
                tagSet.Add(eventPrefix + eventNameAndYear);
                tagSet.Add(eventPrefix + eventNameOnly);
            }

            if (ShouldUseCustomTags && CustomTagString.Length > 0)
            {
                var newTags = CustomTagString.Split(';');
                foreach (var tag in newTags)
                {
                    if (tag.Length > 0)
                    {
                        tagSet.Add(tag);
                        changed = true;
                    }
                }
            }

            if (ShouldTagColors)
            {
                //TODO Is there a way to do this without getting overwhelmed by background colors
            }

            return changed;
        }

        public async void ApplyMetadataNoBrowser()
        {
            _browserImgIndex = 0;
            for (_browserImgIndex = 0; _browserImgIndex < _fileList.Count(); _browserImgIndex++)
            {
                var file = _fileList.ElementAt(_browserImgIndex);
                var img = ExifLibrary.JPEGFile.FromFile(file);

                if (img != null)
                {
                    var tagSet = Tags.GetTagStringList(img);

                    //tagSet.Add("tag:tagging_incomplete");
                    if (editTags(ref img, ref tagSet))
                    {
                        //Store tags if applicable
                        var tagString = String.Join(';', tagSet.Distinct());
                        img.Properties.Set(ExifTag.WindowsKeywords, tagString);

                        //Save file
                        //img.Save(path);
                        img.SaveAsync(file);
                    }
                }
            }

        }

        //this will search for the element until a timeout is reached
        public IWebElement WaitUntilElementVisible(By elementLocator, int timeout = 20)
        {
            try
            {
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeout));
                return wait.Until(drv => drv.FindElement(elementLocator));
                //return wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.ElementIsVisible(elementLocator));
            }
            catch (NoSuchElementException)
            {
                Console.WriteLine("Element with locator: '" + elementLocator + "' was not found.");
                throw;
            }
        }

        private void ExamineScreen()
        {
            //TODO Strategies to maximize number of images available?
            //-Make Window wider
            //-More scrolls
            if (_driver == null) { return; }
            var scrollableElement = WaitUntilElementVisible(By.ClassName("b57KQc"));
            
            int scrollMax = 10;
            int imageSize = 150;
            try
            {
                for (var i = 0; i < scrollMax; i++)
                {
                    scrollableElement.SendKeys(OpenQA.Selenium.Keys.PageDown);
                }
            }
            catch (OpenQA.Selenium.ElementNotInteractableException)
            {
                //Can't scroll so do nothing
            }
            ImagesPane.ScrollToTop();

            var frameCondition = By.CssSelector(".ksQYvb");
            WaitUntilElementVisible(frameCondition);
            var elements = _driver.FindElements(frameCondition);
            int maxImages = Math.Min(elements.Count, 10 * scrollMax);
            //SimilarImages.Children.Clear();
            for (int i = 0; i < maxImages; i++)
            {
                int realIndex = i % 2 == 0 ? i / 2 : (i + elements.Count) / 2;
                var element = elements[realIndex];
                var imgCondition = By.TagName("img");
                WaitUntilElementVisible(imgCondition);
                var subImgs = element.FindElements(imgCondition);
                if (subImgs.Count == 0) continue;
                var imgElement = subImgs.ElementAt(0);
                string ImageUrl = imgElement.GetAttribute("src");
                string ImageName = imgElement.GetAttribute("alt");

                if (ImageUrl.StartsWith("https://"))
                {
                    string imgTooltip = ImageName + "\n" + ImageUrl;
                    var givenURL = element.GetAttribute("data-action-url");
                    var newImage = new System.Windows.Controls.Image
                    {
                        Source = new BitmapImage(new Uri(ImageUrl)),
                        Tag = ImageName,
                        Height = imageSize,
                        ToolTip = givenURL,
                        Margin = new System.Windows.Thickness(2),
                        Cursor = System.Windows.Input.Cursors.Hand
                    };
                    newImage.Width = Math.Min(imageSize, imageSize * newImage.Source.Width / newImage.Source.Height);
                    newImage.MouseDown += new System.Windows.Input.MouseButtonEventHandler(Image_MouseDown);
                    SimilarImages.Children.Add(newImage);
                }
            }

        }


        private void NextImage()
        {
            SimilarImages.Children.Clear();
            NameEntryBox.Text = "";
            string filePath;
            var fileCount = _fileList.Count();

            for (_browserImgIndex ++; (_browserImgIndex < fileCount); _browserImgIndex++)
            {
                filePath = _fileList.ElementAt(_browserImgIndex);

                //If it's untagged, break to move on
                //Check if this image needs tagging
                var imgMeta = ExifLibrary.JPEGFile.FromFile(filePath);
                var tags = Tags.GetTagStringList(imgMeta);

                bool DoSearchImg;
                if (tags.Count == 0)
                {
                    DoSearchImg = true;
                }
                else
                {
                    DoSearchImg = !Tags.HasMetadataSocials(tags, ShouldBrowseSkipped);
                }

                if (DoSearchImg)
                {
                    if (_driver == null)
                    {
                        ShowWebBrowserWindow();
                    }
                    VisualSearchImage(filePath);
                    break;
                }
            }
            
            if (_browserImgIndex >= fileCount)
            {
                System.Windows.MessageBox.Show("Reached the end of images to search.");
                CloseBrowser();
                return;
            }
            
        }

        private void VisualSearchImage(string path)
        {
            //If this image needs tagging, display it, but leave the file available after
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            SoughtImage.Source = image;
            SoughtImage.ToolTip = path;
            //SoughtImage.Source = new BitmapImage(new Uri(filePath));
            _driver.Navigate().GoToUrl("https://www.google.com/?olud");
            var title = _driver.Title;

            IWebElement fileInput = WaitUntilElementVisible(By.CssSelector("input[type=file]"));
            fileInput.SendKeys(path);


            //Class name looks weird.
            //What we really want is data-action-url tag
            DelayAction(2000, ExamineScreen);
        }


        private void SubmitTag(string tag)
        {
            //TODO Open image, apply tag, save

            var filePath = _fileList.ElementAt(_browserImgIndex);
            var img = ExifLibrary.JPEGFile.FromFile(filePath);

            var tags = Tags.GetTagStringList(img);
            tags.Remove(Tags.nameless);
            editTags(ref img, ref tags);
            tags.Add(tag);

            var tagString = String.Join(';', tags.Distinct());
            img.Properties.Set(ExifTag.WindowsKeywords, tagString);

            //Save file
            //img.Save(path);
            img.SaveAsync(filePath);

            LastNameLabel.Content = tag;

        }

        private void JoinCharacterTags(ref List<string> theirTags)
        {
            var filePath = _fileList.ElementAt(_browserImgIndex);
            var img = ExifLibrary.JPEGFile.FromFile(filePath);
            var ourTags = Tags.GetTagStringList(img);
            editTags(ref img, ref ourTags);

            var toAdd = theirTags.Where(item => Tags.IsTaggingTag(item));
            ourTags.AddRange(toAdd);

            ourTags.Remove(Tags.nameless);

            var tagString = String.Join(';', ourTags.Distinct());
            img.Properties.Set(ExifTag.WindowsKeywords, tagString);

            //Save file
            //img.Save(path);
            img.SaveAsync(filePath);

            LastNameLabel.Content = String.Format("({0} tags.)", toAdd.Count());
        }

        private void NameSubmitted(string dialog)
        {
            //TODO Tag images and move on
            dialog = Regex.Replace(dialog, @"[;]", "").Trim();

            if (dialog.StartsWith("https://"))
            {
                //Decipher web link
                dialog = dialog[8..];
                if (dialog.StartsWith("www."))
                    dialog = dialog[4..];
                if (dialog.Contains('/') && dialog.Contains('.'))
                {
                    string host = dialog[..dialog.IndexOf('.')];
                    string user = dialog[(dialog.IndexOf('/') + 1)..];
                    string profileString = "profile/";
                    if (user.Contains(profileString))
                    {
                        int startIndex = user.IndexOf(profileString) + profileString.Length;
                        user = user[startIndex..];
                    }
                    if (user.Contains('/'))
                        user = user[..user.IndexOf('/')];
                    if (user.Contains('@'))
                        user = user[1..];
                    if (user.Contains('?'))
                        user = user[..user.IndexOf('?')];
                    if (host == "x")
                        host = "twitter";

                    SubmitTag(host + ":" + user);
                }
            }
            else
            {
                SubmitTag("name:" + dialog);
            }

            NextImage();
        }

        private async void OptionsStartButton_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(SourceDirPath))
            {
                //Count jpg files
                _fileList = Directory.EnumerateFiles(SourceDirPath, "*.jpg", SearchOption.TopDirectoryOnly);
                int imgCount = _fileList.Count();

                FileCountLabel.Content = "Applying metadata...";
                OptionsStack.IsEnabled = false;
                SaveSettings();
                ((MainWindow)System.Windows.Application.Current.MainWindow).UpdateLayout();

                if (ShouldOpenBrowser)
                {
                    ShowBrowserSide();
                }
                else
                {
                    if (ShouldTagMissingDates || ShouldTagColors || ShouldTagEvents || ShouldUseCustomTags)
                    {
                        await Task.Run(() => { ApplyMetadataNoBrowser(); });
                    }
                    CompleteTagging();
                }
            }
        }


        private static void DelayAction(int millisecond, Action action)
        {
            var timer = new DispatcherTimer();
            timer.Tick += delegate

            {
                action.Invoke();
                timer.Stop();
            };

            timer.Interval = TimeSpan.FromMilliseconds(millisecond);
            timer.Start();
        }

        private void ShowBrowserSide()
        {
            //Activate browser mode
            _browserImgIndex = -1;

            BrowserGrid.IsEnabled = true;
            OptionsStack.IsEnabled = false;

            BrowserGrid.Visibility = Visibility.Visible;
            OptionsStack.Visibility = Visibility.Collapsed;

            InfoColumn.Width = _0gridLength;
            BrowserColumn.Width = _1gridLength;
            NextImage();
        }

        private void ShowWebBrowserWindow()
        {
            var opts = new FirefoxOptions();
            // If you wanna look classy uncomment one of these.
            //  Otherwise having the window is actually pretty useful
            if (!ShowBrowserWindow)
            {
                //This version stops Lens from loading beyond the initial results
                //opts.AddArgument("headless");
                //This version spawns the browser window offscreen
                opts.AddArgument("--window-position=-32000,-32000");
            }
            _driver = new OpenQA.Selenium.Firefox.FirefoxDriver(opts);
        }
        private void CloseBrowser()
        {
            if (_driver != null)
            {
                _driver.Quit();
                _driver = null;
            }
            CompleteTagging();
        }

        private void CompleteTagging()
        {
            HideBrowserSide();
            UpdateDirectoryInfo();
            //FileCountLabel.Content = "Metadata applied.";
        }

        private void HideBrowserSide()
        {
            BrowserGrid.IsEnabled = false;
            OptionsStack.IsEnabled = true;

            BrowserGrid.Visibility = Visibility.Collapsed;
            OptionsStack.Visibility = Visibility.Visible;

            InfoColumn.Width = _1gridLength;
            BrowserColumn.Width = _0gridLength;

        }

        private void BrowserSkipButton_Click(object sender, RoutedEventArgs e)
        {
            //Move to next image
            //Close browser if no more pics to search
            SubmitTag(Tags.nameless);
            NextImage();
        }

        private void BrowserEndButton_Click(object sender, RoutedEventArgs e)
        {
            CloseBrowser();
        }

        private void NameSubmitButton_Click(object sender, RoutedEventArgs e)
        {
            NameSubmitted(NameEntryBox.Text);
            e.Handled = true;
        }

        private void TextBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;

            NameSubmitted(NameEntryBox.Text);
            e.Handled = true;
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.RightButton == MouseButtonState.Pressed)
            {
                System.Windows.Clipboard.SetText((string)((System.Windows.Controls.Image)sender).ToolTip);
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                NameSubmitted((string)((System.Windows.Controls.Image)sender).ToolTip);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSettings();
            if (_driver != null)
            {
                _driver.Dispose();
                _driver = null;
            }
        }

        private void BrowserGrid_LayoutUpdated(object sender, EventArgs e)
        {
            var nowVertical = BrowserGrid.ActualHeight / BrowserGrid.ActualWidth >= 1;

            //If scrollviewer is below (row 1 colspan 2)
            if (nowVertical)
            {
                if (Grid.GetRow(ImagesPane) != 1)
                {
                    Grid.SetRow(ImagesPane, 1);
                    Grid.SetColumn(ImagesPane, 0);
                    Grid.SetRowSpan(ImagesPane, 1);
                    Grid.SetColumnSpan(ImagesPane, 2);
                }
            }
            else if (Grid.GetColumn(ImagesPane) != 1)
            {
                Grid.SetRow(ImagesPane, 0);
                Grid.SetColumn(ImagesPane, 1);
                Grid.SetRowSpan(ImagesPane, 2);
                Grid.SetColumnSpan(ImagesPane, 1);
            }

        }

        private void BrowserMatchingButton_Click(object sender, RoutedEventArgs e)
        {
            //Open file dialog.
            var filebrowser = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "JPG images (*.JPG)|*.jpg",
                Title = "Select matching image..."
            };
            var picked = filebrowser.ShowDialog();
            string message = "";
            switch (picked)
            {
                case System.Windows.Forms.DialogResult.OK:
                    {
                        var filePath = filebrowser.FileName;
                        if (File.Exists(filePath))
                        {
                            var imgMeta = ExifLibrary.JPEGFile.FromFile(filePath);
                            var theirTags = Tags.GetTagStringList(imgMeta);

                            if (Tags.HasMetadataSocials(theirTags))
                            {
                                //  If the user selects an image that has socials/name
                                //  data on it:
                                //      Copy said socials/name data.
                                //      Move onto the next image
                                JoinCharacterTags(ref theirTags);
                                NextImage();
                                break;
                            }
                            else
                            {
                                //  Selected image has no useful metadata.
                                //  Warn the user as such.
                                //  (Consider adding feature to link two images' paths
                                //  in tags, & then tag the linked file if info is given)
                                message = "File doesn't have any socials tags. " +
                                    "Find them yourself or try another file.";
                            }
                        }
                        else
                        {
                            message = "File doesn't seem to be readable.";
                        }
                        break;
                    }
                default:
                    break;
            }
            if (message.Length > 0)
            {
                System.Windows.MessageBox.Show(message);
            }




            //  Else:
            //      cancel the dialog and take no further action
        }

        private void SoughtImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string FilePath = (string)((System.Windows.Controls.Image)sender).ToolTip;
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //Open jpeg in default image viewer
                Process.Start(new ProcessStartInfo(FilePath) { UseShellExecute = true });
            }
            if (e.RightButton == MouseButtonState.Pressed)
            {
                //Copy file path to clipboard
                System.Windows.Clipboard.SetText(FilePath);
            }
        }
    }
}