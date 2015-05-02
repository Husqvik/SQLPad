namespace SqlPad.Oracle
{
	public enum LiteralType
	{
		Date,
		Timestamp
	}

	public struct OracleLiteral
	{
		public LiteralType Type;
		
		public StatementGrammarNode Terminal;

		public bool IsMultibyte
		{
			get
			{
				if (Terminal == null || Terminal.Id != OracleGrammarDescription.Terminals.StringLiteral)
				{
					return false;
				}

				return Terminal.Token.Value[0] == 'n' || Terminal.Token.Value[0] == 'N';
			}
		}
	}
}