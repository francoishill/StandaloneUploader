using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using SharedClasses;
using System.IO;

namespace StandaloneUploader
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public static MainWindow mainwindow;

		protected override void OnExit(ExitEventArgs e)
		{
			ApplicationRecoveryAndRestart.UnregisterForRecoveryAndRestart();
			base.OnExit(e);
		}

		public static void ShowError(string err)
		{
			System.Windows.Forms.Application.EnableVisualStyles();
			if (mainwindow == null)
				System.Windows.Forms.MessageBox.Show(err, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
			else
				mainwindow.Dispatcher.Invoke((Action)delegate
				{
					System.Windows.Forms.MessageBox.Show(mainwindow, err, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
				});
		}

		public static void ShowWarning(string warn)
		{
			System.Windows.Forms.Application.EnableVisualStyles();
			if (mainwindow == null)
				System.Windows.Forms.MessageBox.Show(warn, "Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
			else
				mainwindow.Dispatcher.Invoke((Action)delegate
				{
					System.Windows.Forms.MessageBox.Show(mainwindow, warn, "Warning", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
				});
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			//DONE: //Crashes are saved in folder, this folder is always checked upon startup (NOT ONLY when have /restart flag)
			//if (IsApplicationArestartedInstance(Environment.GetCommandLineArgs()))
			//{
			//
			//}

			//System.Windows.Forms.Application.ThreadException += (snder, exc) =>
			//{
			//    ShowError("Exception" + ":"
			//        + exc.Exception.Message + Environment.NewLine + exc.Exception.StackTrace);
			//};

			if (!UploadingItem.HasUnsuccessfulItems() && Environment.GetCommandLineArgs().Length == 1)
			{
				ShowWarning("Cannot start 'StandaloneUploader' without any commandline arguments (unless there were Unssuccessful items on previous run).");
				Environment.Exit(0);
			}

			//base.OnStartup(e);
			SingleInstanceApplicationManager<MainWindow>.CheckIfAlreadyRunningElseCreateNew(
				(evt, mainwin) =>
				{
					//if (mainwin == null)
					//    ShowError("Mainwindow is null, cannot pass commandline args: " + string.Join(Environment.NewLine, evt.CommandLineArgs));
					AddUploadingItemToCurrentList(mainwin, evt.CommandLineArgs);
				},
				(args, mainwin) =>
				{
					AppDomain.CurrentDomain.UnhandledException += (snder, exc) =>
					{
						Exception exception = (Exception)exc.ExceptionObject;
						ShowError("Exception" + (exc.IsTerminating ? ", application will now exit" : "") + ":"
							+ exception.Message + Environment.NewLine + exception.StackTrace);
					};

					AutoUpdating.CheckForUpdates_ExceptionHandler();
					/*ThreadingInterop.PerformVoidFunctionSeperateThread(() =>
					{
						AutoUpdating.CheckForUpdates(null, null);
					},
					false);*/

					bool mustAddToList = true;
					ApplicationRecoveryAndRestart.RegisterForRecoveryAndRestart(
					delegate
					{
						//TODO: Application Restart and Recovery is there but no use so far?
						//ApplicationRecoveryAndRestart.WriteCrashReportFile("MonitorSystem", "Application crashed, more details not incorporated yet.");
						mainwindow.SaveToDiskWhenCrashing();
					},
					delegate
					{
						mustAddToList = false;//Exit here otherwise we add a blank item to list
					},
					delegate
					{
						//When successfully registered
						//MessageBox.Show("Registered ApplicationRecoveryAndRestart");
					});

					mainwindow = mainwin;
					mainwin.Show();
					if (mustAddToList)
						AddUploadingItemToCurrentList(mainwin, args);
				});
		}

		private void AddUploadingItemToCurrentList(MainWindow mainwindow, string[] commandlineArgs)
		{
			//if (IsApplicationArestartedInstance(commandlineArgs)
			//    && commandlineArgs.Length == 2 && commandlineArgs[1].EndsWith("/restart", StringComparison.InvariantCultureIgnoreCase))//Only exe and /restart
			//    return;//Exit here otherwise we add a blank item to list

			if (commandlineArgs.Length == 1)//Could have been initiated without arguments, then Unssuccessful items will load
				return;

			bool autostartUploading = true;
			if (commandlineArgs.Length < 5)//Means we dont at least have [thisexe, protocol, displayname, localpath, url]
				autostartUploading = false;

			string DisplayName = commandlineArgs.Length >= 2 ? commandlineArgs[1] : "[NO DISPLAY NAME]";
			string Protocol = commandlineArgs.Length >= 3 ? commandlineArgs[2] : "[NO PROTOCOL]";
			string LocalPath = commandlineArgs.Length >= 4 ? commandlineArgs[3] : "[NO LOCAL PATH]";
			string FtpUrl = commandlineArgs.Length >= 5 ? commandlineArgs[4] : "[NO FTP URL]";
			string FtpUsername = commandlineArgs.Length >= 6 ? commandlineArgs[5] : "[NO FTP USERNAME]";
			string FtpPassword = commandlineArgs.Length >= 7 ? commandlineArgs[6] : "";
			bool AutoOverwriteIfExists = commandlineArgs.Length >= 8 && commandlineArgs[7].Trim('\"').Equals("overwrite", StringComparison.InvariantCultureIgnoreCase);

			mainwindow.AddUploadingItem(DisplayName, UploadingItem.ParseProtocolTypeFromString(Protocol), LocalPath, FtpUrl, FtpUsername, FtpPassword, AutoOverwriteIfExists, autostartUploading);
		}
	}
}
