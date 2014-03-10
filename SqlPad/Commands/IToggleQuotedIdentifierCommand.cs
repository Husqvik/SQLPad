namespace SqlPad.Commands
{
	public interface IToggleQuotedIdentifierCommand
	{
		string Execute(string statementText, int offset);
	}
}