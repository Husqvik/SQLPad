using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SqlPad.Oracle.ToolTips
{
	/// <summary>
	/// Interaction logic for ToolTipObject.xaml
	/// </summary>
	public partial class ToolTipColumn : IToolTip
	{
		public ToolTipColumn(ColumnDetailsModel dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
		}

		public UserControl Control { get { return this; } }
	}

	public class ColumnDetailsModel : ModelBase
	{
		private int? _distinctValueCount;
		private int? _nullValueCount;
		private int? _sampleSize;
		private object _minimumValue;
		private object _maximumValue;
		private DateTime? _lastAnalyzed;
		private int? _averageValueSize;
		private PointCollection _histogramPoints;

		public string Owner { get; set; }

		public string Name { get; set; }
		
		public bool Nullable { get; set; }
		
		public string DataType { get; set; }
		
		public int? DistinctValueCount
		{
			get { return _distinctValueCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _distinctValueCount, value); }
		}

		public int? NullValueCount
		{
			get { return _nullValueCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _nullValueCount, value); }
		}

		public int? SampleSize
		{
			get { return _sampleSize; }
			set { UpdateValueAndRaisePropertyChanged(ref _sampleSize, value); }
		}

		public object MinimumValue { get; set; }
		
		public object MaximumValue { get; set; }

		public DateTime? LastAnalyzed
		{
			get { return _lastAnalyzed; }
			set { UpdateValueAndRaisePropertyChanged(ref _lastAnalyzed, value); }
		}

		public int? AverageValueSize
		{
			get { return _averageValueSize; }
			set { UpdateValueAndRaisePropertyChanged(ref _averageValueSize, value); }
		}

		public PointCollection HistogramPoints
		{
			get { return _histogramPoints; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _histogramPoints, value))
				{
					RaisePropertyChanged("HistogramVisibility");
				}
			}
		}

		public Visibility HistogramVisibility
		{
			get { return _histogramPoints == null || _histogramPoints.Count == 0 ? Visibility.Collapsed : Visibility.Visible; }
		}

		public IList<int> HistogramValues
		{
			set { HistogramPoints = ConvertToPointCollection(value); }
		}

		private PointCollection ConvertToPointCollection(IList<int> values, bool smooth = false)
		{
			if (smooth)
			{
				values = SmoothHistogram(values);
			}

			var max = values.Max();

			var points = new PointCollection {new Point(0, max)};
			// first point (lower-left corner)
			// middle points
			for (var i = 0; i < values.Count; i++)
			{
				points.Add(new Point(i, max - values[i]));
			}
			// last point (lower-right corner)
			points.Add(new Point(values.Count - 1, max));

			return points;
		}

		private int[] SmoothHistogram(IList<int> originalValues)
		{
			var smoothedValues = new int[originalValues.Count];

			var mask = new[] { 0.25, 0.5, 0.25 };

			for (var bin = 1; bin < originalValues.Count - 1; bin++)
			{
				double smoothedValue = 0;
				for (var i = 0; i < mask.Length; i++)
				{
					smoothedValue += originalValues[bin - 1 + i] * mask[i];
				}
				
				smoothedValues[bin] = (int)smoothedValue;
			}

			return smoothedValues;
		}
	}
}
