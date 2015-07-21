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
		private const string QuoteCharacter = "\"";

		public static bool RequiresQuotes(this string identifier)
		{
			return !AllCharactersSupportSimpleIdentifier(identifier, 0, false) || identifier.CollidesWithReservedWord();
		}

		public static string ToSimpleIdentifier(this string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
				return String.Empty;

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
			var trimToIndex = GetTrimIndex(oracleString, out isQuotedString);
			return oracleString.Substring(trimToIndex, oracleString.Length - trimToIndex - (isQuotedString ? 2 : 1));
		}

		internal static int GetTrimIndex(string oracleString, out bool isQuotedString)
		{
			var trimToIndex = 1;
			isQuotedString = false;
			if (oracleString[0] == 'n' || oracleString[0] == 'N')
			{
				if ((oracleString[1] == 'q' || oracleString[1] == 'Q'))
				{
					trimToIndex = 4;
					isQuotedString = true;
				}
				else
				{
					trimToIndex = 2;
				}
			}
			else if (oracleString[0] == 'q' || oracleString[0] == 'Q')
			{
				trimToIndex = 3;
				isQuotedString = true;
			}

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
				return false;

			CheckQuotedIdentifier(identifier);

			return identifier.Replace(QuoteCharacter, null).IsReservedWord();
		}

		public static bool IsQuoted(this string identifier)
		{
			if (String.IsNullOrEmpty(identifier))
				return false;

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
				return String.Empty;

			CheckQuotedIdentifier(identifier);

			return identifier.IsQuotedWithoutCheck() ? identifier : String.Format("{0}{1}{0}", QuoteCharacter, identifier.ToUpperInvariant());
		}

		private static void CheckQuotedIdentifier(string identifier)
		{
			if (identifier.IsQuotedWithoutCheck() && (identifier.Length == 1 || identifier[identifier.Length - 1] != '"' || identifier.Substring(1, identifier.Length - 2).Contains(QuoteCharacter)))
				throw new ArgumentException("invalid quoted notation");
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

		public static IEnumerable<T> FilterRecursiveReferences<T>(this IEnumerable<T> source) where T : OracleReference
		{
			return source.Where(r => r.Owner == null || !r.Owner.IsRecursive);
		}

		public static OracleObjectIdentifier ExtractObjectIdentifier(this StatementGrammarNode node)
		{
			var queryTableExpression = node.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.QueryTableExpression);

			var tableIdentifierNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == Terminals.ObjectIdentifier);

			if (tableIdentifierNode == null)
				return OracleObjectIdentifier.Empty;

			var schemaPrefixNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.SchemaPrefix);
		    schemaPrefixNode = schemaPrefixNode?.ChildNodes.First();

		    return OracleObjectIdentifier.Create(schemaPrefixNode, tableIdentifierNode, null);
		}

		public static bool IsWithinSelectClauseOrExpression(this StatementGrammarNode node)
		{
			return node.IsWithinSelectClause() || node.IsWithinExpression();
		}

		public static bool IsWithinExpression(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.Expression) != null;
		}

		public static bool IsWithinSelectClause(this StatementGrammarNode node)
		{
			var selectListNode = node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList);
			return node.Id == Terminals.Select || selectListNode != null;
		}

		public static bool IsWithinGroupByClause(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.GroupByClause) != null;
		}

		public static bool IsWithinHavingClause(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.HavingClause) != null;
		}

		public static bool IsWithinOrderByClause(this StatementGrammarNode node)
		{
			return node.GetPathFilterAncestor(n => n.Id != NonTerminals.Subquery, NonTerminals.OrderByClause) != null;
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
	}
}
