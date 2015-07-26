using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipTable
	{
		public ToolTipTable()
		{
			InitializeComponent();
		}
	}

	public interface IModelWithIndexes
	{
		ICollection<IndexDetailsModel> IndexDetails { get; } 
	}

	public interface IModelWithComment
	{
		string Comment { get; set; }
	}

	public abstract class SegmentDetailsModelBase : ModelBase
	{
		private long? _rowCount;
		private long? _sampleRows;
		private int? _blockCount;
		private string _compression;
		private string _tablespaceName;
		private DateTime? _lastAnalyzed;
		private int? _averageRowSize;
		private bool? _logging;
		private long? _allocatedBytes;
		private long? _largeObjectBytes;
		private string _inMemoryCompression;

		public long? RowCount
		{
			get { return _rowCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _rowCount, value); }
		}

		public long? SampleRows
		{
			get { return _sampleRows; }
			set { UpdateValueAndRaisePropertyChanged(ref _sampleRows, value); }
		}

		public bool? Logging
		{
			get { return _logging; }
			set { UpdateValueAndRaisePropertyChanged(ref _logging, value); }
		}

		public int? BlockCount
		{
			get { return _blockCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _blockCount, value); }
		}

		public string Compression
		{
			get { return _compression; }
			set { UpdateValueAndRaisePropertyChanged(ref _compression, value); }
		}

		public string TablespaceName
		{
			get { return _tablespaceName; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _tablespaceName, value))
				{
					RaisePropertyChanged(nameof(TablespaceNameVisibility));
				}
			}
		}

		public DateTime? LastAnalyzed
		{
			get { return _lastAnalyzed; }
			set { UpdateValueAndRaisePropertyChanged(ref _lastAnalyzed, value); }
		}

		public int? AverageRowSize
		{
			get { return _averageRowSize; }
			set { UpdateValueAndRaisePropertyChanged(ref _averageRowSize, value); }
		}

		public Visibility TablespaceNameVisibility => String.IsNullOrEmpty(_tablespaceName) ? Visibility.Collapsed : Visibility.Visible;

	    public long? AllocatedBytes
		{
			get { return _allocatedBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _allocatedBytes, value); }
		}

		public long? LargeObjectBytes
		{
			get { return _largeObjectBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _largeObjectBytes, value); }
		}

		public string InMemoryCompression
		{
			get { return _inMemoryCompression; }
			set { UpdateValueAndRaisePropertyChanged(ref _inMemoryCompression, value); }
		}
	}

	public class TableDetailsModel : SegmentDetailsModelBase, IModelWithComment, IModelWithIndexes
	{
		public const int MaxVisiblePartitionCount = 16;

		private string _organization;
		private string _clusterName;
		private string _parallelDegree;
		private bool? _isTemporary;
		private string _comment;

		private readonly ObservableCollection<IndexDetailsModel> _indexDetails = new ObservableCollection<IndexDetailsModel>();
		private readonly ObservableCollection<PartitionDetailsModel> _visiblePartitionDetails = new ObservableCollection<PartitionDetailsModel>();
		private readonly Dictionary<string, PartitionDetailsModel> _partitionDetailsDictionary = new Dictionary<string, PartitionDetailsModel>();

		public TableDetailsModel()
		{
			_indexDetails.CollectionChanged += delegate { RaisePropertyChanged(nameof(IndexDetailsVisibility)); };
			_visiblePartitionDetails.CollectionChanged += VisiblePartitionDetailsCollectionChangedHandler;
		}

		private void VisiblePartitionDetailsCollectionChangedHandler(object sender, NotifyCollectionChangedEventArgs args)
		{
			RaisePropertyChanged(nameof(PartitionDetailsVisibility));
		}

		public ICollection<IndexDetailsModel> IndexDetails => _indexDetails;

	    public ICollection<PartitionDetailsModel> VisiblePartitionDetails => _visiblePartitionDetails;

	    public void AddPartition(PartitionDetailsModel partition)
		{
			_partitionDetailsDictionary.Add(partition.Name, partition);

			if (_visiblePartitionDetails.Count < MaxVisiblePartitionCount)
			{
				_visiblePartitionDetails.Add(partition);
			}
			else
			{
				RaisePropertyChanged(nameof(MorePartitionsExistMessageVisibility));
				RaisePropertyChanged(nameof(VisiblePartitionCount));
				RaisePropertyChanged(nameof(PartitionCount));
			}
		}

		public Visibility MorePartitionsExistMessageVisibility => _partitionDetailsDictionary.Count > MaxVisiblePartitionCount ? Visibility.Visible : Visibility.Collapsed;

	    public int VisiblePartitionCount => MaxVisiblePartitionCount;

	    public int PartitionCount => _partitionDetailsDictionary.Count;

	    public PartitionDetailsModel GetPartitions(string partitionName)
		{
			return _partitionDetailsDictionary[partitionName];
		}

		public string Title { get; set; }

		public string PartitionKeys { get; set; }

		public string SubPartitionKeys { get; set; }

		public string Organization
		{
			get { return _organization; }
			set { UpdateValueAndRaisePropertyChanged(ref _organization, value); }
		}

		public string ParallelDegree
		{
			get { return _parallelDegree; }
			set { UpdateValueAndRaisePropertyChanged(ref _parallelDegree, value); }
		}

		public string ClusterName
		{
			get { return _clusterName; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _clusterName, value))
				{
					RaisePropertyChanged(nameof(ClusterNameVisibility));
				}
			}
		}

		public bool? IsTemporary
		{
			get { return _isTemporary; }
			set { UpdateValueAndRaisePropertyChanged(ref _isTemporary, value); }
		}

		public Visibility ClusterNameVisibility => String.IsNullOrEmpty(_clusterName) ? Visibility.Collapsed : Visibility.Visible;

	    public Visibility IndexDetailsVisibility => _indexDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	    public Visibility PartitionDetailsVisibility => _visiblePartitionDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

	    public long? InMemoryAllocatedBytes { get; private set; }
		public long? StorageBytes { get; private set; }
		public long? NonPopulatedBytes { get; private set; }
		public string InMemoryPopulationStatus { get; private set; }

		public void SetInMemoryAllocationStatus(long? inMemoryAllocatedBytes, long? storageBytes, long? nonPopulatedBytes, string populationStatus)
		{
			InMemoryAllocatedBytes = inMemoryAllocatedBytes;
			StorageBytes = storageBytes;
			NonPopulatedBytes = nonPopulatedBytes;
			InMemoryPopulationStatus = populationStatus;

			if (!inMemoryAllocatedBytes.HasValue)
			{
				return;
			}

			RaisePropertyChanged(nameof(InMemoryAllocatedBytes));
			RaisePropertyChanged(nameof(StorageBytes));
			RaisePropertyChanged(nameof(NonPopulatedBytes));
			RaisePropertyChanged(nameof(InMemoryPopulationStatus));
			RaisePropertyChanged(nameof(InMemoryAllocationStatusVisibility));
		}

		public Visibility InMemoryAllocationStatusVisibility => InMemoryAllocatedBytes.HasValue ? Visibility.Visible : Visibility.Collapsed;

	    public string Comment
		{
			get { return _comment; }
			set { UpdateValueAndRaisePropertyChanged(ref _comment, value); }
		}
	}

	public class IndexDetailsModel : ModelBase
	{
		private readonly ObservableCollection<IndexColumnModel> _indexColumns = new ObservableCollection<IndexColumnModel>();

		public IndexDetailsModel()
		{
			_indexColumns.CollectionChanged += delegate { RaisePropertyChanged(nameof(IndexColumns)); };
		}

		public string Owner { get; set; }
		
		public string Name { get; set; }
		
		public string Type { get; set; }
		
		public bool IsUnique { get; set; }
		
		public string Compression { get; set; }
		
		public int? PrefixLength { get; set; }
		
		public bool Logging { get; set; }
		
		public long? ClusteringFactor { get; set; }
		
		public string Status { get; set; }
		
		public long? Rows { get; set; }
		
		public long? SampleRows { get; set; }
		
		public DateTime? LastAnalyzed { get; set; }
		
		public int? Blocks { get; set; }
		
		public long? Bytes { get; set; }
		
		public int? LeafBlocks { get; set; }
		
		public int? DegreeOfParallelism { get; set; }
		
		public long? DistinctKeys { get; set; }

		public ICollection<IndexColumnModel> Columns => _indexColumns;

	    public string TablespaceName { get; set; }

		public string IndexColumns
		{
			get { return String.Join(", ", _indexColumns.Select(c => $"{c.ColumnName}{(c.SortOrder == SortOrder.Descending ? " DESC" : null)}")); }
		}
	}

	public class IndexColumnModel
	{
		public string ColumnName { get; set; }
		
		public SortOrder SortOrder { get; set; }
	}

	public enum SortOrder
	{
		Ascending,
		Descending
	}
	
	public class InMemoryAllocationStatusConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			var allocatedInMemoryBytes = (long?)values[0];
			var storageBytes = (long?)values[1];
			var nonPopulatedBytes = (long?)values[2];
			var populationStatus = (string)values[3];
			if (!allocatedInMemoryBytes.HasValue || !storageBytes.HasValue || !nonPopulatedBytes.HasValue || String.IsNullOrEmpty(populationStatus))
			{
				return ValueConverterBase.ValueNotAvailable;
			}

			var populatedRatio = Math.Round(((decimal)storageBytes.Value - nonPopulatedBytes.Value) / storageBytes.Value * 100, 2);
			var isPopulating = populationStatus == "STARTED";
			var populationStatusLabel = isPopulating ? " - ongoing" : null;
			var populatedRatioLabel = populatedRatio < 100 || isPopulating ? $"{populatedRatio} %" : null;
			var populationStatusDetail = populatedRatio == 100 && populationStatusLabel == null
				? null
				: $"({populatedRatioLabel}{populationStatusLabel})";

			return $"{DataSpaceConverter.PrettyPrint(allocatedInMemoryBytes.Value)} {populationStatusDetail}";
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}

	public class SimpleIdentifierConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var stringValue = (string)value;
			return String.IsNullOrEmpty(stringValue) ? ValueNotAvailable : stringValue.ToSimpleIdentifier();
		}
	}
}
