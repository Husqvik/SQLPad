namespace SqlPad
{
	public interface ISqlParser
	{
		StatementCollection Parse(string sqlText);

		bool IsKeyword(string value);
	}
}