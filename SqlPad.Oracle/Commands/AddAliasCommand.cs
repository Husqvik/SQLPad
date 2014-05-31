using System;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddAliasCommand : OracleCommandBase
	{
		public const string Title = "Add Alias";

		public static CommandExecutionHandler ExecutionHandler = CreateStandardExecutionHandler<AddAliasCommand>("AddAlias");

		private AddAliasCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			if (CurrentNode.Id != Terminals.ObjectIdentifier)
				return false;

			var tables = SemanticModel.GetQueryBlock(CurrentNode).ObjectReferences.Where(t => t.ObjectNode == CurrentNode).ToArray();
			return tables.Length == 1 && tables[0].AliasNode == null;
		}

		protected override void Execute()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;

			switch (CurrentNode.Id)
			{
				case Terminals.ObjectIdentifier:
					settingsModel.Title = Title;
					settingsModel.Heading = settingsModel.Title;
					settingsModel.Description = String.Format("Enter an alias for the object '{0}'", CurrentNode.Token.Value);
					break;
			}

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var alias = settingsModel.Value;

			var queryBlock = SemanticModel.GetQueryBlock(CurrentNode);

			var table = queryBlock.ObjectReferences.Single(t => t.ObjectNode == CurrentNode);

			var prefixedColumnReferences = queryBlock.AllColumnReferences
				.Where(c => (c.OwnerNode != null || c.ObjectNode != null) && c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == table);
			
			var asteriskColumnReferences = queryBlock.Columns.Where(c => c.IsAsterisk).SelectMany(c => c.ColumnReferences)
				.Where(c => c.ObjectNodeObjectReferences.Count == 1 && c.ObjectNodeObjectReferences.Single() == table);

			foreach (var columnReference in prefixedColumnReferences.Concat(asteriskColumnReferences))
			{
				var firstPrefixNode = columnReference.OwnerNode ?? columnReference.ObjectNode;

				ExecutionContext.SegmentsToReplace.Add(new TextSegment
				                      {
										  IndextStart = firstPrefixNode == null ? columnReference.ColumnNode.SourcePosition.IndexStart - 1 : firstPrefixNode.SourcePosition.IndexStart,
										  Length = columnReference.ColumnNode.SourcePosition.IndexStart - firstPrefixNode.SourcePosition.IndexStart,
										  Text = alias + "."
				                      });
			}

			ExecutionContext.SegmentsToReplace.Add(new TextSegment
			                      {
									  IndextStart = CurrentNode.SourcePosition.IndexEnd + 1,
									  Length = 0,
									  Text = " " + alias
			                      });
		}
	}
}
