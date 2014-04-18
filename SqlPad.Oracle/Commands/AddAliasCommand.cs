using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class AddAliasCommand : OracleConfigurableCommandBase
	{
		public AddAliasCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal, ICommandSettingsProvider settingsProvider = null)
			: base(semanticModel, currentTerminal, settingsProvider)
		{
		}

		public override bool CanExecute(object parameter)
		{
			if (CurrentTerminal.Id != Terminals.ObjectIdentifier)
				return false;

			var tables = SemanticModel.GetQueryBlock(CurrentTerminal).ObjectReferences.Where(t => t.ObjectNode == CurrentTerminal).ToArray();
			return tables.Length == 1 && tables[0].AliasNode == null;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			if (!SettingsProvider.GetSettings())
				return;

			var alias = SettingsProvider.Settings.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentTerminal);

			var table = queryBlock.ObjectReferences.Single(t => t.ObjectNode == CurrentTerminal);

			var prefixedColumnReferences = queryBlock.Columns.SelectMany(c => c.ColumnReferences)
				.Concat(queryBlock.ColumnReferences)
				.Where(c => (c.OwnerNode != null || c.ObjectNode != null) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == table);
			
			var asteriskColumnReferences = queryBlock.Columns.Where(c => c.IsAsterisk).SelectMany(c => c.ColumnReferences)
				.Where(c => c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.Single() == table);

			foreach (var columnReference in prefixedColumnReferences.Concat(asteriskColumnReferences))
			{
				var firstPrefixNode = columnReference.OwnerNode ?? columnReference.ObjectNode;

				segmentsToReplace.Add(new TextSegment
				                      {
										  IndextStart = firstPrefixNode == null ? columnReference.ColumnNode.SourcePosition.IndexStart - 1 : firstPrefixNode.SourcePosition.IndexStart,
										  Length = columnReference.ColumnNode.SourcePosition.IndexStart - firstPrefixNode.SourcePosition.IndexStart,
										  Text = alias + "."
				                      });
			}

			segmentsToReplace.Add(new TextSegment
			                      {
									  IndextStart = CurrentTerminal.SourcePosition.IndexEnd + 1,
									  Length = 0,
									  Text = " " + alias
			                      });
		}
	}
}
