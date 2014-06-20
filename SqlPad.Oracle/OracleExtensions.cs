using System;
using System.Collections.Generic;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public static class OracleExtensions
	{
		private const string QuoteCharacter = "\"";

		public static string ToSimpleIdentifier(this string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
				return String.Empty;

			var isNotKeyword = !identifier.CollidesWithKeyword();

			return isNotKeyword && identifier.IsQuotedWithoutCheck() && identifier == identifier.ToUpperInvariant() ? identifier.Replace(QuoteCharacter, null) : identifier;
		}

		public static string ToRawUpperInvariant(this string identifier)
		{
			return identifier.ToSimpleIdentifier().Replace("\"", null).ToUpperInvariant();
		}

		public static bool CollidesWithKeyword(this string identifier)
		{
			if (String.IsNullOrEmpty(identifier))
				return false;

			CheckQuotedIdentifier(identifier);

			return identifier.Replace(QuoteCharacter, null).IsKeyword();
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

		public static IEnumerable<StatementDescriptionNode> GetDescendantsWithinSameQuery(this StatementDescriptionNode node, params string[] descendantNodeIds)
		{
			return node.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBoundary, descendantNodeIds);
		}

		public static OracleObjectIdentifier ExtractObjectIdentifier(this StatementDescriptionNode node)
		{
			var queryTableExpression = node.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.QueryTableExpression);

			var tableIdentifierNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == Terminals.ObjectIdentifier);

			if (tableIdentifierNode == null)
				return OracleObjectIdentifier.Empty;

			var schemaPrefixNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.SchemaPrefix);
			if (schemaPrefixNode != null)
			{
				schemaPrefixNode = schemaPrefixNode.ChildNodes.First();
			}

			return OracleObjectIdentifier.Create(schemaPrefixNode, tableIdentifierNode, null);
		}

		public static bool IsWithinSelectClauseOrExpression(this StatementDescriptionNode node)
		{
			return node.IsWithinSelectClause() ||
			       node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.Expression) != null;
		}

		public static bool IsWithinSelectClause(this StatementDescriptionNode node)
		{
			var selectListNode = node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList);
			return node.Id == Terminals.Select || selectListNode != null;
		}

		public static bool IsWithinGroupByClause(this StatementDescriptionNode node)
		{
			return node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.GroupByClause) != null;
		}

		public static bool IsWithinHavingClause(this StatementDescriptionNode node)
		{
			return node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.HavingClause) != null;
		}

		public static StatementDescriptionNode GetParentExpression(this StatementDescriptionNode node)
		{
			return node == null
				? null
				: node.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBoundary, NonTerminals.Expression);
		}

		public static string ToCategoryLabel(this TableReferenceType type)
		{
			switch (type)
			{
				case TableReferenceType.CommonTableExpression:
					return OracleCodeCompletionCategory.CommonTableExpression;
				case TableReferenceType.SchemaObject:
					return OracleCodeCompletionCategory.SchemaObject;
				case TableReferenceType.InlineView:
					return OracleCodeCompletionCategory.InlineView;
			}

			throw new NotSupportedException(String.Format("Value '{0}' is not supported. ", type));
		}

		public static bool IsSingleCharacterTerminal(this string terminalId)
		{
			return OracleGrammarDescription.SingleCharacterTerminals.Contains(terminalId);
		}
	}

	public static class NodeFilters
	{
		public static bool BreakAtNestedQueryBoundary(StatementDescriptionNode node)
		{
			return node.Id != NonTerminals.NestedQuery;
		}

		public static bool AnyIdentifier(StatementDescriptionNode node)
		{
			return node.Id.IsIdentifier();
		}

		public static bool In(StatementDescriptionNode node, params string[] ids)
		{
			return node.Id.In(ids);
		}
	}
}