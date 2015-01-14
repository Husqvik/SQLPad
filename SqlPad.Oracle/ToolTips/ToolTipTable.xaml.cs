using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipTable : IToolTip
	{
		public ToolTipTable()
		{
			InitializeComponent();
		}

		public UserControl Control { get { return this; } }
	}

	public class TableDetailsModel : ModelBase
	{
		private int? _rowCount;
		private int? _blockCount;
		private string _compression;
		private string _organization;
		private string _clusterName;
		private string _parallelDegree;
		private string _inMemoryCompression;
		private DateTime? _lastAnalyzed;
		private int? _averageRowSize;
		private bool? _isTemporary;
		private bool? _isPartitioned;
		private long? _allocatedBytes;

		private readonly ObservableCollection<IndexDetailsModel> _indexDetails = new ObservableCollection<IndexDetailsModel>();

		public TableDetailsModel()
		{
			_indexDetails.CollectionChanged += (sender, args) => RaisePropertyChanged("IndexDetailsVisibility");
		}

		public ICollection<IndexDetailsModel> IndexDetails { get { return _indexDetails; } } 

		public string Title { get; set; }

		public int? RowCount
		{
			get { return _rowCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _rowCount, value); }
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

		public string ClusterName
		{
			get { return _clusterName; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _clusterName, value))
				{
					RaisePropertyChanged("ClusterNameVisible");
				}
			}
		}

		public bool? IsPartitioned
		{
			get { return _isPartitioned; }
			set { UpdateValueAndRaisePropertyChanged(ref _isPartitioned, value); }
		}

		public bool? IsTemporary
		{
			get { return _isTemporary; }
			set { UpdateValueAndRaisePropertyChanged(ref _isTemporary, value); }
		}

		public Visibility ClusterNameVisibility
		{
			get { return String.IsNullOrEmpty(_clusterName) ? Visibility.Collapsed : Visibility.Visible; }
		}

		public Visibility IndexDetailsVisibility
		{
			get { return _indexDetails.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
		}

		public long? AllocatedBytes
		{
			get { return _allocatedBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _allocatedBytes, value); }
		}

		public string InMemoryCompression
		{
			get { return _inMemoryCompression; }
			set { UpdateValueAndRaisePropertyChanged(ref _inMemoryCompression, value); }
		}

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

			RaisePropertyChanged("InMemoryAllocatedBytes");
			RaisePropertyChanged("StorageBytes");
			RaisePropertyChanged("NonPopulatedBytes");
			RaisePropertyChanged("InMemoryPopulationStatus");
			RaisePropertyChanged("InMemoryAllocationStatusVisible");
		}

		public Visibility InMemoryAllocationStatusVisible
		{
			get { return InMemoryAllocatedBytes.HasValue ? Visibility.Visible : Visibility.Collapsed; }
		}
	}

	public class IndexDetailsModel
	{
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
				return ValueConverter.ValueNotAvailable;
			}

			var populatedRatio = Math.Round(((decimal)storageBytes.Value - nonPopulatedBytes.Value) / storageBytes.Value * 100, 2);
			var isPopulating = populationStatus == "STARTED";
			var populationStatusLabel = isPopulating ? " - ongoing" : null;
			var populatedRatioLabel = populatedRatio < 100 || isPopulating ? String.Format("{0} %", populatedRatio) : null;
			var populationStatusDetail = populatedRatio == 100 && populationStatusLabel == null
				? null
				: String.Format("({0}{1})", populatedRatioLabel, populationStatusLabel);

			return String.Format("{0} {1}", DataSpaceConverter.PrettyPrint(allocatedInMemoryBytes.Value), populationStatusDetail);
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}
