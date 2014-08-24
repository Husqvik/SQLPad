using System.Collections.Generic;

namespace SqlPad.Oracle
{
	/// <summary>
	/// This class provides token constants for Oracle SQL grammar.
	/// </summary>
	public static class OracleGrammarDescription
	{
		/// <summary>
		/// This class provides the non-terminal constants.
		/// </summary>
		public static class NonTerminals
		{
			public const string AggregateFunction = "AggregateFunction";
			public const string AggregateFunctionCall = "AggregateFunctionCall";
			public const string AggregateFunctionParameter = "AggregateFunctionParameter";
			public const string AggregationFunctionParameters = "AggregationFunctionParameters";
			public const string AliasedExpression = "AliasedExpression";
			public const string AliasedExpressionListOrAliasedGroupingExpressionList = "AliasedExpressionListOrAliasedGroupingExpressionList";
			public const string AliasedExpressionListOrAliasedGroupingExpressionListChained = "AliasedExpressionListOrAliasedGroupingExpressionListChained";
			public const string AliasedExpressionOrAllTableColumns = "AliasedExpressionOrAllTableColumns";
			public const string AllOrDistinct = "AllOrDistinct";
			public const string AnalyticClause = "AnalyticClause";
			public const string AnalyticFunction = "AnalyticFunction";
			public const string AnalyticFunctionCall = "AnalyticFunctionCall";
			public const string AnalyticOrKeepClause = "AnalyticOrKeepClause";
			public const string AnyChainedList = "AnyChainedList";
			public const string AsAliasOrEvaluatedNameExpression = "AsAliasOrEvaluatedNameExpression";
			public const string AsXmlAlias = "AsXmlAlias";
			public const string BetweenFollowingClause = "BetweenFollowingClause";
			public const string BetweenPrecedingAndFollowingClause = "BetweenPrecedingAndFollowingClause";
			public const string BetweenPrecedingClause = "BetweenPrecedingClause";
			public const string BetweenSystemChangeNumberOrTimestamp = "BetweenSystemChangeNumberOrTimestamp";
			public const string BetweenSystemChangeNumberOrTimestampOrPeriodForBetween = "BetweenSystemChangeNumberOrTimestampOrPeriodForBetween";
			public const string BindVariableExpression = "BindVariableExpression";
			public const string BindVariableExpressionCommaChainedList = "BindVariableExpressionCommaChainedList";
			public const string ByteOrChar = "ByteOrChar";
			public const string ByValue = "ByValue";
			public const string CascadeConstraints = "CascadeConstraints";
			public const string CaseExpression = "CaseExpression";
			public const string CaseExpressionElseBranch = "CaseExpressionElseBranch";
			public const string ChainedCondition = "ChainedCondition";
			public const string ColumnAsAlias = "ColumnAsAlias";
			public const string ColumnIdentifierChainedList = "ColumnIdentifierChainedList";
			public const string ColumnIdentifierList = "ColumnIdentifierList";
			public const string ColumnReference = "ColumnReference";
			public const string CommaChainedExpressionWithAlias = "CommaChainedExpressionWithAlias";
			public const string CommaPrefixedXmlAttributesClause = "CommaPrefixedXmlAttributesClause";
			public const string CommitComment = "CommitComment";
			public const string CommitCommentOrWriteOrForce = "CommitCommentOrWriteOrForce";
			public const string CommitSetScn = "CommitSetScn";
			public const string CommitStatement = "CommitStatement";
			public const string CommitWriteClause = "CommitWriteClause";
			public const string ConcatenatedSubquery = "ConcatenatedSubquery";
			public const string Condition = "Condition";
			public const string ConditionalInsertClause = "ConditionalInsertClause";
			public const string ConditionalInsertConditionBranch = "ConditionalInsertConditionBranch";
			public const string ConditionalInsertElseBranch = "ConditionalInsertElseBranch";
			public const string ConstraintName = "ConstraintName";
			public const string CountAsteriskParameter = "CountAsteriskParameter";
			public const string CrossOrOuter = "CrossOrOuter";
			public const string CrossOrOuterApplyClause = "CrossOrOuterApplyClause";
			public const string DatabaseLink = "DatabaseLink";
			public const string DataType = "DataType";
			public const string DataTypeDefinition = "DataTypeDefinition";
			public const string DataTypeIntervalPrecisionAndScale = "DataTypeIntervalPrecisionAndScale";
			public const string DataTypeNumericPrecisionAndScale = "DataTypeNumericPrecisionAndScale";
			public const string DataTypeOrXmlType = "DataTypeOrXmlType";
			public const string DataTypeSimplePrecision = "DataTypeSimplePrecision";
			public const string DataTypeVarcharSimplePrecision = "DataTypeVarcharSimplePrecision";
			public const string DayOrHourOrMinute = "DayOrHourOrMinute";
			public const string DayOrHourOrMinuteOrSecondWithLeadingPrecision = "DayOrHourOrMinuteOrSecondWithLeadingPrecision";
			public const string DayOrHourOrMinuteOrSecondWithPrecision = "DayOrHourOrMinuteOrSecondWithPrecision";
			public const string DefaultNamespace = "DefaultNamespace";
			public const string DeleteStatement = "DeleteStatement";
			public const string DepthOrBreadth = "DepthOrBreadth";
			public const string DistinctModifier = "DistinctModifier";
			public const string DmlTableExpressionClause = "DmlTableExpressionClause";
			public const string DocumentOrContent = "DocumentOrContent";
			public const string DropContext = "DropContext";
			public const string DropIndex = "DropIndex";
			public const string DropMaterializedView = "DropMaterializedView";
			public const string DropObjectClause = "DropObjectClause";
			public const string DropOther = "DropOther";
			public const string DropPackage = "DropPackage";
			public const string DropStatement = "DropStatement";
			public const string DropTable = "DropTable";
			public const string DropView = "DropView";
			public const string EntityEscapingOrNoEntityEscaping = "EntityEscapingOrNoEntityEscaping";
			public const string ErrorLoggingClause = "ErrorLoggingClause";
			public const string ErrorLoggingIntoObject = "ErrorLoggingIntoObject";
			public const string ErrorLoggingRejectLimit = "ErrorLoggingRejectLimit";
			public const string EscapeClause = "EscapeClause";
			public const string Expression = "Expression";
			public const string ExpressionAsXmlAliasList = "ExpressionAsXmlAliasList";
			public const string ExpressionAsXmlAliasListChained = "ExpressionAsXmlAliasListChained";
			public const string ExpressionAsXmlAliasWithMandatoryAsList = "ExpressionAsXmlAliasWithMandatoryAsList";
			public const string ExpressionAsXmlAliasWithMandatoryAsListChained = "ExpressionAsXmlAliasWithMandatoryAsListChained";
			public const string ExpressionCommaChainedList = "ExpressionCommaChainedList";
			public const string ExpressionList = "ExpressionList";
			public const string ExpressionListOrNestedQuery = "ExpressionListOrNestedQuery";
			public const string ExpressionMathOperatorChainedList = "ExpressionMathOperatorChainedList";
			public const string ExpressionOrMultiset = "ExpressionOrMultiset";
			public const string ExpressionOrNestedQuery = "ExpressionOrNestedQuery";
			public const string ExpressionOrNestedQueryOrDefaultValue = "ExpressionOrNestedQueryOrDefaultValue";
			public const string ExpressionOrOrDefaultValue = "ExpressionOrOrDefaultValue";
			public const string ExpressionOrOrDefaultValueList = "ExpressionOrOrDefaultValueList";
			public const string ExpressionOrOrDefaultValueListChained = "ExpressionOrOrDefaultValueListChained";
			public const string ExtractElement = "ExtractElement";
			public const string FirstOrAll = "FirstOrAll";
			public const string FirstOrLast = "FirstOrLast";
			public const string FirstOrNext = "FirstOrNext";
			public const string FlashbackAsOfClause = "FlashbackAsOfClause";
			public const string FlashbackMaximumValue = "FlashbackMaximumValue";
			public const string FlashbackMinimumValue = "FlashbackMinimumValue";
			public const string FlashbackPeriodFor = "FlashbackPeriodFor";
			public const string FlashbackQueryClause = "FlashbackQueryClause";
			public const string FlashbackVersionsClause = "FlashbackVersionsClause";
			public const string ForceTransactionIdentifier = "ForceTransactionIdentifier";
			public const string ForUpdateClause = "ForUpdateClause";
			public const string ForUpdateColumn = "ForUpdateColumn";
			public const string ForUpdateColumnChained = "ForUpdateColumnChained";
			public const string ForUpdateLockingClause = "ForUpdateLockingClause";
			public const string ForUpdateOfColumnsClause = "ForUpdateOfColumnsClause";
			public const string ForUpdateWaitClause = "ForUpdateWaitClause";
			public const string FromClause = "FromClause";
			public const string FromClauseChained = "FromClauseChained";
			public const string GroupByClause = "GroupByClause";
			public const string GroupingClause = "GroupingClause";
			public const string GroupingClauseChained = "GroupingClauseChained";
			public const string GroupingExpressionList = "GroupingExpressionList";
			public const string GroupingExpressionListChained = "GroupingExpressionListChained";
			public const string GroupingExpressionListOrNestedQuery = "GroupingExpressionListOrNestedQuery";
			public const string GroupingExpressionListOrRollupCubeClause = "GroupingExpressionListOrRollupCubeClause";
			public const string GroupingExpressionListOrRollupCubeClauseChained = "GroupingExpressionListOrRollupCubeClauseChained";
			public const string GroupingExpressionListWithMandatoryExpressions = "GroupingExpressionListWithMandatoryExpressions";
			public const string GroupingExpressionListWithMandatoryExpressionsChained = "GroupingExpressionListWithMandatoryExpressionsChained";
			public const string GroupingSetsClause = "GroupingSetsClause";
			public const string HavingClause = "HavingClause";
			public const string HierarchicalQueryClause = "HierarchicalQueryClause";
			public const string HierarchicalQueryConnectByClause = "HierarchicalQueryConnectByClause";
			public const string HierarchicalQueryStartWithClause = "HierarchicalQueryStartWithClause";
			public const string IdentifierOrParenthesisEnclosedColumnIdentifierList = "IdentifierOrParenthesisEnclosedColumnIdentifierList";
			public const string IgnoreNulls = "IgnoreNulls";
			public const string ImmediateOrBatch = "ImmediateOrBatch";
			public const string IncludeOrExclude = "IncludeOrExclude";
			public const string InnerJoinClause = "InnerJoinClause";
			public const string InnerTableReference = "InnerTableReference";
			public const string InsertIntoClause = "InsertIntoClause";
			public const string InsertIntoValuesChainedList = "InsertIntoValuesChainedList";
			public const string InsertStatement = "InsertStatement";
			public const string InsertValuesClause = "InsertValuesClause";
			public const string InsertValuesOrSubquery = "InsertValuesOrSubquery";
			public const string IntegerOrAsterisk = "IntegerOrAsterisk";
			public const string IntervalDayToSecond = "IntervalDayToSecond";
			public const string IntervalExpression = "IntervalExpression";
			public const string IntervalYearToMonth = "IntervalYearToMonth";
			public const string JoinClause = "JoinClause";
			public const string JoinColumnsOrCondition = "JoinColumnsOrCondition";
			public const string KeepClause = "KeepClause";
			public const string LikeOperator = "LikeOperator";
			public const string ListAggregationClause = "ListAggregationClause";
			public const string ListAggregationDelimiter = "ListAggregationDelimiter";
			public const string LogicalOperator = "LogicalOperator";
			public const string MandatoryAsXmlAlias = "MandatoryAsXmlAlias";
			public const string MathOperator = "MathOperator";
			public const string MergeDeleteClause = "MergeDeleteClause";
			public const string MergeInsertClause = "MergeInsertClause";
			public const string MergeInsertValuesExpressionOrOrDefaultValueList = "MergeInsertValuesExpressionOrOrDefaultValueList";
			public const string MergeSetColumnEqualsExpressionOrOrDefaultValueCommaChainedList = "MergeSetColumnEqualsExpressionOrOrDefaultValueCommaChainedList";
			public const string MergeSetColumnEqualsExpressionOrOrDefaultValueList = "MergeSetColumnEqualsExpressionOrOrDefaultValueList";
			public const string MergeSource = "MergeSource";
			public const string MergeStatement = "MergeStatement";
			public const string MergeUpdateClause = "MergeUpdateClause";
			public const string MergeUpdateInsertClause = "MergeUpdateInsertClause";
			public const string MultisetOperator = "MultisetOperator";
			public const string MultisetOperatorClause = "MultisetOperatorClause";
			public const string MultiTableInsert = "MultiTableInsert";
			public const string NamedExpressionOrEvaluatedNameExpression = "NamedExpressionOrEvaluatedNameExpression";
			public const string NamedExpressionOrEvaluatedNameExpressionChained = "NamedExpressionOrEvaluatedNameExpressionChained";
			public const string NaturalOrOuterJoinType = "NaturalOrOuterJoinType";
			public const string NestedQuery = "NestedQuery";
			public const string NullNaNOrInfinite = "NullNaNOrInfinite";
			public const string NullsClause = "NullsClause";
			public const string ObjectPrefix = "ObjectPrefix";
			public const string ObjectTypeNestedFunctionCallChained = "ObjectTypeNestedFunctionCallChained";
			public const string OnlyOrWithTies = "OnlyOrWithTies";
			public const string OptionalParameter = "OptionalParameter";
			public const string OptionalParameterExpressionCommaChainedList = "OptionalParameterExpressionCommaChainedList";
			public const string OptionalParameterExpressionList = "OptionalParameterExpressionList";
			public const string OrderByClause = "OrderByClause";
			public const string OrderExpression = "OrderExpression";
			public const string OrderExpressionChained = "OrderExpressionChained";
			public const string OrderExpressionType = "OrderExpressionType";
			public const string OuterJoinClause = "OuterJoinClause";
			public const string OuterJoinOld = "OuterJoinOld";
			public const string OuterJoinPartitionClause = "OuterJoinPartitionClause";
			public const string OuterJoinType = "OuterJoinType";
			public const string OuterJoinTypeWithKeyword = "OuterJoinTypeWithKeyword";
			public const string OverQueryPartitionClause = "OverQueryPartitionClause";
			public const string ParenthesisEnclosedAggregationFunctionParameters = "ParenthesisEnclosedAggregationFunctionParameters";
			public const string ParenthesisEnclosedColumnIdentifierList = "ParenthesisEnclosedColumnIdentifierList";
			public const string ParenthesisEnclosedExpression = "ParenthesisEnclosedExpression";
			public const string ParenthesisEnclosedExpressionList = "ParenthesisEnclosedExpressionList";
			public const string ParenthesisEnclosedExpressionListWithIgnoreNulls = "ParenthesisEnclosedExpressionListWithIgnoreNulls";
			public const string ParenthesisEnclosedExpressionListWithMandatoryExpressions = "ParenthesisEnclosedExpressionListWithMandatoryExpressions";
			public const string ParenthesisEnclosedExpressionOrOrDefaultValueList = "ParenthesisEnclosedExpressionOrOrDefaultValueList";
			public const string ParenthesisEnclosedFunctionParameters = "ParenthesisEnclosedFunctionParameters";
			public const string ParenthesisEnclosedGroupingExpressionList = "ParenthesisEnclosedGroupingExpressionList";
			public const string ParenthesisEnclosedMergeInsertValuesExpressionOrOrDefaultValueList = "ParenthesisEnclosedMergeInsertValuesExpressionOrOrDefaultValueList";
			public const string ParenthesisEnclosedStringOrIntegerLiteralList = "ParenthesisEnclosedStringOrIntegerLiteralList";
			public const string PartitionExtensionClause = "PartitionExtensionClause";
			public const string PartitionNameOrKeySet = "PartitionNameOrKeySet";
			public const string PartitionOrDatabaseLink = "PartitionOrDatabaseLink";
			public const string PivotAliasedAggregationFunctionList = "PivotAliasedAggregationFunctionList";
			public const string PivotAliasedAggregationFunctionListChained = "PivotAliasedAggregationFunctionListChained";
			public const string PivotClause = "PivotClause";
			public const string PivotClauseOrUnpivotClauseOrRowPatternClause = "PivotClauseOrUnpivotClauseOrRowPatternClause";
			public const string PivotExpressionsOrAnyListOrNestedQuery = "PivotExpressionsOrAnyListOrNestedQuery";
			public const string PivotForClause = "PivotForClause";
			public const string PivotInClause = "PivotInClause";
			public const string PrecedingOnlyClause = "PrecedingOnlyClause";
			public const string PrecedingOnlyOrBetweenPrecedingAndFollowing = "PrecedingOnlyOrBetweenPrecedingAndFollowing";
			public const string PrecedingOrFollowing = "PrecedingOrFollowing";
			public const string Prefix = "Prefix";
			public const string PrefixedColumnReference = "PrefixedColumnReference";
			public const string PreserveTable = "PreserveTable";
			public const string QueryBlock = "QueryBlock";
			public const string QueryPartitionClause = "QueryPartitionClause";
			public const string QueryTableExpression = "QueryTableExpression";
			public const string ReadOnlyOrCheckOption = "ReadOnlyOrCheckOption";
			public const string RelationalEquiOperator = "RelationalEquiOperator";
			public const string RelationalNonEquiOperator = "RelationalNonEquiOperator";
			public const string RelationalOperator = "RelationalOperator";
			public const string ReturningClause = "ReturningClause";
			public const string ReturningSequenceByRef = "ReturningSequenceByRef";
			public const string ReturnOrReturning = "ReturnOrReturning";
			public const string RollbackStatement = "RollbackStatement";
			public const string RollupCubeClause = "RollupCubeClause";
			public const string RowLimitingClause = "RowLimitingClause";
			public const string RowLimitingFetchClause = "RowLimitingFetchClause";
			public const string RowLimitingOffsetClause = "RowLimitingOffsetClause";
			public const string RowOrRows = "RowOrRows";
			public const string RowsOrRange = "RowsOrRange";
			public const string SampleClause = "SampleClause";
			public const string SavepointStatement = "SavepointStatement";
			public const string Scale = "Scale";
			public const string SchemaCheckOrNoSchemaCheck = "SchemaCheckOrNoSchemaCheck";
			public const string SchemaPrefix = "SchemaPrefix";
			public const string SearchedCaseExpressionBranch = "SearchedCaseExpressionBranch";
			public const string SeedClause = "SeedClause";
			public const string SelectExpressionExpressionChainedList = "SelectExpressionExpressionChainedList";
			public const string SelectList = "SelectList";
			public const string SelectStatement = "SelectStatement";
			public const string SerializableOrReadCommitted = "SerializableOrReadCommitted";
			public const string SetColumnEqualsExpressionOrNestedQueryOrDefaultValue = "SetColumnEqualsExpressionOrNestedQueryOrDefaultValue";
			public const string SetColumnListEqualsNestedQuery = "SetColumnListEqualsNestedQuery";
			public const string SetObjectValueEqualsExpressionOrNestedQuery = "SetObjectValueEqualsExpressionOrNestedQuery";
			public const string SetOperation = "SetOperation";
			public const string SetQualifier = "SetQualifier";
			public const string SetTransactionStatement = "SetTransactionStatement";
			public const string SimpleCaseExpressionBranch = "SimpleCaseExpressionBranch";
			public const string SimpleSchemaObject = "SimpleSchemaObject";
			public const string SingleTableInsert = "SingleTableInsert";
			public const string SingleTableInsertOrMultiTableInsert = "SingleTableInsertOrMultiTableInsert";
			public const string SortOrder = "SortOrder";
			public const string StringOrIntegerLiteral = "StringOrIntegerLiteral";
			public const string StringOrIntegerLiteralList = "StringOrIntegerLiteralList";
			public const string StringOrIntegerLiteralListChained = "StringOrIntegerLiteralListChained";
			public const string StringOrIntegerLiteralOrParenthesisEnclosedStringOrIntegerLiteralList = "StringOrIntegerLiteralOrParenthesisEnclosedStringOrIntegerLiteralList";
			public const string Subquery = "Subquery";
			public const string SubqueryComponent = "SubqueryComponent";
			public const string SubqueryComponentChained = "SubqueryComponentChained";
			public const string SubqueryFactoringClause = "SubqueryFactoringClause";
			public const string SubqueryFactoringCycleClause = "SubqueryFactoringCycleClause";
			public const string SubqueryFactoringSearchClause = "SubqueryFactoringSearchClause";
			public const string SubQueryRestrictionClause = "SubQueryRestrictionClause";
			public const string SystemChangeNumberOrTimestamp = "SystemChangeNumberOrTimestamp";
			public const string SystemChangeNumberOrTimestampOrPeriodFor = "SystemChangeNumberOrTimestampOrPeriodFor";
			public const string TableCollectionExpression = "TableCollectionExpression";
			public const string TableCollectionInnerExpression = "TableCollectionInnerExpression";
			public const string TableReference = "TableReference";
			public const string TimestampAtTimeZone = "TimestampAtTimeZone";
			public const string ToDayOrHourOrMinuteOrSecondWithPrecision = "ToDayOrHourOrMinuteOrSecondWithPrecision";
			public const string ToSavepointOrForceTransactionIdentifier = "ToSavepointOrForceTransactionIdentifier";
			public const string ToYearOrMonth = "ToYearOrMonth";
			public const string TransactionModeOrIsolationLevelOrRollbackSegment = "TransactionModeOrIsolationLevelOrRollbackSegment";
			public const string TransactionNameClause = "TransactionNameClause";
			public const string TransactionReadOnlyOrReadWrite = "TransactionReadOnlyOrReadWrite";
			public const string UnlimitedOrIntegerLiteral = "UnlimitedOrIntegerLiteral";
			public const string UnpivotClause = "UnpivotClause";
			public const string UnpivotInClause = "UnpivotInClause";
			public const string UnpivotNullsClause = "UnpivotNullsClause";
			public const string UnpivotValueSelector = "UnpivotValueSelector";
			public const string UnpivotValueToColumnTransformationList = "UnpivotValueToColumnTransformationList";
			public const string UnpivotValueToColumnTransformationListChained = "UnpivotValueToColumnTransformationListChained";
			public const string UpdateSetClause = "UpdateSetClause";
			public const string UpdateSetColumnOrColumnList = "UpdateSetColumnOrColumnList";
			public const string UpdateSetColumnOrColumnListChainedList = "UpdateSetColumnOrColumnListChainedList";
			public const string UpdateSetColumnsOrObjectValue = "UpdateSetColumnsOrObjectValue";
			public const string UpdateStatement = "UpdateStatement";
			public const string WaitOrNowait = "WaitOrNowait";
			public const string WhereClause = "WhereClause";
			public const string WindowingClause = "WindowingClause";
			public const string WithTimeZone = "WithTimeZone";
			public const string XmlAggregateClause = "XmlAggregateClause";
			public const string XmlElementClause = "XmlElementClause";
			public const string XmlNameOrEvaluatedName = "XmlNameOrEvaluatedName";
			public const string XmlNamespaceDefinition = "XmlNamespaceDefinition";
			public const string XmlNamespaceOrDefaultNamespace = "XmlNamespaceOrDefaultNamespace";
			public const string XmlNamespaceOrDefaultNamespaceChained = "XmlNamespaceOrDefaultNamespaceChained";
			public const string XmlNamespacesClause = "XmlNamespacesClause";
			public const string XmlParseFunction = "XmlParseFunction";
			public const string XmlPassingClause = "XmlPassingClause";
			public const string XmlQueryClause = "XmlQueryClause";
			public const string XmlRootFunction = "XmlRootFunction";
			public const string XmlRootFunctionExpressionOrNoValue = "XmlRootFunctionExpressionOrNoValue";
			public const string XmlRootFunctionNoValue = "XmlRootFunctionNoValue";
			public const string XmlRootFunctionStandaloneClause = "XmlRootFunctionStandaloneClause";
			public const string XmlRootFunctionStandaloneClauseYesOrNoOrNoValue = "XmlRootFunctionStandaloneClauseYesOrNoOrNoValue";
			public const string XmlSimpleFunction = "XmlSimpleFunction";
			public const string XmlSimpleFunctionClause = "XmlSimpleFunctionClause";
			public const string XmlTableClause = "XmlTableClause";
			public const string XmlTableColumn = "XmlTableColumn";
			public const string XmlTableColumnDefaultClause = "XmlTableColumnDefaultClause";
			public const string XmlTableColumnDefinition = "XmlTableColumnDefinition";
			public const string XmlTableColumnList = "XmlTableColumnList";
			public const string XmlTableColumnListChained = "XmlTableColumnListChained";
			public const string XmlTableColumnPathClause = "XmlTableColumnPathClause";
			public const string XmlTableOptions = "XmlTableOptions";
			public const string XmlTypeSequenceByRef = "XmlTypeSequenceByRef";
			public const string YearOrMonth = "YearOrMonth";
			public const string YearToMonthOrDayToSecond = "YearToMonthOrDayToSecond";
		}


