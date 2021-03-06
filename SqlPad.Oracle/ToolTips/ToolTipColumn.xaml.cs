﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipColumn
	{
		public ToolTipColumn(ColumnDetailsModel dataModel)
		{
			InitializeComponent();

			DataContext = dataModel;
		}
	}

	public abstract class ModelWithConstraints : ModelBase
	{
		public ICollection<ConstraintDetailsModel> ConstraintDetails { get; } = new ObservableCollection<ConstraintDetailsModel>();
	}

	public class ColumnDetailsModel : ModelWithConstraints, IModelWithComment, IModelWithIndexes
	{
		private int _distinctValueCount;
		private int _nullValueCount;
		private int? _sampleSize;
		//private object _minimumValue;
		//private object _maximumValue;
		private DateTime _lastAnalyzed;
		private int _averageValueSize;
		private string _inMemoryCompression;
		private string _histogramType;
		private string _comment;
		private int _histogramBucketCount;
		private double _histogramHeight;
		private PointCollection _histogramPoints;

		public ICollection<IndexDetailsModel> IndexDetails { get; } = new ObservableCollection<IndexDetailsModel>();

		public string Owner { get; set; }

		public string Name { get; set; }
		
		public bool Nullable { get; set; }
		
		public bool Invisible { get; set; }
		
		public bool IsSystemGenerated { get; set; }
		
		public bool Virtual { get; set; }
		
		public string DataType { get; set; }
		
		public string DefaultValue { get; set; }
		
		public int DistinctValueCount
		{
			get { return _distinctValueCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _distinctValueCount, value); }
		}

		public int NullValueCount
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

		public DateTime LastAnalyzed
		{
			get { return _lastAnalyzed; }
			set { UpdateValueAndRaisePropertyChanged(ref _lastAnalyzed, value); }
		}

		public int AverageValueSize
		{
			get { return _averageValueSize; }
			set { UpdateValueAndRaisePropertyChanged(ref _averageValueSize, value); }
		}

		public string InMemoryCompression
		{
			get { return _inMemoryCompression; }
			set { UpdateValueAndRaisePropertyChanged(ref _inMemoryCompression, value); }
		}

	    public string HistogramType
		{
			get { return _histogramType; }
			set { UpdateValueAndRaisePropertyChanged(ref _histogramType, value); }
		}

		public int HistogramBucketCount
		{
			get { return _histogramBucketCount; }
			set { UpdateValueAndRaisePropertyChanged(ref _histogramBucketCount, value); }
		}

		public PointCollection HistogramPoints
		{
			get { return _histogramPoints; }
			private set { UpdateValueAndRaisePropertyChanged(ref _histogramPoints, value); }
		}

	    public double HistogramHeight
		{
			get { return _histogramHeight; }
			set { UpdateValueAndRaisePropertyChanged(ref _histogramHeight, value); }
		}

		public IList<double> HistogramValues
		{
			set { HistogramPoints = ConvertToPointCollection(NormalizeHistogramValues(value)); }
		}

		public string Comment
		{
			get { return _comment; }
			set { UpdateValueAndRaisePropertyChanged(ref _comment, value); }
		}

		private static PointCollection ConvertToPointCollection(IList<double> values, bool smooth = false)
		{
			if (smooth)
			{
				values = SmoothHistogram(values);
			}

			var max = values.Max();

			var points = new PointCollection {new Point(0, max)};

			for (var i = 0; i < values.Count; i++)
			{
				points.Add(new Point(i, max - values[i]));
			}

			points.Add(new Point(values.Count - 1, max));

			return points;
		}

		private IList<double> NormalizeHistogramValues(IList<double> originalValues)
		{
			NormalizeHistogramChartValues(originalValues);
			return ComputeHorizontalHistogramChartValues(originalValues);
		}

		private void NormalizeHistogramChartValues(IList<double> originalValues)
		{
			var previousBucketRowCount = originalValues[0];
			var totalRowCount = originalValues[originalValues.Count - 1];
			var maximumBucketRowCount = previousBucketRowCount;
			for (var i = 1; i < originalValues.Count; i++)
			{
				var currentBucketRowCount = originalValues[i];
				var bucketRowCount = currentBucketRowCount - previousBucketRowCount;
				previousBucketRowCount = currentBucketRowCount;
				originalValues[i] = bucketRowCount;
				maximumBucketRowCount = Math.Max(maximumBucketRowCount, bucketRowCount);
			}

			var ratio = totalRowCount / 254d;
			HistogramHeight = maximumBucketRowCount / ratio;

			for (var i = 0; i < originalValues.Count; i++)
			{
				originalValues[i] = originalValues[i] / ratio;
			}
		}

		private static IList<double> ComputeHorizontalHistogramChartValues(IList<double> originalValues)
		{
			var histogramValues = originalValues;
			double ratio;
			if (originalValues.Count < 254)
			{
				histogramValues = new double[254];
				ratio = originalValues.Count / 254d;

				for (var i = 0; i < histogramValues.Count; i++)
				{
					var sourceIndex = (int)(i * ratio);
					histogramValues[i] = originalValues[sourceIndex];
				}
			}
			else if (originalValues.Count > 254)
			{
				var rangeStart = 0d;
				histogramValues = new double[254];
				ratio = originalValues.Count / 254d;

				for (var i = 0; i < histogramValues.Count; i++)
				{
					var rangeEnd = rangeStart + ratio;
					var valuesToCount = Math.Round(rangeEnd - rangeStart);
					var sourceIndexStart = (int)rangeStart;
					var sum = 0d;
					for (var j = sourceIndexStart + 1; j <= sourceIndexStart + valuesToCount; j++)
					{
						sum += originalValues[j];
					}

					rangeStart += ratio;
					histogramValues[i] = sum / valuesToCount;
				}
			}

			return histogramValues;
		}

		private static IList<double> SmoothHistogram(IList<double> originalValues)
		{
			var smoothedValues = new double[originalValues.Count];

			var mask = new[] { 0.25, 0.5, 0.25 };

			for (var bin = 1; bin < originalValues.Count - 1; bin++)
			{
				var smoothedValue = mask.Select((t, i) => originalValues[bin - 1 + i] * t).Sum();

				smoothedValues[bin] = smoothedValue;
			}

			return smoothedValues;
		}
	}

	public class ConstraintDetailsModel
	{
		public string Owner { get; set; }

		public string Name { get; set; }

		public string Type { get; set; }
		
		public string SearchCondition { get; set; }
		
		public string DeleteRule { get; set; }
		
		public bool IsEnabled { get; set; }

		public bool IsDeferrable { get; set; }

		public bool IsDeferred { get; set; }
		
		public bool IsValidated { get; set; }

		public string Reliability { get; set; }
		
		public DateTime LastChange { get; set; }
	}
}
