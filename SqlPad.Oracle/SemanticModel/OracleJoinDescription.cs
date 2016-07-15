using System.Collections.Generic;

namespace SqlPad.Oracle.SemanticModel
{
	public class OracleJoinDescription
	{
		public OracleDataObjectReference MasterObjectReference { get; set; }

		public StatementGrammarNode MasterPartitionClause { get; set; }

		public OracleDataObjectReference SlaveObjectReference { get; set; }

		public StatementGrammarNode SlavePartitionClause { get; set; }

		public ICollection<string> Columns { get; set; }

		public JoinType Type { get; set; }

		public JoinDefinition Definition { get; set; }
	}

	public enum JoinType
	{
		Inner,
		Left,
		Right,
		Full
	}

	public enum JoinDefinition
	{
		Explicit,
		Natural
	}
}