using System;
using System.Collections.Generic;

namespace SqlPad.Oracle
{
	public static class OracleExtensions
	{
		public static string ToOracleIdentifier(this string identifier)
		{
			if (String.IsNullOrWhiteSpace(identifier))
				return String.Empty;

			return identifier[0] == '"' ? identifier : "\"" + identifier.ToUpperInvariant() + "\"";
		}

		public static IEnumerable<StatementDescriptionNode> GetDescendantsWithinSameQuery(this StatementDescriptionNode node, params string[] descendantNodeIds)
		{
			return node.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBoundary, descendantNodeIds);
		}
	}
}