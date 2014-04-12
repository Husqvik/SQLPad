using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.AvalonEdit;

namespace SqlPad
{
	public class MultiNodeEditor
	{
		private readonly ISqlParser _parser;
		private readonly TextEditor _editor;

		public MultiNodeEditor(TextEditor editor, IInfrastructureFactory infrastructureFactory)
		{
			_parser = infrastructureFactory.CreateSqlParser();
			_editor = editor;
		}

		public void InsertText(string text)
		{
			var data = GetSynchronizationData();

			foreach (var node in data.SynchronizedNodes)
			{
				_editor.Document.Insert(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex, text);
			}
		}

		public void RemoveCharacter()
		{
			var data = GetSynchronizationData();
			foreach (var node in data.SynchronizedNodes)
			{
				_editor.Document.Remove(node.SourcePosition.IndexStart + data.OffsetFromNodeStartIndex, 1);
			}
		}

		private SynchronizationData GetSynchronizationData()
		{
			var statements = _parser.Parse(_editor.Text);
			var currentNode = statements.GetTerminalAtPosition(_editor.CaretOffset, n => n.Id.In("Alias", "ObjectIdentifier", "SchemaIdentifier", "Identifier"));

			// TODO: Handle by provider
			var synchronizedNodes = currentNode.Statement.AllTerminals
				.Where(t => t != currentNode && String.Equals(t.Token.Value, currentNode.Token.Value, StringComparison.InvariantCultureIgnoreCase))
				.OrderByDescending(t => t.SourcePosition.IndexStart);

			var offsetFromNodeStartIndex = _editor.CaretOffset - currentNode.SourcePosition.IndexStart;

			return new SynchronizationData { OffsetFromNodeStartIndex = offsetFromNodeStartIndex, SynchronizedNodes = synchronizedNodes };
		}

		private struct SynchronizationData
		{
			public int OffsetFromNodeStartIndex { get; set; }
			public IEnumerable<StatementDescriptionNode> SynchronizedNodes { get; set; }
		}
	}
}