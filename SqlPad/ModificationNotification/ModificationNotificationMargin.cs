using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace SqlPad.ModificationNotification
{
	public class ModificationNotificationMargin : AbstractMargin
	{
		private const int MarginWith = 4;
		private static readonly Pen Pen = new Pen(Brushes.LimeGreen, 0);

		private readonly List<LineStatus> _statuses = new List<LineStatus>();
		private readonly TextEditor _editor;

		static ModificationNotificationMargin()
		{
			Pen.Freeze();
		}

		public ModificationNotificationMargin(TextEditor editor)
		{
			_editor = editor;
		}

		public void NotifySaveChanges()
		{
			for (var i = 0; i < _statuses.Count; i++)
			{
				var status = _statuses[i];
				if (status.Notification == LineNotification.Modified)
				{
					status.Notification = LineNotification.ModifiedAndSaved;
				}

				var line = TextView.Document.Lines[i];
				status.OriginalText = TextView.Document.GetText(line);
			}
		}

		private void Initialize()
		{
			var statuses = TextView.Document.Lines
				.Select(l =>
					new LineStatus
					{
						Notification = LineNotification.None,
						OriginalText = TextView.Document.GetText(l)
					});

			_statuses.Clear();
			_statuses.AddRange(statuses);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(MarginWith, 0);
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			var textView = TextView;
			if (textView == null || !textView.VisualLinesValid)
			{
				return;
			}

			for (var i = 0; i < textView.Document.Lines.Count; i++)
			{
				Brush brush;

				var line = textView.Document.Lines[i];
				//var currentText = textView.Document.GetText(line);
				if (_statuses[i].Notification == LineNotification.Modified)
				{
					brush = Brushes.Gold;
				}
				else if (_statuses[i].Notification == LineNotification.ModifiedAndSaved)
				{
					brush = Brushes.LimeGreen;
				}
				else
				{
					brush = Brushes.Transparent;
				}

				var visualLine = textView.GetVisualLine(line.LineNumber);
				var lineTop = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[0], VisualYPosition.LineTop);
				var lineBottom = visualLine.GetTextLineVisualYPosition(visualLine.TextLines[visualLine.TextLines.Count - 1], VisualYPosition.LineBottom);
				drawingContext.DrawRectangle(brush, Pen, new Rect(new Point(0, lineTop), new Size(MarginWith, lineBottom - lineTop)));
			}
		}

		protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
		{
			if (oldTextView != null)
			{
				oldTextView.VisualLinesChanged -= TextViewVisualLinesChangedHandler;
				TextView.Document.TextChanged -= DocumentTextChangedHandler;
			}

			base.OnTextViewChanged(oldTextView, newTextView);

			if (newTextView != null)
			{
				newTextView.VisualLinesChanged += TextViewVisualLinesChangedHandler;
				TextView.Document.TextChanged += DocumentTextChangedHandler;
			}

			InvalidateVisual();
		}

		private void DocumentTextChangedHandler(object sender, EventArgs eventArgs)
		{
			if (_statuses.Count == 0)
			{
				Initialize();
			}
			else
			{
				var linesEnumerator = TextView.Document.Lines.GetEnumerator();
				var statusesEnumerator = _statuses.ToArray().GetEnumerator();
			}

			InvalidateVisual();
		}

		private void TextViewVisualLinesChangedHandler(object sender, EventArgs eventArgs)
		{
			InvalidateVisual();
		}

		protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
		{
			return new PointHitTestResult(this, hitTestParameters.HitPoint);
		}

		private class LineStatus
		{
			public LineNotification Notification { get; set; }
			
			public string OriginalText { get; set; }
		}

		private enum LineNotification
		{
			None,
			Modified,
			ModifiedAndSaved
		}
	}
}
