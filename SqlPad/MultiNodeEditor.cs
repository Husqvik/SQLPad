using System.Collections.Generic;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public class MultiNodeEditor
	{
		private readonly TextEditor _editor;
		private readonly IMultiNodeEditorDataProvider _dataProvider;
		private StatementDescriptionNode _currentNode;

		private MultiNodeEditor(TextEditor editor, IMultiNodeEditorDataProvider dataProvider, StatementDescriptionNode currentNode)
		{
			_dataProvider = dataProvider;
			_editor = editor;
			_currentNode = currentNode;
		}

		public bool Replace(string newText)
		{
			var data = GetSynchronizationData(_dataProvider, _editor);
			if (!IsModificationValid(data))
				return false;

			_currentNode = data.CurrentNode;

			foreach (var node in data.SynchronizedNodes)
			{
				_editor.Document.Replace(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex, _editor.SelectionLength, newText);
			}

			return true;
		}

		public bool RemoveCharacter(bool reverse)
		{
			var data = GetSynchronizationData(_dataProvider, _editor);
			if (!IsModificationValid(data))
				return false;

			_currentNode = data.CurrentNode;

			foreach (var node in data.SynchronizedNodes)
			{
				var selectionCharacter = reverse && _editor.SelectionLength == 0 ? 1 : 0;
				var removedCharacters = _editor.SelectionLength == 0 ? 1 : _editor.SelectionLength;
				_editor.Document.Remove(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex - selectionCharacter, removedCharacters);
			}

			return true;
		}

		public static bool TryCreateMultiNodeEditor(TextEditor editor, IInfrastructureFactory infrastructureFactory, out MultiNodeEditor multiNodeEditor)
		{
			var dataProvider = infrastructureFactory.CreateMultiNodeEditorDataProvider();
			var data = GetSynchronizationData(dataProvider, editor);

			multiNodeEditor = data.CurrentNode != null ? new MultiNodeEditor(editor, dataProvider, data.CurrentNode) : null;
			return multiNodeEditor != null;
		}

		private bool IsModificationValid(MultiNodeEditorData data)
		{
			return data.CurrentNode != null && data.CurrentNode.SourcePosition.IndexStart <= _editor.CaretOffset && data.CurrentNode.SourcePosition.IndexEnd + 1 >= _editor.CaretOffset;
		}

		private static MultiNodeEditorData GetSynchronizationData(IMultiNodeEditorDataProvider dataProvider, TextEditor editor)
		{
			return dataProvider.GetMultiNodeEditorData(editor.Text, editor.CaretOffset, editor.SelectionStart, editor.SelectionLength);
		}
	}

	public struct MultiNodeEditorData
	{
		public int OffsetFromNodeStartIndex { get; set; }
		public StatementDescriptionNode CurrentNode { get; set; }
		public IEnumerable<StatementDescriptionNode> SynchronizedNodes { get; set; }
	}
}