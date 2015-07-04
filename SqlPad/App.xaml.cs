using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SqlPad
{
	public partial class App
	{
		public static readonly string VersionTimestamp;
		public static readonly string Version;

		public new static MainWindow MainWindow
		{
			get { return (MainWindow)Current.MainWindow; }
		}

		static App()
		{
			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
			var assembly = typeof(App).Assembly;
			Version = assembly.GetName().Version.ToString();
			var buildInfo = assembly.GetCustomAttribute<AssemblyBuildInfo>();
			VersionTimestamp = buildInfo.VersionTimestampString;
		}

		internal static async Task<Exception> SafeActionAsync(Func<Task> action)
		{
			try
			{
				await action();

				return null;
			}
			catch (Exception exception)
			{
				return exception;
			}
		}

		internal static bool SafeActionWithUserError(Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception e)
			{
				Messages.ShowError(e.Message);
				return false;
			}
		}

		internal static void CreateErrorLog(object exceptionObject)
		{
			var mainWindow = (MainWindow)Current.MainWindow;

			if (mainWindow.ActiveDocument != null)
			{
				BuildErrorLog(exceptionObject, mainWindow.ActiveDocument);
			}
		}

		private static void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs unhandledExceptionEventArgs)
		{
			CreateErrorLog(unhandledExceptionEventArgs.ExceptionObject);

			try
			{
				var mainWindow = (MainWindow)Current.MainWindow;
				mainWindow.SaveWorkingDocuments();
			}
			catch(Exception e)
			{
				Trace.WriteLine("Work area storing operation failed: " + e);
			}
		}

		private static void BuildErrorLog(object exception, DocumentPage page)
		{
			var logBuilder = new StringBuilder("Unhandled exception occurred at ");
			logBuilder.Append(DateTime.Now.ToString(CultureInfo.InvariantCulture));
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

				if (page.WorkDocument.File != null)
				{
					logBuilder.Append("Document name: ");
					logBuilder.AppendLine(page.WorkDocument.File.FullName);
				}
			}

			File.WriteAllText(Path.Combine(ConfigurationProvider.FolderNameErrorLog, String.Format("Error_{0}.log", DateTime.UtcNow.Ticks)), logBuilder.ToString());
		}

		public App() { }
	}
}
