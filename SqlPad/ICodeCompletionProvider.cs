using System.Collections.Generic;

namespace SqlPad
{
	public interface ICodeCompletionProvider
	{
		ICollection<FunctionOverloadDescription> ResolveFunctionOverloads(StatementCollection statementCollection, IDatabaseModel databaseModel, int cursorPosition);

		ICollection<ICodeCompletionItem> ResolveItems(SqlDocumentStore sqlDocumentStore, IDatabaseModel databaseModel, string statementText, int cursorPosition);
	}

	public class FunctionOverloadDescription
	{
		public string Name { get; set; }

		public ICollection<string> Parameters { get; set; }

		public int CurrentParameterIndex { get; set; }
		
		public string ReturnedDatatype { get; set; }
	}

	public interface ICodeCompletionItem
	{
		string Category { get; }
		
		string Name { get; }

		StatementDescriptionNode StatementNode { get; }

		int Priority { get; }

		int CategoryPriority { get; }

		int Offset { get; }

		int CaretOffset { get; }

		string Text { get; }
	}
}