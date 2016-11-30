using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using SqlPad.DataExport;

namespace SqlPad
{
	public partial class FileResultViewer
	{
		public static readonly DependencyProperty DataExporterProperty = DependencyProperty.Register(nameof(DataExporter), typeof(IDataExporter), typeof(FileResultViewer), new UIPropertyMetadata(DataExporters.Csv, DataExporterPropertyChangedCallbackHandler));
		public static readonly DependencyProperty OutputPathProperty = DependencyProperty.Register(nameof(OutputPath), typeof(string), typeof(FileResultViewer), new UIPropertyMetadata(OutputPathPropertyChangedCallbackHandler));
		public static readonly DependencyProperty FileNameProperty = DependencyProperty.Register(nameof(FileName), typeof(string), typeof(FileResultViewer), new UIPropertyMetadata("Result", FileNamePropertyChangedCallbackHandler));
		public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(FileResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty IsWaitingForResultProperty = DependencyProperty.Register(nameof(IsWaitingForResult), typeof(bool), typeof(FileResultViewer), new UIPropertyMetadata());

		[Bindable(true)]
		public IDataExporter DataExporter
		{
			get { return (IDataExporter)GetValue(DataExporterProperty); }
			set { SetValue(DataExporterProperty, value); }
		}

		private static void DataExporterPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var fileResultViewer = (FileResultViewer)dependencyObject;
			fileResultViewer._outputViewer.DocumentPage.WorkDocument.DataExporter = args.NewValue.GetType().FullName;
		}

		[Bindable(true)]
		public string OutputPath
		{
			get { return (string)GetValue(OutputPathProperty); }
			set { SetValue(OutputPathProperty, value); }
		}

		private static void OutputPathPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var fileResultViewer = (FileResultViewer)dependencyObject;
			fileResultViewer._outputViewer.DocumentPage.WorkDocument.ExportOutputPath = (string)args.NewValue;
		}

		[Bindable(true)]
		public string FileName
		{
			get { return (string)GetValue(FileNameProperty); }
			set { SetValue(FileNameProperty, value); }
		}

