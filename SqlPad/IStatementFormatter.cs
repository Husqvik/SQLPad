using SqlPad.Commands;

namespace SqlPad
{
	public interface IStatementFormatter
	{
		CommandExecutionHandler ExecutionHandler { get; }
		CommandExecutionHandler SingleLineExecutionHandler { get; }
	}

	public class SqlFormatterOptions
	{
		
	}
}