using System.Collections.Generic;

namespace SqlPad
{
	public interface IStatementFormatter
	{
		ICollection<TextSegment> FormatStatement(StatementCollection statements, int selectionStart, int selectionLength);
	}

	public class SqlFormatterOptions
	{
		
	}
}