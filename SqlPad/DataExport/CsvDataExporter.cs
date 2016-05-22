using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.DataExport
{
	public class CsvDataExporter : IDataExporter
	{
		private const string CsvSeparator = ";";

		public virtual string Name { get; } = "Comma separated value";

		protected virtual string Separator { get; } = CsvSeparator;

		public virtual string FileNameFilter { get; } = "CSV files (*.csv)|*.csv|All files (*.*)|*";

		public bool HasAppendSupport { get; } = false;

		public Task ExportToClipboardAsync(ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			return ExportToFileAsync(null, resultViewer, dataExportConverter, cancellationToken, reportProgress);
		}

		public Task ExportToFileAsync(string fileName, ResultViewer resultViewer, IDataExportConverter dataExportConverter, CancellationToken cancellationToken, IProgress<int> reportProgress = null)
		{
			var orderedColumns = DataExportHelper.GetOrderedExportableColumns(resultViewer.ResultGrid);

			var rows = (ICollection)resultViewer.ResultGrid.Items;

			return DataExportHelper.RunExportActionAsync(fileName, w => ExportInternal(orderedColumns, rows, w, reportProgress, cancellationToken));
		}

		private void ExportInternal(IReadOnlyList<ColumnHeader> orderedColumns, ICollection rows, TextWriter writer, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			var exportContext = new CsvDataExportContext(writer, orderedColumns, Separator, rows.Count, reportProgress, cancellationToken);
			DataExportHelper.ExportRowsUsingContext(rows, exportContext);
		}
	}

	internal abstract class DataExportContextBase : IDataExportContext
	{
		private readonly int? _totalRows;
		private readonly IProgress<int> _reportProgress;
		private readonly CancellationToken _cancellationToken;

		private bool _isInitialized;
		private bool _isFinalized;

		protected int CurrentRowIndex { get; private set; }

		protected DataExportContextBase(int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			_totalRows = totalRows;
			_reportProgress = reportProgress;
			_cancellationToken = cancellationToken;
		}

		public void Initialize()
		{
			if (_isInitialized)
			{
				throw new InvalidOperationException($"{GetType().Name} has been already initialized. ");
			}

			InitializeExport();

			_isInitialized = true;
			CurrentRowIndex = 0;
		}

		public void Complete()
		{
			FinalizeExport();

			_isFinalized = true;
		}

		public void AppendRows(IEnumerable<object[]> rows)
		{
			if (!_isInitialized)
			{
				throw new InvalidOperationException($"{GetType().Name} has not been initialized. ");
			}

			if (_isFinalized)
			{
				throw new InvalidOperationException($"{GetType().Name} has been finalized. ");
			}

			foreach (var rowValues in rows)
			{
				_cancellationToken.ThrowIfCancellationRequested();

				ExportRow(rowValues);

				if (_reportProgress != null)
				{
					var progress = _totalRows.HasValue
						? (int)Math.Round(CurrentRowIndex * 100f / _totalRows.Value)
						: CurrentRowIndex;

					_reportProgress.Report(progress);
				}

				CurrentRowIndex++;
			}

			_reportProgress?.Report(100);
		}

		protected static bool IsNull(object value)
		{
			if (value == DBNull.Value)
			{
				return true;
			}

			var nullable = value as IValue;
			return nullable != null && nullable.IsNull;
		}

		protected abstract void ExportRow(object[] rowValues);

		protected virtual void InitializeExport() { }

		protected virtual void FinalizeExport() { }
	}

	internal class CsvDataExportContext : DataExportContextBase
	{
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";

		private readonly IReadOnlyList<ColumnHeader> _columns;
		private readonly TextWriter _writer;
		private readonly string _separator;

		public CsvDataExportContext(TextWriter writer, IReadOnlyList<ColumnHeader> columns, string separator, int? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
			: base(totalRows, reportProgress, cancellationToken)
		{
			_writer = writer;
			_columns = columns;
			_separator = separator;
		}

		protected override void InitializeExport()
		{
			WriteHeader();
		}

		private void WriteHeader()
		{
			var columnHeaders = _columns.Select(h => EscapeIfNeeded(h.Name));
			var headerLine = String.Join(_separator, columnHeaders);
			_writer.WriteLine(headerLine);
		}

		protected override void ExportRow(object[] rowValues)
		{
			_writer.WriteLine(String.Join(_separator, _columns.Select(c => FormatCsvValue(rowValues[c.ColumnIndex]))));
		}

		private static string FormatCsvValue(object value)
		{
			if (IsNull(value))
			{
				return null;
			}

			var stringValue = CellValueConverter.Instance.Convert(value, typeof(String), null, CultureInfo.CurrentUICulture).ToString();
			return EscapeIfNeeded(stringValue);
		}

		private static string EscapeIfNeeded(string value)
		{
			return String.Format(MaskWrapByQuote, value.Replace(QuoteCharacter, DoubleQuotes));
		}
	}
}
