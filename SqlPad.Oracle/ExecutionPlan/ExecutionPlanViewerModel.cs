using System.Windows;

namespace SqlPad.Oracle.ExecutionPlan
{
	internal class ExecutionPlanViewerModel : ModelBase
	{
		private Visibility _cursorStatisticsOptionsVisibility = Visibility.Collapsed;
		private string _textExecutionPlan;
		private int _totalExecutions;

		public int TotalExecutions
		{
			get { return _totalExecutions; }
			set { UpdateValueAndRaisePropertyChanged(ref _totalExecutions, value); }
		}

		public Visibility CursorStatisticsOptionsVisibility
		{
			get { return _cursorStatisticsOptionsVisibility; }
			set { UpdateValueAndRaisePropertyChanged(ref _cursorStatisticsOptionsVisibility, value); }
		}

		public string TextExecutionPlan
		{
			get { return _textExecutionPlan; }
			set
			{
				if (!UpdateValueAndRaisePropertyChanged(ref _textExecutionPlan, value))
				{
					return;
				}

				RaisePropertyChanged("IsExecutionPlanAvailable");
			}
		}
	}
}