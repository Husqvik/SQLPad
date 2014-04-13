using System.Collections.Generic;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public class MultiNodeEditor
	{
		private readonly TextEditor _editor;
		private readonly IMultiNodeEditorDataProvider _dataProvider;

		public MultiNodeEditor(TextEditor editor, IInfrastructureFactory infrastructureFactory)
		{
			_dataProvider = infrastructureFactory.CreateMultiNodeEditorDataProvider();
			_editor = editor;
		}

		/*public void InsertText(string text)
		{
			var data = GetSynchronizationData();

			foreach (var node in data.SynchronizedNodes)
			{
				_editor.Document.Insert(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex, text);
			}
		}*/

		public void Replace(string newText)
		{
			var data = GetSynchronizationData();
			foreach (var node in data.SynchronizedNodes)
			{
				_editor.Document.Replace(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex, _editor.SelectionLength, newText);
			}
		}

		public void RemoveCharacter(bool reverse)
		{
			var data = GetSynchronizationData();
			foreach (var node in data.SynchronizedNodes)
			{
				var selectionCharacter = reverse && _editor.SelectionLength == 0 ? 1 : 0;
				_editor.Document.Remove(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex - selectionCharacter, data.Characters);
			}
		}

		private MultiNodeEditorData GetSynchronizationData()
		{
			return _dataProvider.GetMultiNodeEditorData(_editor.Text, _editor.CaretOffset, _editor.SelectionStart, _editor.SelectionLength);
		}
	}

	public struct MultiNodeEditorData
	{
		public bool IsAllowed { get; set; }
		public int OffsetFromNodeStartIndex { get; set; }
		public int Characters { get; set; }
		public StatementDescriptionNode CurrentNode { get; set; }
		public IEnumerable<StatementDescriptionNode> SynchronizedNodes { get; set; }
	}
}