using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using SqlPad.Commands;

namespace SqlPad
{
	internal class MultiNodeEditor
	{
		private readonly TextEditor _editor;
		private readonly string _originalValue;
		private readonly int _originalCaretOffset;
		private readonly TextAnchor _masterAnchorStart;
		private readonly TextAnchor _masterAnchorEnd;
		private readonly List<TextAnchor> _anchors = new List<TextAnchor>();

		private bool IsModificationValid => _masterAnchorStart.Offset <= _editor.CaretOffset && _masterAnchorEnd.Offset >= _editor.CaretOffset && _masterAnchorEnd.Offset >= _masterAnchorStart.Offset;

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

		public MultiNodeEditor(TextEditor editor, TextAnchor masterAnchorStart, TextAnchor masterAnchorEnd, IEnumerable<TextAnchor> synchronizedAnchors)
		{
			_editor = editor;
			_originalValue = editor.Document.GetText(masterAnchorStart.Offset, masterAnchorEnd.Offset - masterAnchorStart.Offset);
			_originalCaretOffset = editor.CaretOffset;
			_masterAnchorStart = masterAnchorStart;
			_masterAnchorEnd = masterAnchorEnd;
			_anchors.AddRange(synchronizedAnchors);
		}

		public bool Replace(string newText)
		{
			if (!IsModificationValid)
			{
				return false;
			}

			var editTerminalOffset = _editor.SelectionStart - _masterAnchorStart.Offset;

			_editor.Document.BeginUpdate();

			foreach (var anchor in _anchors)
			{
				var insertOffset = anchor.Offset + editTerminalOffset;
				_editor.Document.Replace(insertOffset, _editor.SelectionLength, newText);
			}

			return true;
		}

		public bool RemoveCharacter(bool reverse)
		{
			if (!IsModificationValid)
			{
				return false;
			}

			var editTerminalOffset = _editor.SelectionStart - _masterAnchorStart.Offset;
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

			var masterAnchorStart = editor.Document.CreateAnchor(data.CurrentNode.SourcePosition.IndexStart);
			masterAnchorStart.MovementType = AnchorMovementType.BeforeInsertion;
			var masterAnchorEnd = editor.Document.CreateAnchor(data.CurrentNode.SourcePosition.IndexEnd + 1);

			var anchors = new List<TextAnchor>();
			foreach (var segment in data.SynchronizedSegments)
			{
				var anchor = editor.Document.CreateAnchor(segment.IndexStart);
				anchor.MovementType = AnchorMovementType.BeforeInsertion;
				anchors.Add(anchor);
			}

			multiNodeEditor = new MultiNodeEditor(editor, masterAnchorStart, masterAnchorEnd, anchors);
			return true;
		}

		public void Cancel()
		{
			var segment = MasterSegment;

			var textLength = segment.Length - 1;
			var currentText = _editor.Document.GetText(segment.IndexStart, textLength);
			if (String.Equals(currentText, _originalValue))
			{
				return;
			}

			_editor.BeginChange();

			_editor.Document.Replace(segment.IndexStart, textLength, _originalValue);

			foreach (var anchor in _anchors)
			{
				_editor.Document.Replace(anchor.Offset, textLength, _originalValue);
			}

			_editor.EndChange();

			_editor.CaretOffset = _originalCaretOffset;
		}
	}

	public struct MultiNodeEditorData
	{
		public StatementGrammarNode CurrentNode { get; set; }

		public IReadOnlyCollection<SourcePosition> SynchronizedSegments { get; set; }
	}
}