		/// <summary>
		/// This class provides the terminal constants.
		/// </summary>
		public static class Terminals
		{
			public const string A = "A";
			public const string All = "All";
			public const string Alter = "Alter";
			public const string And = "And";
			public const string Any = "Any";
			public const string Apply = "Apply";
			public const string As = "As";
			public const string Asc = "Asc";
			public const string Asterisk = "Asterisk";
			public const string At = "At";
			public const string AtCharacter = "AtCharacter";
			public const string Avg = "Avg";
			public const string Batch = "Batch";
			public const string Between = "Between";
			public const string BindVariableIdentifier = "BindVariableIdentifier";
			public const string Block = "Block";
			public const string Body = "Body";
			public const string Breadth = "Breadth";
			public const string By = "By";
			public const string Byte = "Byte";
			public const string Cascade = "Cascade";
			public const string Case = "Case";
			public const string Cast = "Cast";
			public const string Char = "Char";
			public const string Check = "Check";
			public const string Cluster = "Cluster";
			public const string Colon = "Colon";
			public const string Column = "Column";
			public const string ColumnAlias = "ColumnAlias";
			public const string Columns = "Columns";
			public const string Comma = "Comma";
			public const string Comment = "Comment";
			public const string Commit = "Commit";
			public const string Committed = "Committed";
			public const string Compress = "Compress";
			public const string Connect = "Connect";
			public const string Constraint = "Constraint";
			public const string Constraints = "Constraints";
			public const string Content = "Content";
			public const string Context = "Context";
			public const string Count = "Count";
			public const string Create = "Create";
			public const string Cross = "Cross";
			public const string Cube = "Cube";
			public const string Current = "Current";
			public const string Cycle = "Cycle";
			public const string DatabaseLinkIdentifier = "DatabaseLinkIdentifier";
			public const string DataTypeIdentifier = "DataTypeIdentifier";
			public const string Date = "Date";
			public const string Day = "Day";
			public const string Decimal = "Decimal";
			public const string Default = "Default";
			public const string Delete = "Delete";
			public const string DenseRank = "DenseRank";
			public const string Depth = "Depth";
			public const string Desc = "Desc";
			public const string Distinct = "Distinct";
			public const string Document = "Document";
			public const string Dot = "Dot";
			public const string Drop = "Drop";
			public const string Else = "Else";
			public const string Empty = "Empty";
			public const string End = "End";
			public const string EntityEscaping = "EntityEscaping";
			public const string Errors = "Errors";
			public const string Escape = "Escape";
			public const string EvaluatedName = "EvaluatedName";
			public const string Except = "Except";
			public const string Exclude = "Exclude";
			public const string Exclusive = "Exclusive";
			public const string Exists = "Exists";
			public const string Extract = "Extract";
			public const string Fetch = "Fetch";
			public const string First = "First";
			public const string FirstValue = "FirstValue";
			public const string Float = "Float";
			public const string Following = "Following";
			public const string For = "For";
			public const string Force = "Force";
			public const string From = "From";
			public const string Full = "Full";
			public const string Function = "Function";
			public const string Grant = "Grant";
			public const string Group = "Group";
			public const string Grouping = "Grouping";
			public const string Having = "Having";
			public const string Hour = "Hour";
			public const string Identified = "Identified";
			public const string Identifier = "Identifier";
			public const string Ignore = "Ignore";
			public const string Immediate = "Immediate";
			public const string In = "In";
			public const string Include = "Include";
			public const string Index = "Index";
			public const string Inner = "Inner";
			public const string Insert = "Insert";
			public const string Integer = "Integer";
			public const string IntegerLiteral = "IntegerLiteral";
			public const string Intersect = "Intersect";
			public const string Interval = "Interval";
			public const string Into = "Into";
			public const string Is = "Is";
			public const string Isolation = "Isolation";
			public const string Join = "Join";
			public const string Keep = "Keep";
			public const string Lag = "Lag";
			public const string Last = "Last";
			public const string LastValue = "LastValue";
			public const string Lateral = "Lateral";
			public const string Lead = "Lead";
			public const string Left = "Left";
			public const string LeftParenthesis = "LeftParenthesis";
			public const string Level = "Level";
			public const string Like = "Like";
			public const string LikeUcs2 = "LikeUcs2";
			public const string LikeUcs4 = "LikeUcs4";
			public const string LikeUnicode = "LikeUnicode";
			public const string Limit = "Limit";
			public const string ListAggregation = "ListAggregation";
			public const string Lock = "Lock";
			public const string Locked = "Locked";
			public const string Log = "Log";
			public const string Long = "Long";
			public const string Matched = "Matched";
			public const string Materialized = "Materialized";
			public const string MathDivide = "MathDivide";
			public const string MathEquals = "MathEquals";
			public const string MathFactor = "MathFactor";
			public const string MathGreatherThan = "MathGreatherThan";
			public const string MathGreatherThanOrEquals = "MathGreatherThanOrEquals";
			public const string MathInfinite = "MathInfinite";
			public const string MathLessThan = "MathLessThan";
			public const string MathLessThanOrEquals = "MathLessThanOrEquals";
			public const string MathMinus = "MathMinus";
			public const string MathNotANumber = "MathNotANumber";
			public const string MathNotEqualsC = "MathNotEqualsC";
			public const string MathNotEqualsCircumflex = "MathNotEqualsCircumflex";
			public const string MathNotEqualsSql = "MathNotEqualsSql";
			public const string MathPlus = "MathPlus";
			public const string Max = "Max";
			public const string MaximumValue = "MaximumValue";
			public const string Member = "Member";
			public const string MemberFunctionIdentifier = "MemberFunctionIdentifier";
			public const string Merge = "Merge";
			public const string Min = "Min";
			public const string MinimumValue = "MinimumValue";
			public const string Minute = "Minute";
			public const string Mode = "Mode";
			public const string Model = "Model";
			public const string Month = "Month";
			public const string Multiset = "Multiset";
			public const string Name = "Name";
			public const string Natural = "Natural";
			public const string NChar = "NChar";
			public const string NegationOrNull = "NegationOrNull";
			public const string Next = "Next";
			public const string No = "No";
			public const string Nocompress = "Nocompress";
			public const string NoCycle = "NoCycle";
			public const string NoEntityEscaping = "NoEntityEscaping";
			public const string NoSchemaCheck = "NoSchemaCheck";
			public const string Not = "Not";
			public const string Nowait = "Nowait";
			public const string Null = "Null";
			public const string Nulls = "Nulls";
			public const string Number = "Number";
			public const string NumberLiteral = "NumberLiteral";
			public const string NVarchar = "NVarchar";
			public const string NVarchar2 = "NVarchar2";
			public const string ObjectAlias = "ObjectAlias";
			public const string ObjectIdentifier = "ObjectIdentifier";
			public const string Of = "Of";
			public const string Offset = "Offset";
			public const string On = "On";
			public const string Online = "Online";
			public const string Only = "Only";
			public const string OperatorConcatenation = "OperatorConcatenation";
			public const string Option = "Option";
			public const string OptionalParameterOperator = "OptionalParameterOperator";
			public const string Or = "Or";
			public const string Order = "Order";
			public const string Ordinality = "Ordinality";
			public const string Outer = "Outer";
			public const string Over = "Over";
			public const string Package = "Package";
			public const string ParameterIdentifier = "ParameterIdentifier";
			public const string Partition = "Partition";
			public const string Passing = "Passing";
			public const string Path = "Path";
			public const string Pctfree = "Pctfree";
			public const string Percent = "Percent";
			public const string Period = "Period";
			public const string Pivot = "Pivot";
			public const string Preceding = "Preceding";
			public const string Preserve = "Preserve";
			public const string Procedure = "Procedure";
			public const string Public = "Public";
			public const string Purge = "Purge";
			public const string Range = "Range";
			public const string Raw = "Raw";
			public const string Read = "Read";
			public const string Ref = "Ref";
			public const string Reject = "Reject";
			public const string Rename = "Rename";
			public const string Resource = "Resource";
			public const string Return = "Return";
			public const string Returning = "Returning";
			public const string Revoke = "Revoke";
			public const string Right = "Right";
			public const string RightParenthesis = "RightParenthesis";
			public const string Rollback = "Rollback";
			public const string Rollup = "Rollup";
			public const string Row = "Row";
			public const string RowIdPseudoColumn = "RowIdPseudoColumn";
			public const string RowNumberPseudoColumn = "RowNumberPseudoColumn";
			public const string Rows = "Rows";
			public const string Sample = "Sample";
			public const string Savepoint = "Savepoint";
			public const string SchemaCheck = "SchemaCheck";
			public const string SchemaIdentifier = "SchemaIdentifier";
			public const string Search = "Search";
			public const string Second = "Second";
			public const string Seed = "Seed";
			public const string Segment = "Segment";
			public const string Select = "Select";
			public const string Semicolon = "Semicolon";
			public const string Sequence = "Sequence";
			public const string Serializable = "Serializable";
			public const string Set = "Set";
			public const string SetMinus = "SetMinus";
			public const string Sets = "Sets";
			public const string Share = "Share";
			public const string Siblings = "Siblings";
			public const string Size = "Size";
			public const string Skip = "Skip";
			public const string Smallint = "Smallint";
			public const string Some = "Some";
			public const string Space = "Space";
			public const string Standalone = "Standalone";
			public const string StandardDeviation = "StandardDeviation";
			public const string Start = "Start";
			public const string StringLiteral = "StringLiteral";
			public const string SubMultiset = "SubMultiset";
			public const string Subpartition = "Subpartition";
			public const string Sum = "Sum";
			public const string Synonym = "Synonym";
			public const string SystemChangeNumber = "SystemChangeNumber";
			public const string SystemDate = "SystemDate";
			public const string Table = "Table";
			public const string Then = "Then";
			public const string Ties = "Ties";
			public const string Time = "Time";
			public const string Timestamp = "Timestamp";
			public const string TimezoneAbbreviation = "TimezoneAbbreviation";
			public const string TimezoneHour = "TimezoneHour";
			public const string TimezoneMinute = "TimezoneMinute";
			public const string TimezoneRegion = "TimezoneRegion";
			public const string To = "To";
			public const string Transaction = "Transaction";
			public const string Treat = "Treat";
			public const string Trigger = "Trigger";
			public const string Unbounded = "Unbounded";
			public const string Union = "Union";
			public const string Unique = "Unique";
			public const string Unlimited = "Unlimited";
			public const string Unpivot = "Unpivot";
			public const string Update = "Update";
			public const string Use = "Use";
			public const string Using = "Using";
			public const string Wait = "Wait";
			public const string Value = "Value";
			public const string Values = "Values";
			public const string Varchar = "Varchar";
			public const string Varchar2 = "Varchar2";
			public const string Variance = "Variance";
			public const string WellFormed = "WellFormed";
			public const string Version = "Version";
			public const string Versions = "Versions";
			public const string When = "When";
			public const string Where = "Where";
			public const string View = "View";
			public const string With = "With";
			public const string Within = "Within";
			public const string Work = "Work";
			public const string Write = "Write";
			public const string Xml = "Xml";
			public const string XmlAggregate = "XmlAggregate";
			public const string XmlAlias = "XmlAlias";
			public const string XmlAttributes = "XmlAttributes";
			public const string XmlColumnValue = "XmlColumnValue";
			public const string XmlElement = "XmlElement";
			public const string XmlForest = "XmlForest";
			public const string XmlNamespaces = "XmlNamespaces";
			public const string XmlParse = "XmlParse";
			public const string XmlQuery = "XmlQuery";
			public const string XmlRoot = "XmlRoot";
			public const string XmlTable = "XmlTable";
			public const string XmlType = "XmlType";
			public const string Year = "Year";
			public const string Yes = "Yes";
			public const string Zone = "Zone";
			
