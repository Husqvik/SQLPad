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

			CheckQuotedIdentifier(identifier);

			return identifier[0] == '"' && identifier == identifier.ToUpperInvariant() ? identifier.Replace(QuoteCharacter, null) : identifier;
		}

		public static string ToQuotedIdentifier(this string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
				return String.Empty;

			CheckQuotedIdentifier(identifier);

			return identifier[0] == '"' ? identifier : QuoteCharacter + identifier.ToUpperInvariant() + QuoteCharacter;
		}

		private static void CheckQuotedIdentifier(string identifier)
		{
			if (identifier[0] == '"' && (identifier.Length == 1 || identifier[identifier.Length - 1] != '"' || identifier.Substring(1, identifier.Length - 2).Contains(QuoteCharacter)))
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
			var filter = new Func<StatementDescriptionNode, bool>(n => n.Id != NonTerminals.QueryBlock);
			var selectListNode = node.GetPathFilterAncestor(filter, NonTerminals.SelectList);
			var expressionNode = node.GetPathFilterAncestor(filter, NonTerminals.Expression);
			return selectListNode != null || expressionNode != null;
		}

		public static bool IsWithinHavingClause(this StatementDescriptionNode node)
		{
			return node.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.HavingClause) != null;
		}

		public static string ToCategoryLabel(this TableReferenceType type)
		{
			switch (type)
			{
				case TableReferenceType.CommonTableExpression:
					return OracleCodeCompletionCategory.CommonTableExpression;
				case TableReferenceType.PhysicalObject:
					return OracleCodeCompletionCategory.SchemaObject;
				case TableReferenceType.NestedQuery:
					return OracleCodeCompletionCategory.Subquery;
			}

			throw new NotSupportedException(String.Format("Value '{0}' is not supported. ", type));
		}
	}
}