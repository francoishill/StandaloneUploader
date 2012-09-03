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
			ApplicationRecoveryAndRestart.UnregisterApplicationRecoveryAndRestart();
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

			AppDomain.CurrentDomain.UnhandledException += (snder, exc) =>
			{
				Exception exception = (Exception)exc.ExceptionObject;
				ShowError("Exception" + (exc.IsTerminating ? ", application will now exit" : "") + ":"
					+ exception.Message + Environment.NewLine + exception.StackTrace);
			};
			//System.Windows.Forms.Application.ThreadException += (snder, exc) =>
			//{
			//    ShowError("Exception" + ":"
			//        + exc.Exception.Message + Environment.NewLine + exc.Exception.StackTrace);
			//};

			AutoUpdating.CheckForUpdates(null, null);

			if (Environment.GetCommandLineArgs().Length == 1)
			{
				ShowWarning("Cannot start 'StandaloneUploader' without any commandline arguments.");
				Environment.Exit(0);
			}

			ApplicationRecoveryAndRestart.RegisterApplicationRecoveryAndRestart(
			delegate
			{
				//TODO: Application Restart and Recovery is there but no use so far?
				//ApplicationRecoveryAndRestart.WriteCrashReportFile("MonitorSystem", "Application crashed, more details not incorporated yet.");
				mainwindow.SaveToDiskWhenCrashing();
			},
			delegate
			{
				//When successfully registered
				//MessageBox.Show("Registered ApplicationRecoveryAndRestart");
			},
			(err) =>
			{
				System.Windows.Forms.Application.EnableVisualStyles();
				System.Windows.Forms.MessageBox.Show(err, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
				Environment.Exit(0);
			});

			//base.OnStartup(e);
			SingleInstanceApplicationManager<MainWindow>.CheckIfAlreadyRunningElseCreateNew(
				(evt, mainwin) =>
				{
					AddUploadingItemToCurrentList(mainwin, evt.CommandLineArgs);
				},
				(args, mainwin) =>
				{
					mainwindow = mainwin;
					mainwin.Show();
					AddUploadingItemToCurrentList(mainwin, args);
				});
		}

		private bool IsApplicationArestartedInstance(string[] commandlineArgs)
		{
			return commandlineArgs.Length > 1
				&& commandlineArgs[1] == "/restart";
		}

		private void AddUploadingItemToCurrentList(MainWindow mainwindow, string[] commandlineArgs)
		{
			if (IsApplicationArestartedInstance(commandlineArgs) && commandlineArgs.Length == 2)//Only exe and /restart
				return;//Exit here otherwise we add a blank item to list

			bool autostartUploading = true;
			if (commandlineArgs.Length < 5)//Means we dont at least have [thisexe, displayname, localpath, ftpurl, ftpusername]
				autostartUploading = false;

			string DisplayName = commandlineArgs.Length >= 2 ? commandlineArgs[1] : "[NO DISPLAY NAME]";
			string LocalPath = commandlineArgs.Length >= 3 ? commandlineArgs[2] : "[NO LOCAL PATH]";
			string FtpUrl = commandlineArgs.Length >= 4 ? commandlineArgs[3] : "[NO FTP URL]";
			string FtpUsername = commandlineArgs.Length >= 5 ? commandlineArgs[4] : "[NO FTP USERNAME]";
			string FtpPassword = commandlineArgs.Length >= 6 ? commandlineArgs[5] : "";
			bool AutoOverwriteIfExists = commandlineArgs.Length >= 7 && commandlineArgs[6].Trim('\"').Equals("overwrite", StringComparison.InvariantCultureIgnoreCase);

			mainwindow.AddUploadingItem(DisplayName, LocalPath, FtpUrl, FtpUsername, FtpPassword, AutoOverwriteIfExists, autostartUploading);
		}
	}
}
