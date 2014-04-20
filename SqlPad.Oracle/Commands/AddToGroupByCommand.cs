using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class AddToGroupByCommand : OracleCommandBase
	{
		private readonly StatementDescriptionNode _fromClause;

		public AddToGroupByCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
			: base(semanticModel, currentTerminal)
		{
			_fromClause = CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
		}

		public override bool CanExecute(object parameter)
		{
			// TODO: Check ambiguous references
			return CurrentTerminal.Id == Terminals.Identifier && _fromClause != null && _fromClause.IsGrammarValid;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			var groupByClause = CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.GroupByClause).SingleOrDefault();
			if (groupByClause == null)
			{
				segmentsToReplace.Add(new TextSegment
				                      {
					                      IndextStart = _fromClause.LastTerminalNode.SourcePosition.IndexEnd + 1,
					                      Length = 0,
					                      Text = " GROUP BY "
				                      });
			}
			else
			{
				// TODO: Find existing elements
			}

			// TODO: Find handle multiple selected columns
		}
	}
}
