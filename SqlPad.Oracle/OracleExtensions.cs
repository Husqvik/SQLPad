using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public static class OracleExtensions
	{
		private const string ApostropheCharacter = "'";
		private const string QuoteCharacter = "\"";

		public static bool RequiresQuotes(this string identifier)
		{
			return !AllCharactersSupportSimpleIdentifier(identifier, 0, false) || identifier.CollidesWithReservedWord();
		}

		public static string ToSimpleIdentifier(this string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return String.Empty;
			}

			var isNotKeyword = !identifier.CollidesWithReservedWord();

			return isNotKeyword && identifier.IsQuotedWithoutCheck() && AllCharactersSupportSimpleIdentifier(identifier, 1, true) ? identifier.Replace(QuoteCharacter, null) : identifier;
		}

		private static bool AllCharactersSupportSimpleIdentifier(string identifier, int offset, bool checkLowerCase)
		{
			if (!Char.IsLetter(identifier[offset]))
			{
				return false;
			}

			for (var i = offset; i < identifier.Length - offset; i++)
			{
				var character = identifier[i];
				if (!Char.IsLetterOrDigit(character) && character != '_' && character != '$' && character != '#')
				{
					return false;
				}

				if (checkLowerCase && Char.IsLower(character))
				{
					return false;
				}
			}

			return true;
		}

		public static string ToPlainString(this string oracleString)
		{
			if (String.IsNullOrEmpty(oracleString))
			{
				return oracleString;
			}

			bool isQuotedString;
			char? quoteInitializer;
			var trimToIndex = GetTrimIndex(oracleString, out isQuotedString, out quoteInitializer);
			var length = oracleString.Length;
			var value = oracleString.Substring(trimToIndex);
			var trimEnd = isQuotedString && length >= trimToIndex
				? $"{oracleString[trimToIndex - 1]}'"
				: ApostropheCharacter;

			if (value.EndsWith(trimEnd))
			{
				value = value.Remove(value.Length - trimEnd.Length);
			}
			else if (value.EndsWith(ApostropheCharacter))
			{
				value = value.Remove(value.Length - 1);
			}

			if (!isQuotedString)
			{
				value = value.Replace("''", "'");
			}

			return value;
		}

		internal static int GetTrimIndex(string oracleString, out bool isQuotedString, out char? quoteInitializer)
		{
			var trimToIndex = 1;
			isQuotedString = false;
			var firstCharacter = oracleString[0];
			if (firstCharacter == 'n' || firstCharacter == 'N')
			{
				var secondCharacter = oracleString[1];
				if (secondCharacter == 'q' || secondCharacter == 'Q')
				{
					trimToIndex = 4;
					isQuotedString = true;
				}
				else
				{
					trimToIndex = 2;
				}
			}
			else if (firstCharacter == 'q' || firstCharacter == 'Q')
			{
				trimToIndex = 3;
				isQuotedString = true;
			}

			quoteInitializer = isQuotedString && oracleString.Length >= trimToIndex
				? oracleString[trimToIndex - 1]
				: (char?)null;

			return trimToIndex;
		}

		public static string ToOracleString(this string value)
		{
			return value.Replace("'", "''");
		}

		public static string ToRawUpperInvariant(this string identifier)
		{
			return identifier.ToSimpleIdentifier().Replace(QuoteCharacter, null).ToUpperInvariant();
		}

		public static bool CollidesWithReservedWord(this string identifier)
		{
			if (String.IsNullOrEmpty(identifier))
			{
				return false;
			}

			CheckQuotedIdentifier(identifier);

			return identifier.Replace(QuoteCharacter, null).IsReservedWord();
		}

		public static bool IsQuoted(this string identifier)
		{
			if (String.IsNullOrEmpty(identifier))
			{
				return false;
			}

			CheckQuotedIdentifier(identifier);
			return IsQuotedWithoutCheck(identifier);
		}

		private static bool IsQuotedWithoutCheck(this string identifier)
		{
			return identifier[0] == '"';
		}

		public static string ToQuotedIdentifier(this string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
			{
				return String.Empty;
			}

			CheckQuotedIdentifier(identifier);

			return identifier.IsQuotedWithoutCheck() ? identifier : String.Format("{0}{1}{0}", QuoteCharacter, identifier.ToUpperInvariant());
		}

		private static void CheckQuotedIdentifier(string identifier)
		{
			if (identifier.IsQuotedWithoutCheck() && (identifier.Length == 1 || identifier[identifier.Length - 1] != '"' || identifier.Substring(1, identifier.Length - 2).Contains(QuoteCharacter)))
			{
				throw new ArgumentException("invalid quoted notation");
			}
		}

		public static string ToNormalizedBindVariableIdentifier(this string identifier)
		{
			return identifier.All(Char.IsDigit)
				? identifier
				: identifier.ToQuotedIdentifier().ToSimpleIdentifier();
		}

		public static IEnumerable<StatementGrammarNode> GetDescendantsWithinSameQueryBlock(this StatementGrammarNode node, params string[] descendantNodeIds)
		{
			return node.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBlock, descendantNodeIds);
		}

		public static OracleObjectIdentifier ExtractObjectIdentifier(this StatementGrammarNode node)
		{
			var queryTableExpression = node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.FromClause), NonTerminals.QueryTableExpression);

			var tableIdentifierNode = queryTableExpression.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, Terminals.ObjectIdentifier));

			if (tableIdentifierNode == null)
			{
				return OracleObjectIdentifier.Empty;
			}

			var schemaPrefixNode = queryTableExpression.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.SchemaPrefix));
		    schemaPrefixNode = schemaPrefixNode?.ChildNodes.First();

		    return OracleObjectIdentifier.Create(schemaPrefixNode, tableIdentifierNode, null);
		}

		public static bool IsWithinSelectClauseOrExpression(this StatementGrammarNode node)
		{
			return node.IsWithinSelectClause() || node.IsWithinExpression();
		}

		public static bool IsWithinExpression(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.Expression) != null;
		}

		public static bool IsWithinSelectClause(this StatementGrammarNode node)
		{
			var selectListNode = node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.SelectList);
			return node.Id == Terminals.Select || selectListNode != null;
		}

		public static bool IsWithinGroupByClause(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.GroupByClause) != null;
		}

		public static bool IsWithinHavingClause(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.HavingClause) != null;
		}

		public static bool IsWithinOrderByClause(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.Subquery), NonTerminals.OrderByClause) != null;
		}

		public static StatementGrammarNode GetParentExpression(this StatementGrammarNode node)
		{
			return node?.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.Expression);
		}

		public static string ToCategoryLabel(this ReferenceType type)
		{
			switch (type)
			{
				case ReferenceType.CommonTableExpression:
					return OracleCodeCompletionCategory.CommonTableExpression;
				case ReferenceType.SchemaObject:
					return OracleCodeCompletionCategory.SchemaObject;
				case ReferenceType.InlineView:
					return OracleCodeCompletionCategory.InlineView;
				case ReferenceType.XmlTable:
					return OracleCodeCompletionCategory.XmlTable;
				case ReferenceType.JsonTable:
					return OracleCodeCompletionCategory.JsonTable;
				case ReferenceType.TableCollection:
					return OracleCodeCompletionCategory.TableCollection;
				case ReferenceType.SqlModel:
					return OracleCodeCompletionCategory.SqlModel;
				case ReferenceType.PivotTable:
					return OracleCodeCompletionCategory.PivotTable;
			}

			throw new NotSupportedException($"Value '{type}' is not supported. ");
		}

		public static OracleSchemaObject GetTargetSchemaObject(this OracleSchemaObject schemaObject)
		{
			var synonym = schemaObject as OracleSynonym;
			return synonym == null ? schemaObject : synonym.SchemaObject;
		}
	}

	public static class NodeFilters
	{
		public static bool BreakAtNestedQueryBlock(StatementGrammarNode node)
		{
			return !String.Equals(node.Id, NonTerminals.NestedQuery);
		}

		public static bool BreakAtExpression(StatementGrammarNode node)
		{
			return !String.Equals(node.Id, NonTerminals.Expression);
		}

		public static bool BreakAtPlSqlSubProgramOrSqlCommand(StatementGrammarNode node)
		{
			return !String.Equals(node.Id, NonTerminals.PlSqlBlock) && !String.Equals(node.Id, NonTerminals.PlSqlSqlStatementType) && !String.Equals(node.Id, NonTerminals.SelectStatement);
		}
	}
}
