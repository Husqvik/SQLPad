using System;
using System.Collections.Generic;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using SqlPad.Commands;

namespace SqlPad
{
	public class MultiNodeEditor
	{
		private readonly TextEditor _editor;

		private readonly TextAnchor _masterAnchorStart;
		private readonly TextAnchor _masterAnchorEnd;
		private readonly List<TextAnchor> _anchors = new List<TextAnchor>();

		private bool IsModificationValid => _masterAnchorStart.Offset <= _editor.CaretOffset && _masterAnchorEnd.Offset >= _editor.CaretOffset;

		private MultiNodeEditor(TextEditor editor, MultiNodeEditorData data)
		{
			_editor = editor;

			_masterAnchorStart = editor.Document.CreateAnchor(data.CurrentNode.SourcePosition.IndexStart);
			_masterAnchorEnd = editor.Document.CreateAnchor(data.CurrentNode.SourcePosition.IndexEnd + 1);

			foreach (var segment in data.SynchronizedSegments)
			{
				_anchors.Add(editor.Document.CreateAnchor(segment.IndexStart));
			}
		}

		public bool Replace(string newText)
		{
			if (!IsModificationValid)
			{
				return false;
			}

			var editTerminalOffset = _editor.CaretOffset - _masterAnchorStart.Offset;

			foreach (var anchor in _anchors)
			{
				_editor.Document.Replace(anchor.Offset + editTerminalOffset, _editor.SelectionLength, newText);
			}

			return true;
		}

		public bool RemoveCharacter(bool reverse)
		{
			if (!IsModificationValid)
			{
				return false;
			}

			var editTerminalOffset = _editor.CaretOffset - _masterAnchorStart.Offset;
			var selectionCharacter = reverse && _editor.SelectionLength == 0 ? 1 : 0;
			var removedCharacters = _editor.SelectionLength == 0 ? 1 : _editor.SelectionLength;
			foreach (var anchor in _anchors)
			{
				_editor.Document.Remove(anchor.Offset + editTerminalOffset - selectionCharacter, removedCharacters);
			}

			return true;
		}

		public static bool TryCreateMultiNodeEditor(TextEditor editor, ActionExecutionContext executionContext, IMultiNodeEditorDataProvider dataProvider, out MultiNodeEditor multiNodeEditor)
		{
			multiNodeEditor = null;
			if (!String.Equals(editor.Text, executionContext.DocumentRepository.StatementText))
			{
				return false;
			}

			var data = dataProvider.GetMultiNodeEditorData(executionContext);
			if (data.CurrentNode == null)
			{
				return false;
			}

			multiNodeEditor = new MultiNodeEditor(editor, data);
			return true;
		}

		public SourcePosition MasterSegment => SourcePosition.Create(_masterAnchorStart.Offset, _masterAnchorEnd.Offset);

		public IReadOnlyCollection<SourcePosition> SynchronizedSegments
		{
			get
			{
				var segment = MasterSegment;
				var segments = new List<SourcePosition>(_anchors.Count);

				foreach (var anchor in _anchors)
				{
					segments.Add(SourcePosition.Create(anchor.Offset, anchor.Offset + segment.Length - 1));
				}

				return segments;
			}
		}
	}

	public struct MultiNodeEditorData
	{
		public StatementGrammarNode CurrentNode { get; set; }

		public IReadOnlyCollection<SourcePosition> SynchronizedSegments { get; set; }
	}

	public class MultiNodeEditorBoxRenderer : IBackgroundRenderer
	{
		private static readonly Pen MasterEdgePen = new Pen(Brushes.Red, 1);
		private static readonly Pen SynchronizedEdgePen = new Pen(Brushes.Black, 1) { DashStyle = DashStyles.Dot };

		public KnownLayer Layer { get; } = KnownLayer.Background;

		public IReadOnlyCollection<SourcePosition> SynchronizedSegments { get; set; }

		public SourcePosition? MasterSegment { get; set; }

		static MultiNodeEditorBoxRenderer()
		{
			SynchronizedEdgePen.Freeze();
			MasterEdgePen.Freeze();
		}

		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (SynchronizedSegments == null)
			{
				return;
			}

			if (MasterSegment.HasValue)
			{
				DrawBoundingBox(textView, drawingContext, MasterSegment.Value, MasterEdgePen);
			}

			foreach (var segment in SynchronizedSegments)
			{
				DrawBoundingBox(textView, drawingContext, segment, SynchronizedEdgePen);
			}
		}

		private static void DrawBoundingBox(TextView textView, DrawingContext drawingContext, SourcePosition sourceSegment, Pen pen)
		{
			var segment = new ICSharpCode.AvalonEdit.Document.TextSegment { StartOffset = sourceSegment.IndexStart, EndOffset = sourceSegment.IndexEnd };
			foreach (var rectangle in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
			{
				drawingContext.DrawRectangle(Brushes.Transparent, pen, rectangle);
			}
		}
	}
}