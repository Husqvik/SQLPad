using System;
using System.Collections.Generic;
using System.Threading;

namespace SqlPad.DataExport
{
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
}
