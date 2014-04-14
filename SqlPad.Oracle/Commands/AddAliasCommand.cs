using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class AddAliasCommand : OracleCommandBase
	{
		public AddAliasCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
			: base(semanticModel, currentTerminal)
		{

		}

		public override bool CanExecute(object parameter)
		{
			if (CurrentTerminal.Id != Terminals.ObjectIdentifier)
				return false;

			var tables = SemanticModel.GetQueryBlock(CurrentTerminal).TableReferences.Where(t => t.TableNode == CurrentTerminal).ToArray();
			return tables.Length == 1 && tables[0].AliasNode == null;
		}

		protected override void ExecuteInternal(ICollection<TextSegment> segmentsToReplace)
		{
			var model = new EditDialogViewModel { Value = "Enter value", ValidationRule = new OracleIdentifierValidationRule() };
			var editDialog = new EditDialog(model);
			var result = editDialog.ShowDialog();
			if (!result.HasValue || !result.Value)
				return;

			var alias = model.Value;

			var root = CurrentTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.QueryBlock);
			var queryBlock = SemanticModel.QueryBlocks.Single(qb => qb.RootNode == root);

			var table = queryBlock.TableReferences.Single(t => t.TableNode == CurrentTerminal);

			var prefixedColumnReferences = queryBlock.Columns.SelectMany(c => c.ColumnReferences)
				.Where(c => (c.OwnerNode != null || c.ObjectNode != null) && c.ColumnNodeReferences.Count == 1 && c.ColumnNodeReferences.Single() == table);
			var asteriskColumnReferences = queryBlock.Columns.Where(c => c.IsAsterisk).SelectMany(c => c.ColumnReferences)
				.Where(c => c.ObjectNodeReferences.Count == 1 && c.ObjectNodeReferences.Single() == table);

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