			public static ICollection<string> AllTerminals
			{
				get { return AllTerminalsInternal; }
			}
		}

		private static readonly HashSet<string> AllTerminalsInternal = new HashSet<string> { Terminals.A, Terminals.All, Terminals.Alter, Terminals.And, Terminals.Any, Terminals.Apply, Terminals.As, Terminals.Asc, Terminals.Asterisk, Terminals.At, Terminals.AtCharacter, Terminals.Avg, Terminals.Batch, Terminals.Between, Terminals.BindVariableIdentifier, Terminals.Block, Terminals.Body, Terminals.Breadth, Terminals.By, Terminals.Byte, Terminals.Cascade, Terminals.Case, Terminals.Cast, Terminals.Char, Terminals.Check, Terminals.Cluster, Terminals.Colon, Terminals.Column, Terminals.ColumnAlias, Terminals.Columns, Terminals.Comma, Terminals.Comment, Terminals.Commit, Terminals.Committed, Terminals.Compress, Terminals.Connect, Terminals.Constraint, Terminals.Constraints, Terminals.Content, Terminals.Context, Terminals.Count, Terminals.Create, Terminals.Cross, Terminals.Cube, Terminals.Current, Terminals.Cycle, Terminals.DatabaseLinkIdentifier, Terminals.DataTypeIdentifier, Terminals.Date, Terminals.Day, Terminals.Decimal, Terminals.Default, Terminals.Delete, Terminals.DenseRank, Terminals.Depth, Terminals.Desc, Terminals.Distinct, Terminals.Document, Terminals.Dot, Terminals.Drop, Terminals.Else, Terminals.Empty, Terminals.End, Terminals.EntityEscaping, Terminals.Errors, Terminals.Escape, Terminals.EvaluatedName, Terminals.Except, Terminals.Exclude, Terminals.Exclusive, Terminals.Exists, Terminals.Extract, Terminals.Fetch, Terminals.First, Terminals.FirstValue, Terminals.Float, Terminals.Following, Terminals.For, Terminals.Force, Terminals.From, Terminals.Full, Terminals.Function, Terminals.Grant, Terminals.Group, Terminals.Grouping, Terminals.Having, Terminals.Hour, Terminals.Identified, Terminals.Identifier, Terminals.Ignore, Terminals.Immediate, Terminals.In, Terminals.Include, Terminals.Index, Terminals.Inner, Terminals.Insert, Terminals.Integer, Terminals.IntegerLiteral, Terminals.Intersect, Terminals.Interval, Terminals.Into, Terminals.Is, Terminals.Isolation, Terminals.Join, Terminals.Keep, Terminals.Lag, Terminals.Last, Terminals.LastValue, Terminals.Lateral, Terminals.Lead, Terminals.Left, Terminals.LeftParenthesis, Terminals.Level, Terminals.Like, Terminals.LikeUcs2, Terminals.LikeUcs4, Terminals.LikeUnicode, Terminals.Limit, Terminals.ListAggregation, Terminals.Lock, Terminals.Locked, Terminals.Log, Terminals.Long, Terminals.Matched, Terminals.Materialized, Terminals.MathDivide, Terminals.MathEquals, Terminals.MathFactor, Terminals.MathGreatherThan, Terminals.MathGreatherThanOrEquals, Terminals.MathInfinite, Terminals.MathLessThan, Terminals.MathLessThanOrEquals, Terminals.MathMinus, Terminals.MathNotANumber, Terminals.MathNotEqualsC, Terminals.MathNotEqualsCircumflex, Terminals.MathNotEqualsSql, Terminals.MathPlus, Terminals.Max, Terminals.MaximumValue, Terminals.Member, Terminals.MemberFunctionIdentifier, Terminals.Merge, Terminals.Min, Terminals.MinimumValue, Terminals.Minute, Terminals.Mode, Terminals.Model, Terminals.Month, Terminals.Multiset, Terminals.Name, Terminals.Natural, Terminals.NChar, Terminals.NegationOrNull, Terminals.Next, Terminals.No, Terminals.Nocompress, Terminals.NoCycle, Terminals.NoEntityEscaping, Terminals.NoSchemaCheck, Terminals.Not, Terminals.Nowait, Terminals.Null, Terminals.Nulls, Terminals.Number, Terminals.NumberLiteral, Terminals.NVarchar, Terminals.NVarchar2, Terminals.ObjectAlias, Terminals.ObjectIdentifier, Terminals.Of, Terminals.Offset, Terminals.On, Terminals.Online, Terminals.Only, Terminals.OperatorConcatenation, Terminals.Option, Terminals.OptionalParameterOperator, Terminals.Or, Terminals.Order, Terminals.Ordinality, Terminals.Outer, Terminals.Over, Terminals.Package, Terminals.ParameterIdentifier, Terminals.Partition, Terminals.Passing, Terminals.Path, Terminals.Pctfree, Terminals.Percent, Terminals.Period, Terminals.Pivot, Terminals.Preceding, Terminals.Preserve, Terminals.Procedure, Terminals.Public, Terminals.Purge, Terminals.Range, Terminals.Raw, Terminals.Read, Terminals.Ref, Terminals.Reject, Terminals.Rename, Terminals.Resource, Terminals.Return, Terminals.Returning, Terminals.Revoke, Terminals.Right, Terminals.RightParenthesis, Terminals.Rollback, Terminals.Rollup, Terminals.Row, Terminals.RowIdPseudoColumn, Terminals.RowNumberPseudoColumn, Terminals.Rows, Terminals.Sample, Terminals.Savepoint, Terminals.SchemaCheck, Terminals.SchemaIdentifier, Terminals.Search, Terminals.Second, Terminals.Seed, Terminals.Segment, Terminals.Select, Terminals.Semicolon, Terminals.Sequence, Terminals.Serializable, Terminals.Set, Terminals.SetMinus, Terminals.Sets, Terminals.Share, Terminals.Siblings, Terminals.Size, Terminals.Skip, Terminals.Smallint, Terminals.Some, Terminals.Space, Terminals.Standalone, Terminals.StandardDeviation, Terminals.Start, Terminals.StringLiteral, Terminals.SubMultiset, Terminals.Subpartition, Terminals.Sum, Terminals.Synonym, Terminals.SystemChangeNumber, Terminals.SystemDate, Terminals.Table, Terminals.Then, Terminals.Ties, Terminals.Time, Terminals.Timestamp, Terminals.TimezoneAbbreviation, Terminals.TimezoneHour, Terminals.TimezoneMinute, Terminals.TimezoneRegion, Terminals.To, Terminals.Transaction, Terminals.Treat, Terminals.Trigger, Terminals.Unbounded, Terminals.Union, Terminals.Unique, Terminals.Unlimited, Terminals.Unpivot, Terminals.Update, Terminals.Use, Terminals.Using, Terminals.Wait, Terminals.Value, Terminals.Values, Terminals.Varchar, Terminals.Varchar2, Terminals.Variance, Terminals.WellFormed, Terminals.Version, Terminals.Versions, Terminals.When, Terminals.Where, Terminals.View, Terminals.With, Terminals.Within, Terminals.Work, Terminals.Write, Terminals.Xml, Terminals.XmlAggregate, Terminals.XmlAlias, Terminals.XmlAttributes, Terminals.XmlColumnValue, Terminals.XmlElement, Terminals.XmlForest, Terminals.XmlNamespaces, Terminals.XmlParse, Terminals.XmlQuery, Terminals.XmlRoot, Terminals.XmlTable, Terminals.XmlType, Terminals.Year, Terminals.Yes, Terminals.Zone };
			
