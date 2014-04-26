namespace SqlPad
{
	public interface ISqlParser
	{
		StatementCollection Parse(string sqlText);

		bool IsKeyword(string value);

		bool IsLiteral(string terminalId);

		bool IsAlias(string terminalId);
	}
}