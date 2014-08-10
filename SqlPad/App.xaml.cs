using System;
using System.IO;
using System.Text;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		public const string RecoveredDocumentFileNameTemplate = "RecoveredDocument.{0}.sql.tmp";
		private const string FolderNameSqlPad = "SQL Pad";

		public static readonly string FolderNameUserData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderNameSqlPad);
		public static readonly string FolderNameErrorLog = Path.Combine(FolderNameUserData, "ErrorLog");
		public static readonly string FolderNameRecoveryFiles = Path.Combine(FolderNameUserData, "Recovery");
		public static readonly string FolderNameHistory = Path.Combine(FolderNameUserData, "History");
		public static readonly string FolderNameApplication = Path.GetDirectoryName(typeof(App).Assembly.Location);

		static App()
		{
			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
		}

		public static string[] GetRecoverableDocuments()
		{
			return Directory.GetFiles(FolderNameRecoveryFiles, String.Format(RecoveredDocumentFileNameTemplate, "*"));
		}

		public static void PurgeRecoveryFiles()
		{
			var files = new DirectoryInfo(FolderNameRecoveryFiles).EnumerateFiles();
			foreach (var file in files)
			{
				file.Delete();
			}
		}

		private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			CreateDirectoryIfNotExists(FolderNameErrorLog);

			var mainWindow = (MainWindow)Current.MainWindow;

			var counter = 1;
			foreach (var page in mainWindow.AllPages)
			{
				if (page == mainWindow.CurrentPage)
				{
					BuildErrorLog(unhandledExceptionEventArgs.ExceptionObject, page);
				}

				File.WriteAllText(Path.Combine(FolderNameRecoveryFiles, String.Format(RecoveredDocumentFileNameTemplate, counter)), page.Editor.Text);
				counter++;
			}
		}

		private static void BuildErrorLog(object exception, DocumentPage page)
		{
			var logBuilder = new StringBuilder("Unhandled exception occured at ");
			logBuilder.Append(DateTime.Now.ToLongTimeString());
			logBuilder.AppendLine(". ");
			logBuilder.AppendLine("Exception: ");
			logBuilder.AppendLine(exception.ToString());

			if (page != null)
			{
				logBuilder.AppendLine("Statement: ");
				logBuilder.AppendLine(page.Editor.Text);
				logBuilder.Append("Caret offset: ");
				logBuilder.AppendLine(Convert.ToString(page.Editor.CaretOffset));
				logBuilder.Append("Selection start: ");
				logBuilder.AppendLine(Convert.ToString(page.Editor.SelectionStart));
				logBuilder.Append("Selection length: ");
				logBuilder.AppendLine(Convert.ToString(page.Editor.SelectionLength));

				if (page.File != null)
				{
					logBuilder.Append("Document name: ");
					logBuilder.AppendLine(page.File.FullName);
				}
			}

			File.WriteAllText(Path.Combine(FolderNameErrorLog, String.Format("Error_{0}.log", DateTime.Now.Ticks)), logBuilder.ToString());
		}

		private static void CreateDirectoryIfNotExists(params string[] directoryNames)
		{
			foreach (var directoryName in directoryNames)
			{
				if (!Directory.Exists(directoryName))
				{
					Directory.CreateDirectory(directoryName);
				}
			}
		}

		public App()
		{
			CreateDirectoryIfNotExists(FolderNameUserData, FolderNameRecoveryFiles, FolderNameHistory);
		}
	}
}
