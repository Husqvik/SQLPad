using System;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class GenerateCreateTableScriptFromQueryCommand : OracleCommandBase
	{
		public const string Title = "Generate CREATE TABLE script";
		private CommandSettingsModel _settingsModel;
		private bool _useDefaultSettings;

		private GenerateCreateTableScriptFromQueryCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction
		{
			get { return n => n.Id == Terminals.Select; }
		}

		protected override bool CanExecute()
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
			_useDefaultSettings = _settingsModel.UseDefaultSettings == null || _settingsModel.UseDefaultSettings();
			_settingsModel.ValidationRule = new OracleIdentifierValidationRule();

			_settingsModel.Title = "Create table script";
			_settingsModel.Description = "Enter table name: ";

			_settingsModel.Heading = _settingsModel.Title;
		}

		protected override void Execute()
		{
			ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var textSegment = _useDefaultSettings
				? AddCreateTableAsPrefix()
				: BuildCreateTableCommand();

			ExecutionContext.SegmentsToReplace.Add(textSegment);
		}

		private TextSegment AddCreateTableAsPrefix()
		{
			var builder = new StringBuilder();
			BuildCreateTableColumnDefinitions(builder, false);

			builder.AppendLine();
			builder.AppendLine("AS");

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
			builder.Append("CREATE TABLE ");
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
				builder.Append(String.IsNullOrEmpty(columnName) ? String.Format("COLUMN{0}", columnPosition) : columnName);

				if (!includeDataTypes)
				{
					continue;
				}
				
				builder.Append(" ");

				var columnType = column.ColumnDescription == null || column.ColumnDescription == null || String.IsNullOrEmpty(column.ColumnDescription.FullTypeName)
					? "VARCHAR2(255)"
					: column.ColumnDescription.FullTypeName;
				builder.Append(columnType);
			}

			builder.AppendLine();
			builder.Append(")");
		}
	}
}
