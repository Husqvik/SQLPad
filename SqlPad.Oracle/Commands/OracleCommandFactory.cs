using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	public class OracleCommandFactory : ICommandFactory
	{
		public DisplayCommandBase CreateFindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel)
		{
			return new FindUsagesCommand(statementText, currentPosition, databaseModel);
		}
	}
}