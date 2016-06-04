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
		private readonly ObservableCollection<ExportResultInfo> _resultSets = new ObservableCollection<ExportResultInfo>();
		private readonly OutputViewer _outputViewer;
		private readonly StatementExecutionResult _executionResult;

		public IReadOnlyList<ExportResultInfo> ResultSets => _resultSets;

		public TabItem TabItem { get; }

		public FileResultViewer(OutputViewer outputViewer, StatementExecutionResult executionResult)
		{
			InitializeComponent();

			_outputViewer = outputViewer;
			_executionResult = executionResult;

			TabItem =
				new TabItem
				{
					Header = "tmp",
					Content = this
				};
		}

		public void Close()
		{
			_outputViewer.TabControlResult.RemoveTabItemWithoutBindingError(TabItem);
		}

		private async void FileResultViewerLoadedHandler(object sender, RoutedEventArgs e)
		{
			await _outputViewer.ExecuteUsingCancellationToken(ExportRows);
		}

		private async Task ExportRows(CancellationToken cancellationToken)
		{
			foreach (var resultInfo in _executionResult.ResultInfoColumnHeaders.Keys)
			{
				var dataExporter = new JsonDataExporter();

				while (_outputViewer.ConnectionAdapter.CanFetch(resultInfo))
				{
					if (cancellationToken.IsCancellationRequested)
					{
						return;
					}

					await ExportNextBatch(null, resultInfo, cancellationToken);
				}
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
				//AppendRows(innerTask.Result);

				await _outputViewer.UpdateExecutionStatisticsIfEnabled();
			}
		}
	}

	public class ExportResultInfo : ModelBase
	{
		private long _rowCount;

		public string ResultSetName { get; set; }

		public long RowCount
		{
			get { return _rowCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _rowCount, value); }
		}
	}
}
