using System;
using System.Linq;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	internal class PropagateColumnCommand : OracleCommandBase
	{
		public const string Title = "Propagate";
		private OracleSelectListColumn _selectListColumn;

		private PropagateColumnCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null || !CurrentNode.IsWithinSelectClause())
			{
				return false;
			}

			_selectListColumn = CurrentQueryBlock.Columns.SingleOrDefault(c => c.ExplicitDefinition && CurrentNode.HasAncestor(c.RootNode));
			return _selectListColumn != null && !_selectListColumn.IsAsterisk && !_selectListColumn.IsReferenced;
		}

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;
			settingsModel.ValidationRule = new OracleIdentifierValidationRule();

			settingsModel.Title = "Add Column Alias";
			settingsModel.Description = String.Format("Enter an alias for the expression '{0}'", CurrentNode.Token.Value);

			settingsModel.Heading = settingsModel.Title;

			return settingsModel;
		}

		protected override void Execute()
		{
			string columnAlias;
			if (_selectListColumn.AliasNode == null)
			{
				var settingsModel = ConfigureSettings();

				if (!ExecutionContext.SettingsProvider.GetSettings())
					return;

				columnAlias = settingsModel.Value;

				ExecutionContext.SegmentsToReplace.Add(
					new TextSegment
					{
						IndextStart = _selectListColumn.RootNode.SourcePosition.IndexEnd + 1,
						Text = " " + columnAlias
					});
			}
			else
			{
				columnAlias = _selectListColumn.AliasNode.Token.Value;
			}

			AddColumnToQueryBlock(CurrentQueryBlock, columnAlias);
		}

		private void AddColumnToQueryBlock(OracleQueryBlock queryBlock, string columnName)
		{
			foreach (var parentDataObjectReference in AliasCommandHelper.GetParentObjectReferences(queryBlock).Where(r => r.QueryBlocks.Count == 1))
			{
				var parentQueryBlock = parentDataObjectReference.Owner;
				var lastColumn = parentQueryBlock.Columns.LastOrDefault(c => c.ExplicitDefinition);
				if (lastColumn != null)
				{
					ExecutionContext.SegmentsToReplace.Add(
					new TextSegment
					{
						IndextStart = lastColumn.RootNode.SourcePosition.IndexEnd + 1,
						Text = ", " + columnName
					});
				}

				AddColumnToQueryBlock(parentQueryBlock, columnName);
			}
		}
	}
}