		private static readonly HashSet<string> IdentifiersInternal = new HashSet<string> { Terminals.BindVariableIdentifier, Terminals.DatabaseLinkIdentifier, Terminals.DataTypeIdentifier, Terminals.Identifier, Terminals.MemberFunctionIdentifier, Terminals.ObjectIdentifier, Terminals.ParameterIdentifier, Terminals.SchemaIdentifier };

		private static readonly HashSet<string> Keywords = new HashSet<string> { "ALL", "ALTER", "AND", "ANY", "ASC", "BETWEEN", "BY", "CHAR", "CHECK", "CLUSTER", "COLUMN", "COMMENT", "COMPRESS", "CONNECT", "CREATE", "CURRENT", "DATE", "DECIMAL", "DEFAULT", "DELETE", "DESC", "DISTINCT", "DROP", "ELSE", "EXCLUSIVE", "EXISTS", "FLOAT", "FOR", "FROM", "GRANT", "GROUP", "HAVING", "IDENTIFIED", "IMMEDIATE", "IN", "INDEX", "INSERT", "INTEGER", "INTERSECT", "INTO", "IS", "LEVEL", "LIKE", "LOCK", "LONG", "MINUS", "MODE", "NOCOMPRESS", "NOT", "NOWAIT", "NULL", "NUMBER", "OF", "ON", "OPTION", "OR", "ORDER", "PCTFREE", "PUBLIC", "RAW", "RENAME", "RESOURCE", "REVOKE", "ROW", "ROWID", "ROWNUM", "ROWS", "SELECT", "SET", "SHARE", "SIZE", "SMALLINT", "SOME", "START", "SYNONYM", "SYSDATE", "TABLE", "THEN", "TO", "TRIGGER", "UNION", "UNIQUE", "UPDATE", "VALUES", "VARCHAR", "VARCHAR2", "WHERE", "VIEW", "WITH" };
		
