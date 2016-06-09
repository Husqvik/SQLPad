using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using SqlPad.DataExport;

namespace SqlPad
{
	public partial class FileResultViewer
	{
		public static readonly DependencyProperty DataExporterProperty = DependencyProperty.Register(nameof(DataExporter), typeof(IDataExporter), typeof(FileResultViewer), new UIPropertyMetadata(DataExporters.Csv));
		public static readonly DependencyProperty OutputPathProperty = DependencyProperty.Register(nameof(OutputPath), typeof(string), typeof(FileResultViewer), new UIPropertyMetadata());
		public static readonly DependencyProperty IsExecutingProperty = DependencyProperty.Register(nameof(IsExecuting), typeof(bool), typeof(FileResultViewer), new UIPropertyMetadata());

		[Bindable(true)]
		public IDataExporter DataExporter
		{
			get { return (IDataExporter)GetValue(DataExporterProperty); }
			set { SetValue(DataExporterProperty, value); }
		}

		[Bindable(true)]
		public string OutputPath
		{
			get { return (string)GetValue(OutputPathProperty); }
			set { SetValue(OutputPathProperty, value); }
		}

		[Bindable(true)]
		public bool IsExecuting
		{
			get { return (bool)GetValue(IsExecutingProperty); }
			private set { SetValue(IsExecutingProperty, value); }
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

			if (String.IsNullOrEmpty(OutputPath))
			{
				var userDocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				OutputPath = userDocumentPath;
			}
		}

		public async Task SaveExecutionResult(StatementExecutionBatchResult executionResult)
		{
			IsExecuting = true;

			_exportClockTimer.Start();
			_exportResultInfoCollection.Clear();

			var commandNumber = 0;
			foreach (var statementResult in executionResult.StatementResults)
			{
				commandNumber++;

				foreach (var kvp in statementResult.ResultInfoColumnHeaders)
				{
					var exportResultInfo = new ExportResultInfo(commandNumber, kvp.Key, kvp.Value);
					_exportResultInfoCollection.Add(exportResultInfo);
				}
			}

			await _outputViewer.ExecuteUsingCancellationToken(ExportRows);

			_exportClockTimer.Stop();

			IsExecuting = false;
		}

		private async Task ExportRows(CancellationToken cancellationToken)
		{
			foreach (var exportResultInfo in _exportResultInfoCollection)
			{
				exportResultInfo.StartTimestamp = DateTime.Now;
				_exportClockTimer.Tag = exportResultInfo;

				var exportFileName = $@"E:\sqlpad_export_test_{exportResultInfo.CommandNumber}_{DateTime.Now.Ticks}.tmp"; // TODO
				using (var exportContext = await DataExporter.StartExportAsync(exportFileName, exportResultInfo.ColumnHeaders, _outputViewer.DocumentPage.InfrastructureFactory.DataExportConverter, cancellationToken))
				{
					exportResultInfo.FileName = exportFileName;

					while (_outputViewer.ConnectionAdapter.CanFetch(exportResultInfo.ResultInfo))
					{
						if (cancellationToken.IsCancellationRequested)
						{
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
					Trace.WriteLine("User has canceled export operation. ");
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

		public int CommandNumber { get; }

		public ResultInfo ResultInfo { get; }

		public IReadOnlyList<ColumnHeader> ColumnHeaders { get; }

		public ExportResultInfo(int commandNumber, ResultInfo resultInfo, IReadOnlyList<ColumnHeader> columnHeaders)
		{
			CommandNumber = commandNumber;
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
