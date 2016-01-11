using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		private static readonly Regex DateValidator = new Regex(@"^(?<Year>([+-]\s*)?[0-9]{1,4})\s*-\s*(?<Month>[0-9]{1,2})\s*-\s*(?<Day>[0-9]{1,2})\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		private static readonly Regex TimestampValidator = new Regex(@"^(?<Year>([+-]\s*)?[0-9]{1,4})\s*-\s*(?<Month>[0-9]{1,2})\s*-\s*(?<Day>[0-9]{1,2})\s*(?<Hour>[0-9]{1,2})\s*:\s*(?<Minute>[0-9]{1,2})\s*:\s*(?<Second>[0-9]{1,2})\s*(\.\s*(?<Fraction>[0-9]{1,9}))?\s*(((?<OffsetHour>[+-]\s*[0-9]{1,2})\s*:\s*(?<OffsetMinutes>[0-9]{1,2}))|(?<Timezone>[a-zA-Z]+))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		private static readonly Regex IntervalYearToMonthValidator = new Regex(@"^\s*(?<Years>([+-]\s*)?[0-9]{1,9})\s*([-]\s*(?<Months>[0-9]{1,2}))?\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		private static readonly Regex IntervalDayToSecondValidator = new Regex(@"^\s*(?<Days>([+-]\s*)?[0-9]{1,9})\s*(?<Hours>[0-9]{1,2})?\s*(:\s*(?<Minutes>[0-9]{1,2}))?\s*(:\s*(?<Seconds>[0-9]{1,2}))?\s*(\.\s*(?<Fraction>[0-9]{1,9}))?\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
		private static readonly Version MinimumJsonSupportVersion = new Version(12, 1, 0, 2);

		public IStatementSemanticModel BuildSemanticModel(string statementText, StatementBase statementBase, IDatabaseModel databaseModel)
		{
			return OracleStatementSemanticModelFactory.Build(statementText, (OracleStatement)statementBase, (OracleDatabaseModelBase)databaseModel);
		}

		public async Task<IStatementSemanticModel> BuildSemanticModelAsync(string statementText, StatementBase statementBase, IDatabaseModel databaseModel, CancellationToken cancellationToken)
		{
			return await OracleStatementSemanticModelFactory.BuildAsync(statementText, (OracleStatement)statementBase, (OracleDatabaseModelBase)databaseModel, cancellationToken);
		}

		public IValidationModel BuildValidationModel(IStatementSemanticModel semanticModel)
		{
			if (semanticModel == null)
			{
				throw new ArgumentNullException(nameof(semanticModel));
			}

			var oracleSemanticModel = (OracleStatementSemanticModel)semanticModel;

			var validationModel = new OracleValidationModel { SemanticModel = oracleSemanticModel };

			var databaseModel = semanticModel.HasDatabaseModel ? (OracleDatabaseModelBase)semanticModel.DatabaseModel : null;

			foreach (var referenceContainer in oracleSemanticModel.AllReferenceContainers)
			{
				ResolveContainerValidities(validationModel, referenceContainer);
			}

			foreach (var objectReference in oracleSemanticModel.AllReferenceContainers.SelectMany(c => c.ObjectReferences))
			{
				switch (objectReference.Type)
				{
					case ReferenceType.CommonTableExpression:
						validationModel.ObjectNodeValidity[objectReference.ObjectNode] = new NodeValidationData { IsRecognized = true };
						break;

					case ReferenceType.TableCollection:
						var tableCollectionReference = (OracleTableCollectionReference)objectReference;
						var tableCollectionProgramReference = tableCollectionReference.RowSourceReference as OracleProgramReference;
						var returnParameter = tableCollectionProgramReference?.Metadata?.ReturnParameter;
						if (returnParameter?.DataType.In(OracleTypeCollection.OracleCollectionTypeNestedTable, OracleTypeCollection.OracleCollectionTypeVarryingArray) == false)
						{
							validationModel.ProgramNodeValidity[tableCollectionProgramReference.ProgramIdentifierNode] = new InvalidNodeValidationData(OracleSemanticErrorType.FunctionReturningRowSetRequired) {Node = tableCollectionProgramReference.ProgramIdentifierNode};
						}

						var tableCollectionColumnReference = tableCollectionReference.RowSourceReference as OracleColumnReference;
						if (tableCollectionColumnReference != null && databaseModel != null && databaseModel.IsMetadataAvailable && tableCollectionColumnReference.ColumnDescription != null &&
						    !tableCollectionColumnReference.ColumnDescription.DataType.IsDynamicCollection && !String.IsNullOrEmpty(tableCollectionColumnReference.ColumnDescription.DataType.FullyQualifiedName.Name))
						{
							INodeValidationData validationData;
							var collectionType = databaseModel.GetFirstSchemaObject<OracleTypeCollection>(tableCollectionColumnReference.ColumnDescription.DataType.FullyQualifiedName);
							if (collectionType == null && validationModel.ColumnNodeValidity.TryGetValue(tableCollectionColumnReference.ColumnNode, out validationData) &&
							    validationData.IsRecognized && String.IsNullOrEmpty(validationData.SemanticErrorType))
							{
								validationModel.ColumnNodeValidity[tableCollectionColumnReference.ColumnNode] =
									new InvalidNodeValidationData(OracleSemanticErrorType.CannotAccessRowsFromNonNestedTableItem)
									{
										Node = tableCollectionColumnReference.ColumnNode
									};
							}
						}

						break;

					case ReferenceType.SchemaObject:
						if (objectReference.DatabaseLinkNode == null)
						{
							if (objectReference.OwnerNode != null)
							{
								var isRecognized = databaseModel != null && databaseModel.ExistsSchema(objectReference.OwnerNode.Token.Value);
								validationModel.ObjectNodeValidity[objectReference.OwnerNode] = new NodeValidationData { IsRecognized = isRecognized };
							}

							validationModel.ObjectNodeValidity[objectReference.ObjectNode] = new NodeValidationData { IsRecognized = objectReference.SchemaObject != null, Node = objectReference.ObjectNode };
						}
						else
						{
							ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, objectReference);
						}
						
						break;

					case ReferenceType.PivotTable:
						var pivotTableCollectionReference = (OraclePivotTableReference)objectReference;
						foreach (var aggregateFunctions in pivotTableCollectionReference.AggregateFunctions)
						{
							var aggregateExpression = aggregateFunctions[NonTerminals.Expression];
							if (aggregateExpression == null)
							{
								continue;
							}

							StatementGrammarNode unwrappedExpression;
							while ((unwrappedExpression = aggregateExpression[NonTerminals.ParenthesisEnclosedExpression, NonTerminals.Expression]) != null)
							{
								aggregateExpression = unwrappedExpression;
							}

							if (aggregateExpression[NonTerminals.ExpressionMathOperatorChainedList] != null || aggregateExpression[NonTerminals.AggregateFunctionCall] == null)
							{
								validationModel.InvalidNonTerminals[aggregateExpression] = new InvalidNodeValidationData(OracleSemanticErrorType.ExpectAggregateFunctionInsidePivotOperation) { Node = aggregateExpression };
							}
						}

						var unmatchedUnpivotDatatypeNodes = Enumerable.Empty<StatementGrammarNode>();
						if (pivotTableCollectionReference.AreUnpivotColumnSelectorValuesValid == false)
						{
							unmatchedUnpivotDatatypeNodes = pivotTableCollectionReference.UnpivotColumnSelectorValues;
						}

						if (pivotTableCollectionReference.AreUnpivotColumnSourceDataTypesMatched == false)
						{
							unmatchedUnpivotDatatypeNodes = unmatchedUnpivotDatatypeNodes.Concat(pivotTableCollectionReference.UnpivotColumnSources);
						}

						foreach (var selectorValue in unmatchedUnpivotDatatypeNodes)
						{
							validationModel.InvalidNonTerminals[selectorValue] = new InvalidNodeValidationData(OracleSemanticErrorType.ExpressionMustHaveSameDatatypeAsCorrespondingExpression) { Node = selectorValue };
						}

						break;

					case ReferenceType.JsonTable:
						if (databaseModel != null && databaseModel.Version < MinimumJsonSupportVersion)
						{
							validationModel.InvalidNonTerminals[objectReference.RootNode] = new InvalidNodeValidationData(OracleSemanticErrorType.UnsupportedInConnectedDatabaseVersion) { Node = objectReference.RootNode };
						}

						break;
				}

				if (objectReference.PartitionReference != null && objectReference.PartitionReference.Partition == null)
				{
					validationModel.ObjectNodeValidity[objectReference.PartitionReference.ObjectNode] = new NodeValidationData { Node = objectReference.PartitionReference.ObjectNode };
				}
			}

			ResolveSuspiciousConditions(validationModel);

			var invalidIdentifiers = oracleSemanticModel.Statement.AllTerminals
				.Select(GetInvalidIdentifierValidationData)
				.Where(nv => nv != null);

			foreach (var nodeValidity in invalidIdentifiers)
			{
				validationModel.IdentifierNodeValidity[nodeValidity.Node] = nodeValidity;
			}

			foreach (var insertTarget in oracleSemanticModel.InsertTargets)
			{
				var dataObjectReference = insertTarget.DataObjectReference;
				foreach (var columnReference in insertTarget.ColumnReferences)
				{
					if (columnReference.ColumnDescription != null && columnReference.ColumnDescription.Virtual)
					{
						validationModel.InvalidNonTerminals[columnReference.RootNode] =
							new InvalidNodeValidationData(OracleSemanticErrorType.InsertOperationDisallowedOnVirtualColumns) { Node = columnReference.RootNode };
					}
				}

				var dataSourceSpecified = insertTarget.RowSource != null || insertTarget.ValueList != null;
				if (dataObjectReference != null && dataSourceSpecified &&
				    (dataObjectReference.Type == ReferenceType.InlineView ||
				     validationModel.ObjectNodeValidity[dataObjectReference.ObjectNode].IsRecognized))
				{
					var insertColumnCount = insertTarget.Columns == null
						? dataObjectReference.Columns.Count(c => !c.Hidden)
						: insertTarget.Columns.Count;

					var rowSourceColumnCount = insertTarget.RowSource == null
						? insertTarget.ValueList.GetDescendantsWithinSameQueryBlock(NonTerminals.ExpressionOrDefaultValue).Count()
						: insertTarget.RowSource.Columns.Count(c => !c.IsAsterisk);

					if (insertColumnCount == rowSourceColumnCount)
					{
						continue;
					}

					if (insertTarget.ColumnListNode != null)
					{
						validationModel.InvalidNonTerminals[insertTarget.ColumnListNode] =
							new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = insertTarget.ColumnListNode };
					}

					var sourceDataNode = insertTarget.ValueList ?? insertTarget.RowSource.SelectList;
					if (sourceDataNode != null)
					{
						validationModel.InvalidNonTerminals[sourceDataNode] =
							new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = sourceDataNode };
					}
				}
			}

			ValidatePriorOperators(validationModel, validationModel.SemanticModel.NonQueryBlockTerminals);

			ValidateQueryBlocks(validationModel);

			ValidateLiterals(validationModel);

			ValidateVariousClauseSupport(validationModel);
			
			return validationModel;
		}

		private void ValidateVariousClauseSupport(OracleValidationModel validationModel)
		{
			if (validationModel.Statement.IsPlSql)
			{
				return;
			}

			foreach (var terminal in validationModel.Statement.AllTerminals)
			{
				switch (terminal.Id)
				{
					case Terminals.PlSqlCompilationParameter:
						validationModel.InvalidNonTerminals[terminal] =
							new InvalidNodeValidationData(OracleSemanticErrorType.PlSqlCompilationParameterAllowedOnlyWithinPlSqlScope) { Node = terminal };
						break;

					case Terminals.CursorIdentifier:
						validationModel.InvalidNonTerminals[terminal.ParentNode] =
							new InvalidNodeValidationData(OracleSemanticErrorType.CurrentOfConditionAllowedOnlyWithinPlSqlScope) { Node = terminal.ParentNode };
						break;
				}
			}
		}

		private void ResolveSuspiciousConditions(OracleValidationModel validationModel)
		{
			foreach (var container in validationModel.SemanticModel.AllReferenceContainers)
			{
				foreach (var column in container.ColumnReferences)
				{
					StatementGrammarNode expressionIsNullNaNOrInfiniteNode;
					if (!column.HasExplicitDefinition || column.ReferencesAllColumns || column.ColumnDescription == null || column.ColumnDescription.Nullable || column.RootNode == null ||
					    !String.Equals((expressionIsNullNaNOrInfiniteNode = column.RootNode.ParentNode.ParentNode).Id, NonTerminals.ExpressionIsNullNaNOrInfinite) ||
						expressionIsNullNaNOrInfiniteNode[NonTerminals.Expression, NonTerminals.ExpressionMathOperatorChainedList] != null)
					{
						continue;
					}

					var objectReference = column.ValidObjectReference as OracleDataObjectReference;
					if (objectReference?.IsOuterJoined == true)
					{
						continue;
					}

					var nullNaNOrInfiniteNode = expressionIsNullNaNOrInfiniteNode[NonTerminals.NullNaNOrInfinite];
					if (nullNaNOrInfiniteNode?[Terminals.Null] == null)
					{
						continue;
					}

					var suggestionType = expressionIsNullNaNOrInfiniteNode[Terminals.Not] == null
						? OracleSuggestionType.ExpressionIsAlwaysFalse
						: OracleSuggestionType.ExpressionIsAlwaysTrue;

					validationModel.InvalidNonTerminals[expressionIsNullNaNOrInfiniteNode] = new SuggestionData(suggestionType) { Node = expressionIsNullNaNOrInfiniteNode };
				}
			}
		}

		public async Task<ICollection<IReferenceDataSource>> ApplyReferenceConstraintsAsync(StatementExecutionResult executionResult, IDatabaseModel databaseModel, CancellationToken cancellationToken)
		{
			var semanticModel = (OracleStatementSemanticModel)executionResult.StatementModel.ValidationModel?.SemanticModel;
			if (semanticModel == null || executionResult.StatementModel.IsPartialStatement)
			{
				var statements = await OracleSqlParser.Instance.ParseAsync(executionResult.StatementModel.StatementText, cancellationToken);
				semanticModel = (OracleStatementSemanticModel)await BuildSemanticModelAsync(executionResult.StatementModel.StatementText, statements[0], databaseModel, cancellationToken);
			}

			var columnHeaders = executionResult.ResultInfoColumnHeaders.Values.Last();
			return semanticModel.ApplyReferenceConstraints(columnHeaders);
		}

		private static void ValidateLiterals(OracleValidationModel validationModel)
		{
			foreach (var literal in validationModel.SemanticModel.Literals)
			{
				if (literal.Type == LiteralType.Unknown)
				{
					continue;
				}

				ValidateLiteral(literal, validationModel);
			}
		}

		private static void ValidateLiteral(OracleLiteral literal, OracleValidationModel validationModel)
		{
			var value = literal.Terminal.Token.Value.ToPlainString();

			switch (literal.Type)
			{
				case LiteralType.Date:
					var match = DateValidator.Match(value);
					if (literal.IsMultibyte || !IsDateValid(match.Groups["Year"].Value, match.Groups["Month"].Value, match.Groups["Day"].Value, false))
					{
						validationModel.AddSemanticError(literal.Terminal, OracleSemanticErrorType.InvalidDateLiteral, OracleSemanticErrorTooltipText.InvalidDateLiteral);
					}
					break;
				case LiteralType.Timestamp:
					if (!IsTimestampValid(literal, value))
					{
						validationModel.AddSemanticError(literal.Terminal, OracleSemanticErrorType.InvalidTimestampLiteral, OracleSemanticErrorTooltipText.InvalidTimestampLiteral);
					}
					break;
				case LiteralType.IntervalYearToMonth:
					if (!IsIntervalYearToMonthValid(literal, value, validationModel))
					{
						validationModel.AddSemanticError(literal.Terminal, OracleSemanticErrorType.InvalidIntervalLiteral, OracleSemanticErrorTooltipText.InvalidIntervalYearToMonthLiteral);
					}
					break;
				case LiteralType.IntervalDayToSecond:
					if (!IsIntervalDayToSecondValid(literal, value, validationModel))
					{
						validationModel.AddSemanticError(literal.Terminal, OracleSemanticErrorType.InvalidIntervalLiteral, OracleSemanticErrorTooltipText.InvalidIntervalDayToSecondLiteral);
					}
					break;
				default:
					throw new NotSupportedException($"Literal '{literal.Type}' is not supported. ");
			}
		}

		private static bool IsTimestampValid(OracleLiteral literal, string value)
		{
			if (literal.IsMultibyte)
			{
				return false;
			}

			var match = TimestampValidator.Match(value);

			if (!match.Success || !IsDateValid(match.Groups["Year"].Value, match.Groups["Month"].Value, match.Groups["Day"].Value, true))
			{
				return false;
			}

			int hour;
			if (!Int32.TryParse(match.Groups["Hour"].Value, out hour) || hour < 0 || hour > 23)
			{
				return false;
			}

			if (!IsBetweenZeroAndFiftyNine(match.Groups["Minute"].Value) || !IsBetweenZeroAndFiftyNine(match.Groups["Second"].Value))
			{
				return false;
			}

			var hourOffsetGroup = match.Groups["HourOffset"];
			if (hourOffsetGroup.Success)
			{
				var hourOffset = Int32.Parse(hourOffsetGroup.Value.Replace(" ", null));
				if (hourOffset < -12 || hourOffset > 14)
				{
					return false;
				}

				var minuteOffset = Int32.Parse(match.Groups["MinuteOffset"].Value);
				if (minuteOffset < 0 || minuteOffset > 59 || (minuteOffset > 0 && hourOffset == 14))
				{
					return false;
				}
			}

			return true;
		}

		private static bool IsBetweenZeroAndFiftyNine(string stringValue)
		{
			int value;
			return Int32.TryParse(stringValue, out value) && value >= 0 && value < 60;
		}

		private void ValidateDataType(OracleValidationModel validationModel, OracleDataTypeReference dataTypeReference)
		{
			var varcharLimit = OracleDatabaseModelBase.DefaultMaxLengthVarchar;
			var nVarcharLimit = OracleDatabaseModelBase.DefaultMaxLengthNVarchar;
			var rawLimit = OracleDatabaseModelBase.DefaultMaxLengthRaw;
			if (validationModel.SemanticModel.HasDatabaseModel)
			{
				var databaseModel = validationModel.SemanticModel.DatabaseModel;
				varcharLimit = databaseModel.MaximumVarcharLength;
				nVarcharLimit = databaseModel.MaximumNVarcharLength;
				rawLimit = databaseModel.MaximumRawLength;
			}

			var dataType = dataTypeReference.ResolvedDataType;
			switch (dataType.FullyQualifiedName.Name)
			{
				case TerminalValues.Number:
					if (dataType.Precision > 38 || dataType.Precision < 1)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.NumericPrecisionSpecifierOutOfRange) { Node = dataTypeReference.PrecisionNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					if (dataType.Scale > 127 || dataType.Scale < -84)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.NumericScaleSpecifierOutOfRange) { Node = dataTypeReference.ScaleNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}
				
					break;

				case TerminalValues.Char:
					if (validationModel.SemanticModel.HasDatabaseModel && dataType.Length > validationModel.SemanticModel.DatabaseModel.MaximumRawLength)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.SpecifiedLengthTooLongForDatatype) { Node = dataTypeReference.LengthNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					goto default;

				case TerminalValues.Varchar:
				case TerminalValues.Varchar2:
					ValidateDataTypeMaximumLength(validationModel, dataTypeReference, varcharLimit);

					goto default;
				
				case TerminalValues.NVarchar:
				case TerminalValues.NVarchar2:
					ValidateDataTypeMaximumLength(validationModel, dataTypeReference, nVarcharLimit);

					goto default;

				case TerminalValues.NChar:
					if (dataType.Length > 1000)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.SpecifiedLengthTooLongForDatatype) { Node = dataTypeReference.LengthNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					goto default;

				case TerminalValues.Float:
					if (dataType.Precision > 126 || dataType.Precision < 1)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.FloatingPointPrecisionOutOfRange) { Node = dataTypeReference.PrecisionNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					break;

				case TerminalValues.Raw:
					ValidateDataTypeMaximumLength(validationModel, dataTypeReference, rawLimit);

					goto default;

				case TerminalValues.UniversalRowId:
					if (dataType.Length > 4000)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.SpecifiedLengthTooLongForDatatype) { Node = dataTypeReference.LengthNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					goto default;

				case TerminalValues.Timestamp:
				case OracleDatabaseModelBase.BuiltInDataTypeTimestampWithTimeZone:
				case OracleDatabaseModelBase.BuiltInDataTypeTimestampWithLocalTimeZone:
				case OracleDatabaseModelBase.BuiltInDataTypeIntervalYearToMonth:
				case OracleDatabaseModelBase.BuiltInDataTypeIntervalDayToSecond:
					if (dataType.Precision > 9 || dataType.Precision < 0)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange) { Node = dataTypeReference.PrecisionNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					if (dataType.Scale > 9 || dataType.Scale < 0)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange) { Node = dataTypeReference.ScaleNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					break;

				default:
					if (dataType.Length < 1)
					{
						var error = new InvalidNodeValidationData(OracleSemanticErrorType.ZeroLengthColumnsNotAllowed) { Node = dataTypeReference.LengthNode };
						validationModel.InvalidNonTerminals.Add(error.Node, error);
					}

					break;
			}
		}

		private static void ValidateDataTypeMaximumLength(OracleValidationModel validationModel, OracleDataTypeReference dataTypeReference, int maximumLength)
		{
			if (!(dataTypeReference.ResolvedDataType.Length > maximumLength))
			{
				return;
			}
			
			var error = new InvalidNodeValidationData(OracleSemanticErrorType.SpecifiedLengthTooLongForDatatype) { Node = dataTypeReference.LengthNode };
			validationModel.InvalidNonTerminals.Add(error.Node, error);
		}

		private static bool IsDateValid(string year, string month, string day, bool allowYearZero)
		{
			int yearValue;
			if (!Int32.TryParse(year.Replace(" ", null), out yearValue) || yearValue < -4712 || yearValue > 9999)
			{
				return false;
			}

			if (!allowYearZero && yearValue == 0)
			{
				return false;
			}

			int monthValue;
			if (!Int32.TryParse(month, out monthValue) || monthValue < 1 || monthValue > 12)
			{
				return false;
			}

			int dayValue;
			return Int32.TryParse(day, out dayValue) && dayValue >= 1 && dayValue <= (yearValue > 0 ? DateTime.DaysInMonth(yearValue, monthValue) : 31);
		}

		private static bool IsIntervalYearToMonthValid(OracleLiteral literal, string value, OracleValidationModel validationModel)
		{
			var match = IntervalYearToMonthValidator.Match(value);
			var result = match.Success;

			var years = match.Groups["Years"].Value;
			var months = match.Groups["Months"].Value;

			int yearValue;
			result &= Int32.TryParse(years.Replace(" ", null), out yearValue) && yearValue >= -999999999 && yearValue <= 999999999;

			var intervalYearToMonthNode = literal.Terminal.ParentNode[2, 0];
			var yearToMonthNode = intervalYearToMonthNode[2];

			if (yearToMonthNode == null)
			{
				result &= String.IsNullOrEmpty(months);
			}

			var precisionLiteral = intervalYearToMonthNode[NonTerminals.DataTypeSimplePrecision, Terminals.IntegerLiteral];
			int precision;
			if (precisionLiteral == null)
			{
				precision = 2;
			}
			else if (!Int32.TryParse(precisionLiteral.Token.Value, out precision) || precision > 9)
			{
				validationModel.AddSemanticError(precisionLiteral, OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
				precisionLiteral = null;
			}

			var monthValue = 0;
			result &= String.IsNullOrEmpty(months) || Int32.TryParse(months, out monthValue) && monthValue <= 11;

			var isMonthValue = String.Equals(intervalYearToMonthNode.FirstTerminalNode.Id, Terminals.Month);
			var totalYears = isMonthValue ? monthValue / 12m : yearValue;
			if (precisionLiteral != null && Math.Log10((double)totalYears) > precision)
			{
				validationModel.AddSemanticError(precisionLiteral, OracleSemanticErrorType.LeadingPrecisionOfTheIntervalIsTooSmall);
			}

			if (yearToMonthNode != null && isMonthValue && String.Equals(yearToMonthNode.LastTerminalNode.Id, Terminals.Year))
			{
				validationModel.AddSemanticError(yearToMonthNode.LastTerminalNode, OracleSemanticErrorType.InvalidIntervalLiteral);
			}

			return result;
		}

		private static bool IsIntervalDayToSecondValid(OracleLiteral literal, string value, OracleValidationModel validationModel)
		{
			var match = IntervalDayToSecondValidator.Match(value);
			var result = match.Success;

			var days = match.Groups["Days"].Value;
			var hours = match.Groups["Hours"].Value;
			var minutes = match.Groups["Minutes"].Value;
			var seconds = match.Groups["Seconds"].Value;
			var fraction = match.Groups["Fraction"].Value;

			int dayValue;
			result &= Int32.TryParse(days.Replace(" ", null), out dayValue) && dayValue >= -999999999 && dayValue <= 999999999;

			var intervalDayToSecond = literal.Terminal.ParentNode[2, 0];
			var dayOrHourOrMinuteOrSecondNode = intervalDayToSecond[NonTerminals.ToDayOrHourOrMinuteOrSecondWithPrecision, NonTerminals.DayOrHourOrMinuteOrSecondWithPrecision];
			var hasTwoElements = dayOrHourOrMinuteOrSecondNode != null;

			var hourValue = 0;
			var minuteValue = 0;
			var secondValue = 0;
			if (hasTwoElements)
			{
				result = String.IsNullOrEmpty(hours) || Int32.TryParse(hours, out hourValue) && hourValue <= 23;
				result &= String.IsNullOrEmpty(minutes) || Int32.TryParse(minutes, out minuteValue) && minuteValue <= 59;
				result &= String.IsNullOrEmpty(seconds) || Int32.TryParse(seconds, out secondValue) && secondValue <= 59;
				int fractionValue;
				result &= String.IsNullOrEmpty(fraction) || Int32.TryParse(fraction, out fractionValue) && fractionValue <= 999999999;
			}

			var secondPrecisionLiteral = dayOrHourOrMinuteOrSecondNode == null
				? intervalDayToSecond[NonTerminals.DayOrHourOrMinuteOrSecondWithLeadingPrecision, NonTerminals.DataTypeIntervalPrecisionAndScale, Terminals.IntegerLiteral]
				: dayOrHourOrMinuteOrSecondNode[NonTerminals.DataTypeSimplePrecision, Terminals.IntegerLiteral];

			int secondPrecision;
			if (secondPrecisionLiteral != null && (!Int32.TryParse(secondPrecisionLiteral.Token.Value, out secondPrecision) || secondPrecision > 9))
			{
				validationModel.AddSemanticError(secondPrecisionLiteral, OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			}

			var scaleLiteral = secondPrecisionLiteral != null && dayOrHourOrMinuteOrSecondNode == null
				? secondPrecisionLiteral.ParentNode[NonTerminals.Scale, NonTerminals.NegativeInteger]
				: null;

			if (scaleLiteral != null)
			{
				var minusTerminal = scaleLiteral[Terminals.MathMinus];
				var scaleValueLiteral = scaleLiteral[Terminals.IntegerLiteral];
				if (minusTerminal != null || (scaleValueLiteral != null && (!Int32.TryParse(scaleValueLiteral.Token.Value, out secondPrecision) || secondPrecision > 9)))
				{
					validationModel.AddSemanticError(scaleLiteral, OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
				}
			}

			var leadingPrecisionLiteral = intervalDayToSecond[NonTerminals.DayOrHourOrMinuteOrSecondWithLeadingPrecision, NonTerminals.DataTypeSimplePrecision, Terminals.IntegerLiteral];
			ulong leadingPrecision;
			if (leadingPrecisionLiteral == null)
			{
				leadingPrecision = 3;
			}
			else if (!UInt64.TryParse(leadingPrecisionLiteral.Token.Value, out leadingPrecision) || leadingPrecision > 9)
			{
				validationModel.AddSemanticError(leadingPrecisionLiteral, OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
				leadingPrecisionLiteral = null;
				leadingPrecision = 9;
			}

			var totalDays = 0m;
			var unitTerminalId = dayOrHourOrMinuteOrSecondNode?.FirstTerminalNode.Id;
			dayValue = Math.Abs(dayValue);
			switch (intervalDayToSecond.FirstTerminalNode.Id)
			{
				case Terminals.Day:
					totalDays = dayValue + hourValue / 24m + minuteValue / 1440m + secondValue / 86400m;
					break;
				case Terminals.Hour:
					totalDays = dayValue / 24m + hourValue / 1440m + minuteValue / 86400m;
					if (String.Equals(unitTerminalId, Terminals.Day))
					{
						validationModel.AddSemanticError(dayOrHourOrMinuteOrSecondNode.FirstTerminalNode, OracleSemanticErrorType.InvalidIntervalLiteral);
					}

					break;
				case Terminals.Minute:
					totalDays = dayValue / 1440m + hourValue / 86400m;
					if (String.Equals(unitTerminalId, Terminals.Day) || String.Equals(unitTerminalId, Terminals.Hour))
					{
						validationModel.AddSemanticError(dayOrHourOrMinuteOrSecondNode.FirstTerminalNode, OracleSemanticErrorType.InvalidIntervalLiteral);
					}

					break;
				case Terminals.Second:
					totalDays = dayValue / 86400m;
					if (unitTerminalId != null && !String.Equals(unitTerminalId, Terminals.Second))
					{
						validationModel.AddSemanticError(dayOrHourOrMinuteOrSecondNode.FirstTerminalNode, OracleSemanticErrorType.InvalidIntervalLiteral);
					}

					break;
			}

			if (leadingPrecisionLiteral != null && Math.Log10((double)totalDays) > leadingPrecision)
			{
				validationModel.AddSemanticError(leadingPrecisionLiteral, OracleSemanticErrorType.LeadingPrecisionOfTheIntervalIsTooSmall);
			}

			return result;
		}

		private static void ValidateQueryBlocks(OracleValidationModel validationModel)
		{
			foreach (var queryBlock in validationModel.SemanticModel.QueryBlocks)
			{
				if (queryBlock.HierarchicalQueryClause?[NonTerminals.HierarchicalQueryConnectByClause] == null)
				{
					ValidatePriorOperators(validationModel, queryBlock.Terminals);
				}

				ValidateConcatenatedQueryBlocks(validationModel, queryBlock);

				if (queryBlock.OrderByClause != null &&
				    (queryBlock.Type == QueryBlockType.ScalarSubquery || queryBlock.RootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.Condition) != null))
				{
					validationModel.InvalidNonTerminals[queryBlock.OrderByClause] = new InvalidNodeValidationData(OracleSemanticErrorType.ClauseNotAllowed) { Node = queryBlock.OrderByClause };
				}

				if (queryBlock.AsteriskColumns.Count > 0 && queryBlock.ObjectReferences.Any(r => r.DatabaseLinkNode != null))
				{
					foreach (var asteriskColumn in queryBlock.AsteriskColumns)
					{
						var columnNode = asteriskColumn.ColumnReferences.Single().ColumnNode;
						INodeValidationData validationData;
						if (!validationModel.ColumnNodeValidity.TryGetValue(columnNode, out validationData) || String.Equals(validationData.SemanticErrorType, OracleSemanticErrorType.None))
						{
							validationModel.ColumnNodeValidity[asteriskColumn.RootNode] = new SuggestionData(OracleSuggestionType.UseExplicitColumnList) { IsRecognized = true, Node = asteriskColumn.RootNode };
						}
					}
				}

				if (queryBlock.Type == QueryBlockType.CommonTableExpression)
				{
					if (queryBlock.ExplicitColumnNameList != null && queryBlock.ExplicitColumnNames != null)
					{
						var explicitColumnCount = queryBlock.ExplicitColumnNames.Count;
						if (explicitColumnCount > 0 && explicitColumnCount != queryBlock.Columns.Count - queryBlock.AsteriskColumns.Count - queryBlock.AttachedColumns.Count)
						{
							validationModel.InvalidNonTerminals[queryBlock.ExplicitColumnNameList] = new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = queryBlock.ExplicitColumnNameList };
							validationModel.InvalidNonTerminals[queryBlock.SelectList] = new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = queryBlock.SelectList };
						}
					}
					else
					{
						if (queryBlock.RecursiveSearchClause != null)
						{
							validationModel.InvalidNonTerminals[queryBlock.RecursiveSearchClause] = new InvalidNodeValidationData(OracleSemanticErrorType.MissingWithClauseColumnAliasList) { Node = queryBlock.RecursiveSearchClause };
						}

						if (queryBlock.RecursiveCycleClause != null)
						{
							validationModel.InvalidNonTerminals[queryBlock.RecursiveCycleClause] = new InvalidNodeValidationData(OracleSemanticErrorType.MissingWithClauseColumnAliasList) { Node = queryBlock.RecursiveCycleClause };
						}
					}

					if (queryBlock.RecursiveCycleClause != null)
					{
						var cycleMarkLiterals = queryBlock.RecursiveCycleClause.ChildNodes.Where(n => n.Id == NonTerminals.StringOrNumberLiteral);
						foreach (var cycleMarkLiteral in cycleMarkLiterals)
						{
							var isValid = false;
							if (cycleMarkLiteral.FirstTerminalNode.Id == Terminals.StringLiteral)
							{
								var value = cycleMarkLiteral.FirstTerminalNode.Token.Value.ToPlainString();
								isValid = value.Length == 1;
							}
							else
							{
								decimal value;
								if (Decimal.TryParse(cycleMarkLiteral.FirstTerminalNode.Token.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
								{
									isValid = value <= 9 && value == Math.Floor(value);
								}
							}

							if (!isValid)
							{
								validationModel.IdentifierNodeValidity[cycleMarkLiteral.FirstTerminalNode] = new InvalidNodeValidationData(OracleSemanticErrorType.InvalidCycleMarkValue) { Node = cycleMarkLiteral.FirstTerminalNode };
							}
						}
					}
				}
				else if (queryBlock.Type == QueryBlockType.ScalarSubquery && queryBlock.Columns.Count - queryBlock.AsteriskColumns.Count > 1)
				{
					validationModel.InvalidNonTerminals[queryBlock.SelectList] =
						new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = queryBlock.SelectList };
				}

				if (queryBlock.OrderByClause != null && (queryBlock.Type == QueryBlockType.CommonTableExpression || queryBlock.Type == QueryBlockType.Normal) &&
					queryBlock.ObjectReferences.All(o => o.Columns.Count > 0))
				{
					foreach (var invalidColumnIndexReference in queryBlock.OrderByColumnIndexReferences.Where(r => !r.IsValid))
					{
						validationModel.ColumnNodeValidity[invalidColumnIndexReference.Terminal] =
							new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnIndex) { Node = invalidColumnIndexReference.Terminal };
					}
				}

				var supportsIntoClause = queryBlock.Type == QueryBlockType.Normal && queryBlock.Statement.IsPlSql && queryBlock.RootNode.GetAncestor(NonTerminals.QueryBlock) == null;
				var selectIntoClause = queryBlock.RootNode[NonTerminals.IntoVariableClause];
				if (!supportsIntoClause && selectIntoClause != null)
				{
					validationModel.InvalidNonTerminals[selectIntoClause] =
						new InvalidNodeValidationData(OracleSemanticErrorType.SelectIntoClauseAllowedOnlyInMainQueryBlockWithinPlSqlScope) { Node = selectIntoClause };
				}

				var nestedQuery = queryBlock.RootNode.GetAncestor(NonTerminals.NestedQuery);
				var expressionSourceNode =
					nestedQuery.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.ExpressionListOrNestedQuery)
					?? nestedQuery.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.GroupingExpressionListOrNestedQuery);

				if (queryBlock.SelectList != null && expressionSourceNode != null)
				{
					var expressionListSourceNode = expressionSourceNode.ParentNode[0];
					var expressionList =
						expressionListSourceNode[NonTerminals.ExpressionList]
						?? expressionListSourceNode[NonTerminals.ParenthesisEnclosedExpressionListWithMandatoryExpressions, NonTerminals.ExpressionList];

					var queryBlockColumnCount = queryBlock.Columns.Count - queryBlock.AsteriskColumns.Count;
					if (expressionList != null)
					{
						var expressionCount = StatementGrammarNode.GetAllChainedClausesByPath(expressionList, null, NonTerminals.ExpressionCommaChainedList, NonTerminals.ExpressionList).Count();
						if (expressionCount != queryBlockColumnCount)
						{
							validationModel.InvalidNonTerminals[expressionList] =
								new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = expressionList };
							validationModel.InvalidNonTerminals[queryBlock.SelectList] =
								new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = queryBlock.SelectList };
						}
					}
					else if (expressionListSourceNode[NonTerminals.Expression] != null && queryBlockColumnCount > 1)
					{
						validationModel.InvalidNonTerminals[queryBlock.SelectList] =
							new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = queryBlock.SelectList };
					}
				}

				foreach (var joinDescription in queryBlock.JoinDescriptions)
				{
					if (joinDescription.MasterPartitionClause != null && joinDescription.SlavePartitionClause != null)
					{
						validationModel.InvalidNonTerminals[joinDescription.MasterPartitionClause] =
							new InvalidNodeValidationData(OracleSemanticErrorType.PartitionedTableOnBothSidesOfPartitionedOuterJoinNotSupported) { Node = joinDescription.MasterPartitionClause };
						validationModel.InvalidNonTerminals[joinDescription.SlavePartitionClause] =
							new InvalidNodeValidationData(OracleSemanticErrorType.PartitionedTableOnBothSidesOfPartitionedOuterJoinNotSupported) { Node = joinDescription.SlavePartitionClause };
					}
				}

				ValidateNestedAggregateAndAnalyticFunctions(queryBlock, validationModel);

				if (queryBlock.OuterCorrelatedQueryBlock != null)
				{
					foreach (var columnReference in queryBlock.AllColumnReferences)
					{
						INodeValidationData columnValidity;
						if (columnReference.ObjectNode != null || columnReference.ValidObjectReference?.Owner != queryBlock.OuterCorrelatedQueryBlock ||
							(validationModel.ColumnNodeValidity.TryGetValue(columnReference.ColumnNode, out columnValidity) && !String.IsNullOrEmpty(columnValidity.SemanticErrorType)))
						{
							continue;
						}

						validationModel.ColumnNodeValidity[columnReference.ColumnNode] =
							new SuggestionData(OracleSuggestionType.CorrelatedSubqueryColumnNotQualified)
							{
								IsRecognized = true,
								Node = columnReference.ColumnNode
							};
					}
				}
			}
		}

		private static void ValidateNestedAggregateAndAnalyticFunctions(OracleQueryBlock queryBlock, OracleValidationModel validationModel)
		{
			var analyticFunctionReferences = queryBlock.Columns
				.SelectMany(c => c.ProgramReferences)
				.Where(p => p.AnalyticClauseNode != null || p.Metadata?.IsAggregate == true)
				.ToArray();

			if (analyticFunctionReferences.Length == 0)
			{
				return;
			}

			var analyticFunctionExpressions = analyticFunctionReferences.Select(r => r.RootNode).ToHashSet();

			foreach (var programReference in analyticFunctionReferences)
			{
				var parentFunctionRootNode = GetParentAggregateOrAnalyticFunctionRootNode(programReference.RootNode);
				if (!analyticFunctionExpressions.Contains(parentFunctionRootNode))
				{
					continue;
				}

				string semanticError = null;
				if (analyticFunctionExpressions.Contains(GetParentAggregateOrAnalyticFunctionRootNode(parentFunctionRootNode)))
				{
					semanticError = OracleSemanticErrorType.GroupFunctionNestedTooDeeply;
				}
				else if (programReference.AnalyticClauseNode != null)
				{
					semanticError = OracleSemanticErrorType.WindowFunctionsNotAllowedHere;
				}

				if (semanticError != null)
				{
					validationModel.InvalidNonTerminals[programReference.RootNode] =
					   new InvalidNodeValidationData(semanticError) { Node = programReference.RootNode };
				}
			}

			var orderByColumnAliasReferences = queryBlock.ColumnReferences
				.Where(c => c.Placement == StatementPlacement.OrderBy && c.ValidObjectReference?.QueryBlocks.FirstOrDefault() == queryBlock);

			foreach (var columnAliasReference in orderByColumnAliasReferences)
			{
				var aggregateFunctionCallNode = columnAliasReference.RootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.OrderExpression), NonTerminals.AggregateFunctionCall);
				if (aggregateFunctionCallNode == null)
				{
					continue;
				}

				var selectColumn = queryBlock.NamedColumns[columnAliasReference.NormalizedName].First();
				var containsAggregateFunction = false;
				var containsAnalyticFunction = false;
				foreach (var functionReference in selectColumn.ProgramReferences)
				{
					var metadata = functionReference.Metadata;
					if (metadata == null)
					{
						continue;
					}

					if (metadata.IsAggregate)
					{
						containsAggregateFunction = true;
						break;
					}

					containsAnalyticFunction |= functionReference.AnalyticClauseNode != null;
				}

				string semanticError = null;
				if (containsAggregateFunction)
				{
					semanticError = OracleSemanticErrorType.GroupFunctionNestedTooDeeply;
				}
				else if (containsAnalyticFunction)
				{
					semanticError = OracleSemanticErrorType.NotSingleGroupGroupFunction;
				}

				if (semanticError != null)
				{
					validationModel.InvalidNonTerminals[aggregateFunctionCallNode] =
					   new InvalidNodeValidationData(semanticError) { Node = aggregateFunctionCallNode };
				}
			}
		}

		internal static StatementGrammarNode GetParentAggregateOrAnalyticFunctionRootNode(StatementGrammarNode programRootNode)
		{
			return programRootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.AliasedExpression), NonTerminals.AnalyticFunctionCall)
			       ?? programRootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.AliasedExpression), NonTerminals.AggregateFunctionCall);
		}

		private static void ValidateConcatenatedQueryBlocks(OracleValidationModel validationModel, OracleQueryBlock queryBlock)
		{
			var referenceColumnCount = queryBlock.Columns.Count - queryBlock.AsteriskColumns.Count - queryBlock.AttachedColumns.Count;
			var validityNode = queryBlock.SelectList;
			if (queryBlock.Type == QueryBlockType.CommonTableExpression && queryBlock.ExplicitColumnNameList != null && queryBlock.ExplicitColumnNames != null)
			{
				var explicitColumnCount = queryBlock.ExplicitColumnNames.Count;
				if (explicitColumnCount > 0)
				{
					referenceColumnCount = explicitColumnCount;
					validityNode = queryBlock.ExplicitColumnNameList;

					foreach (var names in queryBlock.ExplicitColumnNames.ToLookup(kvp => kvp.Value, kvp => kvp.Key))
					{
						var duplicateNameNodes = names.ToArray();
						if (duplicateNameNodes.Length > 1)
						{
							foreach (var duplicateNameNode in duplicateNameNodes)
							{
								validationModel.InvalidNonTerminals[duplicateNameNode] =
									new InvalidNodeValidationData(OracleSemanticErrorType.DuplicateNameFoundInColumnAliasListForWithClause) { Node = duplicateNameNode };
							}
						}
					}
				}
			}

			if (queryBlock.PrecedingConcatenatedQueryBlock != null || queryBlock.FollowingConcatenatedQueryBlock == null)
			{
				return;
			}

			foreach (var concatenatedQueryBlock in queryBlock.AllFollowingConcatenatedQueryBlocks)
			{
				var concatenatedQueryBlockColumnCount = concatenatedQueryBlock.Columns.Count - concatenatedQueryBlock.AsteriskColumns.Count;
				if (concatenatedQueryBlockColumnCount == referenceColumnCount || concatenatedQueryBlockColumnCount == 0)
				{
					continue;
				}

				validationModel.InvalidNonTerminals[validityNode] =
					new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = validityNode };

				if (String.Equals(validityNode.Id, NonTerminals.SelectList))
				{
					foreach (var invalidColumnCountQueryBlock in queryBlock.AllFollowingConcatenatedQueryBlocks)
					{
						validationModel.InvalidNonTerminals[invalidColumnCountQueryBlock.SelectList] =
							new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = invalidColumnCountQueryBlock.SelectList };
					}

					break;
				}
				
				validationModel.InvalidNonTerminals[concatenatedQueryBlock.SelectList] =
					new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) { Node = concatenatedQueryBlock.SelectList };
			}
		}

		private void ResolveContainerValidities(OracleValidationModel validationModel, OracleReferenceContainer referenceContainer)
		{
			ResolveColumnNodeValidities(validationModel, referenceContainer);

			foreach (var programReference in referenceContainer.ProgramReferences)
			{
				if (programReference.DatabaseLinkNode == null)
				{
					ValidateLocalProgramReference(programReference, validationModel);
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ProgramNodeValidity, programReference);
				}
			}

			foreach (var typeReference in referenceContainer.TypeReferences)
			{
				if (typeReference.DatabaseLinkNode == null)
				{
					var semanticError = GetCompilationError(typeReference);
					var node = typeReference.ObjectNode;
					var targetTypeObject = typeReference.SchemaObject.GetTargetSchemaObject() as OracleTypeObject;
					if (semanticError == OracleSemanticErrorType.None && targetTypeObject != null &&
						targetTypeObject.TypeCode == OracleTypeBase.TypeCodeObject && targetTypeObject.Attributes.Count != typeReference.ParameterReferences.Count)
					{
						semanticError = OracleSemanticErrorType.InvalidParameterCount;
						node = typeReference.ParameterListNode;
					}

					validationModel.ProgramNodeValidity[node] = new InvalidNodeValidationData(semanticError) { Node = node };
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ProgramNodeValidity, typeReference);
				}
			}

			foreach (var sequenceReference in referenceContainer.SequenceReferences)
			{
				if (sequenceReference.DatabaseLinkNode == null)
				{
					var mainQueryBlockOrderByClause = GetOrderByClauseIfWithinMainQueryBlock(sequenceReference);
					var isInQueryBlockWithUnsupportedClause = (sequenceReference.Owner?.HavingClause ?? sequenceReference.Owner?.GroupByClause) != null;
					var isWithinMainQueryBlockWithOrderByClause = mainQueryBlockOrderByClause != null;
					if (isWithinMainQueryBlockWithOrderByClause || isInQueryBlockWithUnsupportedClause || !sequenceReference.Placement.In(StatementPlacement.None, StatementPlacement.ValuesClause, StatementPlacement.SelectList) ||
					    IsNotWithinMainQueryBlock(sequenceReference) || sequenceReference.Owner?.HasDistinctResultSet == true)
					{
						validationModel.InvalidNonTerminals[sequenceReference.RootNode] =
							new InvalidNodeValidationData(OracleSemanticErrorType.SequenceNumberNotAllowedHere) { Node = sequenceReference.RootNode };

						if (isWithinMainQueryBlockWithOrderByClause)
						{
							validationModel.InvalidNonTerminals[mainQueryBlockOrderByClause] =
								new InvalidNodeValidationData(OracleSemanticErrorType.ClauseNotAllowed) { Node = mainQueryBlockOrderByClause };
						}
					}
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, sequenceReference);
				}
			}

			foreach (var dataTypeReference in referenceContainer.DataTypeReferences)
			{
				if (dataTypeReference.DatabaseLinkNode == null)
				{
					var dataTypeName = ((OracleToken)dataTypeReference.ObjectNode.Token).UpperInvariantValue;
					if (dataTypeReference.SchemaObject == null && (String.Equals(dataTypeReference.ObjectNode.Id, Terminals.DataTypeIdentifier) && !OracleDatabaseModelBase.BuiltInDataTypes.Contains(dataTypeName)))
					{
						validationModel.IdentifierNodeValidity[dataTypeReference.ObjectNode] =
							new NodeValidationData { Node = dataTypeReference.ObjectNode, IsRecognized = false };

						if (dataTypeReference.OwnerNode != null &&
						    !validationModel.SemanticModel.DatabaseModel.ExistsSchema(dataTypeReference.FullyQualifiedObjectName.NormalizedOwner))
						{
							validationModel.IdentifierNodeValidity[dataTypeReference.OwnerNode] =
								new NodeValidationData { Node = dataTypeReference.OwnerNode, IsRecognized = false };
						}
					}
					else
					{
						ValidateDataType(validationModel, dataTypeReference);
					}
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, dataTypeReference);
				}
			}

			foreach (var variableReference in referenceContainer.PlSqlVariableReferences)
			{
				if (variableReference.Variables.Count == 0)
				{
					validationModel.ObjectNodeValidity[variableReference.IdentifierNode] =
						new NodeValidationData { Node = variableReference.IdentifierNode };
				}
			}

			foreach (var exceptionReference in referenceContainer.PlSqlExceptionReferences)
			{
				if (exceptionReference.ObjectNode == null && String.Equals(exceptionReference.NormalizedName, "\"OTHERS\"") && !exceptionReference.Name.IsQuoted())
				{
					var isNotOnlyIdentifer = exceptionReference.RootNode.GetAncestor(NonTerminals.PlSqlExceptionHandler)
						?[NonTerminals.ExceptionIdentifierList, NonTerminals.ExceptionIdentifierListChained] != null;

					if (isNotOnlyIdentifer)
					{
						validationModel.InvalidNonTerminals[exceptionReference.IdentifierNode] =
							new InvalidNodeValidationData(OracleSemanticErrorType.NoChoicesMayAppearWithChoiceOthersInExceptionHandler) { Node = exceptionReference.IdentifierNode };
					}

					continue;
				}

				if (exceptionReference.Exceptions.Count == 0)
				{
					validationModel.IdentifierNodeValidity[exceptionReference.IdentifierNode] =
						new NodeValidationData { Node = exceptionReference.IdentifierNode };
				}
			}
		}

		private static void ValidatePriorOperators(OracleValidationModel validationModel, IEnumerable<StatementGrammarNode> terminals)
		{
			foreach (var terminal in terminals)
			{
				if (!String.Equals(terminal.Id, Terminals.Prior) && !String.Equals(terminal.Id, Terminals.ConnectByRoot))
				{
					continue;
				}

				validationModel.InvalidNonTerminals[terminal] =
					new InvalidNodeValidationData(OracleSemanticErrorType.ConnectByClauseRequired)
					{
						Node = terminal
					};
			}
		}

		private static bool IsNotWithinMainQueryBlock(OracleReference reference)
		{
			return reference.Owner != null && reference.Owner != reference.Owner.SemanticModel.MainQueryBlock;
		}

		private static StatementGrammarNode GetOrderByClauseIfWithinMainQueryBlock(OracleReference reference)
		{
			var queryBlock = reference.Owner;
			if (queryBlock == null || queryBlock != queryBlock.SemanticModel.MainQueryBlock)
			{
				return null;
			}
			
			if (queryBlock.OrderByClause != null)
			{
				return queryBlock.OrderByClause;
			}

			if (queryBlock.PrecedingConcatenatedQueryBlock == null)
			{
				return null;
			}

			var firstQueryBlock = queryBlock.AllPrecedingConcatenatedQueryBlocks.LastOrDefault();
			return firstQueryBlock?.OrderByClause;
		}

		private static void ValidateDatabaseLinkReference(IDictionary<StatementGrammarNode, INodeValidationData> nodeValidityDictionary, OracleReference databaseLinkReference)
		{
			var isRecognized = databaseLinkReference.DatabaseLink != null;
			foreach (var terminal in databaseLinkReference.DatabaseLinkNode.Terminals)
			{
				nodeValidityDictionary[terminal] = new InvalidNodeValidationData { IsRecognized = isRecognized, Node = terminal };	
			}
		}

		private void ValidateLocalProgramReference(OracleProgramReference programReference, OracleValidationModel validationModel)
		{
			var metadataFound = programReference.Metadata != null;
			var semanticError = OracleSemanticErrorType.None;
			var isRecognized = false;
			if (metadataFound)
			{
				isRecognized = true;
				if (programReference.ParameterListNode != null)
				{
					var isCollectionConstructor = programReference.SchemaObject is OracleTypeCollection;
					if (!isCollectionConstructor)
					{
						var maximumParameterCount = programReference.Metadata.MinimumArguments > 0 && programReference.Metadata.MaximumArguments == 0
							? Int32.MaxValue
							: programReference.Metadata.MaximumArguments;

						// TODO: Handle optional parameters
						var parameterListSemanticError = OracleSemanticErrorType.None;
						if ((programReference.ParameterReferences.Count < programReference.Metadata.MinimumArguments) ||
						    (programReference.ParameterReferences.Count > maximumParameterCount))
						{
							parameterListSemanticError = OracleSemanticErrorType.InvalidParameterCount;
						}
						else if (String.Equals(programReference.Metadata.DisplayType, OracleProgramMetadata.DisplayTypeNoParenthesis))
						{
							parameterListSemanticError = OracleSemanticErrorType.NonParenthesisFunction;
						}

						if (!String.Equals(parameterListSemanticError, OracleSemanticErrorType.None) && programReference.ParameterListNode.AllChildNodes.All(n => n.IsGrammarValid))
						{
							validationModel.ProgramNodeValidity[programReference.ParameterListNode] = new InvalidNodeValidationData(parameterListSemanticError) { Node = programReference.ParameterListNode };
						}

						if (programReference.Placement.In(StatementPlacement.GroupBy, StatementPlacement.Where, StatementPlacement.Join, StatementPlacement.ValuesClause, StatementPlacement.None) && (programReference.Metadata.IsAggregate || programReference.Metadata.IsAnalytic))
						{
							semanticError = OracleSemanticErrorType.GroupFunctionNotAllowed;
						}

						var namedParameterExists = false;
						foreach (var parameterReference in programReference.ParameterReferences)
						{
							if (parameterReference.OptionalIdentifierTerminal != null)
							{
								namedParameterExists = true;

								OracleProgramParameterMetadata parameterMetadata;
								if ((String.IsNullOrEmpty(programReference.Metadata.Identifier.Owner) || programReference.Metadata.Owner.FullyQualifiedName == OracleDatabaseModelBase.BuiltInFunctionPackageIdentifier) &&
									programReference.Metadata.Type != ProgramType.StatementFunction)
								{
									validationModel.IdentifierNodeValidity[parameterReference.OptionalIdentifierTerminal] =
										new InvalidNodeValidationData(OracleSemanticErrorType.NamedParameterNotAllowed) { Node = parameterReference.OptionalIdentifierTerminal };
								}
								else if (!programReference.Metadata.NamedParameters.TryGetValue(parameterReference.OptionalIdentifierTerminal.Token.Value.ToQuotedIdentifier(), out parameterMetadata))
								{
									validationModel.IdentifierNodeValidity[parameterReference.OptionalIdentifierTerminal] =
										new NodeValidationData { IsRecognized = false, Node = parameterReference.OptionalIdentifierTerminal };
								}
							}
							else if (namedParameterExists)
							{
								validationModel.InvalidNonTerminals[parameterReference.ParameterNode] =
									new InvalidNodeValidationData(OracleSemanticErrorType.PositionalParameterNotAllowed) { Node = parameterReference.ParameterNode };
							}
						}

						if (programReference.Metadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInProgramLnNvl && programReference.ParameterReferences.Count == 1)
						{
							var parameterNode = programReference.ParameterReferences[0].ParameterNode;
							if (parameterNode[NonTerminals.ChainedCondition] != null ||
							    parameterNode[Terminals.Between] != null ||
								!IsInClauseValidWithLnNvl(parameterNode) ||
							    (parameterNode[0] != null && String.Equals(parameterNode[0].Id, Terminals.LeftParenthesis)))
							{
								validationModel.InvalidNonTerminals[parameterNode] =
									new InvalidNodeValidationData(OracleSemanticErrorType.IncorrectUseOfLnNvlOperator) {Node = parameterNode};
							}
						}
						else if (programReference.Metadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInProgramExtract && programReference.ParameterReferences.Count == 1)
						{
							var extractElementNode = programReference.ParameterListNode[1];
							var fromTerminal = programReference.ParameterListNode[2];
							if (extractElementNode == null || !String.Equals(extractElementNode.Id, NonTerminals.ExtractElement) || fromTerminal == null || !String.Equals(fromTerminal.Id, Terminals.From))
							{
								var parameterNode = programReference.ParameterReferences[0].ParameterNode;
								validationModel.InvalidNonTerminals[parameterNode] =
									new InvalidNodeValidationData(OracleSemanticErrorType.NotEnoughArgumentsForFunction) { Node = parameterNode };
							}
						}
					}
				}
				else if (programReference.Metadata.MinimumArguments > 0)
				{
					semanticError = OracleSemanticErrorType.InvalidParameterCount;
				}
				else if (programReference.Metadata.DisplayType == OracleProgramMetadata.DisplayTypeParenthesis)
				{
					semanticError = OracleSemanticErrorType.MissingParenthesis;
				}

				if (programReference.AnalyticClauseNode != null)
				{
					if (!programReference.Metadata.IsAnalytic)
					{
						validationModel.ProgramNodeValidity[programReference.AnalyticClauseNode] = new InvalidNodeValidationData(OracleSemanticErrorType.AnalyticClauseNotSupported) { Node = programReference.AnalyticClauseNode };
					}

					var orderByClause = programReference.AnalyticClauseNode[NonTerminals.OrderByClause];
					if (programReference.Metadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInProgramRatioToReport && orderByClause != null)
					{
						validationModel.ProgramNodeValidity[orderByClause] = new InvalidNodeValidationData(OracleSemanticErrorType.OrderByNotAllowedHere) { Node = orderByClause };
					}
				}
			}

			if (programReference.ObjectNode != null)
			{
				var packageSemanticError = GetCompilationError(programReference);
				validationModel.ProgramNodeValidity[programReference.ObjectNode] = new InvalidNodeValidationData(packageSemanticError) { IsRecognized = programReference.SchemaObject != null, Node = programReference.ObjectNode };
			}

			var isRecognizedWithoutError = semanticError == OracleSemanticErrorType.None && isRecognized;
			if (isRecognizedWithoutError)
			{
				if (!programReference.Metadata.IsPackageFunction && programReference.SchemaObject != null && !programReference.SchemaObject.IsValid)
				{
					semanticError = OracleSemanticErrorType.ObjectStatusInvalid;
				}
				else
				{
					var isLevel = programReference.Metadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInProgramLevel;
					if (isLevel ||
					    programReference.Metadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInProgramSysConnectByPath)
					{
						if (programReference.Owner?.HierarchicalQueryClause?[NonTerminals.HierarchicalQueryConnectByClause] == null)
						{
							validationModel.ProgramNodeValidity[programReference.RootNode] =
								new InvalidNodeValidationData(OracleSemanticErrorType.ConnectByClauseRequired)
								{
									Node = programReference.RootNode
								};

							if (isLevel)
							{
								return;
							}
						}

						if (isLevel && ValidateIdentifierNodeOnly(programReference, validationModel)) return;
					}
					else if (programReference.Metadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInProgramRowNum)
					{
						if (ValidateIdentifierNodeOnly(programReference, validationModel)) return;
					}
				}
			}

			if (!validationModel.ProgramNodeValidity.ContainsKey(programReference.ProgramIdentifierNode))
			{
				validationModel.ProgramNodeValidity[programReference.ProgramIdentifierNode] = new InvalidNodeValidationData(semanticError) { IsRecognized = isRecognized, Node = programReference.ProgramIdentifierNode };
			}
		}

		private static bool ValidateIdentifierNodeOnly(OracleReference programReference, OracleValidationModel validationModel)
		{
			if (programReference.ObjectNode == null)
			{
				return false;
			}

			validationModel.InvalidNonTerminals[programReference.RootNode] =
				new InvalidNodeValidationData(OracleSemanticErrorTooltipText.FunctionOrPseudocolumnMayBeUsedInsideSqlStatementOnly)
				{
					Node = programReference.RootNode
				};

			return true;
		}

		private static bool IsInClauseValidWithLnNvl(StatementGrammarNode parameterNode)
		{
			if (parameterNode[Terminals.In] == null)
			{
				return true;
			}
			
			var parameters = parameterNode[NonTerminals.ExpressionListOrNestedQuery];
		    var expressionList = parameters?[NonTerminals.ExpressionList];
			return expressionList == null || !expressionList.GetDescendants(NonTerminals.ExpressionCommaChainedList).Any();
		}

		private static string GetCompilationError(OracleReference reference)
		{
			return reference.SchemaObject == null || reference.SchemaObject.IsValid
				? OracleSemanticErrorType.None
				: OracleSemanticErrorType.ObjectStatusInvalid;
		}

		private static INodeValidationData GetInvalidIdentifierValidationData(StatementGrammarNode node)
		{
			if (!node.Id.IsIdentifierOrAlias())
				return null;

			var validationResult = ValidateIdentifier(node.Token.Value, node.Id == Terminals.BindVariableIdentifier);
			string errorMessage;
			if (node.Id == Terminals.XmlAlias)
			{
				errorMessage = validationResult.IsEmptyQuotedIdentifier
					? "XML alias length must be at least one character excluding quotes. "
					: null;
			}
			else
			{
				errorMessage = validationResult.ErrorMessage;
			}

			return String.IsNullOrEmpty(errorMessage)
				? null
				: new SemanticErrorNodeValidationData(OracleSemanticErrorType.InvalidIdentifier, errorMessage) { Node = node };
		}

		public static bool IsValidBindVariableIdentifier(string identifier)
		{
			var validationResult = ValidateIdentifier(identifier, true);
			return validationResult.IsValid && (validationResult.IsNumericBindVariable || OracleSqlParser.IsValidIdentifier(identifier)) && !identifier.IsReservedWord();
		}

		private static IdentifierValidationResult ValidateIdentifier(string identifier, bool validateNumericBindVariable)
		{
			var trimmedIdentifier = identifier.Trim('"');
			var result = new IdentifierValidationResult();

			if (validateNumericBindVariable && trimmedIdentifier == identifier)
			{
				result.IsNumericBindVariable = trimmedIdentifier.All(Char.IsDigit);

				int bindVariableNumberIdentifier;
				if (result.IsNumericBindVariable && Int32.TryParse(trimmedIdentifier.Substring(0, trimmedIdentifier.Length > 5 ? 5 : trimmedIdentifier.Length), out bindVariableNumberIdentifier) && bindVariableNumberIdentifier > 65535)
				{
					result.ErrorMessage = "Numeric bind variable identifier must be between 0 and 65535. ";
				}
			}

			result.IsEmptyQuotedIdentifier = trimmedIdentifier.Length == 0;
			if (String.IsNullOrEmpty(result.ErrorMessage) && result.IsEmptyQuotedIdentifier || trimmedIdentifier.Length > 30)
			{
				result.ErrorMessage = "Identifier length must be between one and 30 characters excluding quotes. ";
			}

			return result;
		}

		private static void ResolveColumnNodeValidities(OracleValidationModel validationModel, OracleReferenceContainer referenceContainer)
		{
			if (referenceContainer.ColumnReferences.Count == 0)
			{
				return;
			}

			var firstReference = referenceContainer.ColumnReferences[0];
			var hasRemoteAsteriskReferences = firstReference.Owner != null && firstReference.Owner.HasRemoteAsteriskReferences;

			foreach (var columnReference in referenceContainer.ColumnReferences.Where(columnReference => columnReference.SelectListColumn == null || columnReference.SelectListColumn.HasExplicitDefinition))
			{
				var isAsterisk = columnReference.ReferencesAllColumns;
				var sourceObjectReferences = columnReference.SelectListColumn == null
						? referenceContainer.ObjectReferences
						: columnReference.SelectListColumn.Owner.ObjectReferences;

				var databaseLinkReferenceCount = sourceObjectReferences.Count(r => r.DatabaseLinkNode != null);

				if (columnReference.ValidObjectReference?.DatabaseLinkNode == null)
				{
					// Schema
					if (columnReference.OwnerNode != null)
					{
						validationModel.ObjectNodeValidity[columnReference.OwnerNode] =
							new NodeValidationData(columnReference.ObjectNodeObjectReferences)
							{
								IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0,
								Node = columnReference.OwnerNode
							};
					}

					// Object
					if (columnReference.ObjectNode != null)
					{
						validationModel.ObjectNodeValidity[columnReference.ObjectNode] =
							new NodeValidationData(columnReference.ObjectNodeObjectReferences)
							{
								IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0,
								Node = columnReference.ObjectNode
							};
					}

					// Column
					var isColumnRecognized = isAsterisk || columnReference.ColumnNodeObjectReferences.Count > 0;
					if (isColumnRecognized || databaseLinkReferenceCount == 0)
					{
						validationModel.ColumnNodeValidity[columnReference.ColumnNode] =
							new ColumnNodeValidationData(columnReference)
							{
								IsRecognized = isColumnRecognized || hasRemoteAsteriskReferences,
								Node = columnReference.ColumnNode
							};
					}
					else if (databaseLinkReferenceCount > 0 && sourceObjectReferences.Count > 1)
					{
						ResolveDatabaseLinkQualifierSuggestion(validationModel, columnReference, isAsterisk);
					}

					if (columnReference.ObjectNode == null && !columnReference.Name.IsQuoted())
					{
						var isConnectByIsCyclePseudocolumn = String.Equals(columnReference.NormalizedName, OracleHierarchicalClauseReference.ColumnNameConnectByIsCycle);
						if (String.Equals(columnReference.NormalizedName, OracleHierarchicalClauseReference.ColumnNameConnectByIsLeaf) || isConnectByIsCyclePseudocolumn)
						{
							var hierarchicalClauseReference = columnReference.Owner?.HierarchicalClauseReference;
							if (hierarchicalClauseReference == null)
							{
								validationModel.InvalidNonTerminals[columnReference.RootNode] =
									new InvalidNodeValidationData(OracleSemanticErrorType.ConnectByClauseRequired)
									{
										Node = columnReference.RootNode
									};
							}
							else if (isConnectByIsCyclePseudocolumn && !columnReference.Owner.HierarchicalClauseReference.HasNoCycleSupport)
							{
								validationModel.InvalidNonTerminals[columnReference.RootNode] =
									new InvalidNodeValidationData(OracleSemanticErrorType.NoCycleKeywordRequiredWithConnectByIsCyclePseudocolumn)
									{
										Node = columnReference.RootNode
									};
							}
						}
					}
				}
				else if (databaseLinkReferenceCount > 1)
				{
					ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, columnReference.ValidObjectReference);
					ResolveDatabaseLinkQualifierSuggestion(validationModel, columnReference, isAsterisk);
				}
			}
		}

		private static void ResolveDatabaseLinkQualifierSuggestion(OracleValidationModel validationModel, OracleColumnReference columnReference, bool isAsterisk)
		{
			if (columnReference.ObjectNode == null && !isAsterisk)
			{
				validationModel.ColumnNodeValidity[columnReference.ColumnNode] =
					new SuggestionData(OracleSuggestionType.PotentialDatabaseLink)
					{
						IsRecognized = true,
						Node = columnReference.ColumnNode
					};
			}
		}

		private struct IdentifierValidationResult
		{
			public bool IsValid => String.IsNullOrEmpty(ErrorMessage);

			public string ErrorMessage { get; set; }

			public bool IsNumericBindVariable { get; set; }

			public bool IsEmptyQuotedIdentifier { get; set; }
		}
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _objectNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _columnNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _programNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _identifierNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _invalidNonTerminals = new Dictionary<StatementGrammarNode, INodeValidationData>();

		IStatementSemanticModel IValidationModel.SemanticModel => SemanticModel;

		public OracleStatementSemanticModel SemanticModel { get; set; }

		StatementBase IValidationModel.Statement => Statement;

		public OracleStatement Statement => SemanticModel.Statement;

		public IDictionary<StatementGrammarNode, INodeValidationData> ObjectNodeValidity => _objectNodeValidity;

		public IDictionary<StatementGrammarNode, INodeValidationData> ColumnNodeValidity => _columnNodeValidity;

		public IDictionary<StatementGrammarNode, INodeValidationData> ProgramNodeValidity => _programNodeValidity;

		public IDictionary<StatementGrammarNode, INodeValidationData> IdentifierNodeValidity => _identifierNodeValidity;

		public IDictionary<StatementGrammarNode, INodeValidationData> InvalidNonTerminals => _invalidNonTerminals;

		public IEnumerable<INodeValidationData> AllNodes
		{
			get
			{
				return _columnNodeValidity
					.Concat(_objectNodeValidity)
					.Concat(_programNodeValidity)
					.Concat(_identifierNodeValidity)
					.Concat(_invalidNonTerminals)
					.Select(nv => nv.Value);
			}
		}

		public IEnumerable<INodeValidationData> Errors
		{
			get { return AllNodes.Where(nv => !nv.IsRecognized || nv.SemanticErrorType != OracleSemanticErrorType.None); }
		}

		public IEnumerable<INodeValidationData> SemanticErrors
		{
			get { return AllNodes.Where(nv => nv.SemanticErrorType != OracleSemanticErrorType.None); }
		}

		public IEnumerable<INodeValidationData> Suggestions
		{
			get
			{
				return _columnNodeValidity.Concat(_invalidNonTerminals)
					.Select(nv => nv.Value)
					.Where(v => v.SuggestionType != OracleSuggestionType.None);
			}
		}

		public void AddSemanticError(StatementGrammarNode node, string errorType, string tooltipText = null)
		{
			var validationData = new SemanticErrorNodeValidationData(errorType, tooltipText ?? errorType) { Node = node };
			IdentifierNodeValidity[node] = validationData;
		}
	}

	public class NodeValidationData : INodeValidationData
	{
		private readonly HashSet<OracleObjectWithColumnsReference> _objectReferences;

		public NodeValidationData(OracleDataObjectReference objectReference) : this(Enumerable.Repeat(objectReference, 1))
		{
		}

		public NodeValidationData(IEnumerable<OracleObjectWithColumnsReference> objectReferences = null)
		{
			_objectReferences = (objectReferences ?? Enumerable.Empty<OracleObjectWithColumnsReference>()).ToHashSet();
		}

		public bool IsRecognized { get; set; }

		public virtual string SuggestionType => null;

		public virtual string SemanticErrorType => _objectReferences.Count >= 2 ? OracleSemanticErrorType.AmbiguousReference : OracleSemanticErrorType.None;

		public ICollection<OracleObjectWithColumnsReference> ObjectReferences => _objectReferences;

		public ICollection<string> ObjectNames
		{
			get
			{
				return _objectReferences.Select(t => t.FullyQualifiedObjectName.ToString())
					.Where(n => !String.IsNullOrEmpty(n))
					.OrderByDescending(n => n)
					.ToArray();
			}
		}

		public StatementGrammarNode Node { get; set; }

		public virtual string ToolTipText =>
			SemanticErrorType == OracleSemanticErrorType.None
				? Node.Type == NodeType.NonTerminal
					? null
					: Node.Id
				: FormatToolTipWithObjectNames();

		private string FormatToolTipWithObjectNames()
		{
			var objectNames = ObjectNames;
			return $"{SemanticErrorType}{(objectNames.Count == 0 ? null : $" ({String.Join(", ", ObjectNames)})")}";
		}
	}

	public class InvalidNodeValidationData : NodeValidationData
	{
		private readonly string _semanticError;

		public InvalidNodeValidationData(string semanticError = OracleSemanticErrorType.None)
		{
			_semanticError = semanticError;
			IsRecognized = true;
		}

		public override string SemanticErrorType => _semanticError;

	    public override string ToolTipText => _semanticError;
	}

	public class SuggestionData : NodeValidationData
	{
		private readonly string _suggestionType;

		public SuggestionData(string suggestionType = OracleSuggestionType.None)
		{
			_suggestionType = suggestionType;
		}

		public override string SuggestionType => _suggestionType;

		public override string SemanticErrorType => OracleSemanticErrorType.None;

		public override string ToolTipText => _suggestionType;
	}

	public class ColumnNodeValidationData : NodeValidationData
	{
		private readonly OracleColumnReference _columnReference;
		private readonly string[] _ambiguousColumnNames;

		public ColumnNodeValidationData(OracleColumnReference columnReference)
			: base(columnReference.ColumnNodeObjectReferences)
		{
			if (columnReference == null)
			{
				throw new ArgumentNullException(nameof(columnReference));
			}
			
			_columnReference = columnReference;

			if (_columnReference.SelectListColumn != null && _columnReference.SelectListColumn.IsAsterisk)
			{
				_ambiguousColumnNames = _columnReference.Owner
					.Columns.Where(c => !c.HasExplicitDefinition)
					.SelectMany(c => c.ColumnReferences)
					.Where(c => c.ColumnNodeColumnReferences.Count > 1 && ObjectReferencesEqual(_columnReference, c))
					.SelectMany(c => c.ColumnNodeColumnReferences)
					.Where(c => !String.IsNullOrEmpty(c.Name))
					.Select(c => c.Name.ToSimpleIdentifier())
					.Distinct()
					.ToArray();
			}
			else
			{
				_ambiguousColumnNames = new string[0];
			}
		}

		private static bool ObjectReferencesEqual(OracleReference asteriskColumnReference, OracleColumnReference implicitColumnReference)
		{
			return asteriskColumnReference.ObjectNodeObjectReferences.Count != 1 || implicitColumnReference.ColumnNodeObjectReferences.Count != 1 ||
				   asteriskColumnReference.ObjectNodeObjectReferences.First() == implicitColumnReference.ColumnNodeObjectReferences.First();
		}

		public ICollection<OracleColumn> ColumnNodeColumnReferences => _columnReference.ColumnNodeColumnReferences;

	    public override string SemanticErrorType => _ambiguousColumnNames.Length > 0 || ColumnNodeColumnReferences.Count >= 2
		    ? OracleSemanticErrorType.AmbiguousReference
		    : base.SemanticErrorType;

	    public override string ToolTipText
		{
			get
			{
				var additionalInformation = _ambiguousColumnNames.Length > 0
					? $" ({String.Join(", ", _ambiguousColumnNames)})"
				    : String.Empty;

				return _ambiguousColumnNames.Length > 0 && ObjectReferences.Count <= 1
					? OracleSemanticErrorType.AmbiguousReference + additionalInformation
					: base.ToolTipText;
			}
		}
	}

	public class SemanticErrorNodeValidationData : NodeValidationData
	{
		public SemanticErrorNodeValidationData(string semanticErrorType, string toolTipText)
		{
			ToolTipText = toolTipText;
			SemanticErrorType = semanticErrorType;
			IsRecognized = true;
		}

		public override string SemanticErrorType { get; }

		public override string ToolTipText { get; }
	}
}
