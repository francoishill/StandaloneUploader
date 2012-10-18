using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;
using System.Net;
using System.IO;
using System.Diagnostics;
using SharedClasses;
using System.Windows.Interop;

namespace StandaloneUploader
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window, System.Windows.Forms.IWin32Window
	{
		ObservableCollection<UploadingItem> currentlyUploadingList = new ObservableCollection<UploadingItem>();
		private const string cSavedCrashListFileExtension = ".crash.saved.txt";
		private string savedListsOnCrashesDirpath = Path.GetDirectoryName(SettingsInterop.GetFullFilePathInLocalAppdata("tmp", UploadingItem.cThisAppName, "CrashesSavedList"));

		public MainWindow()
		{
			InitializeComponent();

			listboxCurrentlyUploading.ItemsSource = currentlyUploadingList;
			LoadUnssuccessfulList();//The items that did not succeed yet (from previous run)
			LoadCurrentListFromCrashes();//Loads items if there are from crashes (not used only when /restart flag is passed, as it can crash again on startup??)
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			this.MaxWidth = SystemParameters.WorkArea.Height;
			this.MaxHeight = SystemParameters.WorkArea.Width;
		}

		private void LoadUnssuccessfulList()
		{
			List<string> outFilesToBeDeleted;
			var items = UploadingItem.GetListFromUnssuccessfulDirectory_NotDelete(out outFilesToBeDeleted);
			foreach (var file in items.Keys)
			{
				foreach (var item in items[file])
					AddUploadingItem(item);
				File.Delete(file);//Only delete it here as it is again then added to unsuccessfullist
			}
		}

		private void LoadCurrentListFromCrashes()//Used from App.xaml.cs
		{
			foreach (var crashfile in Directory.GetFiles(savedListsOnCrashesDirpath, "*" + cSavedCrashListFileExtension))
			{
				var tmplist = UploadingItem.GetItemsFromFile(crashfile, true);
				if (tmplist != null && tmplist.Count > 0)
					foreach (var item in tmplist)
						AddUploadingItem(item);
			}
		}

		public void AddUploadingItem(string displayName, UploadingProtocolTypes ProtocolType, string LocalPath, string FtpUrl, string FtpUsername, string FtpPassword, bool AutoOverwriteIfExists, bool AutoStartUploading)
		{
			var alreadyInListCount =
				currentlyUploadingList
				.Where(it => !it.SuccessfullyUploaded)
				.Count(it =>
					it.PropertiesEqualsTo(displayName, ProtocolType, LocalPath, FtpUrl, FtpUsername, FtpPassword));
			if (alreadyInListCount > 0)
			{
				App.ShowError("Cannot add another item with same properties.");
				return;
			}
			var itemtoadd = new UploadingItem(displayName, ProtocolType, LocalPath, FtpUrl, AutoOverwriteIfExists, FtpUsername, FtpPassword, AutoStartUploading, true);
			AddUploadingItem(itemtoadd);
		}

		public void AddUploadingItem(UploadingItem itemtoadd)
		{
			InvokeOnDispatcher(delegate { currentlyUploadingList.Add(itemtoadd); });
		}

		private void InvokeOnDispatcher(Action action)
		{
			Dispatcher.Invoke(action);
		}

		public void SaveToDiskWhenCrashing()
		{
			string thisexepath = Environment.GetCommandLineArgs()[0];
			string savingfilepath = Path.Combine(savedListsOnCrashesDirpath, DateTime.Now.ToString("yyyy_MM_dd HH_mm_ss") + cSavedCrashListFileExtension);
			var incompleteItemsLines = currentlyUploadingList
				.Where(it => !it.SuccessfullyUploaded)
				.Select(it => it.GetFileLineForApplicationRecovery()).ToArray();
			if (incompleteItemsLines.Length > 0)
			{
				File.WriteAllLines(savingfilepath, incompleteItemsLines);
				Process.Start("explorer", "/select,\"" + savingfilepath + "\"");
			}
		}

		private UploadingItem GetUploadingItemFromPossibleFrameworkElement(object possibleButton)
		{
			FrameworkElement fwe = possibleButton as FrameworkElement;
			if (fwe == null) return null;
			var uploadingItem = fwe.DataContext as UploadingItem;
			return uploadingItem;
		}

		private void buttonCancelButtonVisibility_Click(object sender, RoutedEventArgs e)
		{
			var item = GetUploadingItemFromPossibleFrameworkElement(sender);
			if (item == null) return;
			item.CancelUploadingAsync();
		}

		private void buttonDeleteOnlineFileButtonVisibility_Click(object sender, RoutedEventArgs e)
		{
			var item = GetUploadingItemFromPossibleFrameworkElement(sender);
			if (item == null) return;
			item.DeleteOnlineFileAndRetry();
		}

		private void buttonRetryUpload_Click(object sender, RoutedEventArgs e)
		{
			var item = GetUploadingItemFromPossibleFrameworkElement(sender);
			if (item == null) return;
			item.RetryUpload();
		}

		private void buttonRemoveFromList_Click(object sender, RoutedEventArgs e)
		{
			var item = GetUploadingItemFromPossibleFrameworkElement(sender);
			if (item == null) return;
			if (currentlyUploadingList.Contains(item))
			{
				currentlyUploadingList.Remove(item);
				if (currentlyUploadingList.Count == 0)
					this.Close();//Exit application
			}
		}

		private void buttonTestCrash(object sender, RoutedEventArgs e)
		{
			OwnUnhandledExceptionHandler.TestCrash(false, (s) => { return true; });
		}

		private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (Mouse.RightButton == MouseButtonState.Pressed)
				this.DragMove();
		}

		private ScaleTransform smallScale = new ScaleTransform(0.1, 0.1);
		private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Middle)
			{
				if (IsSmall())
					MakeNormalSize();
				else
					MakeSmall();
			}
		}

		Point? lastNotSmallPos = null;
		private void MakeSmall()
		{
			if (IsSmall())
				return;
			lastNotSmallPos = new Point(this.Left, this.Top);
			this.mainGrid.LayoutTransform = smallScale;
			if (this.SizeToContent == System.Windows.SizeToContent.WidthAndHeight)
			{
				this.SizeToContent = System.Windows.SizeToContent.Manual;
				this.UpdateLayout();
				this.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
			}
			this.UpdateLayout();
			this.Left = SystemParameters.WorkArea.Right - this.ActualWidth;
			this.Top = SystemParameters.WorkArea.Bottom - this.ActualHeight;
		}

		private void MakeNormalSize()
		{
			mainGrid.LayoutTransform = null;
			if (lastNotSmallPos.HasValue)
			{
				this.Left = lastNotSmallPos.Value.X;
				this.Top = lastNotSmallPos.Value.Y;
			}
			lastNotSmallPos = null;
			if (this.SizeToContent == System.Windows.SizeToContent.WidthAndHeight)
			{
				this.SizeToContent = System.Windows.SizeToContent.Manual;
				this.UpdateLayout();
				this.SizeToContent = System.Windows.SizeToContent.WidthAndHeight;
			}
			this.UpdateLayout();
		}

		private bool IsSmall()
		{
			return mainGrid.LayoutTransform == smallScale;
		}

		public IntPtr Handle
		{
			get { return new WindowInteropHelper(this).Handle; }
		}

		private void aboutLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			new AboutWindow2(new ObservableCollection<DisplayItem>()
			{
				new DisplayItem("Author", "Francois Hill"),
				new DisplayItem("Icon obtained from", "http://www.visualpharm.com", "http://www.visualpharm.com")
			})
			{
				Owner = this
			}
			.ShowDialog();
		}

		ScaleTransform currentScale = new ScaleTransform(1, 1);
		private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (!IsSmall())
			{
				this.mainGrid.LayoutTransform = currentScale;
				e.Handled = true;
				if (e.Delta > 0)//Up
				{
					currentScale.ScaleX += 0.05;
					currentScale.ScaleY += 0.05;
				}
				else if (e.Delta < 0)//Down
				{
					currentScale.ScaleX -= 0.05;
					currentScale.ScaleY -= 0.05;
				}
			}
		}

		private void frameworkElementSourceUpdated(object sender, DataTransferEventArgs e)
		{
			FrameworkElement fe = sender as FrameworkElement;
			if (fe == null) return;
			fe.UpdateLayout();
		}
	}

	public class UploadingItem : INotifyPropertyChanged
	{
		public const string cThisAppName = "StandaloneUploader";

		private static List<UploadingItem> UploadsInProgress = new List<UploadingItem>();
		private static Queue<UploadingItem> UploadsQueued = new Queue<UploadingItem>();

		public string DisplayName { get; private set; }
		private UploadingProtocolTypes ProtocolType;

		private int _currentprogresspercentage;
		public int CurrentProgressPercentage
		{
			get { return _currentprogresspercentage; }
			private set { _currentprogresspercentage = value; OnPropertyChanged("CurrentProgressPercentage"); }
		}

		private string _currentprogressmessage;
		public string CurrentProgressMessage
		{
			get { return _currentprogressmessage; }
			set { _currentprogressmessage = value; OnPropertyChanged("CurrentProgressMessage"); }
		}

		private Visibility _cancelbuttonvisibility;
		public Visibility CancelButtonVisibility
		{
			get { return _cancelbuttonvisibility; }
			set { _cancelbuttonvisibility = value; OnPropertyChanged("CancelButtonVisibility"); }
		}

		private Visibility _deleteonlinefileandretrybuttonvisibility;
		public Visibility DeleteOnlineFileAndRetryButtonVisibility
		{
			get { return _deleteonlinefileandretrybuttonvisibility; }
			set { _deleteonlinefileandretrybuttonvisibility = value; OnPropertyChanged("DeleteOnlineFileAndRetryButtonVisibility"); }
		}

		private Visibility _retryuploadbuttonvisibility;
		public Visibility RetryUploadButtonVisibility
		{
			get { return _retryuploadbuttonvisibility; }
			set { _retryuploadbuttonvisibility = value; OnPropertyChanged("RetryUploadButtonVisibility"); }
		}

		private Visibility _removefromlistbuttonvisibility;
		public Visibility RemoveFromListButtonVisibility
		{
			get { return _removefromlistbuttonvisibility; }
			set { _removefromlistbuttonvisibility = value; OnPropertyChanged("RemoveFromListButtonVisibility"); }
		}


		private bool AutoOverwriteIfExists;
		public bool SuccessfullyUploaded = false;

		public string LocalPath { get; private set; }
		public string FtpUrl { get; private set; }
		private string FtpUsername;
		private string FtpPassword;

		private static Dictionary<UploadingItem, string> UnssuccessfulListOfFilepathsAndItem = new Dictionary<UploadingItem, string>();//Keeping track of items to remove them (and delete file) once they are successful

		public UploadingItem() { AddToUnssuccesfulList(); }

		public UploadingItem(string DisplayName, UploadingProtocolTypes ProtocolType, string LocalPath, string FtpUrl, bool AutoOverwriteIfExists, string FtpUsername, string FtpPassword, bool AutoStartUploading, bool AllowCancel)
		{
			this.DisplayName = DisplayName;
			this.ProtocolType = ProtocolType;
			this.CurrentProgressPercentage = 0;
			this.CurrentProgressMessage = "Item queued for upload";
			this.LocalPath = LocalPath;
			this.FtpUrl = FtpUrl;
			this.AutoOverwriteIfExists = AutoOverwriteIfExists;
			this.FtpUsername = FtpUsername;
			this.FtpPassword = FtpPassword;
			this.CancelButtonVisibility = AllowCancel ? Visibility.Visible : Visibility.Collapsed;
			this.DeleteOnlineFileAndRetryButtonVisibility = Visibility.Collapsed;
			this.RetryUploadButtonVisibility = Visibility.Collapsed;
			this.RemoveFromListButtonVisibility = Visibility.Collapsed;
			if (AutoStartUploading)
				StartUploading();
			AddToUnssuccesfulList();
		}

		public bool PropertiesEqualsTo(string DisplayName, UploadingProtocolTypes ProtocolType, string LocalPath, string FtpUrl, string FtpUsername, string FtpPassword)
		{
			return
				this.DisplayName == DisplayName
				&& this.ProtocolType == ProtocolType
				&& this.LocalPath == LocalPath
				&& this.FtpUrl == FtpUrl
				&& this.FtpUsername == FtpUsername
				&& this.FtpPassword == FtpPassword;
		}

		public static UploadingProtocolTypes ParseProtocolTypeFromString(string strToParse)
		{
			UploadingProtocolTypes pt;
			if (!Enum.TryParse<UploadingProtocolTypes>(strToParse, out pt))
				pt = UploadingProtocolTypes.Unknown;
			return pt;
		}

		public static List<UploadingItem> GetItemsFromFile(string filepath, bool deletefileIfSuccessful = false)//Can be for crash items or unssuccessful items
		{
			List<UploadingItem> tmplist = new List<UploadingItem>();
			var lines = File.ReadAllLines(filepath).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
			if (lines.Length == 0)
			{
				MessageBox.Show("Could not load crash file as there were no text in file: " + filepath, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return null;
			}
			bool allLinesRead = true;
			foreach (var line in lines)
			{
				var pipeSplits = line.Split('|');
				if (pipeSplits.Length >= 4)//displayname, protocol, localpath, ftpurl, [username,] [password]
					tmplist.Add(new UploadingItem(
						pipeSplits[0],
						ParseProtocolTypeFromString(pipeSplits[1]),
						pipeSplits[2],
						pipeSplits[3],
						pipeSplits[4].Equals("true", StringComparison.InvariantCultureIgnoreCase),
						pipeSplits.Length >= 6 ? pipeSplits[5] : null,
						pipeSplits.Length >= 7 ? pipeSplits[6] : null, true, true));
				else
				{
					allLinesRead = false;
					System.Windows.Forms.MessageBox.Show("Cannot file, line cannot be parsed ass UploadingItem: " + line, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
				}
			}
			if (!allLinesRead)
				return null;
			if (tmplist.Count == 0)
			{
				System.Windows.Forms.MessageBox.Show("Cannot get UploadingItems from file, no valid lines: " + filepath, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
				return null;
			}
			//Successfully read file if reached this point
			if (deletefileIfSuccessful)
				File.Delete(filepath);
			return tmplist;
		}

		private static object lockobject = new object();
		private static string unsuccessfulUploadsDirpath = Path.GetDirectoryName(SettingsInterop.GetFullFilePathInLocalAppdata("tmp", cThisAppName, "UnsuccessfulUploads"));
		private void AddToUnssuccesfulList()
		{
			if (!UnssuccessfulListOfFilepathsAndItem.ContainsKey(this))
			{
			retrywritefile:
				lock (lockobject)
				{
					try
					{
						string filepathToUnsuccessfulList = Path.Combine(unsuccessfulUploadsDirpath, DateTime.Now.ToString("yyyy_MM_dd HH_mm_ss_fffff"));
						File.WriteAllText(filepathToUnsuccessfulList, this.GetFileLineForApplicationRecovery());
						UnssuccessfulListOfFilepathsAndItem.Add(this, filepathToUnsuccessfulList);
					}
					catch
					{
						Thread.Sleep(500);
						goto retrywritefile;
					}
				}
			}
		}

		private void RemoveFromUnsuccessfulList()
		{
			if (UnssuccessfulListOfFilepathsAndItem.ContainsKey(this))
			{
				string filepath = UnssuccessfulListOfFilepathsAndItem[this];
				UnssuccessfulListOfFilepathsAndItem.Remove(this);
				File.Delete(filepath);
			}
		}

		public static bool HasUnsuccessfulItems()
		{
			return Directory.GetFiles(unsuccessfulUploadsDirpath, "*").Length > 0;
		}

		public static Dictionary<string, List<UploadingItem>> GetListFromUnssuccessfulDirectory_NotDelete(out List<string> fileListToBeDeleted)
		{
			Dictionary<string, List<UploadingItem>> tmpreturnDictionary = new Dictionary<string, List<UploadingItem>>();
			fileListToBeDeleted = new List<string>();
			foreach (var unssuccessfulFilepath in Directory.GetFiles(unsuccessfulUploadsDirpath, "*"))
			{
				var tmplistinfile = GetItemsFromFile(unssuccessfulFilepath, false);
				if (tmplistinfile != null && tmplistinfile.Count > 0)
				{
					fileListToBeDeleted.Add(unssuccessfulFilepath);
					tmpreturnDictionary.Add(unssuccessfulFilepath, tmplistinfile);
				}
			}
			return tmpreturnDictionary;
		}

		public string GetFileLineForApplicationRecovery()
		{
			if (this.FtpUsername != null && this.FtpPassword != null)
				return string.Join("|", this.DisplayName, this.ProtocolType.ToString(), this.LocalPath, this.FtpUrl, this.AutoOverwriteIfExists, this.FtpUsername, this.FtpPassword);
			else if (this.FtpUsername != null)
				return string.Join("|", this.DisplayName, this.ProtocolType.ToString(), this.LocalPath, this.FtpUrl, this.AutoOverwriteIfExists, this.FtpUsername);
			else
				return string.Join("|", this.DisplayName, this.ProtocolType.ToString(), this.LocalPath, this.FtpUrl, this.AutoOverwriteIfExists);
		}

		Thread uploadingThread;
		private void StartUploading()
		{
			this.MustCancel = false;

			uploadingThread = new Thread((ThreadStart)delegate
			{
				if (!Path.GetExtension(this.LocalPath).Equals(Path.GetExtension(this.FtpUrl)))
					this.CurrentProgressMessage = "Error: cannot upload file, file-extension different for LocalPath and FtpUrl, please use FULL url for FtpUrl.";
				else
				{
					if (UploadsInProgress.Count == 0)
					{
						UploadsInProgress.Add(this);

						try
						{
							if (this.ProtocolType == UploadingProtocolTypes.Ftp)
							{
								this.CurrentProgressMessage = "Upload started, please wait...";
								if (FtpUploadFile(this.LocalPath, this.FtpUrl, this.FtpUsername, this.FtpPassword,
									(err) => { this.CurrentProgressMessage = "Error: " + err; },
									(status) => { this.CurrentProgressMessage = status; },
									(progress) => { if (progress != this.CurrentProgressPercentage) this.CurrentProgressPercentage = progress != 100 ? progress : 0; }))
								{
									this.CurrentProgressMessage = "Successfully uploaded.";
									AddSuccessfulUploadToHistory();
									this.RemoveFromListButtonVisibility = Visibility.Visible;
								}
							}
							else if (this.ProtocolType == UploadingProtocolTypes.Ownapps)
							{
								this.CurrentProgressMessage = "Upload started, please wait...";
								string err;
								bool? uploadresult = PhpUploadingInterop.PhpUploadFile(this.LocalPath, this.FtpUrl, out err,
									(progress, bytespersec) =>
									{
										if (progress != this.CurrentProgressPercentage) this.CurrentProgressPercentage = progress != 100 ? progress : 0;
										this.CurrentProgressMessage = string.Format("{0:0.###} kB/s", bytespersec / 1024D);
									},
									ref MustCancel,
									null,
									delegate { return true; });

								if (!uploadresult.HasValue)//null means the file already existed
								{
									if (!this.AutoOverwriteIfExists)
									{
										this.CurrentProgressMessage = "File already exists online";
										this.DeleteOnlineFileAndRetryButtonVisibility = Visibility.Visible;
										return;//Exit jumps to the finally section
									}
									else//Auto overwrite
									{
										bool? deleteResult = PhpUploadingInterop.PhpDeleteFile(this.FtpUrl, out err, delegate { return true; });
										if (deleteResult == false)//Could not delete
										{
											this.CurrentProgressMessage = "Unable to delete file";
											this.DeleteOnlineFileAndRetryButtonVisibility = Visibility.Visible;
											return;//Exit jumps to the finally section
										}
										else//true=successfully deleted, null= did not exist
										{
											UploadsQueued.Enqueue(this);
											return;//Exit jumps to the finally section
										}
									}
								}
								else if (uploadresult.Value == false)//Failed
								{
									this.CurrentProgressMessage = "Unable to upload: " + err;
									this.RetryUploadButtonVisibility = Visibility.Visible;
									return;//Exit jumps to the finally section
								}

								this.CurrentProgressMessage = "Successfully uploaded.";
								AddSuccessfulUploadToHistory();
								this.RemoveFromListButtonVisibility = Visibility.Visible;
								if (!this.MustCancel)
								{
									//actionOnStatusMessage("Successfully uploaded.");// + localFilename);
									this.CancelButtonVisibility = Visibility.Collapsed;
									this.SuccessfullyUploaded = true;
									RemoveFromUnsuccessfulList();
								}
							}
							else
								UserMessages.ShowErrorMessage("Only supports Ftp and Ownapps protocols currently");
						}
						finally
						{
							UploadsInProgress.Remove(this);//Removes although could have failed, so that next can start
							if (UploadsQueued.Count > 0)
								UploadsQueued.Dequeue().StartUploading();
						}
					}
					else
						UploadsQueued.Enqueue(this);
				}
			});
			uploadingThread.Start();
		}

		private void AddSuccessfulUploadToHistory()
		{
			Logging.LogSuccessToFile(
				string.Format("Successfully uploaded, localpath = '{0}', ftpurl = '{1}', username = '{2}'",
					this.LocalPath, this.FtpUrl, this.FtpUsername),
				Logging.ReportingFrequencies.Daily,
				cThisAppName,
				"SuccessHistory");
		}

		public void CancelUploadingAsync()
		{
			MustCancel = true;
		}

		public void DeleteOnlineFileAndRetry()
		{
			if (this.ProtocolType == UploadingProtocolTypes.Ownapps)
			{
				string err;
				bool? deleteSuccess = PhpUploadingInterop.PhpDeleteFile(this.FtpUrl, out err, delegate { return true; });
				if (deleteSuccess == false)//Could not delete (true is success, null is did not exist)
					this.CurrentProgressMessage = "Error: " + err;
				else//null meanse did not exist, true means deleted
				{
					this.DeleteOnlineFileAndRetryButtonVisibility = Visibility.Collapsed;
					this.StartUploading();
				}
			}
			else if (this.ProtocolType == UploadingProtocolTypes.Ftp)
			{
				if (DeleteFTPfile(this.FtpUrl, this.FtpUsername, this.FtpPassword, true,
					err => this.CurrentProgressMessage = "Error: " + err,
					status => this.CurrentProgressMessage = status))
				{
					this.DeleteOnlineFileAndRetryButtonVisibility = Visibility.Collapsed;
					this.StartUploading();
				}
			}
			else
				UserMessages.ShowErrorMessage("Only supports Ftp and Ownapps protocols currently");
		}

		public void RetryUpload()
		{
			this.RetryUploadButtonVisibility = Visibility.Collapsed;
			this.StartUploading();
		}

		#region Ftp methods
		Stopwatch stopwatchFromUploadStart;
		private bool MustCancel = false;
		private const int cTimeoutMilliseconds = 10000;

		private class WebClientEx : WebClient
		{

			public int Timeout { get; set; }
			protected override WebRequest GetWebRequest(Uri address)
			{
				var request = base.GetWebRequest(address);
				request.Timeout = Timeout;
				return request;
			}
		}
		private bool FtpUploadFile(string localFilename, string fullFtpUrl, string userName, string password, Action<string> actionOnError, Action<string> actionOnStatusMessage, Action<int> actionOnProgressChanged_Percentage)
		{
			WebInterop.EnsureHttpsTrustAll();
			string ftpRootUri =
					fullFtpUrl.Substring(0, fullFtpUrl.Length - Path.GetFileName(fullFtpUrl).Length)
				.Replace('\\', '/')
				.TrimEnd('/');
			try
			{
				bool? fileAlreadyExists = FtpFileExists(this.FtpUrl, this.FtpUsername, this.FtpPassword, true, actionOnError);
				if (!fileAlreadyExists.HasValue)
					return false;
				else if (fileAlreadyExists.Value == true)
				{
					if (!this.AutoOverwriteIfExists)
					{
						this.CurrentProgressMessage = "File already exists online";
						this.DeleteOnlineFileAndRetryButtonVisibility = Visibility.Visible;
						return false;
					}
					else//Auto overwrite
					{
						if (!DeleteFTPfile(this.FtpUrl, this.FtpUsername, this.FtpPassword, true, actionOnError, actionOnStatusMessage))
						{
							this.CurrentProgressMessage = "Unable to delete file";
							this.DeleteOnlineFileAndRetryButtonVisibility = Visibility.Visible;
							return false;
						}
					}
				}

				bool? createDirResult = CreateFTPDirectory_NullIfExisted(ftpRootUri, userName, password, null, true, actionOnError);
				if (!createDirResult.HasValue || createDirResult.Value == true)//Null means already existed
				{
					using (WebClientEx client = new WebClientEx())
					{
						client.Timeout = cTimeoutMilliseconds;
						client.Credentials = new System.Net.NetworkCredential(userName, password);
						//client.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1)");

						bool isComplete = false;
						client.UploadFileCompleted += (snder, evtargs) =>
						{
							actionOnProgressChanged_Percentage(100);
							isComplete = true;
						};
						client.UploadProgressChanged += (snder, evtargs) =>
						{
							if (!isComplete)
							{
								actionOnProgressChanged_Percentage(evtargs.ProgressPercentage);
								//if (evtargs.ProgressPercentage == 100)
								//    isComplete = true;
								//else
								actionOnStatusMessage(string.Format("{0:0} kB/sec", ((double)evtargs.BytesSent / (double)1024) / (double)stopwatchFromUploadStart.Elapsed.TotalSeconds));
							}
						};

						isComplete = false;

						string startMsg = "Starting upload for ";
						actionOnStatusMessage(startMsg + localFilename);
						stopwatchFromUploadStart = Stopwatch.StartNew();
						client.UploadFileAsync(new Uri(fullFtpUrl), "STOR", localFilename);
						while (!isComplete)
						{
							if (MustCancel)
								client.CancelAsync();
						}// Application.DoEvents();

						if (!MustCancel)
						{
							//actionOnStatusMessage("Successfully uploaded.");// + localFilename);
							this.CancelButtonVisibility = Visibility.Collapsed;
							this.SuccessfullyUploaded = true;
							RemoveFromUnsuccessfulList();
						}
						else
							actionOnError("User cancelled upload");

						client.Dispose();
						GC.Collect();
						GC.WaitForPendingFinalizers();
						return true;
					}
				}
				else
					actionOnError("Could not upload files (could not find/create directory online: " + ftpRootUri);
			}
			catch (Exception exc)
			{
				if (exc.Message.ToLower().Contains("the operation has timed out"))
				{
					actionOnError("Upload to ftp timed out, the System.Net.ServicePointManager.DefaultConnectionLimit has been reached");
				}
				actionOnError("Exception in transfer: " + exc.Message);
			}
			return false;
		}

		public static bool? CreateFTPDirectory_NullIfExisted(string directory, string ftpUser, string ftpPassword, int? timeout, bool EnableSSL, Action<string> actionOnError)
		{
			try
			{
				//create the directory
				FtpWebRequest requestDir = (FtpWebRequest)FtpWebRequest.Create(new Uri(directory));
				requestDir.Timeout = cTimeoutMilliseconds;
				requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
				requestDir.Credentials = new NetworkCredential(ftpUser, ftpPassword);
				requestDir.UsePassive = true;
				requestDir.UseBinary = true;
				requestDir.KeepAlive = false;
				requestDir.EnableSsl = EnableSSL;
				if (timeout.HasValue)
					requestDir.Timeout = timeout.Value;
				FtpWebResponse response = (FtpWebResponse)(requestDir.GetResponse());
				Stream ftpStream = response.GetResponseStream();

				ftpStream.Close();
				response.Close();

				//Directory did not exist, successfully created
				return true;
			}
			catch (WebException ex)
			{
				FtpWebResponse response = (FtpWebResponse)ex.Response;
				if (response != null && response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable
					//DONE: Will it always work to check the StatusDescription?
					//&& response.StatusDescription.IndexOf("Directory already exists", StringComparison.InvariantCultureIgnoreCase) != -1
					)
				{
					actionOnError("FTP directory already existed: " + directory);
					//Directory already existed
					response.Close();
					return null;
				}
				else
				{
					//Error occurred, directory not created/existed (could have timed out?)
					if (response != null)
						response.Close();
					actionOnError("Could not create directory (" + directory + "): " + ex.Message);
					return false;
				}
			}
		}

		public static bool? FtpFileExists(string filePath, string ftpUser, string ftpPassword, bool EnableSsl, Action<string> actionOnError)
		{
			WebInterop.EnsureHttpsTrustAll();
			var request = (FtpWebRequest)WebRequest.Create(filePath);
			request.Timeout = cTimeoutMilliseconds;
			request.Credentials = new NetworkCredential(ftpUser, ftpPassword);
			request.Method = WebRequestMethods.Ftp.GetDateTimestamp;
			request.EnableSsl = EnableSsl;
			request.UsePassive = true;
			request.UseBinary = true;
			try
			{
				FtpWebResponse response = (FtpWebResponse)request.GetResponse();
				response.Close();
				return true;
			}
			catch (WebException ex)
			{
				int COMMAND_NOT_IMPLEMENTED_FROM_WORK_WTF;
				FtpWebResponse response = (FtpWebResponse)ex.Response;
				if (response.StatusCode ==
					FtpStatusCode.ActionNotTakenFileUnavailable)
				{
					response.Close();
					return false;
					//Does not exist
				}
				response.Close();
				actionOnError("Cannot determine whether file '" + filePath + "' exists: " + ex.Message);
				return null;
			}
		}

		public static bool DeleteFTPfile(string ftpFilePath, string ftpUser, string ftpPassword, bool EnableSSL, Action<string> actionOnError, Action<string> actionOnStatus)
		{
			try
			{
				//create the directory
				FtpWebRequest requestDir = (FtpWebRequest)FtpWebRequest.Create(new Uri(ftpFilePath));
				requestDir.Timeout = cTimeoutMilliseconds;
				requestDir.Method = WebRequestMethods.Ftp.DeleteFile;
				requestDir.Credentials = new NetworkCredential(ftpUser, ftpPassword);
				requestDir.UsePassive = true;
				requestDir.UseBinary = true;
				requestDir.KeepAlive = false;
				requestDir.EnableSsl = EnableSSL;

				actionOnStatus("Attempting to delete file from server: " + ftpFilePath);
				FtpWebResponse response = (FtpWebResponse)(requestDir.GetResponse());
				Stream ftpStream = response.GetResponseStream();

				ftpStream.Close();
				response.Close();

				actionOnStatus("Successfully deleted file from server: " + ftpFilePath);
				return true;
			}
			catch (WebException ex)
			{
				FtpWebResponse response = (FtpWebResponse)ex.Response;
				if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
				{
					response.Close();
					actionOnStatus("File was not deleted, did not exist on server: " + ftpFilePath);
					return true;
				}
				else
				{
					response.Close();
					actionOnError("An error occurred trying to delete file (" + ftpFilePath + ") from server:" + Environment.NewLine + ex.Message);
					return false;
				}
			}
		}

		#endregion Ftp methods

		public event PropertyChangedEventHandler PropertyChanged = new PropertyChangedEventHandler(delegate { });
		public void OnPropertyChanged(string propertyName) { PropertyChanged(this, new PropertyChangedEventArgs(propertyName)); }
	}
}
