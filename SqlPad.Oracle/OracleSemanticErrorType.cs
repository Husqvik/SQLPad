namespace SqlPad.Oracle
{
	public static class OracleSemanticErrorType
	{
		public const string None = null;
		public const string AmbiguousReference = "Ambiguous reference";
		public const string InvalidParameterCount = "Invalid parameter count";
		public const string MissingParenthesis = "Missing parenthesis";
		public const string NonParenthesisFunction = "Non-parenthesis function";
		public const string InvalidIdentifier = "Invalid identifier";
		public const string AnalyticClauseNotSupported = "Analytic clause not supported";
		public const string ObjectStatusInvalid = "Object is invalid or unusable";
		public const string ObjectCannotBeUsed = "Object cannot be used here";
		public const string InvalidColumnCount = "Invalid column count";
	}

	public static class OracleSuggestionType
	{
		public const string None = null;
		public const string PotentialDatabaseLink = "Reference over database link? Use object qualifier. ";
	}
}