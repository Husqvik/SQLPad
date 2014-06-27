using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
    public class OracleSqlParserTest
    {
		private static readonly OracleSqlParser Parser = new OracleSqlParser();

		[Test(Description = @"")]
		public void Test1()
		{
			using (var reader = File.OpenText(@"TestFiles\SqlStatements1.sql"))
			{
				var result = Parser.Parse(OracleTokenReader.Create(reader));
				result.ShouldNotBe(null);

				var i = 0;
				foreach (var statement in result)
				{
					Trace.WriteLine(++i + ". statement parse status: " + statement.ProcessingStatus);
				}
			}
		}

		[Test(Description = @"Tests exception raise when null token reader is passed")]
		public void TestNullTokenReader()
		{
			Assert.Throws<ArgumentNullException>(() => Parser.Parse((OracleTokenReader)null));
		}

		[Test(Description = @"Tests trivial query. ")]
		public void TestTrivialQuery()
		{
			const string sqlText = @"SELECT NULL FROM DUAL";
			var result = Parser.Parse(CreateTokenReader(sqlText));
			
			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.RootNode.AllChildNodes.ToList()
				.ForEach(n => n.Statement.ShouldBe(statement));

			var terminals = statement.AllTerminals.ToList();

			terminals.Count.ShouldBe(4);
			terminals[0].Id.ShouldBe(Terminals.Select);
			terminals[0].SourcePosition.IndexStart.ShouldBe(0);
			terminals[0].SourcePosition.IndexEnd.ShouldBe(5);
			terminals[0].IsKeyword.ShouldBe(true);
			terminals[1].Id.ShouldBe(Terminals.Null);
			terminals[1].SourcePosition.IndexStart.ShouldBe(7);
			terminals[1].SourcePosition.IndexEnd.ShouldBe(10);
			terminals[1].IsKeyword.ShouldBe(true);
			terminals[2].Id.ShouldBe(Terminals.From);
			terminals[2].SourcePosition.IndexStart.ShouldBe(12);
			terminals[2].SourcePosition.IndexEnd.ShouldBe(15);
			terminals[2].IsKeyword.ShouldBe(true);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[3].Token.Value.ShouldBe("DUAL");
			terminals[3].SourcePosition.IndexStart.ShouldBe(17);
			terminals[3].SourcePosition.IndexEnd.ShouldBe(20);
			terminals[3].IsKeyword.ShouldBe(false);

			statement.TerminatorNode.ShouldBe(null);
		}

		[Test(Description = @"Tests trivial query. ")]
		public void TestTrivialQueryWithTerminator()
		{
			const string sqlText = @"SELECT NULL FROM DUAL ;";
			var result = Parser.Parse(CreateTokenReader(sqlText));

			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.TerminatorNode.ShouldNotBe(null);
			statement.TerminatorNode.Id.ShouldBe(Terminals.Semicolon);
			statement.TerminatorNode.Token.Value.ShouldBe(";");
			statement.TerminatorNode.Token.Index.ShouldBe(22);
		}

		[Test(Description = @"Tests query with fully qualified names and aliases. ")]
		public void TestQueryWithFullyQualifiedNamesAndAliases()
		{
			const string sqlText = @"SELECT NULL AS "">=;+Alias/*--^"", SYS.DUAL.DUMMY FROM SYS.DUAL";
			var result = Parser.Parse(sqlText);

			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			var rootToken = statement.RootNode;
			var terminals = rootToken.Terminals.ToArray();

			terminals.Length.ShouldBe(14);
			terminals[0].Id.ShouldBe(Terminals.Select);
			terminals[1].Id.ShouldBe(Terminals.Null);
			terminals[2].Id.ShouldBe(Terminals.As);
			terminals[2].IsKeyword.ShouldBe(false);
			terminals[3].Id.ShouldBe(Terminals.ColumnAlias);
			terminals[3].Token.Value.ShouldBe("\">=;+Alias/*--^\"");
			terminals[4].Id.ShouldBe(Terminals.Comma);
			terminals[5].Id.ShouldBe(Terminals.SchemaIdentifier);
			terminals[5].Token.Value.ShouldBe("SYS");
			terminals[6].Id.ShouldBe(Terminals.Dot);
			terminals[7].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[7].Token.Value.ShouldBe("DUAL");
			terminals[8].Id.ShouldBe(Terminals.Dot);
			terminals[9].Id.ShouldBe(Terminals.Identifier);
			terminals[9].Token.Value.ShouldBe("DUMMY");
			terminals[10].Id.ShouldBe(Terminals.From);
			terminals[11].Id.ShouldBe(Terminals.SchemaIdentifier);
			terminals[11].Token.Value.ShouldBe("SYS");
			terminals[12].Id.ShouldBe(Terminals.Dot);
			terminals[13].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[13].Token.Value.ShouldBe("DUAL");
		}

		[Test(Description = @"Tests complex case expression with literals. ")]
		public void TestComplexCaseExpressionWithLiterals()
		{
			const string sqlText = @"SELECT CASE WHEN 1 = 2 THEN 'True1' WHEN 1 = 0 THEN 'True2' ELSE 'False' END - (1 + 2) XXX FROM DUAL";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			var terminals = result.Single().AllTerminals.ToList();

			terminals.Count.ShouldBe(26);
			terminals[0].Id.ShouldBe(Terminals.Select);
			terminals[1].Id.ShouldBe(Terminals.Case);
			terminals[1].IsKeyword.ShouldBe(false);
			terminals[2].Id.ShouldBe(Terminals.When);
			terminals[2].IsKeyword.ShouldBe(false);
			terminals[3].Id.ShouldBe(Terminals.NumberLiteral);
			terminals[3].Token.Value.ShouldBe("1");
		}

		[Test(Description = @"Tests complex mathematic expressions. ")]
		public void TestMathematicExpressions()
		{
			const string sqlText = @"SELECT CASE (1 * (0 + 0)) WHEN (2 + 0) THEN DUAL.DUMMY || 'xxx' ELSE 'a' || ('b' || 'c') END FROM DUAL";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			var terminals = result.Single().AllTerminals.ToList();

			terminals.Count.ShouldBe(34);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests specific aliases. ")]
		public void TestInvalidAlias()
		{
			const string query1 = @"SELECT 1 1 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single().Validate();
			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			var terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(2);
			terminals[0].Id.ShouldBe(Terminals.Select);
			terminals[1].Id.ShouldBe(Terminals.NumberLiteral);
		}
		
		[Test(Description = @"Tests 'AS' as alias. ")]
		public void TestAsKeywordAsAliasValue()
		{
			const string query2 = @"SELECT 1 AS FROM T1";
			var result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void TestInvalidSelectExpressions()
		{
			var sqlText = @"SELECT 1FF F FROM DUAL";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			var statement = result.Single().Validate();
			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			var terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(3);
			terminals[0].Id.ShouldBe(Terminals.Select);
			terminals[1].Id.ShouldBe(Terminals.NumberLiteral);
			terminals[1].Token.Value.ShouldBe("1F");
			terminals[2].Id.ShouldBe(Terminals.ColumnAlias);
			terminals[2].Token.Value.ShouldBe("F");

			sqlText = @"SELECT . FROM DUAL";
			result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			statement = result.Single().Validate();
			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(1);
			terminals[0].Id.ShouldBe(Terminals.Select);
		}

		[Test(Description = @"Tests multiple queries. ")]
		public void TestMultipleQueries()
		{
			const string sqlText = @"SELECT 1 FROM T1;SELECT 2 FROM T2";
			var result = Parser.Parse(sqlText).ToArray();

			result.Length.ShouldBe(2);
			result[0].ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			result[1].ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests query. ")]
		public void TestValidQueryWithExtraToken()
		{
			const string sqlText = @"SELECT 1 FROM DUAL,";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
		}

		[Test(Description = @"Tests simple query with common table expression. ")]
		public void TestSimpleCommonTableExpressions()
		{
			const string sqlText = @"WITH X(A, B, C) AS (SELECT 1 FROM D) SELECT * FROM D;";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple query with IN and BETWEEN clauses. ")]
		public void TestInAndBetweenClauses()
		{
			const string sqlText = @"SELECT 1 FROM DUAL WHERE 1 IN (1, 2, 3) AND 4 NOT IN (5, 6, 7) AND (8 + 0) BETWEEN (0 * 0) AND (9 - 0);";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple query blocks connected by set operations. ")]
		public void TestSimpleQueryBlocksConnectedBySetOperations()
		{
			const string sqlText = @"SELECT 1 FROM DUAL UNION ALL SELECT 1 FROM DUAL UNION SELECT 1 FROM DUAL MINUS SELECT 1 FROM DUAL INTERSECT SELECT 1 FROM DUAL";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with group by and having clauses. ")]
		public void TestSimpleQueriesWithGroupByAndHavingClauses()
		{
			const string query1 = @"SELECT 1 FROM DUAL GROUP BY 1, DUMMY HAVING 1 = 1";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL HAVING 1 = 1";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with for update clause. ")]
		public void TestForUpdateClause()
		{
			const string query1 = @"SELECT 1 FROM DUAL FOR UPDATE SKIP LOCKED";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE NOWAIT";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE WAIT 10E-0";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query4 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE WAIT -1";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query5 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE OF SCHEMA.OBJECT.COLUMN1, COLUMN2, OBJECT.COLUMN3 WAIT 1";
			result = Parser.Parse(query5);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var terminals = result.Single().AllTerminals.ToList();

			terminals.Count.ShouldBe(21);
			terminals[8].Id.ShouldBe(Terminals.SchemaIdentifier);
			terminals[10].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[12].Id.ShouldBe(Terminals.Identifier);
			terminals[14].Id.ShouldBe(Terminals.Identifier);
			terminals[16].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[18].Id.ShouldBe(Terminals.Identifier);
		}

		[Test(Description = @"Tests simple queries with IS (NOT) <terminal> conditions. ")]
		public void TestSimpleQueriesWithIsOperator()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE NULL IS NULL AND 1 IS NOT NULL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 'A' NAN, 'B' AS INFINITE FROM DUAL WHERE 1 IS NOT NAN AND 1 IS NOT INFINITE";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests empty token set. ")]
		public void TestParsingOfEmptyTokenSet()
		{
			const string query1 = @"--SELECT 1 FROM DUAL WHERE NULL IS NULL AND 1 IS NOT NULL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			result.Single().RootNode.ShouldBe(null);
		}

		[Test(Description = @"Tests parsing of valid statement after invalid statement. ")]
		public void TestParsingOfValidStatementAfterInvalidStatement()
		{
			const string query1 = @"/*invalid statement */ SELECT 1 FROM DUAL+;/* valid statement */ SELECT 1 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(2);
			result.First().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			result.First().RootNode.ChildNodes.Count.ShouldBe(1);
			result.First().AllTerminals.Count().ShouldBe(4);

			result.Last().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			result.Last().RootNode.ChildNodes.Count.ShouldBe(1);
			result.Last().AllTerminals.Count().ShouldBe(4);
		}

		[Test(Description = @"Tests EXISTS and NOT EXISTS. ")]
		public void TestExistsAndNotExistsClauses()
		{
			const string query1 = @"SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM DUAL) THEN 'FALSE' ELSE 'TRUE' END FROM DUAL WHERE EXISTS (SELECT 1 FROM DUAL) AND EXISTS (SELECT 2 FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests flashback clauses. ")]
		public void TestFlashbackClauses()
		{
			const string query1 = @"SELECT DATE'2014-03-08' - DATE'2014-02-20' FROM T1 VERSIONS BETWEEN SCN MINVALUE AND MAXVALUE AS OF SCN 123";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			const string query2 = @"SELECT * FROM T1 VERSIONS BETWEEN TIMESTAMP TIMESTAMP'2014-02-20 00:00:00' AND DATE'2014-02-20'";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests chained asterisk clause. ")]
		public void TestAsteriskExpressions()
		{
			const string query1 = @"SELECT T1.*, 1, T1.*, 2, T1.*, 3 FROM T1";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests LIKE clause. ")]
		public void TestLikeClauses()
		{
			const string query1 = @"SELECT CASE WHEN 'abc' LIKE 'a%' OR '123' LIKEC '1%' OR '456' LIKE2 '4%' OR '789' LIKE4 '%7%' ESCAPE '\' THEN 'true' END FROM DUAL WHERE 'def' LIKE 'd%' ESCAPE '\'";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests NOT LIKE clause. ")]
		public void TestNotLikeClauses()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE DUMMY NOT LIKE 'X'";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests IN clause with subquery. ")]
		public void TestInSubqueryClause()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE 1 NOT IN (WITH XXX AS (SELECT 1 FROM DUAL) SELECT 1 FROM DUAL DD) AND EXISTS (WITH XXX AS (SELECT 1 FROM DUAL) SELECT 1 FROM DUAL DD)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests scalar subquery clause. ")]
		public void TestScalarSubqueryClause()
		{
			const string query1 = @"SELECT 1 C1, (SELECT (SELECT 2 FROM DUAL) FROM DUAL) C2, (SELECT 3 FROM DUAL) C3 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests join clauses. ")]
		public void TestJoinClauses()
		{
			const string query1 = @"SELECT * FROM T1 CROSS JOIN T2 JOIN T3 ON 1 = 1 INNER JOIN T4 USING (ID) NATURAL JOIN T5 FULL OUTER JOIN T6 ALIAS ON T5.ID = T6.ID, V$SESSION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var terminals = result.Single().AllTerminals.ToArray();
			terminals.Length.ShouldBe(38);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[3].ParentNode.Id.ShouldBe(NonTerminals.QueryTableExpression);
			terminals[3].Token.Value.ShouldBe("T1");
			terminals[4].Id.ShouldBe(Terminals.Cross);
			terminals[5].Id.ShouldBe(Terminals.Join);
			terminals[7].Id.ShouldBe(Terminals.Join);
			terminals[9].Id.ShouldBe(Terminals.On);
			terminals[13].Id.ShouldBe(Terminals.Inner);
			terminals[14].Id.ShouldBe(Terminals.Join);
			terminals[16].Id.ShouldBe(Terminals.Using);
			terminals[20].Id.ShouldBe(Terminals.Natural);
			terminals[21].Id.ShouldBe(Terminals.Join);
			terminals[23].Id.ShouldBe(Terminals.Full);
			terminals[24].Id.ShouldBe(Terminals.Outer);
			terminals[25].Id.ShouldBe(Terminals.Join);
			terminals[26].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[27].Id.ShouldBe(Terminals.ObjectAlias);

			const string query2 = @"SELECT 1 FROM DUAL T1 LEFT OUTER JOIN DUAL T2 PARTITION BY (T2.DUMMY, DUMMY) ON (T1.DUMMY = T2.DUMMY)";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			const string query3 = @"SELECT 1 FROM DUAL T1 LEFT OUTER JOIN DUAL T2 PARTITION BY (T2.DUMMY, DUMMY, (DUMMY, DUMMY)) ON (T1.DUMMY = T2.DUMMY)";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
		}

		[Test(Description = @"Tests order by clause. ")]
		public void TestOrderByClause()
		{
			const string query1 = @"SELECT 1 FROM DUAL ORDER BY 1 DESC, DUMMY ASC NULLS LAST, (SELECT DBMS_RANDOM.VALUE FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests hierarchical query clauses. ")]
		public void TestHierarchicalQueryClauses()
		{
			const string query1 = @"SELECT LEVEL FROM DUAL CONNECT BY NOCYCLE LEVEL <= 5 AND 1 = 1 START WITH 1 = 1";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			const string query2 = @"SELECT LEVEL FROM DUAL START WITH 1 = 1 CONNECT BY NOCYCLE LEVEL <= 5 AND 1 = 1";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[Test(Description = @"Tests group by rollup, cube and grouping sets clauses. ")]
		public void TestGroupByRollupCubeAndGroupingSetsClauses()
		{
			const string query1 = @"SELECT 1 FROM DUAL GROUP BY 1, 2, (3 || DUMMY), ROLLUP(1, (1, 2 || DUMMY)), CUBE(2, (3, 4 + 2))";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL GROUP BY GROUPING SETS (ROLLUP(DUMMY), 1, DUMMY, (1, DUMMY), ROLLUP(DUMMY), CUBE(DUMMY)), GROUPING SETS (1), CUBE(1), ROLLUP(1, DUMMY);";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL GROUP BY GROUPING SETS (DUMMY, (), DUMMY, ());";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests ANY and ALL operators with expression list and subquery. ")]
		public void TestAnyAllAndSomeOperatorsWithExpressionListAndSubquery()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE DUMMY = ANY(1, 2, 3) OR DUMMY = SOME(1, 2, 3) OR DUMMY = ALL(SELECT * FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL WHERE (1, 2, 3) = ALL(SELECT 1, 2, 3 FROM DUAL)";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL WHERE () = ANY(1, 2, 3)";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query4 = @"SELECT 1 FROM DUAL WHERE (1, 2) = ANY((1, 2), (3, 4), ())";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests sample clause. ")]
		public void TestSampleClause()
		{
			const string query1 = @"SELECT * FROM COUNTRY SAMPLE BLOCK (0.1) SEED (1)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests nested subquery. ")]
		public void TestNestedSubquery()
		{
			const string query1 = @"SELECT * FROM (SELECT LEVEL ORDERED_COLUMN FROM DUAL CONNECT BY LEVEL <= 5 ORDER BY ORDERED_COLUMN DESC) WHERE ROWNUM <= 3";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests incorrectly nested common table expression. ")]
		public void TestIncorrectlyNestedCommonTableExpression()
		{
			const string query1 = @"WITH TEST AS (WITH T2 AS (SELECT 1 FROM DUAL) SELECT * FROM T2) SELECT * FROM TEST";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with multiple tables in from clause; also tests '$' character within standard identifier. ")]
		public void TestMultipleTablesInFromClauseAndDollarCharacterWithinIdentifier()
		{
			const string query1 = @"SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with old outer join clause. ")]
		public void TestOldOuterJoinClause()
		{
			const string query1 = @"SELECT * FROM T1, T2 WHERE T1.C1 = T2.C2(+) AND T1.C2 LIKE T2.C2 (+) ESCAPE '\'";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM DUAL T1, DUAL T2 WHERE T1.DUMMY(+) = (T2.DUMMY)";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT * FROM DUAL T1, DUAL T2 WHERE (T1.DUMMY)(+) = T2.DUMMY";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with fully qualified object name with asterisk operator. ")]
		public void TestFullyQualifiedObjectNameWithAsteriskOperator()
		{
			const string query1 = @"SELECT SYS.DUAL.* FROM SYS.DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var terminals = statement.AllTerminals.ToArray();
			terminals[1].Id.ShouldBe(Terminals.SchemaIdentifier);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
		}

		[Test(Description = @"Tests query with function calls. ")]
		public void TestQueryWithFunctionCalls()
		{
			const string query1 = @"SELECT SCHEMA.PACKAGE.FUNCTION@DBLINK ALIAS, PACKAGE.FUNCTION(P1, P2, P3 + P4 * (P5 + P6)) + P7, AGGREGATE_FUNCTION1(DISTINCT PARAMETER), AGGREGATE_FUNCTION2@DBLINK(ALL PARAMETER), SYS_GUID() RANDOM_GUID FROM SYS.DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single().Validate();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var terminals = statement.AllTerminals.ToArray();
			terminals[1].Id.ShouldBe(Terminals.SchemaIdentifier);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[5].Id.ShouldBe(Terminals.Identifier);
			terminals[7].Id.ShouldBe(Terminals.DatabaseLinkIdentifier);
			terminals[8].Id.ShouldBe(Terminals.ColumnAlias);
		}

		[Test(Description = @"Tests query with function calls in where clause. ")]
		public void TestQueryWithFunctionCallsInWhereClause()
		{
			const string query1 = @"SELECT * FROM SYS.DUAL WHERE PACKAGE.FUNCTION(P1, P2, P3 + P4 * (P5 + P6)) + P7 >= SCHEMA.PACKAGE.FUNCTION@DBLINK AND FUNCTION(P1, P2) ^= 0";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with analytic clause. ")]
		public void TestQueryWithAnalyticClause()
		{
			const string query1 = @"SELECT SUM(LENGTH(DUMMY)) OVER (PARTITION BY DUMMY ORDER BY DUMMY DESC ROWS BETWEEN DBMS_RANDOM.VALUE PRECEDING AND UNBOUNDED FOLLOWING) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT SUM(LENGTH(DUMMY)) OVER (PARTITION BY DUMMY RANGE CURRENT ROW) FROM SYS.""DUAL""";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT SUM(LENGTH(DUMMY) * (2)) OVER () FROM SYS.DUAL";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query4 = @"SELECT LAG(SYS.DUAL.DUMMY, 1) OVER (ORDER BY SYS.DUAL.DUMMY NULLS LAST) FROM SYS.DUAL";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests sequence pseudo columns. ")]
		public void TestSequencePseudoColumns()
		{
			const string query1 = @"SELECT SEQ_TEST.CURRVAL, HUSQVIK.SEQ_TEST.NEXTVAL, SEQ_TEST.CURRVAL@HQ11G2 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var terminals = statement.AllTerminals.ToArray();
			terminals[3].Id.ShouldBe(Terminals.SequenceCurrentValue);
			terminals[9].Id.ShouldBe(Terminals.SequenceNextValue);

			const string query2 = @"SELECT CURRVAL, NEXTVAL, CURRVAL@HQ11G2 FROM DUAL";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			terminals = statement.AllTerminals.ToArray();
			terminals[1].Id.ShouldBe(Terminals.Identifier);
			terminals[3].Id.ShouldBe(Terminals.Identifier);
			terminals[5].Id.ShouldBe(Terminals.Identifier);
		}

		[Test(Description = @"Tests query with Oracle 12c OFFSET and FETCH clauses. ")]
		public void TestOffsetAndFetchClauses()
		{
			const string query1 = @"SELECT 1 FROM DUAL OFFSET 1.1 ROW";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL OFFSET 4 ROWS FETCH NEXT 20 PERCENT ROWS ONLY";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL FETCH NEXT 20 PERCENT ROW ONLY";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests common table expression with SEARCH and CYCLE clauses. ")]
		public void TestCommonTableExpressionWithSearchAndCycleClauses()
		{
			const string query1 = @"WITH T1(VAL1, VAL2) AS (SELECT 1, 2 FROM DUAL) CYCLE VAL1, VAL2 SET IS_CYCLE TO '-' DEFAULT 1, T2(VAL1, VAL2) AS (SELECT 1, 2 FROM DUAL) SEARCH DEPTH FIRST BY VAL1 DESC NULLS FIRST, VAL2 SET SEQ# SELECT 1 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests CROSS and OUTER APPLY clauses. ")]
		public void TestCrossAndOuterApplyClauses()
		{
			const string query1 = @"SELECT DUMMY T1_VAL, T2_VAL, T3_VAL FROM DUAL T1 CROSS APPLY (SELECT DUAL.DUMMY T2_VAL FROM DUAL WHERE T1.DUMMY = DUAL.DUMMY) T2 OUTER APPLY (SELECT DUAL.DUMMY T3_VAL FROM DUAL WHERE T1.DUMMY = DUAL.DUMMY) T3;";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests Flashback PERIOD FOR <identifier> clause. ")]
		public void TestFlashbackPeriodForClause()
		{
			const string query1 = @"SELECT DATE'2014-03-08' - DATE'2014-02-20' FROM T1 AS OF PERIOD FOR ""Column"" TO_DATE('2014-03-15', 'YYYY-MM-DD')";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM T1 VERSIONS PERIOD FOR DUMMY BETWEEN TIMESTAMP'2014-02-20 00:00:00' AND DATE'2014-03-08'";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests PIVOT and UNPIVOT clauses. ")]
		public void TestPivotAndUnpivotClauses()
		{
			const string query1 = @"SELECT * FROM (SELECT 1 DUMMY, 'X' LABEL FROM DUAL) PIVOT (SUM(DUMMY) AS SUM_DUMMY FOR (LABEL) IN ('X' || NULL AS X, ('Y') Y, 'Z'));";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM (SELECT 1 DUMMY, 'X1' LABEL1, 'X2' LABEL2 FROM DUAL) PIVOT XML (SUM(DUMMY) SUM_DUMMY FOR (LABEL1, LABEL2) IN (ANY, ANY))";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT * FROM (SELECT 1 CODE1, 2 CODE2, NULL CODE3 FROM DUAL) UNPIVOT INCLUDE NULLS (QUANTITY FOR CODE IN (CODE1 AS 'Code 1', CODE2 AS 'Code 2', CODE3))";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests table reference parenthesis encapsulation. ")]
		public void TestTableReferenceParenthesisEncapsulation()
		{
			const string query1 = @"SELECT * FROM ((SELECT * FROM DUAL)) D";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM ((DUAL)) D";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT * FROM ((((DUAL)) D))";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
			
			const string query4 = @"SELECT * FROM ((DUAL)) AS OF SCN MAXVALUE D";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query5 = @"SELECT * FROM ((DUAL D))";
			result = Parser.Parse(query5);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query6 = @"SELECT * FROM ((DUAL D)) AS OF SCN MAXVALUE";
			result = Parser.Parse(query6);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query7 = @"SELECT * FROM ((DUAL AS OF TIMESTAMP SYSDATE D))";
			result = Parser.Parse(query7);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions

			const string query8 = @"SELECT * FROM ((DUAL AS OF TIMESTAMP SYSDATE)) D";
			result = Parser.Parse(query8);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"")]
		public void TestDoubleTableAlias()
		{
			const string statement1 = @"SELECT NULL FROM (DUAL D) D";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
		}

		[Test(Description = @"Tests table collection expression. ")]
		public void TestTableCollectionExpression()
		{
			const string query1 = @"SELECT TABLE_EXPRESSION_ALIAS1.COLUMN_VALUE, TABLE_EXPRESSION_ALIAS2.COLUMN_VALUE FROM TABLE(SYS.ODCIVARCHAR2LIST('Value 1', 'Value 2', 'Value 3')) TABLE_EXPRESSION_ALIAS1, TABLE(SYS.ODCIVARCHAR2LIST('Value 1', 'Value 2', 'Value 3'))(+) TABLE_EXPRESSION_ALIAS2";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests table collection expression with nested parenthesis. ")]
		public void TestTableCollectionExpressionWithNestedParenthesis()
		{
			const string query1 = @"SELECT * FROM TABLE(((DATABASE_ALERTS(+))))(+) VALID_ALIAS";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests nested subquery as lateral table reference. ")]
		public void TestLateralTableReference()
		{
			const string query1 = @"SELECT * FROM LATERAL (SELECT * FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests table collection expression with nested parenthesis and invalid alias position. ")]
		public void TestTableCollectionExpressionWithNestedParenthesisAndInvalidAliasPosition()
		{
			const string query1 = @"SELECT * FROM TABLE(((DATABASE_ALERTS INVALID_ALIAS)))";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests table collection expression with invalid outer join position. ")]
		public void TestTableCollectionExpressionWithInvalidOuterJoinPosition()
		{
			const string query1 = @"SELECT * FROM TABLE(((DATABASE_ALERTS)(+))) INVALID_OUTER_JOIN";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests CAST function. ")]
		public void TestCastFunction()
		{
			const string query1 = @"SELECT CAST(3 / (2 - 1) AS NUMBER(*, 1)) AS COLUMN_ALIAS1, CAST('X' || 'Y' AS VARCHAR2(30)) + 1 AS COLUMN_ALIAS2 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
			
			const string query2 = @"SELECT CAST(MULTISET(WITH X AS (SELECT 1 FROM DUAL) SELECT LEVEL FROM DUAL CONNECT BY LEVEL <= 3) AS SYS.""ODCINUMBERLIST"") COLLECTION_ALIAS FROM DUAL";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests CAST function with target type names reserved as keyword. ")]
		public void TestCastFunctionWithKeywords()
		{
			const string query1 = @"SELECT CAST(SYS_EXTRACT_UTC(SYSTIMESTAMP) AS DATE) VALIDATIONTIMESTAMP, CAST(1 AS FLOAT), CAST(1 AS INTEGER), CAST(1 AS INTEGER), CAST(1 AS LONG), CAST(1 AS CHAR), CAST(1 AS VARCHAR), CAST(1 AS VARCHAR2), CAST(1 AS SMALLINT), CAST(1 AS DECIMAL), CAST(1 AS NUMBER), CAST(1 AS RAW) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests TREAT function. "), Ignore]
		public void TestThreatFunction()
		{
			const string query1 = @"SELECT NAME, TREAT(VALUE(PERSONS) AS EMPLOYEE_T).SALARY SALARY FROM PERSONS";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests FROM (SUB)PARTITION clause. ")]
		public void TestPartitionAndSubpartitionClauses()
		{
			const string query1 = @"SELECT * FROM IDX_TEST PARTITION FOR (10 + 1 + LENGTH(6), TO_DATE('2014-03-18', 'YYYY-MM-DD'), 'K1') P1, IDX_TEST SUBPARTITION (P_C3_10_SP_C4_V1) P2";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests subquery restriction clause. ")]
		public void TestSubqueryRestrictionClause()
		{
			const string query1 = @"SELECT * FROM (SELECT * FROM COUNTRY WITH READ ONLY), (SELECT * FROM COUNTRY WITH CHECK OPTION), (SELECT * FROM COUNTRY WITH CHECK OPTION CONSTRAINT DUMMY_CONSTRAINT)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with bind variable expressions. ")]
		public void TestBindVariableExpressions()
		{
			const string query1 = @"SELECT:1 FROM DUAL WHERE DUMMY = :BV1 OR DUMMY IN(:""bv2"", :        BV3)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests KEEP clause. ")]
		public void TestKeepClause()
		{
			const string query1 = @"SELECT MAX(SELECTION_ID) KEEP (DENSE_RANK FIRST ORDER BY RESPONDENTBUCKET_ID) + 1 FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests IGNORE NULLS clause. ")]
		public void TestIgnoreNullsClause()
		{
			const string query1 = @"SELECT ""FIRST_VALUE""(SELECTION_ID IGNORE NULLS) OVER () + 1, LAST_VALUE(SELECTION_ID) IGNORE NULLS OVER () + 1 FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests invalid IGNORE NULLS clause. ")]
		public void TestInvalidIgnoreNullsClause()
		{
			const string query1 = @"SELECT FIRST_VALUE(SELECTION_ID IGNORE NULLS) IGNORE NULLS OVER () FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests SYSDATE keyword. ")]
		public void TestSysDateKeyword()
		{
			const string query1 = @"SELECT SYSDATE + 1 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests SYSDATE as invalid alias. ")]
		public void TestSysDateAsInvalidAlias()
		{
			const string query1 = @"SELECT 1 SYSDATE FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests LNNVL function. ")]
		public void TestLnnvlFunction()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE LNNVL(1 = 0) AND LNNVL(NULL = 0)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests LISTAGG aggregation function. ")]
		public void TestListaggAggregationFunction()
		{
			const string query1 = @"SELECT LISTAGG(NAME || '*') WITHIN GROUP (ORDER BY NULL) || 'Postfix' FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests LISTAGG analytic function. ")]
		public void TestListaggAnalyticFunction()
		{
			const string query1 = @"SELECT LISTAGG(NAME, SQLPAD_FUNCTION(SELECTION_ID) || '()') WITHIN GROUP (ORDER BY NULL) OVER () FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLELEMENT function. ")]
		public void TestXmlElementFunction()
		{
			const string query1 =
@"SELECT
    XMLELEMENT(""Emp"",
        XMLELEMENT(NAME ""Name"", E.JOB_ID || ' ' || E.LAST_NAME),
        XMLELEMENT(""Hiredate"", E.HIRE_DATE),
        XMLELEMENT(""Dept"",
            XMLATTRIBUTES(E.DEPARTMENT_ID,
                (SELECT D.DEPARTMENT_NAME FROM DEPARTMENTS D WHERE D.DEPARTMENT_ID = E.DEPARTMENT_ID) AS ""Dept_name"",
				'data' ""CustomData""
            )
        )
    ) || 'PostFix' AS ""Result""
FROM
	EMPLOYEES E
WHERE
	EMPLOYEE_ID > 200";
			
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLTABLE function. ")]
		public void TestXmlTableFunction()
		{
			const string query1 =
@"SELECT
    XML_FILES.*
FROM
    XMLTABLE(
        XMLNAMESPACES('http://sqlpad.com/test1' AS test1, DEFAULT '', 'http://sqlpad.com/test2' AS ""test2""),
        '//Root/Item'
        PASSING XMLTYPE('<Root><Item Attribute=""A1"">Item11</Item><Item Attribute=""B2"">Item2</Item><Item Attribute=""C3"">Item3</Item></Root>')
        COLUMNS
            ELEMENT VARCHAR2(4000) PATH '.' DEFAULT '(' || (2) || ')',
            ATTRIBUTE VARCHAR2(4000) PATH './@Attribute',
            ORD FOR ORDINALITY
    ) XML_FILES";

			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLQUERY function. ")]
		public void TestXmlQueryFunction()
		{
			const string query1 =
@"SELECT
    'Prefix' ||
	XMLQUERY('for $i in ora:view(""CUSTOMER""),
                  $j in ora:view(""COMPANY"")
              where
                $i/ROW/COMPANY_ID = $j/ROW/ID
                and $i/ROW/CODE = ""3550680""
              return <Result>
                        <Customer>{$i}</Customer>
                        <Company>{$j}</Company>
                     </Result>'   
    RETURNING CONTENT) || 'Postfix' AS RESULT
FROM DUAL";

			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests EXTRACT function. ")]
		public void TestExtractFunction()
		{
			const string query1 = @"SELECT EXTRACT(MONTH FROM SYSDATE + 1) + 1, EXTRACT(TIMEZONE_REGION FROM SYSTIMESTAMP) || 'P', EXTRACT(TIMEZONE_ABBR FROM CURRENT_TIMESTAMP) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLFOREST function. ")]
		public void TestXmlForestFunction()
		{
			const string query1 = @"SELECT XMLFOREST(NAME AS ""Name"", CODE AS ""Code"", 'string' AS ""str"", ID) FROM COUNTRY";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLAGG function. ")]
		public void TestXmlAggregateunction()
		{
			const string query1 = @"SELECT XMLAGG(XMLELEMENT(""Country"", XMLATTRIBUTES(NAME AS ""Name"", CODE AS ""Code"", ID AS ""Id"")) ORDER BY ID) FROM COUNTRY";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests function call with optional parameters. ")]
		public void TestFunctionCallWithOptionalParameters()
		{
			const string query1 = @"SELECT SQLPAD_FUNCTION(1, P2 => 2, P3 => 3) + 1, SYS.STANDARD.ROUND(2.123456, RIGHT => 2) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests MULTISET expressions. ")]
		public void TestMultisetExpressions()
		{
			const string query1 = @"SELECT COL MULTISET EXCEPT DISTINCT COL MULTISET_EXCEPT, COL MULTISET INTERSECT ALL COL MULTISET_INTERSECT, COL MULTISET UNION COL MULTISET_UNION FROM OBJECT_TABLE";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests MULTISET conditions. ")]
		public void TestMultisetConditions()
		{
			const string query1 = @"SELECT * FROM HUSQVIK.OBJECT_TABLE, OBJECT_TABLE OT WHERE HUSQVIK.OBJECT_TABLE.COL IS NOT A SET OR OT.COL IS EMPTY OR TEST_TYPE('Val') MEMBER OF OT.COL OR OT.COL SUBMULTISET OT.COL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests unfinished join clause. ")]
		public void TestUnfinishedJoinClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION JOIN TARGETGROUP ";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single().Validate();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			var terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(6);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.Join);
			terminals[5].Id.ShouldBe(Terminals.ObjectIdentifier);

			const string query2 = @"SELECT NULL FROM SELECTION JOIN HUSQVIK.TARGETGROUP ON ";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single().Validate();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(9);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.Join);
			terminals[5].Id.ShouldBe(Terminals.SchemaIdentifier);
			terminals[6].Id.ShouldBe(Terminals.Dot);
			terminals[7].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[8].Id.ShouldBe(Terminals.On);
			terminals[8].IsKeyword.ShouldBe(true);

			const string query3 = @"SELECT NULL FROM SELECTION MY_SELECTION";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			statement = result.Single().Validate();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(5);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.ObjectAlias);

			const string query4 = @"SELECT NULL FROM SELECTION S JOIN PROJECT";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			statement = result.Single().Validate();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(7);
		}

		[Test(Description = @"Tests select list when entering new columns. ")]
		public void TestSelectListWhenEnteringNewColumns()
		{
			const string query1 = @"SELECT NAME, /* missing expression */, SELECTION_ID FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			var terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(7);

			// TODO: Precise assertions

			const string query2 = @"SELECT NAME, SELECTION./* missing column */, SELECTION_ID FROM SELECTION";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(9);

			// TODO: Precise assertions

			const string query3 = @"SELECT NAME, SELECTION./* missing column */, /* missing expression */, /* missing expression */, SELECTION.SELECTION_ID FROM SELECTION";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			var rootNode = statement.RootNode;
			
			terminals = rootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(13);

			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.Dot);
			terminals[5].Id.ShouldBe(Terminals.Comma);
			terminals[5].ParentNode.Id.ShouldBe(NonTerminals.SelectExpressionExpressionChainedList);
			terminals[6].Id.ShouldBe(Terminals.Comma);
			terminals[6].ParentNode.Id.ShouldBe(NonTerminals.SelectExpressionExpressionChainedList);
			terminals[8].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[8].ParentNode.Id.ShouldBe(NonTerminals.ObjectPrefix);
			terminals[8].ParentNode.ParentNode.Id.ShouldBe(NonTerminals.Prefix);
			terminals[8].ParentNode.ParentNode.IsGrammarValid.ShouldBe(true);

			var selectColumnNodes = rootNode.GetDescendants(NonTerminals.AliasedExpressionOrAllTableColumns).ToArray();
			selectColumnNodes.Count().ShouldBe(5);
			selectColumnNodes[0].IsGrammarValid.ShouldBe(true);
			selectColumnNodes[1].IsGrammarValid.ShouldBe(false);
			selectColumnNodes[2].IsGrammarValid.ShouldBe(false);
			selectColumnNodes[3].IsGrammarValid.ShouldBe(false);
			selectColumnNodes[4].IsGrammarValid.ShouldBe(true);

			var invalidNodes = rootNode.AllChildNodes.Where(n => !n.IsGrammarValid).ToArray();
			invalidNodes.Length.ShouldBe(3);
		}

		[Test(Description = @"Tests select list when entering math expressions. ")]
		public void TestSelectListWhenEnteringMathExpressions()
		{
			const string query1 = @"SELECT NAME, 1 + /* missing expression */, SELECTION_ID FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			var rootNode = statement.RootNode;

			var terminals = rootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(9);

			terminals[4].Id.ShouldBe(Terminals.MathPlus);
			terminals[4].ParentNode.ParentNode.Id.ShouldBe(NonTerminals.ExpressionMathOperatorChainedList);
			terminals[4].ParentNode.ParentNode.IsGrammarValid.ShouldBe(false);

			var invalidNodes = rootNode.AllChildNodes.Where(n => !n.IsGrammarValid).ToArray();
			invalidNodes.Length.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestSelectListWhenEnteringNewColumnsBeforeFromTerminal()
		{
			const string query1 = @"SELECT NAME, /* missing expression 1 */, /* missing expression 2 */ FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			var rootNode = statement.RootNode;

			var terminals = rootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(6);
		}

		[Test(Description = @"")]
		public void TestSelectListWhenEnteringMathExpressionsBeforeFromTerminal()
		{
			const string query1 = @"SELECT NAME, 1 + /* missing expression */ FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			var rootNode = statement.RootNode;

			var terminals = rootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(7);
		}

		[Test(Description = @"")]
		public void TestWithLevelAsColumnAlias()
		{
			const string query1 = @"SELECT 1 LEVEL FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestWithLevelAsTableAlias()
		{
			const string query1 = @"SELECT 1 FROM DUAL LEVEL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestEnteringSecondStatement()
		{
			const string query1 = @"SELECT 1 FROM DUAL; S";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(2);
			var statements = result.ToArray();
			statements[0].ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			statements[0].SourcePosition.IndexStart.ShouldBe(0);
			statements[0].SourcePosition.IndexEnd.ShouldBe(18);
			statements[0].SourcePosition.Length.ShouldBe(19);
			
			statements[1].ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
			statements[1].SourcePosition.IndexStart.ShouldBe(20);
			statements[1].SourcePosition.IndexEnd.ShouldBe(20);
			statements[1].SourcePosition.Length.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestCaseWhenIssue()
		{
			const string query1 = @"SELECT CASE WHEN FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestCountAsterisk()
		{
			const string query1 = @"SELECT COUNT(*), COUNT(ALL *) OVER (PARTITION BY 1 ORDER BY 1) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
		}

		[TestCase("LEFT", Terminals.Left)]
		[TestCase("RIGHT", Terminals.Right)]
		[TestCase("FULL", Terminals.Full)]
		public void TestLeftJoinClauseWhereLeftMustNotBeRecognizedAsAlias(string joinType, string terminalId)
		{
			var query1 = String.Format("SELECT NULL FROM SELECTION {0} JOIN RESPONDENTBUCKET RB ON SELECTION.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID", joinType);
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			var rootNode = statement.RootNode;

			var terminals = rootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(16);
			terminals[4].Token.Value.ShouldBe(joinType);
			terminals[4].Id.ShouldBe(terminalId);
		}

		[Test(Description = @"")]
		public void TestUnfinishedNestedJoinClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION LEFT JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID JOIN";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single().Validate();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.SequenceNotFound);

			var terminals = statement.RootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(16);
			terminals[4].Token.Value.ShouldBe("LEFT");
			terminals[4].Id.ShouldBe(Terminals.Left);
		}

		[Test(Description = @"")]
		public void TestUnfinishedNestedQuery()
		{
			const string statement1 = @"SELECT NULL FROM (SELECT NULL FROM )";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.AllTerminals.Count.ShouldBe(8);
		}

		public class IsRuleValid
		{
			[Test(Description = @"")]
			public void TestIsRuleValidSuccess()
			{
				var isRuleValid = Parser.IsRuleValid(NonTerminals.SelectList, "SELECTION.NAME, SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID");
				isRuleValid.ShouldBe(true);
			}

			[Test(Description = @"")]
			public void TestIsRuleValidFailure()
			{
				var isRuleValid = Parser.IsRuleValid(NonTerminals.SelectList, "SELECTION.NAME, SELECTION./* missing column */, SELECTION.SELECTION_ID");
				isRuleValid.ShouldBe(false);

				isRuleValid = Parser.IsRuleValid(NonTerminals.SelectList, "SELECTION.NAME, SELECTION.RESPONDENTBUCKET_ID, /* missing expression */, SELECTION.SELECTION_ID");
				isRuleValid.ShouldBe(false);

				isRuleValid = Parser.IsRuleValid(NonTerminals.SelectList, "SELECTION.NAME, SELECTION./* missing column */, /* missing expression */, SELECTION.SELECTION_ID");
				isRuleValid.ShouldBe(false);
			}
		}

		public class Commit
		{
			[Test(Description = @"")]
			public void TestCommitStatement()
			{
				const string statement1 = @"COMMIT COMMENT 'In-doubt transaction Code 36, Call (415) 555-2637' WRITE NOWAIT;";
				var result = Parser.Parse(statement1);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(5);

				terminals[0].Id.ShouldBe(Terminals.Commit);
				terminals[1].Id.ShouldBe(Terminals.Comment);
				terminals[2].Id.ShouldBe(Terminals.StringLiteral);
				terminals[3].Id.ShouldBe(Terminals.Write);
				terminals[4].Id.ShouldBe(Terminals.Nowait);
			}

			[Test(Description = @"")]
			public void TestCommitWriteBatchWithoutWait()
			{
				const string statement1 = @"COMMIT WRITE BATCH";
				var result = Parser.Parse(statement1);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(3);

				terminals[0].Id.ShouldBe(Terminals.Commit);
				terminals[1].Id.ShouldBe(Terminals.Write);
				terminals[2].Id.ShouldBe(Terminals.Batch);
			}

			[Test(Description = @"")]
			public void TestCommitForceDistributedTransaction()
			{
				const string statement1 = @"COMMIT FORCE '22.57.53', 1234 /* specific SCN */";
				var result = Parser.Parse(statement1);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(5);

				terminals[0].Id.ShouldBe(Terminals.Commit);
				terminals[1].Id.ShouldBe(Terminals.Force);
				terminals[2].Id.ShouldBe(Terminals.StringLiteral);
				terminals[3].Id.ShouldBe(Terminals.Comma);
				terminals[4].Id.ShouldBe(Terminals.IntegerLiteral);
			}
		}

		public class SetTransaction
		{
			[Test(Description = @"")]
			public void TestSetReadOnlyTransactionWithName()
			{
				const string statement1 = @"SET TRANSACTION READ ONLY NAME 'Toronto'";
				var statement = Parser.Parse(statement1).Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminalCandidates = statement.AllTerminals.ToArray();
				terminalCandidates.Length.ShouldBe(6);
				terminalCandidates[0].Id.ShouldBe(Terminals.Set);
				terminalCandidates[1].Id.ShouldBe(Terminals.Transaction);
				terminalCandidates[2].Id.ShouldBe(Terminals.Read);
				terminalCandidates[3].Id.ShouldBe(Terminals.Only);
				terminalCandidates[4].Id.ShouldBe(Terminals.Name);
				terminalCandidates[5].Id.ShouldBe(Terminals.StringLiteral);
			}

			[Test(Description = @"")]
			public void TestSetTransactionWithReadCommittedIsolationLevel()
			{
				const string statement1 = @"SET TRANSACTION ISOLATION LEVEL READ COMMITTED";
				var statement = Parser.Parse(statement1).Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminalCandidates = statement.AllTerminals.ToArray();
				terminalCandidates.Length.ShouldBe(6);
				terminalCandidates[0].Id.ShouldBe(Terminals.Set);
				terminalCandidates[1].Id.ShouldBe(Terminals.Transaction);
				terminalCandidates[2].Id.ShouldBe(Terminals.Isolation);
				terminalCandidates[3].Id.ShouldBe(Terminals.Level);
				terminalCandidates[4].Id.ShouldBe(Terminals.Read);
				terminalCandidates[5].Id.ShouldBe(Terminals.Committed);
			}

			[Test(Description = @"")]
			public void TestSetTransactionWithRollbackSegment()
			{
				const string statement1 = @"SET TRANSACTION USE ROLLBACK SEGMENT ROLLBACK_SEGMENT NAME 'HQ Transaction'";
				var statement = Parser.Parse(statement1).Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminalCandidates = statement.AllTerminals.ToArray();
				terminalCandidates.Length.ShouldBe(8);
				terminalCandidates[0].Id.ShouldBe(Terminals.Set);
				terminalCandidates[1].Id.ShouldBe(Terminals.Transaction);
				terminalCandidates[2].Id.ShouldBe(Terminals.Use);
				terminalCandidates[3].Id.ShouldBe(Terminals.Rollback);
				terminalCandidates[4].Id.ShouldBe(Terminals.Segment);
				terminalCandidates[5].Id.ShouldBe(Terminals.Identifier);
				terminalCandidates[6].Id.ShouldBe(Terminals.Name);
				terminalCandidates[7].Id.ShouldBe(Terminals.StringLiteral);
			}
		}

		public class TerminalCandidates
		{
			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterSelectColumnWithAlias()
			{
				const string statement1 = @"SELECT 1 A";
				var node = Parser.Parse(statement1).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).OrderBy(t => t).ToArray();
				terminalCandidates.Length.ShouldBe(2);
				terminalCandidates[0].ShouldBe(Terminals.Comma);
				terminalCandidates[1].ShouldBe(Terminals.From);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterSelectColumnWithLiteral()
			{
				const string statement2 = @"SELECT 1";
				var node = Parser.Parse(statement2).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).OrderBy(t => t).ToArray();
				terminalCandidates.Length.ShouldBe(9);
				terminalCandidates[0].ShouldBe(Terminals.As);
				terminalCandidates[1].ShouldBe(Terminals.ColumnAlias);
				terminalCandidates[4].ShouldBe(Terminals.MathDivide);
				terminalCandidates[8].ShouldBe(Terminals.OperatorConcatenation);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterSelectColumnWithObjectPrefix()
			{
				const string statement2 = @"SELECT OBJECT_PREFIX.";
				var node = Parser.Parse(statement2).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).OrderBy(t => t).ToArray();
				terminalCandidates.Length.ShouldBe(5);
				terminalCandidates[0].ShouldBe(Terminals.Asterisk);
				terminalCandidates[1].ShouldBe(Terminals.Identifier);
				terminalCandidates[2].ShouldBe(Terminals.RowIdPseudoColumn);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesWithNullNode()
			{
				var terminalCandidates = Parser.GetTerminalCandidates(null).OrderBy(t => t).ToArray();
				terminalCandidates.Length.ShouldBe(9);
				terminalCandidates[0].ShouldBe(Terminals.Commit);
				terminalCandidates[1].ShouldBe(Terminals.Delete);
				terminalCandidates[2].ShouldBe(Terminals.Insert);
				terminalCandidates[3].ShouldBe(Terminals.LeftParenthesis);
				terminalCandidates[4].ShouldBe(Terminals.Merge);
				terminalCandidates[5].ShouldBe(Terminals.Select);
				terminalCandidates[6].ShouldBe(Terminals.Set);
				terminalCandidates[7].ShouldBe(Terminals.Update);
				terminalCandidates[8].ShouldBe(Terminals.With);
			}
		}

		public class Delete
		{
			[Test(Description = @"")]
			public void TestDeleteWithReturningAndErrorLogging()
			{
				const string statement1 = @"DELETE FROM TEST_TABLE RETURNING COLUMN1, COLUMN2 INTO :C1, :C2 LOG ERRORS INTO SCHEMA.LOG_TABLE ('Delete statement') REJECT LIMIT 10";
				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
				
				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(25);
			}
		}

		public class Update
		{
			[Test(Description = @"")]
			public void TestUpdateWithExpressionAndScalarSubqueryAndReturningAndErrorLogging()
			{
				const string statement1 = @"UPDATE TEST_TABLE SET COLUMN1 = 2 / (COLUMN2 + 2), COLUMN2 = (SELECT 1 FROM DUAL), (COLUMN3, COLUMN4) = (SELECT X1, X2 FROM X3), COLUMN5 = DEFAULT RETURNING COLUMN1, COLUMN2 INTO :C1, :C2 LOG ERRORS INTO SCHEMA.LOG_TABLE ('Delete statement') REJECT LIMIT UNLIMITED";
				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(62);
			}

			[Test(Description = @"")]
			public void TestUpdateWithSetObjectValueFromSubquery()
			{
				const string statement1 = @"UPDATE PEOPLE_DEMO1 P SET VALUE(P) = (SELECT VALUE(Q) FROM PEOPLE_DEMO2 Q WHERE P.DEPARTMENT_ID = Q.DEPARTMENT_ID) WHERE P.DEPARTMENT_ID = 10;";
				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(33);
			}
		}

		public class Merge
		{
			[Test(Description = @"")]
			public void TestMergeWithTableReferenceAndErrorLogging()
			{
				const string statement1 = @"MERGE INTO IDX_TEST TARGET USING IDX_TEST PARTITION (P_C3_10) AS OF TIMESTAMP SYSDATE - 0.01 SOURCE ON (1 = 1) WHEN NOT MATCHED THEN INSERT (C1) VALUES (1) LOG ERRORS INTO SCHEMA.LOG_TABLE ('Merge statement') REJECT LIMIT UNLIMITED;";
				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(47);
			}

			[Test(Description = @"")]
			public void TestMergeWithSubqueryAndAllUpdateDeleteInsertClauses()
			{
				const string statement1 = @"MERGE INTO IDX_TEST USING LATERAL (SELECT * FROM DUAL WITH CHECK OPTION) ON (1 = 1) WHEN MATCHED THEN UPDATE SET C10 = C10 DELETE WHERE C1 IS NULL WHEN NOT MATCHED THEN INSERT (C1) VALUES (1) WHERE 1 = 1";
				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(49);
			}

			[Test(Description = @"")]
			public void TestMergeWithTableCollectionExpression()
			{
				const string statement1 = @"MERGE INTO IDX_TEST USING TABLE((DATABASE_ALERTS)) ON (1 = 1) WHEN NOT MATCHED THEN INSERT (C1) VALUES (1)";
				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(28);
			}
		}

		public class Insert
		{
			[Test(Description = @"")]
			public void TestSingleTableInsertWithErrorLogging()
			{
				const string statement1 =
@"INSERT INTO ERRORLOGTEST(ID, PARENT_ID, VAL1, VAL2, VAL3)
SELECT * FROM
    (SELECT 1 ID, NULL PARENT_ID, 'Row 1 Column 1' VAL1, 'A' VAL2, 'Row 1 Column 3' VAL3 FROM DUAL UNION ALL
    SELECT 2 ID, 999 PARENT_ID, 'Row 2 Column 1' VAL1, 'B' VAL2, 'Row 2 Column 3' VAL3 FROM DUAL) TESTDATA
LOG ERRORS INTO ERR$_ERRORLOGTEST ('COMMAND TAG 1') REJECT LIMIT UNLIMITED;";

				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(66);
			}

			[Test(Description = @"")]
			public void TestMultiTableInsertAll()
			{
				const string statement1 =
@"INSERT ALL
    INTO TT VALUES (C1, NULL, NULL)
    INTO TT VALUES (NULL, C2, NULL)
    INTO TT VALUES (NULL, NULL, C3)
SELECT (ROWNUM - 1) * 3 + 1 C1, (ROWNUM - 1) * 3 + 2 C2, (ROWNUM - 1) * 3 + 3 C3
FROM DUAL CONNECT BY LEVEL <= 3";

				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(72);
			}

			[Test(Description = @"")]
			public void TestMultiTableInsertConditional()
			{
				const string statement1 =
@"INSERT ALL
    WHEN 1 = 1 THEN
        INTO INSERT1
    WHEN VAL > 5 THEN
        INTO INSERT2
    WHEN VAL IN (5, 6) THEN
        INTO INSERT3
SELECT LEVEL VAL FROM DUAL CONNECT BY LEVEL <= 10";

				var statements = Parser.Parse(statement1);
				statements.Count.ShouldBe(1);
				var statement = statements.Single();
				statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(37);
			}
		}

		private static OracleTokenReader CreateTokenReader(string sqlText)
		{
			Trace.WriteLine("SQL text: " + sqlText);

			return OracleTokenReader.Create(new StringReader(sqlText));
		}
    }
}
