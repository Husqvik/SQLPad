using System;
using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipPartition : IToolTip
	{
		private readonly TableDetailsModel _dataModel;
		private readonly OraclePartitionReference _partitionReference;

		public ToolTipPartition(TableDetailsModel dataModel, OraclePartitionReference partitionReference)
		{
			InitializeComponent();

			_partitionReference = partitionReference;
			_dataModel = dataModel;

			//dataModel.LoadingCompleted += LoadingCompletedHandler;
		}

		/*private void LoadingCompletedHandler(object sender, EventArgs args)
		{
			if (_partitionReference.Partition is OracleSubPartition)
			{
				DataContext = _dataModel.GetSubPartition(_partitionReference.NormalizedName);				
			}
			else
			{
				DataContext = _dataModel.GetPartition(_partitionReference.NormalizedName);
			}
		}*/

		public UserControl Control { get { return this; } }
	}
}
