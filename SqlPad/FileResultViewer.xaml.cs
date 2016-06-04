using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SqlPad.DataExport;

namespace SqlPad
{
	public interface IResultViewer
	{
		TabItem TabItem { get; }

		void Close();
	}

	public partial class FileResultViewer : IResultViewer
	{
		private readonly ObservableCollection<ExportResultInfo> _exportResultSets = new ObservableCollection<ExportResultInfo>();
		private readonly OutputViewer _outputViewer;
		private readonly StatementExecutionResult _executionResult;

		public IReadOnlyList<ExportResultInfo> ExportResultSets => _exportResultSets;

		public TabItem TabItem { get; }

		public FileResultViewer(OutputViewer outputViewer, StatementExecutionResult executionResult)
		{
			InitializeComponent();

			_outputViewer = outputViewer;
			_executionResult = executionResult;

			TabItem =
				new TabItem
				{
					Header =
						new HeaderedContentControl
						{
							Content = new AccessText { Text = "_Result to file" }
						},
					Content = this
				};
		}

		public void Close()
		{
			_outputViewer.TabControlResult.RemoveTabItemWithoutBindingError(TabItem);
		}

		private async void FileResultViewerLoadedHandler(object sender, RoutedEventArgs e)
		{
			foreach (var kvp in _executionResult.ResultInfoColumnHeaders)
			{
				var exportResultInfo = new ExportResultInfo(kvp.Key, kvp.Value);
				_exportResultSets.Add(exportResultInfo);
			}

			await _outputViewer.ExecuteUsingCancellationToken(ExportRows);
		}

		private async Task ExportRows(CancellationToken cancellationToken)
		{
			foreach (var exportResultSet in _exportResultSets)
			{
				var dataExporter = new JsonDataExporter();
				var exportFileName = $@"E:\sqlpad_export_test_{DateTime.Now.Ticks}.tmp";
				var exportContext = await dataExporter.StartExportAsync(exportFileName, exportResultSet.ColumnHeaders, _outputViewer.DocumentPage.InfrastructureFactory.DataExportConverter, cancellationToken);
				exportResultSet.FileName = exportFileName;

				while (_outputViewer.ConnectionAdapter.CanFetch(exportResultSet.ResultInfo))
				{
					if (cancellationToken.IsCancellationRequested)
					{
						return;
					}

					await ExportNextBatch(exportContext, exportResultSet.ResultInfo, cancellationToken);
				}

				exportContext.Complete();
			}
		}

		private async Task ExportNextBatch(IDataExportContext exportContext, ResultInfo resultInfo, CancellationToken cancellationToken)
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize - exportContext.CurrentRowIndex % ConfigurationProvider.Configuration.ResultGrid.FetchRowsBatchSize;
			var exception = await App.SafeActionAsync(() => innerTask = _outputViewer.ConnectionAdapter.FetchRecordsAsync(resultInfo, (int)batchSize, cancellationToken));
			if (exception != null)
			{
				var errorMessage = Messages.GetExceptionErrorMessage(exception);
				_outputViewer.AddExecutionLog(DateTime.Now, $"Row retrieval failed: {errorMessage}");
				Messages.ShowError(errorMessage);
			}
			else
			{
				exportContext.AppendRows(innerTask.Result);
				await _outputViewer.UpdateExecutionStatisticsIfEnabled();
			}
		}
	}

	public class ExportResultInfo : ModelBase
	{
		private string _fileName;
		private long _rowCount;

		public ResultInfo ResultInfo { get; }

		public IReadOnlyList<ColumnHeader> ColumnHeaders { get; }

		public ExportResultInfo(ResultInfo resultInfo, IReadOnlyList<ColumnHeader> columnHeaders)
		{
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
	}
}
