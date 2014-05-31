using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class AddAliasCommand : OracleConfigurableCommandBase
	{
		public AddAliasCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode, ICommandSettingsProvider settingsProvider = null)
			: base(semanticModel, currentNode, settingsProvider)
		{
		}

		public override bool CanExecute(object parameter)
		{
			if (CurrentNode.Id != Terminals.ObjectIdentifier)
				return false;

			var tables = SemanticModel.GetQueryBlock(CurrentNode).ObjectReferences.Where(t => t.ObjectNode == CurrentNode).ToArray();
			return tables.Length == 1 && tables[0].AliasNode == null;
		}

		public override string Title
		{
			get { return "Add Alias"; }
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			switch (CurrentNode.Id)
			{
				case Terminals.ObjectIdentifier:
					SettingsModel.Title = Title;
					SettingsModel.Heading = SettingsModel.Title;
					SettingsModel.Description = String.Format("Enter an alias for the object '{0}'", CurrentNode.Token.Value);
					break;
			}

			if (!SettingsProvider.GetSettings())
				return;

			var alias = SettingsProvider.Settings.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentNode);

			var table = queryBlock.ObjectReferences.Single(t => t.ObjectNode == CurrentNode);

			var prefixedColumnReferences = queryBlock.AllColumnReferences
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
									  IndextStart = CurrentNode.SourcePosition.IndexEnd + 1,
									  Length = 0,
									  Text = " " + alias
			                      });
		}
	}
}
