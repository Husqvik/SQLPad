namespace SqlPad.Oracle.SemanticModel
{
	public enum LiteralType
	{
		Unknown,
		Date,
		IntervalYearToMonth,
		IntervalDayToSecond,
		Timestamp,
		Number,
		SinglePrecision,
		DoublePrecision,
		Char
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