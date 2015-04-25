using System.Collections.Generic;

namespace SqlPad
{
	public interface ICodeSnippetProvider
	{
		IEnumerable<ICodeSnippet> GetSnippets(SqlDocumentRepository sqlDocumentRepository, string statementText, int cursorPosition);

		IEnumerable<ICodeSnippet> GetCodeGenerationItems(SqlDocumentRepository sqlDocumentRepository);
	}

	public interface ICodeSnippet
	{
		string Name { get; }

		string Description { get; }

		string BaseText { get; }

		ICollection<ICodeSnippetParameter> Parameters { get; }

		SourcePosition SourceToReplace { get; }
	}

	public interface ICodeSnippetParameter
	{
		string Name { get; }

		int Index { get; }

		string DefaultValue { get; }
	}
}
