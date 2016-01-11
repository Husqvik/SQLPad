using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle
{
	public partial class SessionActivityIndicator
	{
		public static readonly DependencyProperty SessionItemProperty = DependencyProperty.Register(nameof(SessionItem), typeof(SqlMonitorSessionItem), typeof(SessionActivityIndicator), new FrameworkPropertyMetadata(SessionItemChangedCallback));
		public static readonly DependencyProperty CpuSegmentsProperty = DependencyProperty.Register(nameof(CpuSegments), typeof(PathSegmentCollection), typeof(SessionActivityIndicator), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty WaitingSegmentsProperty = DependencyProperty.Register(nameof(WaitingSegments), typeof(PathSegmentCollection), typeof(SessionActivityIndicator), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty IdleSegmentsProperty = DependencyProperty.Register(nameof(IdleSegments), typeof(PathSegmentCollection), typeof(SessionActivityIndicator), new FrameworkPropertyMetadata());
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
				visualizer.BuildPathSegments();

				newSessionItem.PlanItemCollection.PropertyChanged +=
					(sender, e) =>
						(String.Equals(e.PropertyName, nameof(SqlMonitorPlanItemCollection.LastSampleTime)) ? visualizer : null)?.BuildPathSegments();
			}
		}

		public PathSegmentCollection CpuSegments
		{
			get { return (PathSegmentCollection)GetValue(CpuSegmentsProperty); }
			private set { SetValue(CpuSegmentsProperty, value); }
		}

		public PathSegmentCollection WaitingSegments
		{
			get { return (PathSegmentCollection)GetValue(WaitingSegmentsProperty); }
			private set { SetValue(WaitingSegmentsProperty, value); }
		}

		public PathSegmentCollection IdleSegments
		{
			get { return (PathSegmentCollection)GetValue(IdleSegmentsProperty); }
			private set { SetValue(IdleSegmentsProperty, value); }
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

			var cpuSegments = new PathSegmentCollection();
			var waitingSegments = new PathSegmentCollection();
			var idleSegments = new PathSegmentCollection();

			var executionStart = SessionItem.PlanItemCollection.ExecutionStart;
			var totalSeconds = (SessionItem.PlanItemCollection.LastSampleTime.Value - executionStart).TotalSeconds + 1;
			var oneSecondRelativeOffset = 1 / totalSeconds;

			var diagnosticsTooltipBuilder = new StringBuilder();
			var sampleRangeSeconds = (SessionItem.ActiveSessionHistoryItems.LastOrDefault()?.SampleTime - SessionItem.ActiveSessionHistoryItems.FirstOrDefault()?.SampleTime)?.TotalSeconds;
			diagnosticsTooltipBuilder.AppendLine($"SID: {SessionItem.SessionId}; execution start: {executionStart}; last sample: {SessionItem.PlanItemCollection.LastSampleTime.Value}; samples: {SessionItem.ActiveSessionHistoryItems.Count}; idle: {(sampleRangeSeconds ?? 0) - SessionItem.ActiveSessionHistoryItems.Count} s");

			var lastCpuActivity = 1;
			var lastWaitingActivity = 1;
			var x = 0d;
			var lastOffsetSeconds = 0d;
			var index = 0;
			foreach (var historyItem in SessionItem.ActiveSessionHistoryItems)
			{
				var offsetSeconds = (historyItem.SampleTime - executionStart).TotalSeconds + 1;
				var newX = offsetSeconds / totalSeconds;
				var hasBeenIdle = offsetSeconds - lastOffsetSeconds >= 2;
				if (hasBeenIdle)
				{
					TerminateLastActivity(x, ref lastCpuActivity, cpuSegments);
					TerminateLastActivity(x, ref lastWaitingActivity, waitingSegments);

					idleSegments.Add(new LineSegment(new Point(x, 1), false));
					idleSegments.Add(new LineSegment(new Point(x, 0), false));
					idleSegments.Add(new LineSegment(new Point(newX - oneSecondRelativeOffset, 0), false));
					idleSegments.Add(new LineSegment(new Point(newX - oneSecondRelativeOffset, 1), false));
				}

				var cpuActivity = String.Equals(historyItem.SessionState, "ON CPU") ? 0 : 1;
				var waitingActivity = cpuActivity == 0 ? 1 : 0;
				if (lastCpuActivity != cpuActivity)
				{
					cpuSegments.Add(new LineSegment(new Point(newX - oneSecondRelativeOffset, lastCpuActivity), false));
					cpuSegments.Add(new LineSegment(new Point(newX - oneSecondRelativeOffset, cpuActivity), false));
				}

				if (lastWaitingActivity != waitingActivity)
				{
					waitingSegments.Add(new LineSegment(new Point(newX - oneSecondRelativeOffset, lastWaitingActivity), false));
					waitingSegments.Add(new LineSegment(new Point(newX - oneSecondRelativeOffset, waitingActivity), false));
				}

				if (hasBeenIdle)
				{
					diagnosticsTooltipBuilder.AppendLine($"Idle {offsetSeconds - lastOffsetSeconds} s");
				}

				diagnosticsTooltipBuilder.Append($"{++index}. {historyItem.SampleTime} - {historyItem.SessionState}");
				if (cpuActivity == 1)
				{
					diagnosticsTooltipBuilder.Append($"; event: {historyItem.Event}");
				}

				diagnosticsTooltipBuilder.AppendLine();

				lastCpuActivity = cpuActivity;
				lastWaitingActivity = waitingActivity;
				lastOffsetSeconds = offsetSeconds;
				x = newX;
			}

			Diagnostics = diagnosticsTooltipBuilder.ToString();

			TerminateLastActivity(x, ref lastCpuActivity, cpuSegments);
			TerminateLastActivity(x, ref lastWaitingActivity, waitingSegments);

			cpuSegments.Freeze();
			waitingSegments.Freeze();
			idleSegments.Freeze();

			CpuSegments = cpuSegments;
			WaitingSegments = waitingSegments;
			IdleSegments = idleSegments;
		}

		private static void TerminateLastActivity(double x, ref int activity, PathSegmentCollection segments)
		{
			if (activity == 1)
			{
				return;
			}

			activity = 1;
			segments.Add(new LineSegment(new Point(x, 0), false));
			segments.Add(new LineSegment(new Point(x, 1), false));
		}
	}
}
