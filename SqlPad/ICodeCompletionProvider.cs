using System.Collections.Generic;

namespace SqlPad
{
	public interface ICodeCompletionProvider
	{
		ICollection<FunctionOverloadDescription> ResolveFunctionOverloads(SqlDocumentRepository sqlDocumentRepository, int cursorPosition);

		ICollection<ICodeCompletionItem> ResolveItems(SqlDocumentRepository sqlDocumentRepository, IDatabaseModel databaseModel, int cursorPosition, bool forcedInvokation);
	}

	public class FunctionOverloadDescription
	{
		public string Name { get; set; }

		public IList<string> Parameters { get; set; }

		public int CurrentParameterIndex { get; set; }
		
		public string ReturnedDatatype { get; set; }
		
		public bool IsParameterMetadataAvailable { get; set; }
	}

	public interface ICodeCompletionItem
	{
		string Category { get; }
		
		string Name { get; }

		StatementGrammarNode StatementNode { get; }

		int Priority { get; }

		int CategoryPriority { get; }

		int InsertOffset { get; }

		int CaretOffset { get; }

		string Text { get; }
	}
}