using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class StatementDescriptionNodeTest
    {
		private readonly OracleSqlParser _parser = new OracleSqlParser();
		private StatementDescriptionNode _rootNode;

		[SetUp]
		public void SetUp()
		{
			const string sqlText = "WITH XXX AS (SELECT 3 COL FROM DUAL CTE_OUTER_ALIAS_1) SELECT VP1 COL1, (SELECT 1 FROM XXX SC_ALIAS_1) SCALARSUBQUERY FROM (WITH YYY AS (SELECT 1 FROM SYS.DUAL CTE_INNER_ALIAS_1), ZZZ AS (SELECT 2 FROM DUAL CTE_INNER_ALIAS_2), FFF AS (SELECT 4 FROM XXX CTE_INNER_ALIAS_3) SELECT COL + 1 VP1 FROM (SELECT COL FROM XXX TABLE_ALIAS_1, DUAL TABLE_ALIAS_2) TABLE_ALIAS_3) SUBQUERY";
			var result = _parser.Parse(sqlText);

			result.Count.ShouldBe(1);

			var oracleStatement = result.Single();
			oracleStatement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			_rootNode = oracleStatement.RootNode;
		}

		[Test(Description = @"")]
		public void TestGetPathFilterDescendants()
		{
			var rootNestedQuery = _rootNode.ChildNodes.Single(n => n.Id == OracleGrammarDescription.NonTerminals.NestedQuery);
			var commonTableExpressions = rootNestedQuery.GetPathFilterDescendants(n => n.Id != OracleGrammarDescription.NonTerminals.NestedQuery, OracleGrammarDescription.NonTerminals.SubqueryComponent).ToArray();
			commonTableExpressions.Length.ShouldBe(1);

			commonTableExpressions = _rootNode.GetDescendants(OracleGrammarDescription.NonTerminals.SubqueryComponent).ToArray();
			commonTableExpressions.Length.ShouldBe(4);
		}

		[Test(Description = @"")]
		public void TestStatementCollectionGetNodeAtPositionAtSemicolonBetweenStatements()
		{
			var statements = _parser.Parse("SELECT * FROM DUAL;SELECT * FROM DUAL");
			var node = statements.GetNodeAtPosition(18);
			node.Id.ShouldBe(OracleGrammarDescription.Terminals.ObjectIdentifier);

			node = statements.GetNodeAtPosition(19);
			node.Id.ShouldBe(OracleGrammarDescription.Terminals.Select);
		}
    }
}