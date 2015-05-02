using SqlPad.Commands;

namespace SqlPad.Oracle
{
	public class OracleHelpProvider : IHelpProvider
	{
		public void ShowHelp(CommandExecutionContext executionContext)
		{
			var statement = executionContext.DocumentRepository.Statements.GetStatementAtPosition(executionContext.CaretOffset);
			if (statement == null)
			{
				return;
			}

			var semanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[statement].SemanticModel;
			var terminal = statement.GetTerminalAtPosition(executionContext.CaretOffset);
			if (terminal == null)
			{
				return;
			}

			var programReference = semanticModel.GetProgramReference(terminal);
			if (programReference == null || programReference.Metadata == null || programReference.Metadata.Type == ProgramType.StatementFunction)
			{
				return;
			}

			OracleToolTipProvider.ShowSqlFunctionDocumentation(programReference.Metadata.Identifier);
		}
	}
}