		private static readonly HashSet<string> LiteralsInternal = new HashSet<string> { Terminals.IntegerLiteral, Terminals.NumberLiteral, Terminals.StringLiteral };

		private static readonly HashSet<string> SingleCharacterTerminalsInternal =
			new HashSet<string>
			{
				Terminals.LeftParenthesis,
				Terminals.RightParenthesis,
				Terminals.Comma,
				Terminals.Semicolon,
				Terminals.Dot,
				Terminals.MathDivide,
				Terminals.MathPlus,
				Terminals.MathMinus,
				Terminals.AtCharacter,
				Terminals.Colon
			};

		private static readonly HashSet<string> ZeroOffsetTerminalIdsInternal =
			new HashSet<string>
			{
				Terminals.Dot,
				Terminals.Comma,
				Terminals.OperatorConcatenation,
				Terminals.LeftParenthesis,
				Terminals.RightParenthesis,
				Terminals.MathDivide,
				Terminals.MathEquals,
				Terminals.MathFactor,
				Terminals.MathGreatherThan,
				Terminals.MathGreatherThanOrEquals,
				Terminals.MathLessThan,
				Terminals.MathLessThanOrEquals,
				Terminals.MathMinus,
				Terminals.MathNotEqualsC,
				Terminals.MathNotEqualsCircumflex,
				Terminals.MathNotEqualsSql,
				Terminals.MathPlus
			};

