namespace SqlPad.Commands
{
	public interface IAddMissingAliasesCommand
	{
		string Execute(string statementText, int offset);
	}
}