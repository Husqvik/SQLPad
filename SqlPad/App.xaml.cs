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
		public static readonly string FolderNameCommonData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SQL Pad");
		public static readonly string FolderNameApplication = Path.GetDirectoryName(typeof(App).Assembly.Location);

		static App()
		{
			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
		}

		private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			var page = ((MainWindow)Current.MainWindow).CurrentPage;
			var logBuilder = new StringBuilder("Unhandled exception occured at ");
			logBuilder.Append(DateTime.Now.ToLongTimeString());
			logBuilder.AppendLine(". ");
			logBuilder.AppendLine("Exception: ");
			logBuilder.AppendLine(unhandledExceptionEventArgs.ExceptionObject.ToString());

			if (page != null)
			{
				logBuilder.AppendLine("Statement: ");
				logBuilder.AppendLine(page.Editor.Text);
				logBuilder.AppendLine("Caret offset: ");
				logBuilder.AppendLine(Convert.ToString(page.Editor.CaretOffset));
				logBuilder.AppendLine("Selection start: ");
				logBuilder.AppendLine(Convert.ToString(page.Editor.SelectionStart));
				logBuilder.AppendLine("Selection length: ");
				logBuilder.AppendLine(Convert.ToString(page.Editor.SelectionLength));

				if (page.File != null)
				{
					logBuilder.AppendLine("Document name: ");
					logBuilder.AppendLine(page.File.FullName);
				}
			}

			File.WriteAllText(Path.Combine(FolderNameCommonData, String.Format("Error_{0}.Log", DateTime.Now.Ticks)), logBuilder.ToString());
		}

		public App()
		{
			if (!Directory.Exists(FolderNameCommonData))
			{
				Directory.CreateDirectory(FolderNameCommonData);
			}
		}
	}
}
