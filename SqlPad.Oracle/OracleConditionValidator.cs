using System;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle
{
	public static class OracleConditionValidator
	{
		public static void ValidateCondition(OracleValidationModel validationModel, OracleColumnReference columnReference)
		{
			var dataTypeName = columnReference.ColumnDescription?.DataType.FullyQualifiedName.NormalizedName.Trim('"');
			if (String.IsNullOrEmpty(dataTypeName) || !columnReference.ColumnDescription.DataType.IsPrimitive)
			{
				return;
			}

			var condition = FindCondition(columnReference.RootNode);
			if (condition == null)
			{
				return;
			}

			var expression2 = condition[2];
			if (String.Equals(condition[1]?.Id, NonTerminals.RelationalOperator) &&
			    String.Equals(expression2?.Id, NonTerminals.Expression))
			{
				var expression1 = condition[0];
				var referenceExpression = columnReference.RootNode.GetAncestor(NonTerminals.Expression);

				var expressionToCheck = expression1 == referenceExpression
					? expression2
					: expression1;

				if (expressionToCheck[NonTerminals.ExpressionMathOperatorChainedList] != null)
				{
					return;
				}

				var isString = String.Equals(expressionToCheck.FirstTerminalNode.Id, Terminals.StringLiteral);
				var isNumber = String.Equals(expressionToCheck.FirstTerminalNode.Id, Terminals.NumberLiteral);
				var isTime = expressionToCheck.FirstTerminalNode.Id.In(Terminals.Date, Terminals.Timestamp);

				var isTimeOrNumber = IsTime(dataTypeName) || IsNumeric(dataTypeName);
				if (isTimeOrNumber && isString || IsText(dataTypeName) && (isTime || isNumber))
				{
					var literalDataType = expressionToCheck.FirstTerminalNode.Id.ToUpperInvariant();
					if (isString)
					{
						literalDataType = "CHAR";
					}
					else if (isNumber)
					{
						literalDataType = "NUMBER";
					}

					validationModel.AddNonTerminalSuggestion(expressionToCheck, String.Format(OracleSuggestionType.ImplicitConversionWarning, dataTypeName, literalDataType));
				}
			}
		}

		private static StatementGrammarNode FindCondition(StatementGrammarNode rootNode)
		{
			if (!String.Equals(rootNode.ParentNode.Id, NonTerminals.Expression))
				return null;

			return rootNode.GetAncestor(NonTerminals.Condition);
		}

		private static bool IsText(string dataTypeName)
		{
			return dataTypeName.In(TerminalValues.Varchar2, TerminalValues.NVarchar2, TerminalValues.Char, TerminalValues.Varchar, TerminalValues.NVarchar, TerminalValues.NChar);
		}

		private static bool IsNumeric(string dataTypeName)
		{
			return dataTypeName.In(TerminalValues.Integer, TerminalValues.Number, TerminalValues.Numeric, OracleDatabaseModelBase.BuiltInDataTypeInt, OracleDatabaseModelBase.BuiltInDataTypeReal);
		}

		private static bool IsTime(string dataTypeName)
		{
			return dataTypeName.In(TerminalValues.Date, TerminalValues.Timestamp, OracleDatabaseModelBase.BuiltInDataTypeTimestampWithTimeZone, OracleDatabaseModelBase.BuiltInDataTypeTimestampWithLocalTimeZone);
		}
	}
}