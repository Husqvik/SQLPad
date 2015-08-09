using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlPad
{
	public partial class App
	{
		public static readonly string VersionTimestamp;
		public static readonly string Version;
		private static readonly Dictionary<string, StatementExecutionHistory> ExecutionHistoryWindows = new Dictionary<string, StatementExecutionHistory>();

		public new static MainWindow MainWindow => (MainWindow)Current.MainWindow;

		static App()
		{
			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;
			var assembly = typeof(App).Assembly;
			Version = assembly.GetName().Version.ToString();
			var buildInfo = assembly.GetCustomAttribute<AssemblyBuildInfo>();
			VersionTimestamp = buildInfo.VersionTimestampString;

			if (WorkDocumentCollection.WorkingDocuments.Count > 0)
			{
				AdjustThreadPool(WorkDocumentCollection.WorkingDocuments.Count);
			}
		}

		private static void AdjustThreadPool(int requiredThreads)
		{
			var minSize = requiredThreads - 1;

			minSize |= minSize >> 1;
			minSize |= minSize >> 2;
			minSize |= minSize >> 4;
			minSize |= minSize >> 8;
			minSize |= minSize >> 16;
			minSize++;

			minSize = Math.Max(minSize, 8);
			minSize = Math.Min(minSize, 128);

			ThreadPool.SetMinThreads(minSize, 8);

			Trace.WriteLine($"Minimum of {requiredThreads} threads required to handle active documents. Thread pool minimum size adjusted to {minSize}. ");
		}

		internal static void ResultGridBeginningEditCancelTextInputHandlerImplementation(object sender, DataGridBeginningEditEventArgs e)
		{
			var textCompositionArgs = e.EditingEventArgs as TextCompositionEventArgs;
			if (textCompositionArgs != null)
			{
				e.Cancel = true;
			}
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

		internal void ClickBubblingCancellationHandler(object sender, RoutedEventArgs args)
		{
			args.Handled = true;
		}

		internal static void ShowExecutionHistory(string providerName)
		{
			var configuration = WorkDocumentCollection.GetProviderConfiguration(providerName);

			lock (ExecutionHistoryWindows)
			{
				StatementExecutionHistory window;
				if (!ExecutionHistoryWindows.TryGetValue(providerName, out window))
				{
					ExecutionHistoryWindows[providerName] = window = new StatementExecutionHistory(providerName) { Owner = MainWindow };
				}

				window.ExecutionHistoryEntries.Clear();
				window.ExecutionHistoryEntries.AddRange(configuration.StatementExecutionHistory);

				if (window.IsVisible)
				{
					window.Focus();
				}
				else
				{
					window.Show();
				}
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

			File.WriteAllText(Path.Combine(ConfigurationProvider.FolderNameErrorLog, $"Error_{DateTime.UtcNow.Ticks}.log"), logBuilder.ToString());
		}

		public App()
		{
			//var splashScreen = new SplashScreen("ResourceName");
			//splashScreen.Show(true, true);
		}
	}
}
