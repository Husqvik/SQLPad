using System.Collections.Generic;

namespace SqlPad
{
	public interface IStatementFormatter
	{
		ICollection<TextSegment> FormatStatement(SqlDocument sqlDocument, int selectionStart, int selectionLength);
	}

	public class SqlFormatterOptions
	{
		
	}
}