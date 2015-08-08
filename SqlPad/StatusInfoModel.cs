using System.Windows;

namespace SqlPad
{
	public class StatusInfoModel : ModelBase
	{
		private int _affectedRowCount = -1;
		private string _executionTimerMessage;
		private bool _moreRowsAvailable;
		private bool _resultGridAvailable;
		private bool _statementExecutedSuccessfully;

		public bool ResultGridAvailable
		{
			get { return _resultGridAvailable; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _resultGridAvailable, value))
				{
					RaisePropertyChanged(nameof(StatementExecutionInfoSeparatorVisibility));
				}
			}
		}

		public bool StatementExecutedSuccessfully
		{
			get { return _statementExecutedSuccessfully; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _statementExecutedSuccessfully, value))
				{
					RaisePropertyChanged(nameof(StatementExecutionInfoSeparatorVisibility));
				}
			}
		}

		public bool MoreRowsAvailable
		{
			get { return _moreRowsAvailable; }
			set { UpdateValueAndRaisePropertyChanged(ref _moreRowsAvailable, value); }
		}

		public string ExecutionTimerMessage
		{
			get { return _executionTimerMessage; }
			set { UpdateValueAndRaisePropertyChanged(ref _executionTimerMessage, value); }
		}

		public int AffectedRowCount
		{
			get { return _affectedRowCount; }
			set
			{
				if (!UpdateValueAndRaisePropertyChanged(ref _affectedRowCount, value))
					return;

				RaisePropertyChanged(nameof(AffectedRowCountVisibility));
				RaisePropertyChanged(nameof(StatementExecutionInfoSeparatorVisibility));
			}
		}

		public Visibility AffectedRowCountVisibility => _affectedRowCount == -1 ? Visibility.Collapsed : Visibility.Visible;

	    public Visibility StatementExecutionInfoSeparatorVisibility =>
	        !_resultGridAvailable && (AffectedRowCountVisibility == Visibility.Collapsed && !_statementExecutedSuccessfully)
	            ? Visibility.Collapsed
	            : Visibility.Visible;
	}
}
