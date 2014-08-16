using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace SqlPad
{
	public partial class App
	{
		public const string RecoveredDocumentFileNameTemplate = "RecoveredDocument.{0}.sql.tmp";
		private const string FolderNameSqlPad = "SQL Pad";

		public static readonly string FolderNameUserData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FolderNameSqlPad);
		public static readonly string FolderNameErrorLog = Path.Combine(FolderNameUserData, "ErrorLog");
		public static readonly string FolderNameWorkArea = Path.Combine(FolderNameUserData, "WorkArea");
		public static readonly string FolderNameApplication = Path.GetDirectoryName(typeof(App).Assembly.Location);

		static App()
		{
			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
		}

		private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			CreateDirectoryIfNotExists(FolderNameErrorLog);

			var mainWindow = (MainWindow)Current.MainWindow;

			if (mainWindow.ActiveDocument != null)
			{
				BuildErrorLog(unhandledExceptionEventArgs.ExceptionObject, mainWindow.ActiveDocument);
			}

			try
			{
				SqlPadConfiguration.StoreConfiguration();
				WorkingDocumentCollection.Save();
			}
			catch(Exception e)
			{
				Trace.WriteLine("Work area storing operation failed: " + e);
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

				if (page.WorkingDocument.File != null)
				{
					logBuilder.Append("Document name: ");
					logBuilder.AppendLine(page.WorkingDocument.File.FullName);
				}
			}

			File.WriteAllText(Path.Combine(FolderNameErrorLog, String.Format("Error_{0}.log", DateTime.UtcNow.Ticks)), logBuilder.ToString());
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
			CreateDirectoryIfNotExists(FolderNameUserData, FolderNameWorkArea);
		}
	}
}
