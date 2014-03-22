using System;
using System.Collections.Generic;

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

		public static string ToOracleIdentifier(this string identifier)
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
	}
}