		private static void FileNamePropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var fileResultViewer = (FileResultViewer)dependencyObject;
			fileResultViewer._outputViewer.DocumentPage.WorkDocument.ExportOutputFileName = (string)args.NewValue;
		}

		[Bindable(true)]
		public bool IsExecuting
		{
			get { return (bool)GetValue(IsExecutingProperty); }
			private set { SetValue(IsExecutingProperty, value); }
		}

		[Bindable(true)]
		public bool IsWaitingForResult
		{
			get { return (bool)GetValue(IsWaitingForResultProperty); }
			private set { SetValue(IsWaitingForResultProperty, value); }
		}

		private readonly ObservableCollection<ExportResultInfo> _exportResultInfoCollection = new ObservableCollection<ExportResultInfo>();
		private readonly OutputViewer _outputViewer;
		private readonly DispatcherTimer _exportClockTimer;

		public IReadOnlyList<ExportResultInfo> ExportResultInfoCollection => _exportResultInfoCollection;

		public FileResultViewer(OutputViewer outputViewer)
		{
			InitializeComponent();

			_outputViewer = outputViewer;

			_exportClockTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(40) };
			_exportClockTimer.Tick += UpdateExportClockTickHandler;

			InitializeSettings();
		}

		private void InitializeSettings()
		{
			var exportOutputPath = _outputViewer.DocumentPage.WorkDocument.ExportOutputPath;
			if (String.IsNullOrEmpty(exportOutputPath) || !Directory.Exists(exportOutputPath))
			{
				var userDocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				OutputPath = _outputViewer.DocumentPage.WorkDocument.ExportOutputPath = userDocumentPath;
			}
			else
			{
				OutputPath = exportOutputPath;
			}

			var fileName = _outputViewer.DocumentPage.WorkDocument.ExportOutputFileName;
			if (String.IsNullOrEmpty(fileName))
			{
				_outputViewer.DocumentPage.WorkDocument.ExportOutputFileName = FileName;
			}
			else
			{
				FileName = fileName;
			}

			var dataExportTypeName = _outputViewer.DocumentPage.WorkDocument.DataExporter;
			var dataExporter = DataExporters.All.SingleOrDefault(e => String.Equals(e.GetType().FullName, dataExportTypeName));
			if (dataExporter != null)
			{
				DataExporter = dataExporter;
			}
		}

		public void Initialize()
		{
			IsWaitingForResult = true;
		}

		public async Task SaveExecutionResult(StatementExecutionBatchResult executionResult)
		{
			IsWaitingForResult = false;
			IsExecuting = true;

			_exportClockTimer.Start();
			_exportResultInfoCollection.Clear();

			var commandNumber = 0;
			foreach (var statementResult in executionResult.StatementResults)
			{
				commandNumber++;

				var resultNumber = 0;
				foreach (var kvp in statementResult.ResultInfoColumnHeaders)
				{
					resultNumber++;
					var exportResultInfo = new ExportResultInfo(commandNumber, resultNumber, kvp.Key, kvp.Value);
					_exportResultInfoCollection.Add(exportResultInfo);
				}
			}

			var exception = await App.SafeActionAsync(() => _outputViewer.ExecuteUsingCancellationToken(ExportRows));

			_exportClockTimer.Stop();

			IsExecuting = false;

			if (exception != null)
			{
				TraceLog.WriteLine($"Saving result to file failed: {exception}");

				CancelWaitingResults();

				Messages.ShowError(exception.Message);
			}
		}

		private async Task ExportRows(CancellationToken cancellationToken)
		{
			foreach (var exportResultInfo in _exportResultInfoCollection)
			{
				exportResultInfo.StartTimestamp = DateTime.Now;
				_exportClockTimer.Tag = exportResultInfo;

				var exportFileName = Path.Combine(OutputPath, $@"{FileName}_{exportResultInfo.CommandNumber}_{exportResultInfo.ResultNumber}_{DateTime.Now.Ticks}.{DataExporter.FileExtension}");
				var exportOptions = ExportOptions.ToFile(exportFileName, exportResultInfo.ResultSetName);

				using (var exportContext = await DataExporter.StartExportAsync(exportOptions, exportResultInfo.ColumnHeaders, _outputViewer.DocumentPage.InfrastructureFactory.DataExportConverter, cancellationToken))
				{
					exportResultInfo.FileName = exportFileName;

					while (_outputViewer.ConnectionAdapter.CanFetch(exportResultInfo.ResultInfo))
					{
						if (cancellationToken.IsCancellationRequested)
						{
							CancelWaitingResults();
							return;
						}

						await ExportNextBatch(exportContext, exportResultInfo, cancellationToken);
					}

					await exportContext.FinalizeAsync();
				}

				exportResultInfo.CompleteTimestamp = DateTime.Now;
				exportResultInfo.RefreshFileSize();
			}
		}

		private void CancelWaitingResults()
		{
			foreach (var exportResultInfo in _exportResultInfoCollection.Where(i => i.StartTimestamp == null))
			{
				exportResultInfo.IsCancelled = true;
			}
		}

		private async Task ExportNextBatch(IDataExportContext exportContext, ExportResultInfo exportResultInfo, CancellationToken cancellationToken)
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize - exportContext.CurrentRowIndex % ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize;
			var exception = await App.SafeActionAsync(() => innerTask = _outputViewer.ConnectionAdapter.FetchRecordsAsync(exportResultInfo.ResultInfo, (int)batchSize, cancellationToken));
			if (exception != null)
			{
				var errorMessage = Messages.GetExceptionErrorMessage(exception);
				_outputViewer.AddExecutionLog(DateTime.Now, $"Row retrieval failed: {errorMessage}");
				Messages.ShowError(errorMessage);
			}
			else
			{
				try
				{
					await exportContext.AppendRowsAsync(innerTask.Result);
				}
				catch (OperationCanceledException)
				{
					TraceLog.WriteLine("User has canceled export operation. ");
					return;
				}
				finally
				{
					exportResultInfo.RowCount = exportContext.CurrentRowIndex;
					exportResultInfo.RefreshFileSize();
				}
				
				await _outputViewer.UpdateExecutionStatisticsIfEnabled();
			}
		}

		private static void UpdateExportClockTickHandler(object sender, EventArgs e)
		{
			var timer = (DispatcherTimer)sender;
			var exportResultInfo = (ExportResultInfo)timer.Tag;
			exportResultInfo?.RefreshDuration();
		}

		private void BrowseExportFolderClickHandler(object sender, RoutedEventArgs e)
		{
			var selectFolderDialog =
				new CommonOpenFileDialog
				{
					IsFolderPicker = true,
					InitialDirectory = OutputPath,
					AddToMostRecentlyUsedList = false,
					EnsurePathExists = true,
					EnsureReadOnly = false,
					EnsureValidNames = true
				};

			if (selectFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				OutputPath = selectFolderDialog.FileName;
			}
		}

		private void FileNameHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			var hyperlink = (Hyperlink)sender;
			var exportResultInfo = (ExportResultInfo)hyperlink.DataContext;
			App.OpenExplorerAndSelectFile(exportResultInfo.FileName);
		}
	}

	public class ExportResultInfo : ModelBase
	{
		private string _fileName;
		private long _rowCount;
		private long? _fileSizeBytes;
		private DateTime? _startTimestamp;
		private DateTime? _completeTimestamp;
		private bool _isCancelled;

		public int CommandNumber { get; }

		public int ResultNumber { get; }

		public ResultInfo ResultInfo { get; }

		public IReadOnlyList<ColumnHeader> ColumnHeaders { get; }

		public ExportResultInfo(int commandNumber, int resultNumber, ResultInfo resultInfo, IReadOnlyList<ColumnHeader> columnHeaders)
		{
			CommandNumber = commandNumber;
			ResultNumber = resultNumber;
			ColumnHeaders = columnHeaders;
			ResultInfo = resultInfo;
		}

		public string ResultSetName => ResultInfo.Title;

		public string FileName
		{
			get { return _fileName; }
			set { UpdateValueAndRaisePropertyChanged(ref _fileName, value); }
		}

		public long RowCount
		{
			get { return _rowCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _rowCount, value); }
		}

		public long? FileSizeBytes
		{
			get { return _fileSizeBytes; }
			private set { UpdateValueAndRaisePropertyChanged(ref _fileSizeBytes, value); }
		}

		public DateTime? StartTimestamp
		{
			get { return _startTimestamp; }
			set { UpdateValueAndRaisePropertyChanged(ref _startTimestamp, value); }
		}

		public DateTime? CompleteTimestamp
		{
			get { return _completeTimestamp; }
			set
			{
				UpdateValueAndRaisePropertyChanged(ref _completeTimestamp, value);
				RefreshDuration();
			}
		}

		public bool IsCancelled
		{
			get { return _isCancelled; }
			set { UpdateValueAndRaisePropertyChanged(ref _isCancelled, value); }
		}

		public TimeSpan? Duration
		{
			get
			{
				if (_startTimestamp == null)
				{
					return null;
				}

				return _completeTimestamp.HasValue
					? _completeTimestamp - _startTimestamp
					: DateTime.Now - _startTimestamp;
			}
		}

		public void RefreshDuration()
		{
			RaisePropertyChanged(nameof(Duration));
		}

		public void RefreshFileSize()
		{
			var fileInfo = new FileInfo(FileName);

			if (fileInfo.Exists)
			{
				FileSizeBytes = fileInfo.Length;
			}
		}
	}
}
