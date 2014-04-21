using System.Collections.Generic;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public class MultiNodeEditor
	{
		private readonly TextEditor _editor;
		private readonly IMultiNodeEditorDataProvider _dataProvider;
		private readonly IDatabaseModel _databaseModel;

		private MultiNodeEditor(TextEditor editor, IMultiNodeEditorDataProvider dataProvider, IDatabaseModel databaseModel)
		{
			_databaseModel = databaseModel;
			_dataProvider = dataProvider;
			_editor = editor;
		}

		public bool Replace(string newText)
		{
			var data = GetSynchronizationData(_dataProvider, _editor, _databaseModel);
			if (!IsModificationValid(data))
				return false;

			foreach (var node in data.SynchronizedNodes)
			{
				_editor.Document.Replace(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex, _editor.SelectionLength, newText);
			}

			return true;
		}

		public bool RemoveCharacter(bool reverse)
		{
			var data = GetSynchronizationData(_dataProvider, _editor, _databaseModel);
			if (!IsModificationValid(data))
				return false;

			foreach (var node in data.SynchronizedNodes)
			{
				var selectionCharacter = reverse && _editor.SelectionLength == 0 ? 1 : 0;
				var removedCharacters = _editor.SelectionLength == 0 ? 1 : _editor.SelectionLength;
				_editor.Document.Remove(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex - selectionCharacter, removedCharacters);
			}

			return true;
		}

		public static bool TryCreateMultiNodeEditor(TextEditor editor, IMultiNodeEditorDataProvider dataProvider, IDatabaseModel databaseModel, out MultiNodeEditor multiNodeEditor)
		{
			var data = GetSynchronizationData(dataProvider, editor, databaseModel);

			multiNodeEditor = data.CurrentNode != null ? new MultiNodeEditor(editor, dataProvider, databaseModel) : null;
			return multiNodeEditor != null;
		}

		private bool IsModificationValid(MultiNodeEditorData data)
		{
			return data.CurrentNode != null && data.CurrentNode.SourcePosition.IndexStart <= _editor.CaretOffset && data.CurrentNode.SourcePosition.IndexEnd + 1 >= _editor.CaretOffset;
		}

		private static MultiNodeEditorData GetSynchronizationData(IMultiNodeEditorDataProvider dataProvider, TextEditor editor, IDatabaseModel databaseModel)
		{
			return dataProvider.GetMultiNodeEditorData(databaseModel, editor.Text, editor.CaretOffset, editor.SelectionStart, editor.SelectionLength);
		}
	}

	public struct MultiNodeEditorData
	{
		public int OffsetFromNodeStartIndex { get; set; }
		public StatementDescriptionNode CurrentNode { get; set; }
		public IEnumerable<StatementDescriptionNode> SynchronizedNodes { get; set; }
	}
}