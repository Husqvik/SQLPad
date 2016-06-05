using System;
using System.Collections.Generic;
using System.Threading;

namespace SqlPad.DataExport
{
	internal abstract class DataExportContextBase : IDataExportContext
	{
		private readonly long? _totalRows;
		private readonly IProgress<int> _reportProgress;
		private readonly CancellationToken _cancellationToken;

		private bool _isInitialized;
		private bool _isFinalized;

		private string TypeName => GetType().Name;

		public long CurrentRowIndex { get; private set; }

		protected DataExportContextBase(long? totalRows, IProgress<int> reportProgress, CancellationToken cancellationToken)
		{
			_totalRows = totalRows;
			_reportProgress = reportProgress;
			_cancellationToken = cancellationToken;
		}

		public void Initialize()
		{
			if (_isInitialized)
			{
				throw new InvalidOperationException($"{TypeName} has been already initialized. ");
			}

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
				throw new InvalidOperationException($"{TypeName} has not been initialized. ");
			}

			if (_isFinalized)
			{
				throw new InvalidOperationException($"{TypeName} has been completed. ");
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

					_reportProgress.Report((int)progress);
				}

				CurrentRowIndex++;
			}

			_reportProgress?.Report(_totalRows.HasValue ? 100 : (int)CurrentRowIndex);
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
}
