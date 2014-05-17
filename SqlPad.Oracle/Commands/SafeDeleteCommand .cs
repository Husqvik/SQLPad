using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class SafeDeleteCommand  : OracleCommandBase
	{
		private readonly OracleQueryBlock _queryBlock;

		public SafeDeleteCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode)
			: base(semanticModel, currentNode)
		{
			_queryBlock = SemanticModel.GetQueryBlock(CurrentNode);
		}

		public override bool CanExecute(object parameter)
		{
			if (!CurrentNode.Id.In(Terminals.ColumnAlias, Terminals.ObjectAlias))
				return false;

			return _queryBlock != null;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			switch (CurrentNode.Id)
			{
				case Terminals.ObjectAlias:
					AddObjectAliasNodesToRemove(segmentsToReplace);
					break;
				case Terminals.ColumnAlias:
					break;
			}
		}

		public override string Title
		{
			get { return String.Empty; }
		}

		private void AddObjectAliasNodesToRemove(ICollection<TextSegment> segmentsToReplace)
		{
			var objectReference = _queryBlock.ObjectReferences.Single(o => o.AliasNode == CurrentNode);
			if (objectReference.Type == TableReferenceType.InlineView)
				return;

			foreach (var columnReference in _queryBlock.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == objectReference &&
				            c.ObjectNode != null))
			{
				segmentsToReplace.Add(new TextSegment
				                      {
										  IndextStart = columnReference.ObjectNode.SourcePosition.IndexStart,
										  Length = columnReference.ObjectNode.SourcePosition.Length,
										  Text = objectReference.ObjectNode.Token.Value
				                      });
			}

			segmentsToReplace.Add(new TextSegment
			{
				IndextStart = CurrentNode.SourcePosition.IndexStart,
				Length = CurrentNode.SourcePosition.Length,
				Text = String.Empty
			});
		}
	}
}
