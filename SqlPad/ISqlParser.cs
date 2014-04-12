using System.Collections.Generic;

namespace SqlPad
{
	public interface ISqlParser
	{
		ICollection<StatementBase> Parse(string sqlText);
	}
}