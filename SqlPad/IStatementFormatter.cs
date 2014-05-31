using SqlPad.Commands;

namespace SqlPad
{
	public interface IStatementFormatter
	{
		CommandExecutionHandler ExecutionHandler { get; }
	}

	public class SqlFormatterOptions
	{
		
	}
}