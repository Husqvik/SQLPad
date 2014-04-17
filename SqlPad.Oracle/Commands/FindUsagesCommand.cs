using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class FindUsagesCommand : DisplayCommandBase
	{
		private readonly StatementDescriptionNode _currentNode;
		private readonly OracleStatementSemanticModel _semanticModel;

		public FindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel)
		{
			_currentNode = new OracleSqlParser().Parse(statementText).GetTerminalAtPosition(currentPosition);
			_semanticModel = _currentNode == null
				? null
				: new OracleStatementSemanticModel(statementText, (OracleStatement)_currentNode.Statement, (DatabaseModelFake)databaseModel);
		}

		public override bool CanExecute(object parameter)
		{
			return _currentNode != null && (Terminals.IsIdentifier(_currentNode.Id) || _currentNode.Id == Terminals.Alias);
		}

		protected override void ExecuteInternal(ICollection<TextSegment> segmentsToReplace)
		{
			var queryBlock = _semanticModel.GetQueryBlock(_currentNode);

			foreach (var identifier in queryBlock.RootNode.Terminals.Where(t => (Terminals.Identifiers.Contains(t.Id) || t.Id == Terminals.Alias)))
			{
				segmentsToReplace.Add(new TextSegment
				                      {
					                      IndextStart = identifier.SourcePosition.IndexStart,
										  Length = identifier.SourcePosition.Length
				                      });
			}
		}
	}
}
