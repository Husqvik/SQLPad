namespace SqlPad
{
	public interface IStatementFormatter
	{
		string FormatStatement(StatementCollection statements, int selectionStart, int selectionLength);
	}

	public class SqlFormatterOptions
	{
		
	}
}