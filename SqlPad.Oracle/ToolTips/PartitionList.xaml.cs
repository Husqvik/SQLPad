using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;

namespace SqlPad.Oracle.ToolTips
{
	public partial class PartitionList
	{
		public PartitionList()
		{
			InitializeComponent();
		}

		public static readonly DependencyProperty TableDetailsProperty = DependencyProperty.Register("TableDetails", typeof(TableDetailsModel), typeof(PartitionList), new FrameworkPropertyMetadata(TableDetailsPropertyChangedCallbackHandler));

		public TableDetailsModel TableDetails
		{
			get { return (TableDetailsModel)GetValue(TableDetailsProperty); }
			set { SetValue(TableDetailsProperty, value); }
		}

		private static void TableDetailsPropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var partitionList = (PartitionList)dependencyObject;
			partitionList.DataContext = args.NewValue;
		}
	}

	public abstract class PartitionDetailsModelBase : SegmentDetailsModelBase
	{
		public OracleObjectIdentifier Owner { get; set; }

		public string Name { get; set; }

		public string HighValue { get; set; }
		
		public Type Type { get { return GetType(); } }
	}

	public class PartitionDetailsModel : PartitionDetailsModelBase
	{
		private readonly ObservableCollection<SubPartitionDetailsModel> _visibleSubPartitionDetails = new ObservableCollection<SubPartitionDetailsModel>();
		private readonly Dictionary<string, SubPartitionDetailsModel> _subPartitionDetailsDictionary = new Dictionary<string, SubPartitionDetailsModel>();
		private readonly int _maxVisibleSubPartitionCount;

		public PartitionDetailsModel(int maxVisibleSubPartitionCount = 4)
		{
			_maxVisibleSubPartitionCount = maxVisibleSubPartitionCount;
			_visibleSubPartitionDetails.CollectionChanged += VisibleSubPartitionDetailsCollectionChangedHandler;
		}

		private void VisibleSubPartitionDetailsCollectionChangedHandler(object sender, NotifyCollectionChangedEventArgs args)
		{
			RaisePropertyChanged("SubPartitionDetailsVisibility");
		}

		public ICollection<SubPartitionDetailsModel> SubPartitionDetails { get { return _visibleSubPartitionDetails; } }

		public void AddSubPartition(SubPartitionDetailsModel subPartition)
		{
			_subPartitionDetailsDictionary.Add(subPartition.Name, subPartition);

			if (_visibleSubPartitionDetails.Count < _maxVisibleSubPartitionCount)
			{
				_visibleSubPartitionDetails.Add(subPartition);
			}
			else
			{
				RaisePropertyChanged("MoreSubPartitionsExistMessageVisibility");
				RaisePropertyChanged("VisibleSubPartitionCount");
				RaisePropertyChanged("SubPartitionCount");
			}
		}

		public Visibility SubPartitionDetailsVisibility
		{
			get { return _visibleSubPartitionDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
		}

		public Visibility MoreSubPartitionsExistMessageVisibility
		{
			get { return _subPartitionDetailsDictionary.Count > _maxVisibleSubPartitionCount ? Visibility.Visible : Visibility.Collapsed; }
		}

		public int VisibleSubPartitionCount
		{
			get { return _maxVisibleSubPartitionCount; }
		}

		public int SubPartitionCount
		{
			get { return _subPartitionDetailsDictionary.Count; }
		}
	}

	public class SubPartitionDetailsModel : PartitionDetailsModelBase
	{
	}
}
