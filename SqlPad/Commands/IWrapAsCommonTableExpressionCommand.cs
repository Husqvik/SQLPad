namespace SqlPad.Commands
{
	public interface IWrapAsCommonTableExpressionCommand
	{
		string Execute(string statementText, int offset, string queryName);
	}
}