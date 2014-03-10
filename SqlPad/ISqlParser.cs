using System.Collections.Generic;

namespace SqlPad
{
	public interface ISqlParser
	{
		ICollection<IStatement> Parse(string sqlText);
	}
}