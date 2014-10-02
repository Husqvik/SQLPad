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

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;
			settingsModel.ValidationRule = new OracleIdentifierValidationRule();

			settingsModel.Title = "Create table script";
			settingsModel.Description = "Enter table name: ";

			settingsModel.Heading = settingsModel.Title;

			return settingsModel;
		}

		protected override void Execute()
		{
			var settingsModel = ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var builder = new StringBuilder();
			if (CurrentNode.Statement.TerminatorNode == null)
			{
				builder.Append(";");	
			}

			builder.AppendLine();
			builder.AppendLine();
			builder.Append("CREATE TABLE ");
			builder.Append(settingsModel.Value);
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
				builder.Append(" ");
				
				var columnType = column.ColumnDescription == null || column.ColumnDescription == null || String.IsNullOrEmpty(column.ColumnDescription.FullTypeName)
					? "VARCHAR2(255)"
					: column.ColumnDescription.FullTypeName;
				builder.Append(columnType);
			}
			
			builder.AppendLine();
			builder.AppendLine(");");

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = CurrentNode.Statement.SourcePosition.IndexEnd + 1,
					Text = builder.ToString()
				});
		}
	}
}
