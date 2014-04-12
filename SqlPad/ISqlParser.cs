namespace SqlPad
{
	public interface ISqlParser
	{
		StatementCollection Parse(string sqlText);
	}
}