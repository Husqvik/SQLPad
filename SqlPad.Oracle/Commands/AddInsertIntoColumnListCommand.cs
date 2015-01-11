using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using SqlPad.Commands;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddInsertIntoColumnListCommand : OracleCommandBase
	{
		public const string Title = "Add Column List";
		private CommandSettingsModel _settingsModel;
		private StatementGrammarNode _columnList;
		private HashSet<string> _existingColumns;
		private OracleInsertTarget _insertTarget;

		private AddInsertIntoColumnListCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			return CurrentNode != null &&
			       CurrentNode.Statement.RootNode.FirstTerminalNode.Id == Terminals.Insert &&
			       Initialize() &&
			       ExistNamedColumns();
		}

		private bool Initialize()
		{
			if (CurrentNode.Id == Terminals.Into)
			{
				_insertTarget = SemanticModel.InsertTargets.FirstOrDefault(t => CurrentNode.HasAncestor(t.RootNode));
			}
			else
			{
				var isSingleTableInsert = CurrentNode.Id == Terminals.Insert &&
				                          CurrentNode.ParentNode.GetDescendantByPath(NonTerminals.SingleTableInsertOrMultiTableInsert, NonTerminals.SingleTableInsert) != null;
				if (isSingleTableInsert && SemanticModel.InsertTargets.Count == 1)
				{
					_insertTarget = SemanticModel.InsertTargets.First();
				}
			}

			return _insertTarget != null && _insertTarget.DataObjectReference != null;
		}

		private bool ExistNamedColumns()
		{
			var expandedColumns = FillColumnNames();
			return expandedColumns.Count > 0;
		}

		private void ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			_settingsModel = ExecutionContext.SettingsProvider.Settings;

			_settingsModel.TextInputVisibility = Visibility.Collapsed;
			_settingsModel.BooleanOptionsVisibility = Visibility.Visible;

			var columnNames = FillColumnNames();

			foreach (var column in columnNames)
			{
				var isSelected = _settingsModel.UseDefaultSettings == null || !_settingsModel.UseDefaultSettings()
					? _existingColumns.Contains(column.ToQuotedIdentifier())
					: _settingsModel.UseDefaultSettings();

				_settingsModel.AddBooleanOption(
					new BooleanOption
					{
						OptionIdentifier = column,
						Description = column,
						Value = isSelected,
						Tag = column
					});
			}

			_settingsModel.Title = "Add/Modify Columns";
			_settingsModel.Heading = _settingsModel.Title;
		}

		protected override void Execute()
		{
			_columnList = CurrentNode.ParentNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.ParenthesisEnclosedIdentifierList);
			var existingColumns = _columnList == null
				? Enumerable.Empty<string>()
				: _columnList.GetDescendants(Terminals.Identifier).Select(n => n.Token.Value.ToQuotedIdentifier());

			_existingColumns = new HashSet<string>(existingColumns);

			ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var segmentToReplace = GetSegmentToReplace();
			if (!segmentToReplace.Equals(TextSegment.Empty))
			{
				ExecutionContext.SegmentsToReplace.Add(segmentToReplace);
			}
		}

		private TextSegment GetSegmentToReplace()
		{
			var columnNames = _settingsModel.BooleanOptions.Values
				.Where(v => v.Value)
				.Select(v => v.OptionIdentifier)
				.ToArray();

			if (columnNames.Length == 0)
				return TextSegment.Empty;

			var columnListText = String.Format("{0}{1}{2}", "(", String.Join(", ", columnNames), ")");
			if (_columnList == null)
			{
				return new TextSegment
				{
					IndextStart = _insertTarget.TargetNode.SourcePosition.IndexEnd + 1,
					Length = 0,
					Text = String.Format(" {0}", columnListText)
				};
			}

			return new TextSegment
				{
					IndextStart = _columnList.SourcePosition.IndexStart,
					Length = _columnList.SourcePosition.Length,
					Text = columnListText
				};
		}

		private IReadOnlyList<string> FillColumnNames()
		{
			return _insertTarget.DataObjectReference.Columns
				.Where(c => !String.IsNullOrEmpty(c.Name))
				.Select(c => c.Name.ToSimpleIdentifier())
				.ToArray();
		}
	}
}
