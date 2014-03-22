using System.Collections.Generic;

namespace SqlPad
{
	public interface ICodeCompletionProvider
	{
		ICollection<ICodeCompletionItem> ResolveItems(string statementText, int cursorPosition);
	}

	public interface ICodeCompletionItem
	{
		string Name { get; }

		StatementDescriptionNode StatementNode { get; }
	}
}