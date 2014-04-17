namespace SqlPad.Commands
{
	public interface ICommandFactory
	{
		DisplayCommandBase CreateFindUsagesCommand(string statementText, int currentPosition, IDatabaseModel databaseModel);
	}
}
