using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace SqlPad.Oracle.ToolTips
{
	public partial class TablespaceDetails
	{
		public static readonly DependencyProperty IsDetailVisibleProperty = DependencyProperty.Register(nameof(IsDetailVisible), typeof (bool), typeof (TablespaceDetails), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty TablespaceProperty = DependencyProperty.Register(nameof(Tablespace), typeof (TablespaceDetailModel), typeof (TablespaceDetails), new FrameworkPropertyMetadata());

		[Bindable(true)]
		public bool IsDetailVisible
		{
			get { return (bool)GetValue(IsDetailVisibleProperty); }
			private set { SetValue(IsDetailVisibleProperty, value); }
		}

		[Bindable(true)]
		public TablespaceDetailModel Tablespace
		{
			get { return (TablespaceDetailModel)GetValue(TablespaceProperty); }
			set { SetValue(TablespaceProperty, value); }
		}

		public TablespaceDetails()
		{
			InitializeComponent();
		}

		private void TablespaceHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			IsDetailVisible = true;
		}
	}

	public class TablespaceDetailModel : ModelBase
	{
		private string _name;
		private int _blockSize;
		private int _initialExtent;
		private int? _nextExtent;
		private int _minimumExtents;
		private int? _maximumExtents;
		private long _segmentMaximumSizeBlocks;
		private int? _percentIncrease;
		private int _minimumExtentSizeBytes;
		private string _status;
		private string _contents;
		private bool _logging;
		private bool _forceLogging;
		private string _extentManagement;
		private string _allocationType;
		private string _segmentSpaceManagement;
		private string _defaultTableCompression;
		private string _retention;
		private bool _isBigFile;
		private string _predicateEvaluation;
		private bool _isEncrypted;
		private string _compressFor;
		private long? _allocatedBytes;
		private long _usedBytes;

		public ICollection<DatafileDetailModel> Datafiles { get; } = new ObservableCollection<DatafileDetailModel>();

		public string Name
		{
			get { return _name; }
			set { UpdateValueAndRaisePropertyChanged(ref _name, value); }
		}

		public int BlockSize
		{
			get { return _blockSize; }
			set { UpdateValueAndRaisePropertyChanged(ref _blockSize, value); }
		}

		public int InitialExtent
		{
			get { return _initialExtent; }
			set { UpdateValueAndRaisePropertyChanged(ref _initialExtent, value); }
		}

		public int? NextExtent
		{
			get { return _nextExtent; }
			set { UpdateValueAndRaisePropertyChanged(ref _nextExtent, value); }
		}

		public int MinimumExtents
		{
			get { return _minimumExtents; }
			set { UpdateValueAndRaisePropertyChanged(ref _minimumExtents, value); }
		}

		public int? MaximumExtents
		{
			get { return _maximumExtents; }
			set { UpdateValueAndRaisePropertyChanged(ref _maximumExtents, value); }
		}

		public long SegmentMaximumSizeBlocks
		{
			get { return _segmentMaximumSizeBlocks; }
			set { UpdateValueAndRaisePropertyChanged(ref _segmentMaximumSizeBlocks, value); }
		}

		public int? PercentIncrease
		{
			get { return _percentIncrease; }
			set { UpdateValueAndRaisePropertyChanged(ref _percentIncrease, value); }
		}

		public int MinimumExtentSizeBytes
		{
			get { return _minimumExtentSizeBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _minimumExtentSizeBytes, value); }
		}

		public string Status
		{
			get { return _status; }
			set { UpdateValueAndRaisePropertyChanged(ref _status, value); }
		}

		public string Contents
		{
			get { return _contents; }
			set { UpdateValueAndRaisePropertyChanged(ref _contents, value); }
		}

		public bool Logging
		{
			get { return _logging; }
			set { UpdateValueAndRaisePropertyChanged(ref _logging, value); }
		}

		public bool ForceLogging
		{
			get { return _forceLogging; }
			set { UpdateValueAndRaisePropertyChanged(ref _forceLogging, value); }
		}

		public string ExtentManagement
		{
			get { return _extentManagement; }
			set { UpdateValueAndRaisePropertyChanged(ref _extentManagement, value); }
		}

		public string AllocationType
		{
			get { return _allocationType; }
			set { UpdateValueAndRaisePropertyChanged(ref _allocationType, value); }
		}

		public string SegmentSpaceManagement
		{
			get { return _segmentSpaceManagement; }
			set { UpdateValueAndRaisePropertyChanged(ref _segmentSpaceManagement, value); }
		}

		public string DefaultTableCompression
		{
			get { return _defaultTableCompression; }
			set { UpdateValueAndRaisePropertyChanged(ref _defaultTableCompression, value); }
		}

		public string Retention
		{
			get { return _retention; }
			set { UpdateValueAndRaisePropertyChanged(ref _retention, value); }
		}

		public bool IsBigFile
		{
			get { return _isBigFile; }
			set { UpdateValueAndRaisePropertyChanged(ref _isBigFile, value); }
		}

		public string PredicateEvaluation
		{
			get { return _predicateEvaluation; }
			set { UpdateValueAndRaisePropertyChanged(ref _predicateEvaluation, value); }
		}

		public bool IsEncrypted
		{
			get { return _isEncrypted; }
			set { UpdateValueAndRaisePropertyChanged(ref _isEncrypted, value); }
		}

		public string CompressFor
		{
			get { return _compressFor; }
			set { UpdateValueAndRaisePropertyChanged(ref _compressFor, value); }
		}

		public long? AllocatedBytes
		{
			get { return _allocatedBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _allocatedBytes, value); }
		}

		public long UsedBytes
		{
			get { return _usedBytes; }
			set { UpdateValueAndRaisePropertyChanged(ref _usedBytes, value); }
		}
	}

	public class DatafileDetailModel
	{
		public string FileName { get; set; }

		public int FileId { get; set; }

		public long SizeBytes { get; set; }

		public long SizeBlocks { get; set; }

		public string Status { get; set; }

		public int RelativeFileNumber { get; set; }

		public bool IsAutoextensible { get; set; }

		public long MaximumSizeBytes { get; set; }

		public long MaximumSizeBlocks { get; set; }

		public long IncrementByBlocks { get; set; }
		
		public long UserSizeBytes { get; set; }

		public long UserSizeBlocks { get; set; }

		public string OnlineStatus { get; set; }
	}
}
