using System.Collections.Generic;

namespace SqlPad
{
	public interface ICodeCompletionProvider
	{
		ICollection<ICodeCompletionItem> ResolveItems(SqlDocument sqlDocument, IDatabaseModel databaseModel, string statementText, int cursorPosition);
	}

	public interface ICodeCompletionItem
	{
		string Category { get; }
		
		string Name { get; }

		StatementDescriptionNode StatementNode { get; }

		int Priority { get; }

		int CategoryPriority { get; }

		int Offset { get; }

		string Text { get; }
	}
}