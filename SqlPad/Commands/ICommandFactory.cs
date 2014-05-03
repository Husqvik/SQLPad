namespace SqlPad.Commands
{
	public interface ICommandFactory
	{
		DisplayCommandBase CreateFindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel);

		EditCommandBase CreateSafeDeleteCommand(StatementCollection statements, int currentPosition, IDatabaseModel databaseModel);
	}
}
