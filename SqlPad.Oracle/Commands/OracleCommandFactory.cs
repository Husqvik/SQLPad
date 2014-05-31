using System.Collections.Generic;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	public class OracleCommandFactory : ICommandFactory
	{
		private static readonly List<CommandExecutionHandler> CommandHandlerCollection = new List<CommandExecutionHandler>();

		static OracleCommandFactory()
		{
			CommandHandlerCollection.Add(MakeUpperCaseCommand.ExecutionHandler);
		}

		public ICollection<CommandExecutionHandler> CommandHandlers
		{
			get { return CommandHandlerCollection; }
		}

		public DisplayCommandBase CreateFindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel)
		{
			return new FindUsagesCommand(statementText, currentPosition, databaseModel);
		}

		public EditCommandBase CreateSafeDeleteCommand(StatementCollection statements, int currentPosition, IDatabaseModel databaseModel)
		{
			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)statements.GetStatementAtPosition(currentPosition), (OracleDatabaseModel)databaseModel);
			return new SafeDeleteCommand(semanticModel, statements.GetTerminalAtPosition(currentPosition, n => n.Id.IsAlias()));
		}
	}
}