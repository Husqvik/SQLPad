using System;
using System.Text;
using System.Windows;
using System.Windows.Media;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle
{
	public partial class SessionActivityIndicator
	{
		public static readonly DependencyProperty SessionItemProperty = DependencyProperty.Register(nameof(SessionItem), typeof(SqlMonitorSessionItem), typeof(SessionActivityIndicator), new FrameworkPropertyMetadata(SessionItemChangedCallback));
		public static readonly DependencyProperty PathSegmentsProperty = DependencyProperty.Register(nameof(PathSegments), typeof(PathSegmentCollection), typeof(SessionActivityIndicator), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty DiagnosticsProperty = DependencyProperty.Register(nameof(Diagnostics), typeof(string), typeof(SessionActivityIndicator), new FrameworkPropertyMetadata());

		public string Diagnostics
		{
			get { return (string)GetValue(DiagnosticsProperty); }
			private set { SetValue(DiagnosticsProperty, value); }
		}

		public SqlMonitorSessionItem SessionItem
		{
			get { return (SqlMonitorSessionItem)GetValue(SessionItemProperty); }
			set { SetValue(SessionItemProperty, value); }
		}

		private static void SessionItemChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var visualizer = (SessionActivityIndicator)dependencyObject;
			var newSessionItem = (SqlMonitorSessionItem)args.NewValue;
			if (newSessionItem != null)
			{
				newSessionItem.PlanItemCollection.PropertyChanged +=
					(sender, e) =>
						(String.Equals(e.PropertyName, nameof(SqlMonitorPlanItemCollection.LastSampleTime)) ? visualizer : null)?.BuildPathSegments();
			}
		}

		public PathSegmentCollection PathSegments
		{
			get { return (PathSegmentCollection)GetValue(PathSegmentsProperty); }
			private set { SetValue(PathSegmentsProperty, value); }
		}

		public SessionActivityIndicator()
		{
			InitializeComponent();
		}

		private void BuildPathSegments()
		{
			if (SessionItem.PlanItemCollection.LastSampleTime == null)
			{
				return;
			}

			var pathSegments = new PathSegmentCollection();

			var executionStart = SessionItem.PlanItemCollection.ExecutionStart;
			var totalSeconds = (SessionItem.PlanItemCollection.LastSampleTime.Value - executionStart).TotalSeconds;

			var diagnosticsTooltipBuilder = new StringBuilder();
			diagnosticsTooltipBuilder.AppendLine($"Execution start: {executionStart}; last sample: {SessionItem.PlanItemCollection.LastSampleTime.Value}; samples: {SessionItem.ActiveSessionHistoryItems.Count}");

			int? previousActivity = null;
			var x = 0d;
			foreach (var historyItem in SessionItem.ActiveSessionHistoryItems)
			{
				x = (historyItem.SampleTime - executionStart).TotalSeconds / totalSeconds;

				var activity = String.Equals(historyItem.SessionState, "ON CPU") ? 0 : 1;
				if (previousActivity != activity)
				{
					if (previousActivity.HasValue || activity == 0)
					{
						var horizontalSegment = new LineSegment(new Point(x, previousActivity ?? 1), false);
						pathSegments.Add(horizontalSegment);
					}

					var verticalSegment = new LineSegment(new Point(x, activity), false);
					pathSegments.Add(verticalSegment);

					diagnosticsTooltipBuilder.AppendLine($"{historyItem.SampleTime} - {historyItem.SessionState}; event: {historyItem.Event}");
				}

				previousActivity = activity;
			}

			Diagnostics = diagnosticsTooltipBuilder.ToString();

			if (pathSegments.Count <= 1)
			{
				return;
			}

			if (previousActivity == 0)
			{
				pathSegments.Add(new LineSegment(new Point(x, 0), false));
				pathSegments.Add(new LineSegment(new Point(x, 1), false));
			}

			pathSegments.Freeze();
			PathSegments = pathSegments;
		}
	}
}
