using System;
using System.Collections.Generic;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using SqlPad.Commands;

namespace SqlPad
{
	public class MultiNodeEditor
	{
		private readonly TextEditor _editor;

		private TextAnchor _masterAnchorStart;
		private TextAnchor _masterAnchorEnd;
		private readonly List<TextAnchor> _anchors = new List<TextAnchor>();

		private bool IsModificationValid => _masterAnchorStart.Offset <= _editor.CaretOffset && _masterAnchorEnd.Offset >= _editor.CaretOffset;

		private MultiNodeEditor(TextEditor editor)
		{
			_editor = editor;
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

			multiNodeEditor =
				new MultiNodeEditor(editor)
				{
					_masterAnchorStart = editor.Document.CreateAnchor(data.CurrentNode.SourcePosition.IndexStart),
					_masterAnchorEnd = editor.Document.CreateAnchor(data.CurrentNode.SourcePosition.IndexEnd + 1)
				};

			foreach (var editTerminal in data.SynchronizedNodes)
			{
				multiNodeEditor._anchors.Add(editor.Document.CreateAnchor(editTerminal.SourcePosition.IndexStart));
			}

			return true;
		}
	}

	public struct MultiNodeEditorData
	{
		public int OffsetFromNodeStartIndex { get; set; }

		public StatementGrammarNode CurrentNode { get; set; }

		public IReadOnlyCollection<StatementGrammarNode> SynchronizedNodes { get; set; }
	}
}