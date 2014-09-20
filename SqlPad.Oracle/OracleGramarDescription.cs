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
			public const string AsSubquery = "AsSubquery";
			public const string AsXmlAlias = "AsXmlAlias";
			public const string BackwardCompatibilityCompressionType = "BackwardCompatibilityCompressionType";
			public const string BasicFileOrSecureFile = "BasicFileOrSecureFile";
			public const string BasicOrAdvanced = "BasicOrAdvanced";
			public const string BetweenFollowingClause = "BetweenFollowingClause";
			public const string BetweenPrecedingAndFollowingClause = "BetweenPrecedingAndFollowingClause";
			public const string BetweenPrecedingClause = "BetweenPrecedingClause";
			public const string BetweenSystemChangeNumberOrTimestamp = "BetweenSystemChangeNumberOrTimestamp";
			public const string BetweenSystemChangeNumberOrTimestampOrPeriodForBetween = "BetweenSystemChangeNumberOrTimestampOrPeriodForBetween";
			public const string BindVariableExpression = "BindVariableExpression";
			public const string BindVariableExpressionCommaChainedList = "BindVariableExpressionCommaChainedList";
			public const string BufferPoolType = "BufferPoolType";
			public const string ByteOrChar = "ByteOrChar";
			public const string ByValue = "ByValue";
			public const string CacheOrNoCache = "CacheOrNoCache";
			public const string CascadeConstraints = "CascadeConstraints";
			public const string CascadeOrSetNull = "CascadeOrSetNull";
			public const string CaseExpression = "CaseExpression";
			public const string CaseExpressionElseBranch = "CaseExpressionElseBranch";
			public const string ChainedCondition = "ChainedCondition";
			public const string CharacterOrChar = "CharacterOrChar";
			public const string ClusterPhysicalProperties = "ClusterPhysicalProperties";
			public const string ColumnAsAlias = "ColumnAsAlias";
			public const string ColumnDefinition = "ColumnDefinition";
			public const string ColumnDefinitionDefaultClause = "ColumnDefinitionDefaultClause";
			public const string ColumnDefinitionDefaultClauseOrIdentityClause = "ColumnDefinitionDefaultClauseOrIdentityClause";
			public const string ColumnDefinitionEncryptionClause = "ColumnDefinitionEncryptionClause";
			public const string ColumnDefinitionIdentityClause = "ColumnDefinitionIdentityClause";
			public const string ColumnDefinitionInlineConstraintDefinition = "ColumnDefinitionInlineConstraintDefinition";
			public const string ColumnIdentifierChainedList = "ColumnIdentifierChainedList";
			public const string ColumnProperties = "ColumnProperties";
			public const string ColumnProperty = "ColumnProperty";
			public const string ColumnReference = "ColumnReference";
			public const string ColumnStoreCompressionType = "ColumnStoreCompressionType";
			public const string CommaChainedExpressionWithAlias = "CommaChainedExpressionWithAlias";
			public const string CommaChainedExternalTableDirectoryLocation = "CommaChainedExternalTableDirectoryLocation";
			public const string CommaChainedListPartitionList = "CommaChainedListPartitionList";
			public const string CommaChainedRangePartitionList = "CommaChainedRangePartitionList";
			public const string CommaChainedRelationalProperty = "CommaChainedRelationalProperty";
			public const string CommaChainedSupplementalLogColumn = "CommaChainedSupplementalLogColumn";
			public const string CommaChainedSupplementalLogKeyList = "CommaChainedSupplementalLogKeyList";
			public const string CommaChainedTableIndexExpressionList = "CommaChainedTableIndexExpressionList";
			public const string CommaChainedViewRelationalProperty = "CommaChainedViewRelationalProperty";
			public const string CommaPrefixedXmlAttributesClause = "CommaPrefixedXmlAttributesClause";
			public const string CommitComment = "CommitComment";
			public const string CommitCommentOrWriteOrForce = "CommitCommentOrWriteOrForce";
			public const string CommitSetScn = "CommitSetScn";
			public const string CommitStatement = "CommitStatement";
			public const string CommitWriteClause = "CommitWriteClause";
			public const string CommonSequenceOption = "CommonSequenceOption";
			public const string ConcatenatedSubquery = "ConcatenatedSubquery";
			public const string Condition = "Condition";
			public const string ConditionalInsertClause = "ConditionalInsertClause";
			public const string ConditionalInsertConditionBranch = "ConditionalInsertConditionBranch";
			public const string ConditionalInsertElseBranch = "ConditionalInsertElseBranch";
			public const string ConstraintName = "ConstraintName";
			public const string ConstraintOnDeleteClause = "ConstraintOnDeleteClause";
			public const string ConstraintReferencesClause = "ConstraintReferencesClause";
			public const string ConstraintState = "ConstraintState";
			public const string CountAsteriskParameter = "CountAsteriskParameter";
			public const string CreateIndex = "CreateIndex";
			public const string CreateObjectClause = "CreateObjectClause";
			public const string CreateSequence = "CreateSequence";
			public const string CreateStatement = "CreateStatement";
			public const string CreateSynonym = "CreateSynonym";
			public const string CreateTable = "CreateTable";
			public const string CreateTableTypeClause = "CreateTableTypeClause";
			public const string CreateView = "CreateView";
			public const string CreateViewTypeClause = "CreateViewTypeClause";
			public const string CrossOrOuter = "CrossOrOuter";
			public const string CrossOrOuterApplyClause = "CrossOrOuterApplyClause";
			public const string CurrentEditionOrSpecificEdition = "CurrentEditionOrSpecificEdition";
			public const string CurrentUserOrDefiner = "CurrentUserOrDefiner";
			public const string CycleOrNoCycle = "CycleOrNoCycle";
			public const string DatabaseLink = "DatabaseLink";
			public const string DatabaseLinkDomain = "DatabaseLinkDomain";
			public const string DatabaseLinkName = "DatabaseLinkName";
			public const string DataType = "DataType";
			public const string DataTypeDefinition = "DataTypeDefinition";
			public const string DataTypeIntervalPrecisionAndScale = "DataTypeIntervalPrecisionAndScale";
			public const string DataTypeNumericPrecisionAndScale = "DataTypeNumericPrecisionAndScale";
			public const string DataTypeOrXmlType = "DataTypeOrXmlType";
			public const string DataTypeSimplePrecision = "DataTypeSimplePrecision";
			public const string DataTypeSimplePrecisionOrVaryingSize = "DataTypeSimplePrecisionOrVaryingSize";
			public const string DataTypeVarcharSimplePrecision = "DataTypeVarcharSimplePrecision";
			public const string DataTypeVarcharSimplePrecisionOrVaryingSize = "DataTypeVarcharSimplePrecisionOrVaryingSize";
			public const string DayOrHourOrMinute = "DayOrHourOrMinute";
			public const string DayOrHourOrMinuteOrSecondWithLeadingPrecision = "DayOrHourOrMinuteOrSecondWithLeadingPrecision";
			public const string DayOrHourOrMinuteOrSecondWithPrecision = "DayOrHourOrMinuteOrSecondWithPrecision";
			public const string DefaultExpression = "DefaultExpression";
			public const string DefaultNamespace = "DefaultNamespace";
			public const string DefaultOrExpressionList = "DefaultOrExpressionList";
			public const string DefaultOrForce = "DefaultOrForce";
			public const string DefaultOrIdentifier = "DefaultOrIdentifier";
			public const string DeferrableState = "DeferrableState";
			public const string DeleteOrPreserve = "DeleteOrPreserve";
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
			public const string EditionableOrNonEditionable = "EditionableOrNonEditionable";
			public const string EnableOrDisable = "EnableOrDisable";
			public const string EncryptionSalt = "EncryptionSalt";
			public const string EntityEscapingOrNoEntityEscaping = "EntityEscapingOrNoEntityEscaping";
			public const string ErrorLoggingClause = "ErrorLoggingClause";
			public const string ErrorLoggingIntoObject = "ErrorLoggingIntoObject";
			public const string ErrorLoggingRejectLimit = "ErrorLoggingRejectLimit";
			public const string EscapeClause = "EscapeClause";
			public const string EvaluationEdition = "EvaluationEdition";
			public const string EvaluationEditionClause = "EvaluationEditionClause";
			public const string ExplainPlanStatement = "ExplainPlanStatement";
			public const string ExplicitPhysicalOrganization = "ExplicitPhysicalOrganization";
			public const string ExplicitPhysicalOrganizationType = "ExplicitPhysicalOrganizationType";
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
			public const string ExternalTableAccessDriverType = "ExternalTableAccessDriverType";
			public const string ExternalTableAccessParameters = "ExternalTableAccessParameters";
			public const string ExternalTableDataPropeties = "ExternalTableDataPropeties";
			public const string ExternalTableDirectoryLocation = "ExternalTableDirectoryLocation";
			public const string ExtractElement = "ExtractElement";
			public const string FirstOrAll = "FirstOrAll";
			public const string FirstOrLast = "FirstOrLast";
			public const string FirstOrNext = "FirstOrNext";
			public const string FlashbackArchiveClause = "FlashbackArchiveClause";
			public const string FlashbackAsOfClause = "FlashbackAsOfClause";
			public const string FlashbackMaximumValue = "FlashbackMaximumValue";
			public const string FlashbackMinimumValue = "FlashbackMinimumValue";
			public const string FlashbackPeriodFor = "FlashbackPeriodFor";
			public const string FlashbackQueryClause = "FlashbackQueryClause";
			public const string FlashbackVersionsClause = "FlashbackVersionsClause";
			public const string FlashCachePoolType = "FlashCachePoolType";
			public const string ForceTransactionIdentifier = "ForceTransactionIdentifier";
			public const string ForUpdateClause = "ForUpdateClause";
			public const string ForUpdateColumn = "ForUpdateColumn";
			public const string ForUpdateColumnChained = "ForUpdateColumnChained";
			public const string ForUpdateLockingClause = "ForUpdateLockingClause";
			public const string ForUpdateOfColumnsClause = "ForUpdateOfColumnsClause";
			public const string ForUpdateWaitClause = "ForUpdateWaitClause";
			public const string FromClause = "FromClause";
			public const string FromClauseChained = "FromClauseChained";
			public const string GeneratedAlways = "GeneratedAlways";
			public const string GlobalTemporary = "GlobalTemporary";
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
			public const string HeapTableSegmentAttributesClause = "HeapTableSegmentAttributesClause";
			public const string HierarchicalQueryClause = "HierarchicalQueryClause";
			public const string HierarchicalQueryConnectByClause = "HierarchicalQueryConnectByClause";
			public const string HierarchicalQueryStartWithClause = "HierarchicalQueryStartWithClause";
			public const string IdentifiedByClause = "IdentifiedByClause";
			public const string IdentifierList = "IdentifierList";
			public const string IdentifierOrParenthesisEnclosedIdentifierList = "IdentifierOrParenthesisEnclosedIdentifierList";
			public const string IdentityGeneratedAlwaysOrByDefault = "IdentityGeneratedAlwaysOrByDefault";
			public const string IdentityOption = "IdentityOption";
			public const string IdentityOptions = "IdentityOptions";
			public const string IgnoreNulls = "IgnoreNulls";
			public const string ImmediateOrBatch = "ImmediateOrBatch";
			public const string ImmediateOrDeferred = "ImmediateOrDeferred";
			public const string ImplicitHeapPhysicalOrganization = "ImplicitHeapPhysicalOrganization";
			public const string ImplicitHeapPhysicalOrganizationOrExplicitPhysicalOrganization = "ImplicitHeapPhysicalOrganizationOrExplicitPhysicalOrganization";
			public const string IncludeOrExclude = "IncludeOrExclude";
			public const string IndexAttribute = "IndexAttribute";
			public const string IndexAttributesOrPartitionedIndexList = "IndexAttributesOrPartitionedIndexList";
			public const string IndexCompression = "IndexCompression";
			public const string IndexCompressionPrefixOrAdvancedCompression = "IndexCompressionPrefixOrAdvancedCompression";
			public const string IndexingClause = "IndexingClause";
			public const string IndexOrganizedIncludingColumn = "IndexOrganizedIncludingColumn";
			public const string IndexOrganizedOverflowClause = "IndexOrganizedOverflowClause";
			public const string IndexOrganizedTableClause = "IndexOrganizedTableClause";
			public const string IndexOrganizedTableProperties = "IndexOrganizedTableProperties";
			public const string IndexOrganizedTableProperty = "IndexOrganizedTableProperty";
			public const string IndexProperties = "IndexProperties";
			public const string IndexTypeClause = "IndexTypeClause";
			public const string InitialDeferrableState = "InitialDeferrableState";
			public const string InlineConstraintType = "InlineConstraintType";
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
			public const string IntoPlanTable = "IntoPlanTable";
			public const string JoinClause = "JoinClause";
			public const string JoinColumnsOrCondition = "JoinColumnsOrCondition";
			public const string KeepClause = "KeepClause";
			public const string KeepOrNoKeep = "KeepOrNoKeep";
			public const string LargeObjectCacheOption = "LargeObjectCacheOption";
			public const string LargeObjectCompressOption = "LargeObjectCompressOption";
			public const string LargeObjectItemStorage = "LargeObjectItemStorage";
			public const string LargeObjectItemStorageProperties = "LargeObjectItemStorageProperties";
			public const string LargeObjectParameter = "LargeObjectParameter";
			public const string LargeObjectParameters = "LargeObjectParameters";
			public const string LargeObjectSegmentNameOrParenthesisEnclosedLargeObjectStorageParameters = "LargeObjectSegmentNameOrParenthesisEnclosedLargeObjectStorageParameters";
			public const string LargeObjectStorageClause = "LargeObjectStorageClause";
			public const string LargeObjectStorageParameters = "LargeObjectStorageParameters";
			public const string LargeObjectStorageProperty = "LargeObjectStorageProperty";
			public const string LargeObjectTableSpaceOrLargeObjectParameter = "LargeObjectTableSpaceOrLargeObjectParameter";
			public const string LargeObjectTableSpaceOrLargeObjectParameters = "LargeObjectTableSpaceOrLargeObjectParameters";
			public const string LikeOperator = "LikeOperator";
			public const string ListAggregationClause = "ListAggregationClause";
			public const string ListAggregationDelimiter = "ListAggregationDelimiter";
			public const string ListPartitionList = "ListPartitionList";
			public const string ListPartitions = "ListPartitions";
			public const string ListValuesClause = "ListValuesClause";
			public const string LocalOrGlobal = "LocalOrGlobal";
			public const string LocalPartitionedIndex = "LocalPartitionedIndex";
			public const string LocatorOrValue = "LocatorOrValue";
			public const string LoggingClause = "LoggingClause";
			public const string LogicalOperator = "LogicalOperator";
			public const string LowOrHigh = "LowOrHigh";
			public const string MandatoryAsXmlAlias = "MandatoryAsXmlAlias";
			public const string MathOperator = "MathOperator";
			public const string MaximumValueOrNoMaximumValue = "MaximumValueOrNoMaximumValue";
			public const string MergeDeleteClause = "MergeDeleteClause";
			public const string MergeInsertClause = "MergeInsertClause";
			public const string MergeInsertValuesExpressionOrOrDefaultValueList = "MergeInsertValuesExpressionOrOrDefaultValueList";
			public const string MergeSetColumnEqualsExpressionOrOrDefaultValueCommaChainedList = "MergeSetColumnEqualsExpressionOrOrDefaultValueCommaChainedList";
			public const string MergeSetColumnEqualsExpressionOrOrDefaultValueList = "MergeSetColumnEqualsExpressionOrOrDefaultValueList";
			public const string MergeSource = "MergeSource";
			public const string MergeStatement = "MergeStatement";
			public const string MergeTarget = "MergeTarget";
			public const string MergeUpdateClause = "MergeUpdateClause";
			public const string MergeUpdateInsertClause = "MergeUpdateInsertClause";
			public const string MinimumValueOrNoMinimumValue = "MinimumValueOrNoMinimumValue";
			public const string MultisetOperator = "MultisetOperator";
			public const string MultisetOperatorClause = "MultisetOperatorClause";
			public const string MultiTableInsert = "MultiTableInsert";
			public const string NamedExpressionOrEvaluatedNameExpression = "NamedExpressionOrEvaluatedNameExpression";
			public const string NamedExpressionOrEvaluatedNameExpressionChained = "NamedExpressionOrEvaluatedNameExpressionChained";
			public const string NamedInlineConstraintDefinition = "NamedInlineConstraintDefinition";
			public const string NamedOutOfLineConstraint = "NamedOutOfLineConstraint";
			public const string NaturalOrOuterJoinType = "NaturalOrOuterJoinType";
			public const string NestedQuery = "NestedQuery";
			public const string NestedStorageTableProperties = "NestedStorageTableProperties";
			public const string NestedStorageTableProperty = "NestedStorageTableProperty";
			public const string NestedTableColumnProperties = "NestedTableColumnProperties";
			public const string NestedTableReturnType = "NestedTableReturnType";
			public const string NoForce = "NoForce";
			public const string NoLog = "NoLog";
			public const string NullNaNOrInfinite = "NullNaNOrInfinite";
			public const string NullsClause = "NullsClause";
			public const string NumericOrDecimalOrDec = "NumericOrDecimalOrDec";
			public const string ObjectAttributeProperty = "ObjectAttributeProperty";
			public const string ObjectPrefix = "ObjectPrefix";
			public const string ObjectProperties = "ObjectProperties";
			public const string ObjectTable = "ObjectTable";
			public const string ObjectTableObjectIdentifierClause = "ObjectTableObjectIdentifierClause";
			public const string ObjectTableObjectIdentifierIndexClause = "ObjectTableObjectIdentifierIndexClause";
			public const string ObjectTableSubstitutionClause = "ObjectTableSubstitutionClause";
			public const string ObjectTypeColumnProperties = "ObjectTypeColumnProperties";
			public const string ObjectTypeNestedFunctionCallChained = "ObjectTypeNestedFunctionCallChained";
			public const string OnlyOrWithTies = "OnlyOrWithTies";
			public const string OnNull = "OnNull";
			public const string OnOrOff = "OnOrOff";
			public const string OpaqueFormatSpecification = "OpaqueFormatSpecification";
			public const string OpaqueFormatSpecificationOrUsingClobSubquery = "OpaqueFormatSpecificationOrUsingClobSubquery";
			public const string OptionalParameter = "OptionalParameter";
			public const string OptionalParameterExpressionCommaChainedList = "OptionalParameterExpressionCommaChainedList";
			public const string OptionalParameterExpressionList = "OptionalParameterExpressionList";
			public const string OrderByClause = "OrderByClause";
			public const string OrderExpression = "OrderExpression";
			public const string OrderExpressionChained = "OrderExpressionChained";
			public const string OrderExpressionType = "OrderExpressionType";
			public const string OrderOrNoOrder = "OrderOrNoOrder";
			public const string OrReplace = "OrReplace";
			public const string OuterJoinClause = "OuterJoinClause";
			public const string OuterJoinOld = "OuterJoinOld";
			public const string OuterJoinPartitionClause = "OuterJoinPartitionClause";
			public const string OuterJoinType = "OuterJoinType";
			public const string OuterJoinTypeWithKeyword = "OuterJoinTypeWithKeyword";
			public const string OutOfLineConstraint = "OutOfLineConstraint";
			public const string OutOfLineConstraintDefinition = "OutOfLineConstraintDefinition";
			public const string OverQueryPartitionClause = "OverQueryPartitionClause";
			public const string ParallelClause = "ParallelClause";
			public const string ParenthesisEnclosedAggregationFunctionParameters = "ParenthesisEnclosedAggregationFunctionParameters";
			public const string ParenthesisEnclosedExpression = "ParenthesisEnclosedExpression";
			public const string ParenthesisEnclosedExpressionList = "ParenthesisEnclosedExpressionList";
			public const string ParenthesisEnclosedExpressionListWithIgnoreNulls = "ParenthesisEnclosedExpressionListWithIgnoreNulls";
			public const string ParenthesisEnclosedExpressionListWithMandatoryExpressions = "ParenthesisEnclosedExpressionListWithMandatoryExpressions";
			public const string ParenthesisEnclosedExpressionOrOrDefaultValueList = "ParenthesisEnclosedExpressionOrOrDefaultValueList";
			public const string ParenthesisEnclosedFunctionParameters = "ParenthesisEnclosedFunctionParameters";
			public const string ParenthesisEnclosedGroupingExpressionList = "ParenthesisEnclosedGroupingExpressionList";
			public const string ParenthesisEnclosedIdentifierList = "ParenthesisEnclosedIdentifierList";
			public const string ParenthesisEnclosedIdentityOptions = "ParenthesisEnclosedIdentityOptions";
			public const string ParenthesisEnclosedLargeObjectStorageParameters = "ParenthesisEnclosedLargeObjectStorageParameters";
			public const string ParenthesisEnclosedMergeInsertValuesExpressionOrOrDefaultValueList = "ParenthesisEnclosedMergeInsertValuesExpressionOrOrDefaultValueList";
			public const string ParenthesisEnclosedNestedStorageTableProperties = "ParenthesisEnclosedNestedStorageTableProperties";
			public const string ParenthesisEnclosedObjectProperties = "ParenthesisEnclosedObjectProperties";
			public const string ParenthesisEnclosedPeriodStartEndColumns = "ParenthesisEnclosedPeriodStartEndColumns";
			public const string ParenthesisEnclosedRelationalProperties = "ParenthesisEnclosedRelationalProperties";
			public const string ParenthesisEnclosedStringOrIntegerLiteralList = "ParenthesisEnclosedStringOrIntegerLiteralList";
			public const string PartialOrFull = "PartialOrFull";
			public const string PartitionedIndex = "PartitionedIndex";
			public const string PartitionExtensionClause = "PartitionExtensionClause";
			public const string PartitionInterval = "PartitionInterval";
			public const string PartitionIntervalTablespaces = "PartitionIntervalTablespaces";
			public const string PartitionNameOrKeySet = "PartitionNameOrKeySet";
			public const string PartitionOrDatabaseLink = "PartitionOrDatabaseLink";
			public const string PartitionOrNoPartition = "PartitionOrNoPartition";
			public const string PeriodDefinition = "PeriodDefinition";
			public const string PhysicalAttribute = "PhysicalAttribute";
			public const string PhysicalAttributesClause = "PhysicalAttributesClause";
			public const string PhysicalAttributesClauseOrTableSpaceIdentifier = "PhysicalAttributesClauseOrTableSpaceIdentifier";
			public const string PhysicalAttributesClauseOrTableSpaceIdentifierList = "PhysicalAttributesClauseOrTableSpaceIdentifierList";
			public const string PhysicalProperties = "PhysicalProperties";
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
			public const string PrefixedAsterisk = "PrefixedAsterisk";
			public const string PrefixedColumnReference = "PrefixedColumnReference";
			public const string PreserveTable = "PreserveTable";
			public const string PurgeOption = "PurgeOption";
			public const string PurgeStatement = "PurgeStatement";
			public const string QueryBlock = "QueryBlock";
			public const string QueryOrArchive = "QueryOrArchive";
			public const string QueryPartitionClause = "QueryPartitionClause";
			public const string QueryTableExpression = "QueryTableExpression";
			public const string RangePartitionList = "RangePartitionList";
			public const string RangePartitions = "RangePartitions";
			public const string RangeValuesClause = "RangeValuesClause";
			public const string ReadOnlyOrCheckOption = "ReadOnlyOrCheckOption";
			public const string RelationalEquiOperator = "RelationalEquiOperator";
			public const string RelationalNonEquiOperator = "RelationalNonEquiOperator";
			public const string RelationalOperator = "RelationalOperator";
			public const string RelationalProperties = "RelationalProperties";
			public const string RelationalProperty = "RelationalProperty";
			public const string RelationalTable = "RelationalTable";
			public const string RelyOrNoRely = "RelyOrNoRely";
			public const string ResultCacheClause = "ResultCacheClause";
			public const string RetentionOption = "RetentionOption";
			public const string ReturningClause = "ReturningClause";
			public const string ReturningSequenceByRef = "ReturningSequenceByRef";
			public const string ReturnOrReturning = "ReturnOrReturning";
			public const string RollbackStatement = "RollbackStatement";
			public const string RollupCubeClause = "RollupCubeClause";
			public const string RowArchival = "RowArchival";
			public const string RowDependenciesOrNoRowDependencies = "RowDependenciesOrNoRowDependencies";
			public const string RowLevelLocking = "RowLevelLocking";
			public const string RowLimitingClause = "RowLimitingClause";
			public const string RowLimitingFetchClause = "RowLimitingFetchClause";
			public const string RowLimitingOffsetClause = "RowLimitingOffsetClause";
			public const string RowMovementClause = "RowMovementClause";
			public const string RowOrRows = "RowOrRows";
			public const string RowsOrRange = "RowsOrRange";
			public const string SampleClause = "SampleClause";
			public const string SavepointStatement = "SavepointStatement";
			public const string Scale = "Scale";
			public const string SchemaCheckOrNoSchemaCheck = "SchemaCheckOrNoSchemaCheck";
			public const string SchemaPrefix = "SchemaPrefix";
			public const string SearchedCaseExpressionBranch = "SearchedCaseExpressionBranch";
			public const string SeedClause = "SeedClause";
			public const string SegmentAttribute = "SegmentAttribute";
			public const string SegmentAttributeOrTableCompressionOrTableProperties = "SegmentAttributeOrTableCompressionOrTableProperties";
			public const string SegmentAttributesClause = "SegmentAttributesClause";
			public const string SegmentCreationClause = "SegmentCreationClause";
			public const string SelectExpressionExpressionChainedList = "SelectExpressionExpressionChainedList";
			public const string SelectList = "SelectList";
			public const string SelectStatement = "SelectStatement";
			public const string SequenceCacheOrNoCache = "SequenceCacheOrNoCache";
			public const string SequenceOption = "SequenceOption";
			public const string SequenceOptions = "SequenceOptions";
			public const string SerializableOrReadCommitted = "SerializableOrReadCommitted";
			public const string SessionOrGlobal = "SessionOrGlobal";
			public const string SetColumnEqualsExpressionOrNestedQueryOrDefaultValue = "SetColumnEqualsExpressionOrNestedQueryOrDefaultValue";
			public const string SetColumnListEqualsNestedQuery = "SetColumnListEqualsNestedQuery";
			public const string SetObjectValueEqualsExpressionOrNestedQuery = "SetObjectValueEqualsExpressionOrNestedQuery";
			public const string SetOperation = "SetOperation";
			public const string SetQualifier = "SetQualifier";
			public const string SetStatementId = "SetStatementId";
			public const string SetTransactionStatement = "SetTransactionStatement";
			public const string SimpleCaseExpressionBranch = "SimpleCaseExpressionBranch";
			public const string SimpleSchemaObject = "SimpleSchemaObject";
			public const string SingleTableInsert = "SingleTableInsert";
			public const string SingleTableInsertOrMultiTableInsert = "SingleTableInsertOrMultiTableInsert";
			public const string SizeClause = "SizeClause";
			public const string SizeClauseOrUnlimited = "SizeClauseOrUnlimited";
			public const string SortOrder = "SortOrder";
			public const string SortOrNoSort = "SortOrNoSort";
			public const string Statement = "Statement";
			public const string StorageClause = "StorageClause";
			public const string StorageParameter = "StorageParameter";
			public const string StorageParameterList = "StorageParameterList";
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
			public const string SubstitutableColumnClause = "SubstitutableColumnClause";
			public const string SubstitutableColumnClauseOrVariableElementArrayStorageClause = "SubstitutableColumnClauseOrVariableElementArrayStorageClause";
			public const string SupplementalLogColumnList = "SupplementalLogColumnList";
			public const string SupplementalLoggingProperties = "SupplementalLoggingProperties";
			public const string SupplementalLogGroupClauseOrSupplementalIdKeyClause = "SupplementalLogGroupClauseOrSupplementalIdKeyClause";
			public const string SupplementalLogKey = "SupplementalLogKey";
			public const string SupplementalLogKeyList = "SupplementalLogKeyList";
			public const string SystemChangeNumberOrTimestamp = "SystemChangeNumberOrTimestamp";
			public const string SystemChangeNumberOrTimestampOrPeriodFor = "SystemChangeNumberOrTimestampOrPeriodFor";
			public const string SystemGeneratedOrPrimaryKey = "SystemGeneratedOrPrimaryKey";
			public const string TableCollectionExpression = "TableCollectionExpression";
			public const string TableCollectionInnerExpression = "TableCollectionInnerExpression";
			public const string TableCompression = "TableCompression";
			public const string TableIndexClause = "TableIndexClause";
			public const string TableIndexExpressionList = "TableIndexExpressionList";
			public const string TableIndexOrBitmapJoinIndexClause = "TableIndexOrBitmapJoinIndexClause";
			public const string TablePartitionDescription = "TablePartitionDescription";
			public const string TablePartitioningClauses = "TablePartitioningClauses";
			public const string TablePartitioningType = "TablePartitioningType";
			public const string TableProperties = "TableProperties";
			public const string TableReference = "TableReference";
			public const string TemporaryTableOnCommitClause = "TemporaryTableOnCommitClause";
			public const string TimestampAtTimeZone = "TimestampAtTimeZone";
			public const string ToDayOrHourOrMinuteOrSecondWithPrecision = "ToDayOrHourOrMinuteOrSecondWithPrecision";
			public const string ToSavepointOrForceTransactionIdentifier = "ToSavepointOrForceTransactionIdentifier";
			public const string ToYearOrMonth = "ToYearOrMonth";
			public const string TransactionModeOrIsolationLevelOrRollbackSegment = "TransactionModeOrIsolationLevelOrRollbackSegment";
			public const string TransactionNameClause = "TransactionNameClause";
			public const string TransactionReadOnlyOrReadWrite = "TransactionReadOnlyOrReadWrite";
			public const string UniqueOrBitmap = "UniqueOrBitmap";
			public const string UnitPostfix = "UnitPostfix";
			public const string UnlimitedOrIntegerLiteral = "UnlimitedOrIntegerLiteral";
			public const string UnpivotClause = "UnpivotClause";
			public const string UnpivotInClause = "UnpivotInClause";
			public const string UnpivotNullsClause = "UnpivotNullsClause";
			public const string UnpivotValueSelector = "UnpivotValueSelector";
			public const string UnpivotValueToColumnTransformationList = "UnpivotValueToColumnTransformationList";
			public const string UnpivotValueToColumnTransformationListChained = "UnpivotValueToColumnTransformationListChained";
			public const string UnusableEditionClause = "UnusableEditionClause";
			public const string UpdateSetClause = "UpdateSetClause";
			public const string UpdateSetColumnOrColumnList = "UpdateSetColumnOrColumnList";
			public const string UpdateSetColumnOrColumnListChainedList = "UpdateSetColumnOrColumnListChainedList";
			public const string UpdateSetColumnsOrObjectValue = "UpdateSetColumnsOrObjectValue";
			public const string UpdateStatement = "UpdateStatement";
			public const string UsableOrUnusable = "UsableOrUnusable";
			public const string UserIdentifier = "UserIdentifier";
			public const string UsingEncryptionAlgorithm = "UsingEncryptionAlgorithm";
			public const string UsingIndexClause = "UsingIndexClause";
			public const string UsingIndexOption = "UsingIndexOption";
			public const string WaitOrNowait = "WaitOrNowait";
			public const string ValidateOrNoValidate = "ValidateOrNoValidate";
			public const string VariableElementArrayColumnProperties = "VariableElementArrayColumnProperties";
			public const string VariableElementArrayColumnPropertiesOrLargeObjectStorageClause = "VariableElementArrayColumnPropertiesOrLargeObjectStorageClause";
			public const string VariableElementArrayStorageClause = "VariableElementArrayStorageClause";
			public const string WhereClause = "WhereClause";
			public const string ViewBequeath = "ViewBequeath";
			public const string ViewEditioning = "ViewEditioning";
			public const string ViewInlineConstraintDefinition = "ViewInlineConstraintDefinition";
			public const string ViewRelationalProperties = "ViewRelationalProperties";
			public const string ViewRelationalProperty = "ViewRelationalProperty";
			public const string WindowingClause = "WindowingClause";
			public const string VirtualColumnDefinition = "VirtualColumnDefinition";
			public const string VisibleOrInvisible = "VisibleOrInvisible";
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
			public const string Advanced = "Advanced";
			public const string All = "All";
			public const string Alter = "Alter";
			public const string Always = "Always";
			public const string And = "And";
			public const string Any = "Any";
			public const string Apply = "Apply";
			public const string Archival = "Archival";
			public const string Archive = "Archive";
			public const string As = "As";
			public const string Asc = "Asc";
			public const string Asterisk = "Asterisk";
			public const string At = "At";
			public const string AtCharacter = "AtCharacter";
			public const string Auto = "Auto";
			public const string Avg = "Avg";
			public const string Basic = "Basic";
			public const string BasicFile = "BasicFile";
			public const string Batch = "Batch";
			public const string Before = "Before";
			public const string Beginning = "Beginning";
			public const string Bequeath = "Bequeath";
			public const string Between = "Between";
			public const string BindVariableIdentifier = "BindVariableIdentifier";
			public const string Bitmap = "Bitmap";
			public const string Block = "Block";
			public const string Body = "Body";
			public const string Breadth = "Breadth";
			public const string BufferPool = "BufferPool";
			public const string By = "By";
			public const string Byte = "Byte";
			public const string Cache = "Cache";
			public const string Cascade = "Cascade";
			public const string Case = "Case";
			public const string Cast = "Cast";
			public const string CellFlashCache = "CellFlashCache";
			public const string Char = "Char";
			public const string Character = "Character";
			public const string Check = "Check";
			public const string Chunk = "Chunk";
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
			public const string Creation = "Creation";
			public const string Cross = "Cross";
			public const string Cube = "Cube";
			public const string Current = "Current";
			public const string Cycle = "Cycle";
			public const string Data = "Data";
			public const string DatabaseLinkIdentifier = "DatabaseLinkIdentifier";
			public const string DataTypeIdentifier = "DataTypeIdentifier";
			public const string Date = "Date";
			public const string Day = "Day";
			public const string DbaRecycleBin = "DbaRecycleBin";
			public const string Dec = "Dec";
			public const string Decimal = "Decimal";
			public const string Decrypt = "Decrypt";
			public const string Deduplicate = "Deduplicate";
			public const string Default = "Default";
			public const string Deferrable = "Deferrable";
			public const string Deferred = "Deferred";
			public const string Delete = "Delete";
			public const string DenseRank = "DenseRank";
			public const string Depth = "Depth";
			public const string Desc = "Desc";
			public const string Disable = "Disable";
			public const string Distinct = "Distinct";
			public const string Document = "Document";
			public const string Dot = "Dot";
			public const string Double = "Double";
			public const string Drop = "Drop";
			public const string Edition = "Edition";
			public const string Editionable = "Editionable";
			public const string Editioning = "Editioning";
			public const string Element = "Element";
			public const string Else = "Else";
			public const string Empty = "Empty";
			public const string Enable = "Enable";
			public const string Encrypt = "Encrypt";
			public const string End = "End";
			public const string EntityEscaping = "EntityEscaping";
			public const string Errors = "Errors";
			public const string Escape = "Escape";
			public const string Evaluate = "Evaluate";
			public const string EvaluatedName = "EvaluatedName";
			public const string Exa = "Exa";
			public const string Except = "Except";
			public const string Exclude = "Exclude";
			public const string Exclusive = "Exclusive";
			public const string Exists = "Exists";
			public const string Explain = "Explain";
			public const string External = "External";
			public const string Extract = "Extract";
			public const string Fetch = "Fetch";
			public const string FileSystemLikeLogging = "FileSystemLikeLogging";
			public const string First = "First";
			public const string FirstValue = "FirstValue";
			public const string Flashback = "Flashback";
			public const string FlashCache = "FlashCache";
			public const string Float = "Float";
			public const string Following = "Following";
			public const string For = "For";
			public const string Force = "Force";
			public const string Foreign = "Foreign";
			public const string FreeList = "FreeList";
			public const string FreeLists = "FreeLists";
			public const string FreePools = "FreePools";
			public const string From = "From";
			public const string Full = "Full";
			public const string Function = "Function";
			public const string Generated = "Generated";
			public const string Giga = "Giga";
			public const string Global = "Global";
			public const string Grant = "Grant";
			public const string Group = "Group";
			public const string Grouping = "Grouping";
			public const string Groups = "Groups";
			public const string Having = "Having";
			public const string Heap = "Heap";
			public const string High = "High";
			public const string Hour = "Hour";
			public const string Identified = "Identified";
			public const string Identifier = "Identifier";
			public const string Identity = "Identity";
			public const string Ignore = "Ignore";
			public const string Immediate = "Immediate";
			public const string In = "In";
			public const string Include = "Include";
			public const string Including = "Including";
			public const string Increment = "Increment";
			public const string Index = "Index";
			public const string Indexing = "Indexing";
			public const string Initial = "Initial";
			public const string Initially = "Initially";
			public const string InitialTransactions = "InitialTransactions";
			public const string Inner = "Inner";
			public const string Insert = "Insert";
			public const string Int = "Int";
			public const string Integer = "Integer";
			public const string IntegerLiteral = "IntegerLiteral";
			public const string Intersect = "Intersect";
			public const string Interval = "Interval";
			public const string Into = "Into";
			public const string Invisible = "Invisible";
			public const string Is = "Is";
			public const string Isolation = "Isolation";
			public const string Join = "Join";
			public const string Keep = "Keep";
			public const string KeepDuplicates = "KeepDuplicates";
			public const string Key = "Key";
			public const string Kilo = "Kilo";
			public const string Lag = "Lag";
			public const string LargeObject = "LargeObject";
			public const string Last = "Last";
			public const string LastValue = "LastValue";
			public const string Lateral = "Lateral";
			public const string Lead = "Lead";
			public const string Left = "Left";
			public const string LeftParenthesis = "LeftParenthesis";
			public const string Less = "Less";
			public const string Level = "Level";
			public const string Levels = "Levels";
			public const string Like = "Like";
			public const string LikeUcs2 = "LikeUcs2";
			public const string LikeUcs4 = "LikeUcs4";
			public const string LikeUnicode = "LikeUnicode";
			public const string Limit = "Limit";
			public const string List = "List";
			public const string ListAggregation = "ListAggregation";
			public const string Local = "Local";
			public const string Locator = "Locator";
			public const string Lock = "Lock";
			public const string Locked = "Locked";
			public const string Locking = "Locking";
			public const string Log = "Log";
			public const string Logging = "Logging";
			public const string Long = "Long";
			public const string Low = "Low";
			public const string Mapping = "Mapping";
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
			public const string MaximumExtents = "MaximumExtents";
			public const string MaximumSize = "MaximumSize";
			public const string MaximumTransactions = "MaximumTransactions";
			public const string MaximumValue = "MaximumValue";
			public const string Medium = "Medium";
			public const string Mega = "Mega";
			public const string Member = "Member";
			public const string MemberFunctionIdentifier = "MemberFunctionIdentifier";
			public const string Merge = "Merge";
			public const string Min = "Min";
			public const string MinimumExtents = "MinimumExtents";
			public const string MinimumValue = "MinimumValue";
			public const string Minute = "Minute";
			public const string Mode = "Mode";
			public const string Model = "Model";
			public const string Month = "Month";
			public const string Movement = "Movement";
			public const string Multiset = "Multiset";
			public const string Name = "Name";
			public const string National = "National";
			public const string Natural = "Natural";
			public const string NChar = "NChar";
			public const string NegationOrNull = "NegationOrNull";
			public const string Nested = "Nested";
			public const string Next = "Next";
			public const string No = "No";
			public const string NoCache = "NoCache";
			public const string Nocompress = "Nocompress";
			public const string NoCompress = "NoCompress";
			public const string NoCycle = "NoCycle";
			public const string NoEntityEscaping = "NoEntityEscaping";
			public const string NoKeep = "NoKeep";
			public const string NoLogging = "NoLogging";
			public const string NoMapping = "NoMapping";
			public const string NoMaximumValue = "NoMaximumValue";
			public const string NoMinimumValue = "NoMinimumValue";
			public const string None = "None";
			public const string NonEditionable = "NonEditionable";
			public const string NoOrder = "NoOrder";
			public const string NoParallel = "NoParallel";
			public const string NoPartition = "NoPartition";
			public const string NoRely = "NoRely";
			public const string NoRowDependencies = "NoRowDependencies";
			public const string NoSchemaCheck = "NoSchemaCheck";
			public const string NoSort = "NoSort";
			public const string Not = "Not";
			public const string Nowait = "Nowait";
			public const string NoValidate = "NoValidate";
			public const string Null = "Null";
			public const string Nulls = "Nulls";
			public const string Number = "Number";
			public const string NumberLiteral = "NumberLiteral";
			public const string Numeric = "Numeric";
			public const string NVarchar = "NVarchar";
			public const string NVarchar2 = "NVarchar2";
			public const string Object = "Object";
			public const string ObjectAlias = "ObjectAlias";
			public const string ObjectIdentifier = "ObjectIdentifier";
			public const string ObjectIdentifierIndex = "ObjectIdentifierIndex";
			public const string Of = "Of";
			public const string Off = "Off";
			public const string Offset = "Offset";
			public const string Oltp = "Oltp";
			public const string On = "On";
			public const string Online = "Online";
			public const string Only = "Only";
			public const string OperatorConcatenation = "OperatorConcatenation";
			public const string Option = "Option";
			public const string OptionalParameterOperator = "OptionalParameterOperator";
			public const string Or = "Or";
			public const string Order = "Order";
			public const string Ordinality = "Ordinality";
			public const string Organization = "Organization";
			public const string Outer = "Outer";
			public const string Over = "Over";
			public const string Overflow = "Overflow";
			public const string Package = "Package";
			public const string Parallel = "Parallel";
			public const string ParameterIdentifier = "ParameterIdentifier";
			public const string Partition = "Partition";
			public const string Passing = "Passing";
			public const string Path = "Path";
			public const string Pctfree = "Pctfree";
			public const string Percent = "Percent";
			public const string PercentFree = "PercentFree";
			public const string PercentIncrease = "PercentIncrease";
			public const string PercentThreshold = "PercentThreshold";
			public const string PercentUsed = "PercentUsed";
			public const string PercentVersion = "PercentVersion";
			public const string Period = "Period";
			public const string Peta = "Peta";
			public const string Pivot = "Pivot";
			public const string Plan = "Plan";
			public const string Preceding = "Preceding";
			public const string Precision = "Precision";
			public const string Preserve = "Preserve";
			public const string Primary = "Primary";
			public const string Procedure = "Procedure";
			public const string Public = "Public";
			public const string Purge = "Purge";
			public const string Query = "Query";
			public const string Range = "Range";
			public const string Raw = "Raw";
			public const string Read = "Read";
			public const string Reads = "Reads";
			public const string Real = "Real";
			public const string Recycle = "Recycle";
			public const string RecycleBin = "RecycleBin";
			public const string Ref = "Ref";
			public const string References = "References";
			public const string Reject = "Reject";
			public const string Rely = "Rely";
			public const string Rename = "Rename";
			public const string Replace = "Replace";
			public const string Resource = "Resource";
			public const string ResultCache = "ResultCache";
			public const string Retention = "Retention";
			public const string Return = "Return";
			public const string Returning = "Returning";
			public const string Reverse = "Reverse";
			public const string Revoke = "Revoke";
			public const string Right = "Right";
			public const string RightParenthesis = "RightParenthesis";
			public const string Rollback = "Rollback";
			public const string Rollup = "Rollup";
			public const string Row = "Row";
			public const string RowDependencies = "RowDependencies";
			public const string RowIdPseudoColumn = "RowIdPseudoColumn";
			public const string RowNumberPseudoColumn = "RowNumberPseudoColumn";
			public const string Rows = "Rows";
			public const string Salt = "Salt";
			public const string Sample = "Sample";
			public const string Savepoint = "Savepoint";
			public const string SchemaCheck = "SchemaCheck";
			public const string SchemaIdentifier = "SchemaIdentifier";
			public const string Search = "Search";
			public const string Second = "Second";
			public const string SecureFile = "SecureFile";
			public const string Seed = "Seed";
			public const string Segment = "Segment";
			public const string Select = "Select";
			public const string Semicolon = "Semicolon";
			public const string Sequence = "Sequence";
			public const string Serializable = "Serializable";
			public const string Session = "Session";
			public const string Set = "Set";
			public const string SetMinus = "SetMinus";
			public const string Sets = "Sets";
			public const string Share = "Share";
			public const string Siblings = "Siblings";
			public const string Size = "Size";
			public const string Skip = "Skip";
			public const string Smallint = "Smallint";
			public const string Some = "Some";
			public const string Sort = "Sort";
			public const string Space = "Space";
			public const string Standalone = "Standalone";
			public const string StandardDeviation = "StandardDeviation";
			public const string Start = "Start";
			public const string StatementId = "StatementId";
			public const string Storage = "Storage";
			public const string Store = "Store";
			public const string StringLiteral = "StringLiteral";
			public const string SubMultiset = "SubMultiset";
			public const string Subpartition = "Subpartition";
			public const string Substitutable = "Substitutable";
			public const string Sum = "Sum";
			public const string Supplemental = "Supplemental";
			public const string Synonym = "Synonym";
			public const string System = "System";
			public const string SystemChangeNumber = "SystemChangeNumber";
			public const string SystemDate = "SystemDate";
			public const string Table = "Table";
			public const string TableSpace = "TableSpace";
			public const string Temporary = "Temporary";
			public const string Tera = "Tera";
			public const string Than = "Than";
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
			public const string Type = "Type";
			public const string Unbounded = "Unbounded";
			public const string Union = "Union";
			public const string Unique = "Unique";
			public const string UniversalRowId = "UniversalRowId";
			public const string Unlimited = "Unlimited";
			public const string Unpivot = "Unpivot";
			public const string Unusable = "Unusable";
			public const string Update = "Update";
			public const string Usable = "Usable";
			public const string Use = "Use";
			public const string User = "User";
			public const string Using = "Using";
			public const string Wait = "Wait";
			public const string Validate = "Validate";
			public const string Value = "Value";
			public const string Values = "Values";
			public const string Varchar = "Varchar";
			public const string Varchar2 = "Varchar2";
			public const string VariableElementArray = "VariableElementArray";
			public const string Variance = "Variance";
			public const string Varying = "Varying";
			public const string WellFormed = "WellFormed";
			public const string Version = "Version";
			public const string Versions = "Versions";
			public const string When = "When";
			public const string Where = "Where";
			public const string View = "View";
			public const string Virtual = "Virtual";
			public const string Visible = "Visible";
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

		private static readonly HashSet<string> AllTerminalsInternal = new HashSet<string> { Terminals.A, Terminals.Advanced, Terminals.All, Terminals.Alter, Terminals.Always, Terminals.And, Terminals.Any, Terminals.Apply, Terminals.Archival, Terminals.Archive, Terminals.As, Terminals.Asc, Terminals.Asterisk, Terminals.At, Terminals.AtCharacter, Terminals.Auto, Terminals.Avg, Terminals.Basic, Terminals.BasicFile, Terminals.Batch, Terminals.Before, Terminals.Beginning, Terminals.Bequeath, Terminals.Between, Terminals.BindVariableIdentifier, Terminals.Bitmap, Terminals.Block, Terminals.Body, Terminals.Breadth, Terminals.BufferPool, Terminals.By, Terminals.Byte, Terminals.Cache, Terminals.Cascade, Terminals.Case, Terminals.Cast, Terminals.CellFlashCache, Terminals.Char, Terminals.Character, Terminals.Check, Terminals.Chunk, Terminals.Cluster, Terminals.Colon, Terminals.Column, Terminals.ColumnAlias, Terminals.Columns, Terminals.Comma, Terminals.Comment, Terminals.Commit, Terminals.Committed, Terminals.Compress, Terminals.Connect, Terminals.Constraint, Terminals.Constraints, Terminals.Content, Terminals.Context, Terminals.Count, Terminals.Create, Terminals.Creation, Terminals.Cross, Terminals.Cube, Terminals.Current, Terminals.Cycle, Terminals.Data, Terminals.DatabaseLinkIdentifier, Terminals.DataTypeIdentifier, Terminals.Date, Terminals.Day, Terminals.DbaRecycleBin, Terminals.Dec, Terminals.Decimal, Terminals.Decrypt, Terminals.Deduplicate, Terminals.Default, Terminals.Deferrable, Terminals.Deferred, Terminals.Delete, Terminals.DenseRank, Terminals.Depth, Terminals.Desc, Terminals.Disable, Terminals.Distinct, Terminals.Document, Terminals.Dot, Terminals.Double, Terminals.Drop, Terminals.Edition, Terminals.Editionable, Terminals.Editioning, Terminals.Element, Terminals.Else, Terminals.Empty, Terminals.Enable, Terminals.Encrypt, Terminals.End, Terminals.EntityEscaping, Terminals.Errors, Terminals.Escape, Terminals.Evaluate, Terminals.EvaluatedName, Terminals.Exa, Terminals.Except, Terminals.Exclude, Terminals.Exclusive, Terminals.Exists, Terminals.Explain, Terminals.External, Terminals.Extract, Terminals.Fetch, Terminals.FileSystemLikeLogging, Terminals.First, Terminals.FirstValue, Terminals.Flashback, Terminals.FlashCache, Terminals.Float, Terminals.Following, Terminals.For, Terminals.Force, Terminals.Foreign, Terminals.FreeList, Terminals.FreeLists, Terminals.FreePools, Terminals.From, Terminals.Full, Terminals.Function, Terminals.Generated, Terminals.Giga, Terminals.Global, Terminals.Grant, Terminals.Group, Terminals.Grouping, Terminals.Groups, Terminals.Having, Terminals.Heap, Terminals.High, Terminals.Hour, Terminals.Identified, Terminals.Identifier, Terminals.Identity, Terminals.Ignore, Terminals.Immediate, Terminals.In, Terminals.Include, Terminals.Including, Terminals.Increment, Terminals.Index, Terminals.Indexing, Terminals.Initial, Terminals.Initially, Terminals.InitialTransactions, Terminals.Inner, Terminals.Insert, Terminals.Int, Terminals.Integer, Terminals.IntegerLiteral, Terminals.Intersect, Terminals.Interval, Terminals.Into, Terminals.Invisible, Terminals.Is, Terminals.Isolation, Terminals.Join, Terminals.Keep, Terminals.KeepDuplicates, Terminals.Key, Terminals.Kilo, Terminals.Lag, Terminals.LargeObject, Terminals.Last, Terminals.LastValue, Terminals.Lateral, Terminals.Lead, Terminals.Left, Terminals.LeftParenthesis, Terminals.Less, Terminals.Level, Terminals.Levels, Terminals.Like, Terminals.LikeUcs2, Terminals.LikeUcs4, Terminals.LikeUnicode, Terminals.Limit, Terminals.List, Terminals.ListAggregation, Terminals.Local, Terminals.Locator, Terminals.Lock, Terminals.Locked, Terminals.Locking, Terminals.Log, Terminals.Logging, Terminals.Long, Terminals.Low, Terminals.Mapping, Terminals.Matched, Terminals.Materialized, Terminals.MathDivide, Terminals.MathEquals, Terminals.MathFactor, Terminals.MathGreatherThan, Terminals.MathGreatherThanOrEquals, Terminals.MathInfinite, Terminals.MathLessThan, Terminals.MathLessThanOrEquals, Terminals.MathMinus, Terminals.MathNotANumber, Terminals.MathNotEqualsC, Terminals.MathNotEqualsCircumflex, Terminals.MathNotEqualsSql, Terminals.MathPlus, Terminals.Max, Terminals.MaximumExtents, Terminals.MaximumSize, Terminals.MaximumTransactions, Terminals.MaximumValue, Terminals.Medium, Terminals.Mega, Terminals.Member, Terminals.MemberFunctionIdentifier, Terminals.Merge, Terminals.Min, Terminals.MinimumExtents, Terminals.MinimumValue, Terminals.Minute, Terminals.Mode, Terminals.Model, Terminals.Month, Terminals.Movement, Terminals.Multiset, Terminals.Name, Terminals.National, Terminals.Natural, Terminals.NChar, Terminals.NegationOrNull, Terminals.Nested, Terminals.Next, Terminals.No, Terminals.NoCache, Terminals.Nocompress, Terminals.NoCompress, Terminals.NoCycle, Terminals.NoEntityEscaping, Terminals.NoKeep, Terminals.NoLogging, Terminals.NoMapping, Terminals.NoMaximumValue, Terminals.NoMinimumValue, Terminals.None, Terminals.NonEditionable, Terminals.NoOrder, Terminals.NoParallel, Terminals.NoPartition, Terminals.NoRely, Terminals.NoRowDependencies, Terminals.NoSchemaCheck, Terminals.NoSort, Terminals.Not, Terminals.Nowait, Terminals.NoValidate, Terminals.Null, Terminals.Nulls, Terminals.Number, Terminals.NumberLiteral, Terminals.Numeric, Terminals.NVarchar, Terminals.NVarchar2, Terminals.Object, Terminals.ObjectAlias, Terminals.ObjectIdentifier, Terminals.ObjectIdentifierIndex, Terminals.Of, Terminals.Off, Terminals.Offset, Terminals.Oltp, Terminals.On, Terminals.Online, Terminals.Only, Terminals.OperatorConcatenation, Terminals.Option, Terminals.OptionalParameterOperator, Terminals.Or, Terminals.Order, Terminals.Ordinality, Terminals.Organization, Terminals.Outer, Terminals.Over, Terminals.Overflow, Terminals.Package, Terminals.Parallel, Terminals.ParameterIdentifier, Terminals.Partition, Terminals.Passing, Terminals.Path, Terminals.Pctfree, Terminals.Percent, Terminals.PercentFree, Terminals.PercentIncrease, Terminals.PercentThreshold, Terminals.PercentUsed, Terminals.PercentVersion, Terminals.Period, Terminals.Peta, Terminals.Pivot, Terminals.Plan, Terminals.Preceding, Terminals.Precision, Terminals.Preserve, Terminals.Primary, Terminals.Procedure, Terminals.Public, Terminals.Purge, Terminals.Query, Terminals.Range, Terminals.Raw, Terminals.Read, Terminals.Reads, Terminals.Real, Terminals.Recycle, Terminals.RecycleBin, Terminals.Ref, Terminals.References, Terminals.Reject, Terminals.Rely, Terminals.Rename, Terminals.Replace, Terminals.Resource, Terminals.ResultCache, Terminals.Retention, Terminals.Return, Terminals.Returning, Terminals.Reverse, Terminals.Revoke, Terminals.Right, Terminals.RightParenthesis, Terminals.Rollback, Terminals.Rollup, Terminals.Row, Terminals.RowDependencies, Terminals.RowIdPseudoColumn, Terminals.RowNumberPseudoColumn, Terminals.Rows, Terminals.Salt, Terminals.Sample, Terminals.Savepoint, Terminals.SchemaCheck, Terminals.SchemaIdentifier, Terminals.Search, Terminals.Second, Terminals.SecureFile, Terminals.Seed, Terminals.Segment, Terminals.Select, Terminals.Semicolon, Terminals.Sequence, Terminals.Serializable, Terminals.Session, Terminals.Set, Terminals.SetMinus, Terminals.Sets, Terminals.Share, Terminals.Siblings, Terminals.Size, Terminals.Skip, Terminals.Smallint, Terminals.Some, Terminals.Sort, Terminals.Space, Terminals.Standalone, Terminals.StandardDeviation, Terminals.Start, Terminals.StatementId, Terminals.Storage, Terminals.Store, Terminals.StringLiteral, Terminals.SubMultiset, Terminals.Subpartition, Terminals.Substitutable, Terminals.Sum, Terminals.Supplemental, Terminals.Synonym, Terminals.System, Terminals.SystemChangeNumber, Terminals.SystemDate, Terminals.Table, Terminals.TableSpace, Terminals.Temporary, Terminals.Tera, Terminals.Than, Terminals.Then, Terminals.Ties, Terminals.Time, Terminals.Timestamp, Terminals.TimezoneAbbreviation, Terminals.TimezoneHour, Terminals.TimezoneMinute, Terminals.TimezoneRegion, Terminals.To, Terminals.Transaction, Terminals.Treat, Terminals.Trigger, Terminals.Type, Terminals.Unbounded, Terminals.Union, Terminals.Unique, Terminals.UniversalRowId, Terminals.Unlimited, Terminals.Unpivot, Terminals.Unusable, Terminals.Update, Terminals.Usable, Terminals.Use, Terminals.User, Terminals.Using, Terminals.Wait, Terminals.Validate, Terminals.Value, Terminals.Values, Terminals.Varchar, Terminals.Varchar2, Terminals.VariableElementArray, Terminals.Variance, Terminals.Varying, Terminals.WellFormed, Terminals.Version, Terminals.Versions, Terminals.When, Terminals.Where, Terminals.View, Terminals.Virtual, Terminals.Visible, Terminals.With, Terminals.Within, Terminals.Work, Terminals.Write, Terminals.Xml, Terminals.XmlAggregate, Terminals.XmlAlias, Terminals.XmlAttributes, Terminals.XmlColumnValue, Terminals.XmlElement, Terminals.XmlForest, Terminals.XmlNamespaces, Terminals.XmlParse, Terminals.XmlQuery, Terminals.XmlRoot, Terminals.XmlTable, Terminals.XmlType, Terminals.Year, Terminals.Yes, Terminals.Zone };
			
		private static readonly HashSet<string> IdentifiersInternal = new HashSet<string> { Terminals.BindVariableIdentifier, Terminals.DatabaseLinkIdentifier, Terminals.DataTypeIdentifier, Terminals.Identifier, Terminals.MemberFunctionIdentifier, Terminals.ObjectIdentifier, Terminals.ObjectIdentifierIndex, Terminals.ParameterIdentifier, Terminals.SchemaIdentifier };

		private static readonly HashSet<string> Keywords = new HashSet<string> { "ALL", "ALTER", "AND", "ANY", "ASC", "BETWEEN", "BY", "CHAR", "CHECK", "CLUSTER", "COLUMN", "COMMENT", "COMPRESS", "CONNECT", "CREATE", "CURRENT", "DATE", "DECIMAL", "DEFAULT", "DELETE", "DESC", "DISTINCT", "DROP", "ELSE", "EXCLUSIVE", "EXISTS", "FLOAT", "FOR", "FROM", "GRANT", "GROUP", "HAVING", "IDENTIFIED", "IMMEDIATE", "IN", "INDEX", "INSERT", "INTEGER", "INTERSECT", "INTO", "IS", "LEVEL", "LIKE", "LOCK", "LONG", "MINUS", "MODE", "NOCOMPRESS", "NOT", "NOWAIT", "NULL", "NUMBER", "OF", "ON", "OPTION", "OR", "ORDER", "PCTFREE", "PUBLIC", "RAW", "RENAME", "RESOURCE", "REVOKE", "ROW", "ROWID", "ROWNUM", "ROWS", "SELECT", "SET", "SHARE", "SIZE", "SMALLINT", "SOME", "START", "SYNONYM", "SYSDATE", "TABLE", "THEN", "TO", "TRIGGER", "UNION", "UNIQUE", "UPDATE", "USER", "VALUES", "VARCHAR", "VARCHAR2", "WHERE", "VIEW", "WITH" };
		
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