		private static readonly HashSet<string> MathTerminalsInternal =
			new HashSet<string>
			{
				Terminals.MathFactor,
				Terminals.MathDivide,
				Terminals.MathPlus,
				Terminals.MathMinus,
				Terminals.OperatorConcatenation
			};

		public static ICollection<string> SingleCharacterTerminals
		{
			get { return SingleCharacterTerminalsInternal; }
		}

		public static ICollection<string> MathTerminals
		{
			get { return MathTerminalsInternal; }
		}

		public static ICollection<string> Identifiers
		{
			get { return IdentifiersInternal; }
		}

		public static bool IsKeyword(this string value)
		{
			return Keywords.Contains(value.ToUpperInvariant());
		}

		public static bool IsIdentifier(this string terminalId)
		{
			return IdentifiersInternal.Contains(terminalId);
		}

		public static bool IsZeroOffsetTerminalId(this string terminalId)
		{
			return ZeroOffsetTerminalIdsInternal.Contains(terminalId);
		}

		public static bool IsAlias(this string terminalId)
		{
			return terminalId == "ColumnAlias" || terminalId == "ObjectAlias" || terminalId == "XmlAlias";
		}

		public static bool IsIdentifierOrAlias(this string terminalId)
		{
			return terminalId.IsIdentifier() || terminalId.IsAlias();
		}

		public static bool IsLiteral(this string terminalId)
		{
			return LiteralsInternal.Contains(terminalId);
		}
	}
}
