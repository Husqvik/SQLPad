using System;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;

using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle
{
	public static class OracleConditionValidator
	{
		public static void ValidateCondition(OracleValidationModel validationModel, OracleColumnReference columnReference)
		{
			var columnDataTypeName = columnReference.ColumnDescription?.DataType.FullyQualifiedName.NormalizedName.Trim('"');
			if (String.IsNullOrEmpty(columnDataTypeName) || !columnReference.ColumnDescription.DataType.IsPrimitive)
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

				var dummyColumn = new OracleColumn();
				if (!OracleDataType.TryResolveDataTypeFromExpression(expressionToCheck, dummyColumn))
				{
					return;
				}

				var literalDataTypeName = dummyColumn.DataType.FullyQualifiedName.NormalizedName.Trim('"');

				var isColumnTimeOrNumber = IsTime(columnDataTypeName) || IsNumeric(columnDataTypeName);
				var isLiteralTimeOrNumber = IsTime(literalDataTypeName) || IsNumeric(literalDataTypeName);
				if (isColumnTimeOrNumber && IsText(literalDataTypeName) || isLiteralTimeOrNumber && IsText(columnDataTypeName))
				{
					validationModel.AddNonTerminalSuggestion(expressionToCheck, String.Format(OracleSuggestionType.ImplicitConversionWarning, columnDataTypeName, literalDataTypeName));
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