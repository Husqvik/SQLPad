using System;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.Commands
{
	internal class AddCreateTableAsCommand : OracleCommandBase
	{
		public const string Title = "Add CREATE TABLE AS";
		public const string CreateSeparateStatement = "CreateSeparateStatement";

		private CommandSettingsModel _settingsModel;

		private AddCreateTableAsCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction { get; } = n => String.Equals(n.Id, Terminals.Select);

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null)
			{
				return false;
			}

			return CurrentQueryBlock.Columns.Any(c => !c.IsAsterisk);
		}

		private void ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			_settingsModel = ExecutionContext.SettingsProvider.Settings;
			_settingsModel.ValidationRule = new OracleIdentifierValidationRule();

			_settingsModel.Title = "Create table script";
			_settingsModel.Description = "Enter table name: ";

			_settingsModel.Heading = _settingsModel.Title;

			var createTableAsAllowed = CurrentQueryBlock == SemanticModel.MainQueryBlock;
			_settingsModel.AddBooleanOption(
				new BooleanOption
				{
					OptionIdentifier = CreateSeparateStatement,
					DescriptionContent = "Create separate statement",
					Value = !createTableAsAllowed,
					IsEnabled = createTableAsAllowed
				});
		}

		protected override void Execute()
		{
			ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var textSegment = _settingsModel.BooleanOptions[CreateSeparateStatement].Value
				? BuildCreateTableCommand()
				: AddCreateTableAsPrefix();

			ExecutionContext.SegmentsToReplace.Add(textSegment);
		}

		private TextSegment AddCreateTableAsPrefix()
		{
			var builder = new StringBuilder();
			BuildCreateTableColumnDefinitions(builder, false);

			builder.AppendLine();

			var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions;

			builder.AppendLine(OracleStatementFormatter.FormatTerminalValue(TerminalValues.As, formatOption.Keyword));

			return
				new TextSegment
				{
					IndextStart = CurrentNode.Statement.SourcePosition.IndexStart,
					Text = builder.ToString()
				};
		}

		private TextSegment BuildCreateTableCommand()
		{
			var builder = new StringBuilder();
			if (CurrentNode.Statement.TerminatorNode == null)
			{
				builder.Append(";");
			}

			builder.AppendLine();
			builder.AppendLine();
			
			BuildCreateTableColumnDefinitions(builder, true);
			
			builder.AppendLine(";");
			
			return
				new TextSegment
				{
					IndextStart = CurrentNode.Statement.SourcePosition.IndexEnd + 1,
					Text = builder.ToString()
				};
		}

		private void BuildCreateTableColumnDefinitions(StringBuilder builder, bool includeDataTypes)
		{
			var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions;

			builder.Append(OracleStatementFormatter.FormatTerminalValue("CREATE TABLE ", formatOption.ReservedWord));
			builder.Append(_settingsModel.Value);
			builder.AppendLine(" (");

			var columnPosition = 0;
			foreach (var column in CurrentQueryBlock.Columns.Where(c => !c.IsAsterisk))
			{
				if (columnPosition > 0)
				{
					builder.Append(",");
					builder.AppendLine();
				}

				columnPosition++;
				builder.Append("\t");
				var columnName = column.NormalizedName.ToSimpleIdentifier();
				if (String.IsNullOrEmpty(columnName))
				{
					columnName = $"COLUMN{columnPosition}";
				}

				builder.Append(OracleStatementFormatter.FormatTerminalValue(columnName, formatOption.Identifier));

				if (!includeDataTypes)
				{
					continue;
				}
				
				builder.Append(" ");

				var columnType = String.IsNullOrEmpty(column.ColumnDescription?.FullTypeName)
					? "VARCHAR2(255)"
					: column.ColumnDescription.FullTypeName;

				builder.Append(OracleStatementFormatter.FormatTerminalValue(columnType, formatOption.ReservedWord));
			}

			builder.AppendLine();
			builder.Append(")");
		}
	}
}
