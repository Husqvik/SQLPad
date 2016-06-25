using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SqlPad.DataExport
{
	internal abstract class DataExportContextBase : IDataExportContext
	{
		private bool _isInitialized;
		private bool _isFinalized;
		private long _totalRows;
		private IProgress<int> _progress;
		private CancellationToken _cancellationToken;

		protected readonly MemoryStream ClipboardData;

		private string TypeName => GetType().Name;

		protected ExportOptions ExportOptions { get; }

		public long CurrentRowIndex { get; private set; }

		protected DataExportContextBase(ExportOptions exportOptions)
		{
			ExportOptions = exportOptions;

			if (ExportOptions.IntoClipboard)
			{
				ClipboardData = new MemoryStream();
			}
		}

		public void SetProgress(long totalRowCount, IProgress<int> progress)
		{
			_totalRows = totalRowCount;
			_progress = progress;
		}

		public async Task InitializeAsync(CancellationToken cancellationToken)
		{
			await Task.Run(() => Initialize(), _cancellationToken = cancellationToken);
		}

		private void Initialize()
		{
			if (_isInitialized)
			{
				throw new InvalidOperationException($"{TypeName} has been already initialized. ");
			}

			_progress?.Report(0);

			InitializeExport();

			_isInitialized = true;
			CurrentRowIndex = 0;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			ClipboardData?.Dispose();
		}

		public async Task FinalizeAsync()
		{
			await FinalizeExport();

			_progress?.Report(100);

			_isFinalized = true;

			SetToClipboard();
		}

		private void SetToClipboard()
		{
			if (!ExportOptions.IntoClipboard)
			{
				return;
			}

			ClipboardData.Seek(0, SeekOrigin.Begin);

			using (var reader = new StreamReader(ClipboardData, Encoding.UTF8, true, 32768, true))
			{
				Clipboard.SetText(reader.ReadToEnd());
			}
		}

		public async Task AppendRowsAsync(IEnumerable<object[]> rows)
		{
			if (!_isInitialized)
			{
				throw new InvalidOperationException($"{TypeName} has not been initialized. ");
			}

			if (_isFinalized)
			{
				throw new InvalidOperationException($"{TypeName} has been completed. ");
			}

			await Task.Run(() => AppendRowsInternal(rows), _cancellationToken);
		}

		private void AppendRowsInternal(IEnumerable<object[]> rows)
		{
			foreach (var rowValues in rows)
			{
				_cancellationToken.ThrowIfCancellationRequested();

				ExportRow(rowValues);

				if (_progress != null)
				{
					var progress = _totalRows > 0
						? (int)Math.Round(CurrentRowIndex * 100f / _totalRows)
						: 100;

					_progress.Report(progress);
				}

				CurrentRowIndex++;
			}
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

		protected virtual Task FinalizeExport()
		{
			return Task.CompletedTask;
		}
	}
}
