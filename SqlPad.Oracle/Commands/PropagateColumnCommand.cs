using System;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

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

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null || !CurrentNode.IsWithinSelectClause())
			{
				return false;
			}

			_selectListColumn = CurrentQueryBlock.Columns.SingleOrDefault(c => c.HasExplicitDefinition && CurrentNode.HasAncestor(c.RootNode));
			return _selectListColumn != null && !_selectListColumn.IsAsterisk && !_selectListColumn.IsReferenced && _selectListColumn.Owner != SemanticModel.MainQueryBlock;
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
			foreach (var parentDataObjectReference in AliasCommandHelper.GetParentObjectReferences(queryBlock))
			{
				var parentQueryBlock = parentDataObjectReference.Owner;
				if (parentQueryBlock.ModelReference != null && parentQueryBlock.ModelReference.MeasureExpressionList != null && parentQueryBlock.ModelReference.MeasureExpressionList.LastTerminalNode != null)
				{
					ExecutionContext.SegmentsToReplace.Add(
						new TextSegment
						{
							IndextStart = parentQueryBlock.ModelReference.MeasureExpressionList.LastTerminalNode.Id == Terminals.RightParenthesis
								? parentQueryBlock.ModelReference.MeasureExpressionList.LastTerminalNode.SourcePosition.IndexStart
								: parentQueryBlock.ModelReference.MeasureExpressionList.LastTerminalNode.SourcePosition.IndexEnd + 1,
							Text = ", " + columnName
						});
				}

				var isNotReferencedByAsterisk = parentQueryBlock.AsteriskColumns.Count == 0;
				if (isNotReferencedByAsterisk)
				{
					var lastColumn = parentQueryBlock.Columns.LastOrDefault(c => c.HasExplicitDefinition);
					if (lastColumn != null)
					{
						ExecutionContext.SegmentsToReplace.Add(
							new TextSegment
							{
								IndextStart = lastColumn.RootNode.SourcePosition.IndexEnd + 1,
								Text = ", " + columnName
							});
					}
				}

				AddColumnToQueryBlock(parentQueryBlock, columnName);
			}
		}
	}
}
