namespace SqlPad
{
	public class StatusInfoModel : ModelBase
	{
		private string _executionTimerMessage;
		private bool _moreRowsAvailable;
		private bool _resultGridAvailable;
		private string _successfulExecutionMessage;

		public bool ResultGridAvailable
		{
			get { return _resultGridAvailable; }
			set { UpdateValueAndRaisePropertyChanged(ref _resultGridAvailable, value); }
		}

		public string SuccessfulExecutionMessage
		{
			get { return _successfulExecutionMessage; }
			set { UpdateValueAndRaisePropertyChanged(ref _successfulExecutionMessage, value); }
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
	}
}
