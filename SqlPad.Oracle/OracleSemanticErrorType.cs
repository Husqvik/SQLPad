namespace SqlPad.Oracle
{
	public static class OracleSemanticErrorType
	{
		public const string None = null;
		public const string ClauseNotAllowed = "Clause not allowed";
		public const string ConnectByClauseRequired = "CONNECT BY clause required in this query block";
		public const string NoCycleKeywordRequiredWithConnectByIsCyclePseudocolumn = "NOCYCLE keyword is required with CONNECT_BY_ISCYCLE pseudocolumn";
		public const string InvalidDateLiteral = "Invalid date literal";
		public const string InvalidTimestampLiteral = "Invalid timestamp literal";
		public const string InvalidIntervalLiteral = "Invalid interval";
		public const string AmbiguousReference = "Ambiguous reference";
		public const string InvalidParameterCount = "Invalid parameter count";
		public const string MissingParenthesis = "Missing parenthesis";
		public const string NonParenthesisFunction = "Non-parenthesis function";
		public const string InvalidIdentifier = "Invalid identifier";
		public const string AnalyticClauseNotSupported = "Analytic clause not supported";
		public const string OrderByNotAllowedHere = "ORDER BY not allowed here";
		public const string ObjectStatusInvalid = "Object is invalid or unusable";
		public const string SequenceNumberNotAllowedHere = "Sequence number not allowed here";
		public const string InvalidColumnCount = "Invalid column count";
		public const string FunctionReturningRowSetRequired = "Function must return a row set";
		public const string NamedParameterNotAllowed = "Named parameter not allowed";
		public const string MultipleInstancesOfNamedArgumentInList = "Multiple instances of named argument in list";
		public const string PositionalParameterNotAllowed = "A positional parameter association may not follow a named association";
		public const string InvalidColumnIndex = "Invalid column index";
		public const string GroupFunctionNotAllowed = "Group function is not allowed here";
		public const string MissingWithClauseColumnAliasList = "WITH clause element did not have a column alias list";
		public const string InvalidCycleMarkValue = "Cycle mark value and non-cycle mark value must be one byte character string values";
		public const string IncorrectUseOfLnNvlOperator = "Incorrect use of LNNVL operator";
		public const string ExpectAggregateFunctionInsidePivotOperation = "Expect aggregate function inside pivot operation";
		public const string CannotAccessRowsFromNonNestedTableItem = "Cannot access rows from a non-nested table item";
		public const string NotEnoughArgumentsForFunction = "Not enough arguments for function";
		public const string NumericPrecisionSpecifierOutOfRange = "Numeric precision specifier is out of range (1 to 38)";
		public const string NumericScaleSpecifierOutOfRange = "Numeric scale specifier is out of range (-84 to 127)";
		public const string FloatingPointPrecisionOutOfRange = "Floating point precision is out of range (1 to 126)";
		public const string SpecifiedLengthTooLongForDatatype = "Specified length too long for its datatype";
		public const string DatetimeOrIntervalPrecisionIsOutOfRange = "Datetime/interval precision is out of range";
		public const string ZeroLengthColumnsNotAllowed = "Zero-length columns are not allowed";
		public const string UnsupportedInConnectedDatabaseVersion = "Unsupported in connected database version";
		public const string ExpressionMustHaveSameDatatypeAsCorrespondingExpression = "Expression must have same datatype as corresponding expression";
		public const string PartitionedTableOnBothSidesOfPartitionedOuterJoinNotSupported = "Partitioned table on both sides of PARTITIONED OUTER JOIN is not supported";
		public const string LeadingPrecisionOfTheIntervalIsTooSmall = "The leading precision of the interval is too small";
		public const string SelectIntoClauseAllowedOnlyInMainQueryBlockWithinPlSqlScope = "SELECT INTO clause is allowed only in main query block within PL/SQL scope";
		public const string WindowFunctionsNotAllowedHere = "Window functions are not allowed here";
		public const string GroupFunctionNestedTooDeeply = "Group function is nested too deeply";
		public const string NotSingleGroupGroupFunction = "Not a single-group group function";
		public const string PlSqlCompilationParameterAllowedOnlyWithinPlSqlScope = "PL/SQL compilation parameter is allowed only within PL/SQL scope";
		public const string CurrentOfConditionAllowedOnlyWithinPlSqlScope = "Current of <cursor> condition is allowed only within PL/SQL scope";
		public const string InsertOperationDisallowedOnVirtualColumns = "INSERT operation disallowed on virtual columns";
		public const string DuplicateNameFoundInColumnAliasListForWithClause = "Duplicate name found in column alias list for WITH clause";
		public const string OldStyleOuterJoinCannotBeUsedWithAnsiJoins = "Old style outer join cannot be used with ANSI joins";
		public const string InvalidDataTypeForJsonTableColumn = "Invalid data type for JSON_TABLE column";
		public const string ForUpdateNotAllowed = "FOR UPDATE of this query expression is not allowed";
		public const string CannotSelectForUpdateFromViewWithDistinctOrGroupBy = "Cannot select FOR UPDATE from view with DISTINCT, GROUP BY, etc. ";

		public static class PlSql
		{
			public const string NoChoicesMayAppearWithChoiceOthersInExceptionHandler = "No choices may appear with choice OTHERS in an exception handler";
			public const string UnsupportedTableIndexType = "unsupported table index type";
		}
	}

	public static class OracleSemanticErrorTooltipText
	{
		public const string InvalidDateLiteral = "ANSI format of DATE literal must be [-]YYYY-MM-DD";
		public const string InvalidTimestampLiteral = "ANSI format of TIMESTAMP literal must be [-]YYYY-MM-DD HH24:MI:SS[.1-9 digits] [time zone definition]";
		public const string InvalidIntervalYearToMonthLiteral = "INTERVAL DAY TO SECOND literal must be [-][1-9 digits][-(0-11)] and match target elements";
		public const string InvalidIntervalDayToSecondLiteral = "INTERVAL YEAR TO MONTH literal must be [-][1-9 digits][0-23][:0-59][:0-59][.1-9 digits] and match target elements";
		public const string FunctionOrPseudocolumnMayBeUsedInsideSqlStatementOnly = "Function or pseudo-column may be used inside a SQL statement only";
	}

	public static class OracleSuggestionType
	{
		public const string None = null;
		public const string PotentialDatabaseLink = "Reference over database link? Use object qualifier. ";
		public const string UseExplicitColumnList = "Use explicit column list";
		public const string ExpressionIsAlwaysTrue = "Expression is always true";
		public const string ExpressionIsAlwaysFalse = "Expression is always false";
		public const string CorrelatedSubqueryColumnNotQualified = "Use qualifier when referencing correlated sub-query column";
	}
}