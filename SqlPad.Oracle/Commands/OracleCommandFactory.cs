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
			CommandHandlerCollection.Add(SafeDeleteCommand.ExecutionHandler);
		}

		public ICollection<CommandExecutionHandler> CommandHandlers
		{
			get { return CommandHandlerCollection; }
		}

		public DisplayCommandBase CreateFindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel)
		{
			return new FindUsagesCommand(statementText, currentPosition, databaseModel);
		}
	}
}