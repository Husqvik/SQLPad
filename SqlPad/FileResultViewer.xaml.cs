using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SqlPad.DataExport;

namespace SqlPad
{
	public partial class FileResultViewer
	{
		public static readonly DependencyProperty DataExporterProperty = DependencyProperty.Register(nameof(DataExporter), typeof(IDataExporter), typeof(FileResultViewer), new UIPropertyMetadata(DataExporters.Json));

		[Bindable(true)]
		public IDataExporter DataExporter
		{
			get { return (IDataExporter)GetValue(DataExporterProperty); }
			set { SetValue(DataExporterProperty, value); }
		}

		private readonly ObservableCollection<ExportResultInfo> _exportResultSets = new ObservableCollection<ExportResultInfo>();
		private readonly OutputViewer _outputViewer;

		public IReadOnlyList<ExportResultInfo> ExportResultSets => _exportResultSets;

		public FileResultViewer(OutputViewer outputViewer)
		{
			InitializeComponent();

			_outputViewer = outputViewer;
		}

		public async Task SaveExecutionResult(StatementExecutionBatchResult executionResult)
		{
			_exportResultSets.Clear();

			var commandNumber = 0;
			foreach (var statementResult in executionResult.StatementResults)
			{
				commandNumber++;

				foreach (var kvp in statementResult.ResultInfoColumnHeaders)
				{
					var exportResultInfo = new ExportResultInfo(commandNumber, kvp.Key, kvp.Value);
					_exportResultSets.Add(exportResultInfo);
				}
			}

			await _outputViewer.ExecuteUsingCancellationToken(ExportRows);
		}

		private async Task ExportRows(CancellationToken cancellationToken)
		{
			foreach (var exportResultInfo in _exportResultSets)
			{
				var exportFileName = $@"E:\sqlpad_export_test_{exportResultInfo.CommandNumber}_{DateTime.Now.Ticks}.tmp";
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

					exportContext.Complete();
				}

				exportResultInfo.IsCompleted = true;
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
					exportContext.AppendRows(innerTask.Result);
				}
				catch (OperationCanceledException)
				{
					Trace.WriteLine("User has canceled export operation. ");
					return;
				}
				finally
				{
					exportResultInfo.RowCount = exportContext.CurrentRowIndex;
				}
				
				await _outputViewer.UpdateExecutionStatisticsIfEnabled();
			}
		}

		private void BrowseExportFolderClickHandler(object sender, RoutedEventArgs e)
		{
			
		}
	}

	public class ExportResultInfo : ModelBase
	{
		private string _fileName;
		private long _rowCount;
		private bool _isCompleted;

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

		public bool IsCompleted
		{
			get { return _isCompleted; }
			set { UpdateValueAndRaisePropertyChanged(ref _isCompleted, value); }
		}
	}
}
