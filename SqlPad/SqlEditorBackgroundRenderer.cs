using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SqlPad
{
	internal class SqlEditorBackgroundRenderer : IBackgroundRenderer
	{
		private static readonly Pen NullPen = new Pen(Brushes.Transparent, 0);
		private static readonly Pen MasterEdgePen = new Pen(Brushes.Red, 1);
		private static readonly Pen SynchronizedEdgePen = new Pen(Brushes.Black, 1) { DashStyle = DashStyles.Dot };

		private static readonly SolidColorBrush HighlightUsageBrush = Brushes.Turquoise;
		private static readonly SolidColorBrush HighlightDefinitionBrush = Brushes.SandyBrown;

		private readonly Stack<IReadOnlyCollection<HighlightSegment>> _highlightSegments = new Stack<IReadOnlyCollection<HighlightSegment>>();
		private readonly TextEditor _textEditor;

		private ActiveSnippet _activeSnippet;

		public KnownLayer Layer { get; } = KnownLayer.Background;

		public IEnumerable<TextSegment> HighlightSegments => _highlightSegments.SelectMany(g => g.Select(s => s.Segment));

		public IReadOnlyCollection<SourcePosition> SynchronizedSegments { get; set; }

		public SourcePosition? MasterSegment { get; set; }

		public ActiveSnippet ActiveSnippet
		{
			get { return _activeSnippet; }
			set
			{
				if (_activeSnippet == value)
				{
					return;
				}

				_activeSnippet = value;

				if (value == null && _textEditor.SelectionLength > 0)
				{
					var caretOffset = _textEditor.SelectionStart + _textEditor.SelectionLength;
					_textEditor.SelectionLength = 0;
					_textEditor.CaretOffset = caretOffset;
				}

				_textEditor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
			}
		}

		static SqlEditorBackgroundRenderer()
		{
			SynchronizedEdgePen.Freeze();
			MasterEdgePen.Freeze();
			NullPen.Freeze();
		}

		public SqlEditorBackgroundRenderer(TextEditor textEditor)
		{
			_textEditor = textEditor;
		}

		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (SynchronizedSegments != null)
			{
				if (MasterSegment.HasValue)
				{
					DrawRectangle(textView, drawingContext, MasterSegment.Value, Brushes.Transparent, MasterEdgePen);
				}

				foreach (var segment in SynchronizedSegments)
				{
					DrawRectangle(textView, drawingContext, segment, Brushes.Transparent, SynchronizedEdgePen);
				}
			}

			if (_activeSnippet != null && _activeSnippet.ActiveAnchorsValid)
			{
				DrawRectangle(textView, drawingContext, SourcePosition.Create(_activeSnippet.ActiveAnchors.Item1.Offset, _activeSnippet.ActiveAnchors.Item2.Offset), Brushes.Transparent, MasterEdgePen);

				foreach (var anchorGroup in _activeSnippet.FollowingAnchorGroups)
				{
					foreach (var anchors in anchorGroup)
					{
						if (anchors.Item1.IsDeleted || anchors.Item2.IsDeleted)
						{
							continue;
						}

						DrawRectangle(textView, drawingContext, SourcePosition.Create(anchors.Item1.Offset, anchors.Item2.Offset), Brushes.Transparent, SynchronizedEdgePen);
					}
				}
			}

			foreach (var highlightSegmentGroup in _highlightSegments)
			{
				foreach (var highlightSegment in highlightSegmentGroup)
				{
					var brush = highlightSegment.Segment.DisplayOptions == DisplayOptions.Definition ? HighlightDefinitionBrush : HighlightUsageBrush;
					if (highlightSegment.HighlightStartAnchor.IsDeleted || highlightSegment.HighlightEndAnchor.IsDeleted)
					{
						continue;
					}

					var indexStart = highlightSegment.HighlightStartAnchor.Offset;
					var indexEnd = highlightSegment.HighlightEndAnchor.Offset;
					if (indexEnd > indexStart)
					{
						DrawRectangle(textView, drawingContext, SourcePosition.Create(indexStart, indexEnd), brush, NullPen);
					}
				}
			}
		}

		private static void DrawRectangle(TextView textView, DrawingContext drawingContext, SourcePosition sourceSegment, Brush brush, Pen pen)
		{
			var segment = new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = sourceSegment.IndexStart, EndOffset = sourceSegment.IndexEnd };
			foreach (var rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
			{
				drawingContext.DrawRectangle(brush, pen, rectangle);
			}
		}

		public void AddHighlightSegments(ICollection<TextSegment> highlightSegments)
		{
			if (highlightSegments != null)
			{
				if (_highlightSegments.SelectMany(g => g).Any(s => s.Segment.Equals(highlightSegments.First())))
				{
					return;
				}

				var anchoredSegment =
					highlightSegments.Select(
						s =>
							new HighlightSegment
							{
								Segment = s,
								HighlightStartAnchor = _textEditor.Document.CreateAnchor(s.IndextStart),
								HighlightEndAnchor = _textEditor.Document.CreateAnchor(s.IndextStart + s.Length)
							}).ToArray();

				_highlightSegments.Push(anchoredSegment);
			}
			else if (_highlightSegments.Count > 0)
			{
				_highlightSegments.Pop();
			}
		}

		private struct HighlightSegment
		{
			public TextAnchor HighlightStartAnchor;

			public TextAnchor HighlightEndAnchor;

			public TextSegment Segment;
		}
	}
}
