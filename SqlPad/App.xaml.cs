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
using System.Windows.Controls.Primitives;
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

		public static void OpenExplorerAndSelectFile(string fileName)
		{
			Process.Start("explorer.exe", $"/select,{fileName}");
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

		public static void DataGridBeginningEditCancelTextInputHandlerImplementation(object sender, DataGridBeginningEditEventArgs e)
		{
			var textCompositionArgs = e.EditingEventArgs as TextCompositionEventArgs;
			if (textCompositionArgs != null)
			{
				e.Cancel = true;
			}
		}

		public static async Task ExecuteExternalProcess(string fileName, string arguments, TimeSpan? timeout, Action successAction, Action timeoutAction, Action<int, string> errorAction, CancellationToken cancellationToken)
		{
			var startInfo =
				new ProcessStartInfo(fileName, arguments)
				{
					RedirectStandardError = errorAction != null,
					UseShellExecute = errorAction == null
				};

			Process process = null;

			if (!SafeActionWithUserError(() => process = Process.Start(startInfo)))
			{
				return;
			}

			var completedWithinTimeout = await ExecuteAsynchronous(() => process.Kill(), () => process.WaitForExit(timeout == null ? -1 : (int)timeout.Value.TotalMilliseconds), cancellationToken);
			if (completedWithinTimeout)
			{
				if (process.ExitCode == 0)
				{
					successAction?.Invoke();
				}
				else
				{
					errorAction?.Invoke(process.ExitCode, process.StandardError.ReadToEnd());
				}
			}
			else
			{
				timeoutAction?.Invoke();
			}
		}

		public static Task<T> ExecuteAsynchronous<T>(Action cancellationAction, Func<T> synchronousOperation, CancellationToken cancellationToken)
		{
			var source = new TaskCompletionSource<T>();
			var registration = new CancellationTokenRegistration();

			if (cancellationToken.CanBeCanceled)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					source.SetCanceled();
					return source.Task;
				}

				registration = cancellationToken.Register(cancellationAction);
			}

			try
			{
				Task.Factory.StartNew(synchronousOperation, cancellationToken)
					.ContinueWith(t => AfterExecutionHandler(t, source, registration), CancellationToken.None);
			}
			catch (Exception exception)
			{
				source.SetException(exception);
			}

			return source.Task;
		}

		private static void AfterExecutionHandler<T>(Task<T> executionTask, TaskCompletionSource<T> source, CancellationTokenRegistration registration)
		{
			registration.Dispose();

			if (executionTask.IsFaulted)
			{
				source.SetException(executionTask.Exception.InnerException);
			}
			else if (executionTask.IsCanceled)
			{
				source.SetCanceled();
			}
			else
			{
				source.SetResult(executionTask.Result);
			}
		}

		public static async Task<Exception> SafeActionAsync(Func<Task> action)
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

		public static bool SafeActionWithUserError(Action action)
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
			lock (ExecutionHistoryWindows)
			{
				MainWindow.ActiveDocument.EnsurePopupClosed();

				var configuration = WorkDocumentCollection.GetProviderConfiguration(providerName);

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

		public static void LogErrorAndShowMessage(Exception exception)
		{
			CreateErrorLog(exception);
			Messages.ShowError(exception.ToString());
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
			Current.Dispatcher.Invoke(() => UnhandledExceptionHandlerInternal(unhandledExceptionEventArgs.ExceptionObject));
		}

		private static void UnhandledExceptionHandlerInternal(object exceptionObject)
		{
			CreateErrorLog(exceptionObject);

			try
			{
				var mainWindow = (MainWindow)Current.MainWindow;
				mainWindow.SaveWorkingDocuments();
			}
			catch (Exception e)
			{
				Trace.WriteLine("Working documents save failed: " + e);
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

		private void ButtonToggleBarChartClickHandler(object sender, RoutedEventArgs e)
		{
			var toggleButton = (ToggleButton)sender;
			var columnHeader = toggleButton.FindParentVisual<DataGridColumnHeader>();
			var dataGridTemplateColumn = (DataGridTemplateColumn)columnHeader.Column;
			var cellTemplateSelector = (ResultSetDataGridTemplateSelector)dataGridTemplateColumn.CellTemplateSelector;
			cellTemplateSelector.UseBarChart = toggleButton.IsChecked ?? false;

			//var dataGrid = columnHeader.FindParentVisual<DataGrid>();

			dataGridTemplateColumn.CellTemplateSelector = null;
			dataGridTemplateColumn.CellTemplateSelector = cellTemplateSelector;
		}
	}
}
