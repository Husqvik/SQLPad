using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SqlPad
{
	public interface IStatementValidator
	{
		IStatementSemanticModel BuildSemanticModel(string statementText, StatementBase statementBase, IDatabaseModel databaseModel);

		IValidationModel BuildValidationModel(IStatementSemanticModel semanticModel);
	}

	public interface IStatementSemanticModel
	{
		IDatabaseModel DatabaseModel { get; }
		StatementBase Statement { get; }
		string StatementText { get; }
		bool HasDatabaseModel { get; }
		ICollection<RedundantTerminalGroup> RedundantSymbolGroups { get; }
	}

	public class RedundantTerminalGroup : ReadOnlyCollection<StatementGrammarNode>
	{
		public RedundantTerminalGroup(IEnumerable<StatementGrammarNode> items, RedundancyType redundancyType)
			: base(items.ToArray())
		{
			RedundancyType = redundancyType;
		}

		public RedundancyType RedundancyType { get; private set; }
	}

	public enum RedundancyType
	{
		Qualifier,
		UnusedColumn,
		RedundantColumnAlias,
		RedundantObjectAlias,
		UnusedQueryBlock
	}
}
