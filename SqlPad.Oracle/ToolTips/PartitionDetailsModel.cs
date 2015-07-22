using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
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

		public ICollection<SubPartitionDetailsModel> SubPartitionDetails => _visibleSubPartitionDetails;

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

		public Visibility SubPartitionDetailsVisibility => _visibleSubPartitionDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	    public Visibility MoreSubPartitionsExistMessageVisibility => _subPartitionDetailsDictionary.Count > _maxVisibleSubPartitionCount ? Visibility.Visible : Visibility.Collapsed;

	    public int VisibleSubPartitionCount => _maxVisibleSubPartitionCount;

	    public int SubPartitionCount => _subPartitionDetailsDictionary.Count;
	}

	public abstract class PartitionDetailsModelBase : SegmentDetailsModelBase
	{
		public OracleObjectIdentifier Owner { get; set; }

		public string Name { get; set; }

		public string HighValue { get; set; }

		public Type Type => GetType();
	}

	public class SubPartitionDetailsModel : PartitionDetailsModelBase
	{
	}
}
