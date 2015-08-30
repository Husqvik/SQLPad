using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleSqlParserTest
	{
		private static readonly OracleSqlParser Parser = OracleSqlParser.Instance;

		[Test(Description = @"")]
		public void TestComplexStatementParsing()
		{
			using (var reader = File.OpenText(@"TestFiles\SqlStatements1.sql"))
			{
				var statements = Parser.Parse(OracleTokenReader.Create(reader));
				statements.Count.ShouldBe(18);

				statements.ToList().ForEach(s => s.ParseStatus.ShouldBe(ParseStatus.Success));
			}
		}

		[Test(Description = @"Tests exception raise when null token reader is passed")]
		public void TestNullTokenReader()
		{
			Assert.Throws<ArgumentNullException>(() => Parser.Parse((OracleTokenReader)null));
		}

		[Test(Description = @"Tests empty string. ")]
		public void TestEmptyString()
		{
			var result = Parser.Parse(CreateTokenReader(String.Empty));

			result.ShouldNotBe(null);
			result.Count.ShouldBe(0);
		}

		[Test(Description = @"Tests trivial query. ")]
		public void TestTrivialQuery()
		{
			const string sqlText = @"SELECT NULL FROM DUAL";
			var result = Parser.Parse(CreateTokenReader(sqlText));
			
			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var statement = (OracleStatement)result.Single();
			statement.IsPlSql.ShouldBe(false);
			statement.RootNode.AllChildNodes.ToList()
				.ForEach(n => n.Statement.ShouldBe(statement));

			var terminals = statement.AllTerminals.ToList();

			terminals.Count.ShouldBe(4);
			terminals[0].Id.ShouldBe(Terminals.Select);
			terminals[0].SourcePosition.IndexStart.ShouldBe(0);
			terminals[0].SourcePosition.IndexEnd.ShouldBe(5);
			terminals[0].IsReservedWord.ShouldBe(true);
			terminals[1].Id.ShouldBe(Terminals.Null);
			terminals[1].SourcePosition.IndexStart.ShouldBe(7);
			terminals[1].SourcePosition.IndexEnd.ShouldBe(10);
			terminals[1].IsReservedWord.ShouldBe(true);
			terminals[2].Id.ShouldBe(Terminals.From);
			terminals[2].SourcePosition.IndexStart.ShouldBe(12);
			terminals[2].SourcePosition.IndexEnd.ShouldBe(15);
			terminals[2].IsReservedWord.ShouldBe(true);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[3].Token.Value.ShouldBe("DUAL");
			terminals[3].SourcePosition.IndexStart.ShouldBe(17);
			terminals[3].SourcePosition.IndexEnd.ShouldBe(20);
			terminals[3].IsReservedWord.ShouldBe(false);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var rootToken = statement.RootNode;
			var terminals = rootToken.Terminals.ToArray();

			terminals.Length.ShouldBe(14);
			terminals[0].Id.ShouldBe(Terminals.Select);
			terminals[1].Id.ShouldBe(Terminals.Null);
			terminals[2].Id.ShouldBe(Terminals.As);
			terminals[2].IsReservedWord.ShouldBe(false);
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
			terminals[1].IsReservedWord.ShouldBe(false);
			terminals[2].Id.ShouldBe(Terminals.When);
			terminals[2].IsReservedWord.ShouldBe(false);
			terminals[3].Id.ShouldBe(Terminals.NumberLiteral);
			terminals[3].Token.Value.ShouldBe("1");
			terminals[16].Id.ShouldBe(Terminals.End);
			terminals[16].IsReservedWord.ShouldBe(false);
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
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
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
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

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
			result[0].ParseStatus.ShouldBe(ParseStatus.Success);
			result[1].ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests query. ")]
		public void TestValidQueryWithExtraToken()
		{
			const string sqlText = @"SELECT 1 FROM DUAL,";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"Tests simple query with common table expression. ")]
		public void TestSimpleCommonTableExpressions()
		{
			const string sqlText = @"WITH X(A, B, C) AS (SELECT 1 FROM D) SELECT * FROM D;";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple query with IN and BETWEEN clauses. ")]
		public void TestInAndBetweenClauses()
		{
			const string sqlText = @"SELECT 1 FROM DUAL WHERE 1 IN (1, 2, 3) AND 4 NOT IN (5, 6, 7) AND (8 + 0) BETWEEN (0 * 0) AND (9 - 0);";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple query blocks connected by set operations. ")]
		public void TestSimpleQueryBlocksConnectedBySetOperations()
		{
			const string sqlText = @"SELECT 1 FROM DUAL UNION ALL SELECT 1 FROM DUAL UNION SELECT 1 FROM DUAL MINUS SELECT 1 FROM DUAL INTERSECT SELECT 1 FROM DUAL";
			var result = Parser.Parse(sqlText);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with group by and having clauses. ")]
		public void TestSimpleQueriesWithGroupByAndHavingClauses()
		{
			const string query1 = @"SELECT 1 FROM DUAL GROUP BY 1, DUMMY HAVING 1 = 1";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with for update clause. ")]
		public void TestForUpdateClause()
		{
			const string query1 = @"SELECT 1 FROM DUAL FOR UPDATE SKIP LOCKED";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE NOWAIT";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE WAIT 10E-0";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query4 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE WAIT -1";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query5 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE OF SCHEMA.OBJECT.COLUMN1, COLUMN2, OBJECT.COLUMN3 WAIT 1";
			result = Parser.Parse(query5);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

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
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 'A' NAN, 'B' AS INFINITE FROM DUAL WHERE 1 IS NOT NAN AND 1 IS NOT INFINITE";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests empty token set. ")]
		public void TestParsingOfEmptyTokenSet()
		{
			const string query1 = @"--SELECT 1 FROM DUAL WHERE NULL IS NULL AND 1 IS NOT NULL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(0);
		}

		[Test(Description = @"Tests parsing of valid statement after invalid statement. ")]
		public void TestParsingOfValidStatementAfterInvalidStatement()
		{
			const string query1 = @"/*invalid statement */ SELECT 1 FROM DUAL+;/* valid statement */ SELECT 1 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(2);
			var firstStatement = result.First();
			firstStatement.Validate().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
			firstStatement.RootNode.ChildNodes.Count.ShouldBe(1);
			firstStatement.AllTerminals.Count().ShouldBe(4);

			var secondStatement = result.Last();
			secondStatement.ParseStatus.ShouldBe(ParseStatus.Success);
			secondStatement.RootNode.ChildNodes.Count.ShouldBe(1);
			secondStatement.AllTerminals.Count().ShouldBe(4);
		}

		[Test(Description = @"Tests EXISTS and NOT EXISTS. ")]
		public void TestExistsAndNotExistsClauses()
		{
			const string query1 = @"SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM DUAL) THEN 'FALSE' ELSE 'TRUE' END FROM DUAL WHERE EXISTS (SELECT 1 FROM DUAL) AND EXISTS (SELECT 2 FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests flashback clauses. ")]
		public void TestFlashbackClauses()
		{
			const string query1 = @"SELECT DATE'2014-03-08' - DATE'2014-02-20' FROM T1 VERSIONS BETWEEN SCN MINVALUE AND MAXVALUE AS OF SCN 123";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			const string query2 = @"SELECT * FROM T1 VERSIONS BETWEEN TIMESTAMP TIMESTAMP'2014-02-20 00:00:00' AND DATE'2014-02-20'";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests chained asterisk clause. ")]
		public void TestAsteriskExpressions()
		{
			const string query1 = @"SELECT T1.*, 1, T1.*, 2, T1.*, 3 FROM T1";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests LIKE clause. ")]
		public void TestLikeClauses()
		{
			const string query1 = @"SELECT CASE WHEN 'abc' LIKE 'a%' OR '123' LIKEC '1%' OR '456' LIKE2 '4%' OR '789' LIKE4 '%7%' ESCAPE '\' THEN 'true' END FROM DUAL WHERE 'def' LIKE 'd%' ESCAPE '\'";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests NOT LIKE clause. ")]
		public void TestNotLikeClauses()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE DUMMY NOT LIKE 'X'";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests IN clause with subquery. ")]
		public void TestInSubqueryClause()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE 1 NOT IN (WITH XXX AS (SELECT 1 FROM DUAL) SELECT 1 FROM DUAL DD) AND EXISTS (WITH XXX AS (SELECT 1 FROM DUAL) SELECT 1 FROM DUAL DD)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests scalar subquery clause. ")]
		public void TestScalarSubqueryClause()
		{
			const string query1 = @"SELECT 1 C1, (SELECT (SELECT 2 FROM DUAL) FROM DUAL) C2, (SELECT 3 FROM DUAL) C3 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests join clauses. ")]
		public void TestJoinClauses()
		{
			const string query1 = @"SELECT * FROM T1 CROSS JOIN T2 JOIN T3 ON 1 = 1 INNER JOIN T4 USING (ID) NATURAL JOIN T5 FULL OUTER JOIN T6 ALIAS ON T5.ID = T6.ID, V$SESSION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

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
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			const string query3 = @"SELECT 1 FROM DUAL T1 LEFT OUTER JOIN DUAL T2 PARTITION BY (T2.DUMMY, DUMMY, (DUMMY, DUMMY)) ON (T1.DUMMY = T2.DUMMY)";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"Tests order by clause. ")]
		public void TestOrderByClause()
		{
			const string query1 = @"SELECT 1 FROM DUAL ORDER BY 1 DESC, DUMMY ASC NULLS LAST, (SELECT DBMS_RANDOM.VALUE FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests hierarchical query clauses. ")]
		public void TestHierarchicalQueryClauses()
		{
			const string query1 = @"SELECT LEVEL FROM DUAL CONNECT BY NOCYCLE LEVEL <= 5 AND 1 = 1 START WITH 1 = 1";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			const string query2 = @"SELECT LEVEL FROM DUAL START WITH 1 = 1 CONNECT BY NOCYCLE LEVEL <= 5 AND 1 = 1";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests group by rollup, cube and grouping sets clauses. ")]
		public void TestGroupByRollupCubeAndGroupingSetsClauses()
		{
			const string query1 = @"SELECT 1 FROM DUAL GROUP BY 1, 2, (3 || DUMMY), ROLLUP(1, (1, 2 || DUMMY)), CUBE(2, (3, 4 + 2))";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL GROUP BY GROUPING SETS (ROLLUP(DUMMY), 1, DUMMY, (1, DUMMY), ROLLUP(DUMMY), CUBE(DUMMY)), GROUPING SETS (1), CUBE(1), ROLLUP(1, DUMMY);";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL GROUP BY GROUPING SETS (DUMMY, (), DUMMY, ());";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests ANY and ALL operators with expression list and subquery. ")]
		public void TestAnyAllAndSomeOperatorsWithExpressionListAndSubquery()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE DUMMY = ANY(1, 2, 3) OR DUMMY = SOME(1, 2, 3) OR DUMMY = ALL(SELECT * FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL WHERE (1, 2, 3) = ALL(SELECT 1, 2, 3 FROM DUAL)";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL WHERE () = ANY(1, 2, 3)";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query4 = @"SELECT 1 FROM DUAL WHERE (1, 2) = ANY((1, 2), (3, 4), ())";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests sample clause. ")]
		public void TestSampleClause()
		{
			const string query1 = @"SELECT * FROM COUNTRY SAMPLE BLOCK (0.1) SEED (1)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests nested subquery. ")]
		public void TestNestedSubquery()
		{
			const string query1 = @"SELECT * FROM (SELECT LEVEL ORDERED_COLUMN FROM DUAL CONNECT BY LEVEL <= 5 ORDER BY ORDERED_COLUMN DESC) WHERE ROWNUM <= 3";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests incorrectly nested common table expression. ")]
		public void TestIncorrectlyNestedCommonTableExpression()
		{
			const string query1 = @"WITH TEST AS (WITH T2 AS (SELECT 1 FROM DUAL) SELECT * FROM T2) SELECT * FROM TEST";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with multiple tables in from clause; also tests '$' character within standard identifier. ")]
		public void TestMultipleTablesInFromClauseAndDollarCharacterWithinIdentifier()
		{
			const string query1 = @"SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with old outer join clause. ")]
		public void TestOldOuterJoinClause()
		{
			const string query1 = @"SELECT * FROM T1, T2 WHERE T1.C1 = T2.C2(+) AND T1.C2 LIKE T2.C2 (+) ESCAPE '\'";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM DUAL T1, DUAL T2 WHERE T1.DUMMY(+) = (T2.DUMMY)";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT * FROM DUAL T1, DUAL T2 WHERE (T1.DUMMY)(+) = T2.DUMMY";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with fully qualified object name with asterisk operator. ")]
		public void TestFullyQualifiedObjectNameWithAsteriskOperator()
		{
			const string query1 = @"SELECT SYS.DUAL.* FROM SYS.DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with analytic clause. ")]
		public void TestQueryWithAnalyticClause()
		{
			const string query1 = @"SELECT SUM(LENGTH(DUMMY)) OVER (PARTITION BY DUMMY ORDER BY DUMMY DESC ROWS BETWEEN DBMS_RANDOM.VALUE PRECEDING AND UNBOUNDED FOLLOWING) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT SUM(LENGTH(DUMMY)) OVER (PARTITION BY DUMMY RANGE CURRENT ROW) FROM SYS.""DUAL""";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT SUM(LENGTH(DUMMY) * (2)) OVER () FROM SYS.DUAL";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query4 = @"SELECT LAG(SYS.DUAL.DUMMY, 1) OVER (ORDER BY SYS.DUAL.DUMMY NULLS LAST) FROM SYS.DUAL";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests sequence pseudo columns. ")]
		public void TestSequencePseudoColumns()
		{
			const string query1 = @"SELECT SEQ_TEST.CURRVAL, HUSQVIK.SEQ_TEST.NEXTVAL, SEQ_TEST.CURRVAL@HQ11G2 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var terminals = statement.AllTerminals.ToArray();
			terminals[3].Id.ShouldBe(Terminals.Identifier);
			terminals[9].Id.ShouldBe(Terminals.Identifier);

			const string query2 = @"SELECT CURRVAL, NEXTVAL, CURRVAL@HQ11G2 FROM DUAL";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL OFFSET 4 ROWS FETCH NEXT 20 PERCENT ROWS ONLY";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL FETCH NEXT 20 PERCENT ROW ONLY";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests common table expression with SEARCH and CYCLE clauses. ")]
		public void TestCommonTableExpressionWithSearchAndCycleClauses()
		{
			const string query1 = @"WITH T1(VAL1, VAL2) AS (SELECT 1, 2 FROM DUAL) CYCLE VAL1, VAL2 SET IS_CYCLE TO '-' DEFAULT 1, T2(VAL1, VAL2) AS (SELECT 1, 2 FROM DUAL) SEARCH DEPTH FIRST BY VAL1 DESC NULLS FIRST, VAL2 SET SEQ# SELECT 1 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests CROSS and OUTER APPLY clauses. ")]
		public void TestCrossAndOuterApplyClauses()
		{
			const string query1 = @"SELECT DUMMY T1_VAL, T2_VAL, T3_VAL FROM DUAL T1 CROSS APPLY (SELECT DUAL.DUMMY T2_VAL FROM DUAL WHERE T1.DUMMY = DUAL.DUMMY) T2 OUTER APPLY (SELECT DUAL.DUMMY T3_VAL FROM DUAL WHERE T1.DUMMY = DUAL.DUMMY) T3;";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests Flashback PERIOD FOR <identifier> clause. ")]
		public void TestFlashbackPeriodForClause()
		{
			const string query1 = @"SELECT DATE'2014-03-08' - DATE'2014-02-20' FROM T1 AS OF PERIOD FOR ""Column"" TO_DATE('2014-03-15', 'YYYY-MM-DD')";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM T1 VERSIONS PERIOD FOR DUMMY BETWEEN TIMESTAMP'2014-02-20 00:00:00' AND DATE'2014-03-08'";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests PIVOT and UNPIVOT clauses. ")]
		public void TestPivotAndUnpivotClauses()
		{
			const string query1 = @"SELECT * FROM (SELECT 1 DUMMY, 'X' LABEL FROM DUAL) PIVOT (SUM(DUMMY) AS SUM_DUMMY FOR (LABEL) IN ('X' || NULL AS X, ('Y') Y, 'Z'));";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM (SELECT 1 DUMMY, 'X1' LABEL1, 'X2' LABEL2 FROM DUAL) PIVOT XML (SUM(DUMMY) SUM_DUMMY FOR (LABEL1, LABEL2) IN (ANY, ANY))";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT * FROM (SELECT 1 CODE1, 2 CODE2, NULL CODE3 FROM DUAL) UNPIVOT INCLUDE NULLS (QUANTITY FOR CODE IN (CODE1 AS 'Code 1', CODE2 AS 'Code 2', CODE3))";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests multiple pivot expressions including COUNT(*). ")]
		public void TestMultiplePivotExpressionsIncludingCountAsterisk()
		{
			const string query1 = @"SELECT * FROM SOURCE PIVOT (SUM(DUMMY) AS SUM_DUMMY, COUNT(*) COUNT_DUMMY, AVG(DUMMY) FOR (LABEL) IN ('X' || NULL AS X, ('Y') Y, 'Z'));";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"Tests table reference parenthesis encapsulation. ")]
		public void TestTableReferenceParenthesisEncapsulation()
		{
			const string query1 = @"SELECT * FROM ((SELECT * FROM DUAL)) D";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT * FROM ((DUAL)) D";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT * FROM ((((DUAL)) D))";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
			
			const string query4 = @"SELECT * FROM ((DUAL)) AS OF SCN MAXVALUE D";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query5 = @"SELECT * FROM ((DUAL D))";
			result = Parser.Parse(query5);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query6 = @"SELECT * FROM ((DUAL D)) AS OF SCN MAXVALUE";
			result = Parser.Parse(query6);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions

			const string query7 = @"SELECT * FROM ((DUAL AS OF TIMESTAMP SYSDATE D))";
			result = Parser.Parse(query7);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions

			const string query8 = @"SELECT * FROM ((DUAL AS OF TIMESTAMP SYSDATE)) D";
			result = Parser.Parse(query8);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"")]
		public void TestDoubleTableAlias()
		{
			const string statement1 = @"SELECT NULL FROM (DUAL D) D";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"Tests table collection expression. ")]
		public void TestTableCollectionExpression()
		{
			const string query1 = @"SELECT TABLE_EXPRESSION_ALIAS1.COLUMN_VALUE, TABLE_EXPRESSION_ALIAS2.COLUMN_VALUE FROM TABLE(SYS.ODCIVARCHAR2LIST('Value 1', 'Value 2', 'Value 3')) TABLE_EXPRESSION_ALIAS1, TABLE(SYS.ODCIVARCHAR2LIST('Value 1', 'Value 2', 'Value 3'))(+) TABLE_EXPRESSION_ALIAS2";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests table collection expression with nested parenthesis. ")]
		public void TestTableCollectionExpressionWithNestedParenthesis()
		{
			const string query1 = @"SELECT * FROM TABLE(((DATABASE_ALERTS(+))))(+) VALID_ALIAS";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests nested subquery as lateral table reference. ")]
		public void TestLateralTableReference()
		{
			const string query1 = @"SELECT * FROM LATERAL (SELECT * FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests table collection expression with nested parenthesis and invalid alias position. ")]
		public void TestTableCollectionExpressionWithNestedParenthesisAndInvalidAliasPosition()
		{
			const string query1 = @"SELECT * FROM TABLE(((DATABASE_ALERTS INVALID_ALIAS)))";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests table collection expression with invalid outer join position. ")]
		public void TestTableCollectionExpressionWithInvalidOuterJoinPosition()
		{
			const string query1 = @"SELECT * FROM TABLE(((DATABASE_ALERTS)(+))) INVALID_OUTER_JOIN";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests CAST function. ")]
		public void TestCastFunction()
		{
			const string query1 = @"SELECT CAST(3 / (2 - 1) AS NUMBER(*, 1)) AS COLUMN_ALIAS1, CAST('X' || 'Y' AS VARCHAR2(30)) + 1 AS COLUMN_ALIAS2 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
			
			const string query2 = @"SELECT CAST(MULTISET(WITH X AS (SELECT 1 FROM DUAL) SELECT LEVEL FROM DUAL CONNECT BY LEVEL <= 3) AS SYS.""ODCINUMBERLIST"") COLLECTION_ALIAS FROM DUAL";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests CAST function with target type names reserved as keyword. ")]
		public void TestCastFunctionWithKeywords()
		{
			const string query1 = @"SELECT CAST(SYSTIMESTAMP AS TIMESTAMP(9) WITH TIME ZONE), CAST(SYS_EXTRACT_UTC(SYSTIMESTAMP) AS DATE) VALIDATIONTIMESTAMP, CAST(1 AS FLOAT), CAST(1 AS INTEGER), CAST(1 AS INTEGER), CAST(1 AS LONG), CAST(1 AS CHAR), CAST(1 AS VARCHAR(1 CHAR)), CAST(1 AS VARCHAR2(1)), CAST(1 AS SMALLINT), CAST(1 AS DECIMAL), CAST(1 AS NUMBER), CAST(1 AS RAW(1)), CAST(NULL AS ROWID), CAST(NULL AS TABLE) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests TREAT function. ")]
		public void TestThreatFunction()
		{
			const string query1 = @"SELECT NAME, TREAT(VALUE(PERSONS) AS EMPLOYEE_T).SALARY SALARY FROM PERSONS";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests object type nested function calling. ")]
		public void TestTreatFunctionWithObjectTypeNestedFunctionCalling()
		{
			const string query1 = @"SELECT OBJECT_VALUE, TREAT(OBJECT_VALUE AS TYPE_PARENT).PARENT_TEST_METHOD(name => 'x', value => 1).TEST_METHOD(DUMMY).NESTED_TEST_METHOD(DUMMY) FROM (SELECT TYPE_PARENT('Name1', TYPE_ITEM('ChildName1', 11)).PARENT_TEST_METHOD('x').TEST_METHOD('y') OBJECT_VALUE, DUMMY FROM DUAL)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests FROM (SUB)PARTITION clause. ")]
		public void TestPartitionAndSubpartitionClauses()
		{
			const string query1 = @"SELECT * FROM IDX_TEST PARTITION FOR (10 + 1 + LENGTH(6), TO_DATE('2014-03-18', 'YYYY-MM-DD'), 'K1') P1, IDX_TEST SUBPARTITION (P_C3_10_SP_C4_V1) P2";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests subquery restriction clause. ")]
		public void TestSubqueryRestrictionClause()
		{
			const string query1 = @"SELECT * FROM (SELECT * FROM COUNTRY WITH READ ONLY), (SELECT * FROM COUNTRY WITH CHECK OPTION), (SELECT * FROM COUNTRY WITH CHECK OPTION CONSTRAINT DUMMY_CONSTRAINT)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with bind variable expressions. ")]
		public void TestBindVariableExpressions()
		{
			const string query1 = @"SELECT:1 FROM DUAL WHERE DUMMY = :BV1 OR DUMMY IN(:""bv2"", :        BV3, : ""BV1"")";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = (OracleStatement)result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			statement.IsDataManipulation.ShouldBe(false);

			var bindVariableTerminalCount = statement.BindVariables.SelectMany(c => c.Nodes).Count();
			bindVariableTerminalCount.ShouldBe(5);

			var bindVariables = statement.BindVariables.OrderBy(v => v.Nodes.First().SourcePosition.IndexStart).ToArray();
			bindVariables.Length.ShouldBe(4);
			bindVariables[0].Name.ShouldBe("1");
			bindVariables[1].Name.ShouldBe("BV1");
			bindVariables[2].Name.ShouldBe("\"bv2\"");
			bindVariables[3].Name.ShouldBe("BV3");
		}

		[Test(Description = @"Tests data manipulation resolution. ")]
		public void TestIsDataManipulation()
		{
			const string statementText =
@"UPDATE TEST_TABLE SET TEST_COLUMN = NULL;
INSERT INTO TEST_TABLE VALUES (NULL);
DELETE TEST_TABLE;
MERGE INTO TEST_TABLE;
CREATE TABLE TEST_TABLE (TEST_COLUMN NUMBER);
SELECT NULL FROM DUAL";

			var result = Parser.Parse(statementText);

			result.Count.ShouldBe(6);
			result[0].ParseStatus.ShouldBe(ParseStatus.Success);
			result[0].IsDataManipulation.ShouldBe(true);
			result[1].ParseStatus.ShouldBe(ParseStatus.Success);
			result[1].IsDataManipulation.ShouldBe(true);
			result[2].ParseStatus.ShouldBe(ParseStatus.Success);
			result[2].IsDataManipulation.ShouldBe(true);
			result[3].ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
			result[3].IsDataManipulation.ShouldBe(true);
			result[4].ParseStatus.ShouldBe(ParseStatus.Success);
			result[4].IsDataManipulation.ShouldBe(false);
			result[5].ParseStatus.ShouldBe(ParseStatus.Success);
			result[5].IsDataManipulation.ShouldBe(false);
		}

		[Test(Description = @"Tests KEEP clause. ")]
		public void TestKeepClause()
		{
			const string query1 = @"SELECT MAX(SELECTION_ID) KEEP (DENSE_RANK FIRST ORDER BY RESPONDENTBUCKET_ID) + 1 FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests RESPECT and IGNORE NULLS clause. ")]
		public void TestRespectAndIgnoreNullsClause()
		{
			const string query1 =
@"SELECT
	""FIRST_VALUE""(SELECTION_ID IGNORE NULLS) OVER () + 1,
	FIRST_VALUE(SELECTION_ID RESPECT NULLS) OVER () + 1,
	""LAST_VALUE""(SELECTION_ID) IGNORE NULLS OVER () + 1,
	LAST_VALUE(SELECTION_ID) RESPECT NULLS OVER () + 1
FROM SELECTION";

			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests invalid IGNORE NULLS clause. ")]
		public void TestInvalidIgnoreNullsClause()
		{
			const string query1 = @"SELECT FIRST_VALUE(SELECTION_ID IGNORE NULLS) IGNORE NULLS OVER () FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests SYSDATE keyword. ")]
		public void TestSysDateKeyword()
		{
			const string query1 = @"SELECT SYSDATE + 1 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests SYSDATE as invalid alias. ")]
		public void TestSysDateAsInvalidAlias()
		{
			const string query1 = @"SELECT 1 SYSDATE FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().Validate().ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests LNNVL function. ")]
		public void TestLnnvlFunction()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE LNNVL(1 = 0) AND LNNVL(NULL = 0)";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests LISTAGG aggregation function. ")]
		public void TestListaggAggregationFunction()
		{
			const string query1 = @"SELECT LISTAGG(NAME || '*') WITHIN GROUP (ORDER BY NULL) || 'Postfix' FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests LISTAGG analytic function. ")]
		public void TestListaggAnalyticFunction()
		{
			const string query1 = @"SELECT LISTAGG(NAME, SQLPAD_FUNCTION(SELECTION_ID) || '()') WITHIN GROUP (ORDER BY NULL) OVER () FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

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
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

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
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLTABLE function without column specification. ")]
		public void TestXmlTableFunctionWithoutColumnListSpecification()
		{
			const string query1 =
@"SELECT
    *
FROM
    XMLTABLE(
        XMLNAMESPACES('http://sqlpad.com/test1' AS test1, DEFAULT '', 'http://sqlpad.com/test2' AS ""test2""),
        '//Root/Item'
        PASSING XMLTYPE('<Root><Item Attribute=""A1"">Item11</Item><Item Attribute=""B2"">Item2</Item><Item Attribute=""C3"">Item3</Item></Root>')
    )";

			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);
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
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests EXTRACT function. ")]
		public void TestExtractFunction()
		{
			const string query1 = @"SELECT EXTRACT(MONTH FROM SYSDATE + 1) + 1, EXTRACT(TIMEZONE_REGION FROM SYSTIMESTAMP) || 'P', EXTRACT(TIMEZONE_ABBR FROM CURRENT_TIMESTAMP) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLFOREST function. ")]
		public void TestXmlForestFunction()
		{
			const string query1 = @"SELECT XMLFOREST(NAME AS ""Name"", CODE AS ""Code"", 'string' AS ""str"", ID) FROM COUNTRY";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLAGG function. ")]
		public void TestXmlAggregateFunction()
		{
			const string query1 = @"SELECT XMLAGG(XMLELEMENT(""Country"", XMLATTRIBUTES(NAME AS ""Name"", CODE AS ""Code"", ID AS ""Id"")) ORDER BY ID) FROM COUNTRY";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLPARSE function. ")]
		public void TestXmlParseFunction()
		{
			const string query1 = @"SELECT XMLPARSE(CONTENT '124 <purchaseOrder poNo=""12435""><customerName>Acme Enterprises</customerName><itemNo>32987457</itemNo></purchaseOrder>' WELLFORMED) || 'PostFix' AS PO FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests XMLROOT function. ")]
		public void TestXmlRootFunction()
		{
			const string query1 = @"SELECT XMLROOT(XMLTYPE('<poid>143598</poid>'), VERSION '1.0', STANDALONE YES) AS ""XMLROOT1"", XMLROOT(XMLTYPE('<poid>143598</poid>'), VERSION NO VALUE, STANDALONE NO VALUE) || 'PostFix' XML2 FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests function call with optional parameters. ")]
		public void TestFunctionCallWithOptionalParameters()
		{
			const string query1 = @"SELECT SQLPAD_FUNCTION(1, P2 => 2, P3 => 3) + 1, SYS.STANDARD.ROUND(2.123456, RIGHT => 2) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests MULTISET expressions. ")]
		public void TestMultisetExpressions()
		{
			const string query1 = @"SELECT COL MULTISET EXCEPT DISTINCT COL MULTISET_EXCEPT, COL MULTISET INTERSECT ALL COL MULTISET_INTERSECT, COL MULTISET UNION COL MULTISET_UNION FROM OBJECT_TABLE";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests MULTISET conditions. ")]
		public void TestMultisetConditions()
		{
			const string query1 = @"SELECT * FROM HUSQVIK.OBJECT_TABLE, OBJECT_TABLE OT WHERE HUSQVIK.OBJECT_TABLE.COL IS NOT A SET OR OT.COL IS EMPTY OR TEST_TYPE('Val') MEMBER OF OT.COL OR OT.COL SUBMULTISET OT.COL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			result.Single().ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests unfinished join clause. ")]
		public void TestUnfinishedJoinClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION JOIN TARGETGROUP ";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
			var terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(6);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.Join);
			terminals[5].Id.ShouldBe(Terminals.ObjectIdentifier);

			const string query2 = @"SELECT NULL FROM SELECTION JOIN HUSQVIK.TARGETGROUP ON ";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(9);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.Join);
			terminals[5].Id.ShouldBe(Terminals.SchemaIdentifier);
			terminals[6].Id.ShouldBe(Terminals.Dot);
			terminals[7].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[8].Id.ShouldBe(Terminals.On);
			terminals[8].IsReservedWord.ShouldBe(true);

			const string query3 = @"SELECT NULL FROM SELECTION MY_SELECTION";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			statement = result.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(5);
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.ObjectAlias);

			const string query4 = @"SELECT NULL FROM SELECTION S JOIN PROJECT";
			result = Parser.Parse(query4);

			result.Count.ShouldBe(1);
			statement = result.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
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
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
			var terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(7);

			// TODO: Precise assertions

			const string query2 = @"SELECT NAME, SELECTION./* missing column */, SELECTION_ID FROM SELECTION";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
			terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(9);

			// TODO: Precise assertions

			const string query3 = @"SELECT NAME, SELECTION./* missing column */, /* missing expression */, /* missing expression */, SELECTION.SELECTION_ID FROM SELECTION";
			result = Parser.Parse(query3);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
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
			invalidNodes.Length.ShouldBe(4);
		}

		[Test(Description = @"Tests select list when entering math expressions. ")]
		public void TestSelectListWhenEnteringMathExpressions()
		{
			const string query1 = @"SELECT NAME, 1 + /* missing expression */, SELECTION_ID FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			var rootNode = statement.RootNode;

			var terminals = rootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(9);

			terminals[4].Id.ShouldBe(Terminals.MathPlus);
			terminals[4].ParentNode.ParentNode.Id.ShouldBe(NonTerminals.ExpressionMathOperatorChainedList);
			terminals[4].ParentNode.ParentNode.IsGrammarValid.ShouldBe(false);

			var invalidNodes = rootNode.AllChildNodes.Where(n => !n.IsGrammarValid).ToArray();
			invalidNodes.Length.ShouldBe(1);

			var commentNodes = result.Comments.ToArray();
			commentNodes.Length.ShouldBe(1);
			commentNodes[0].Token.Value.ShouldBe("/* missing expression */");
			commentNodes[0].ParentNode.ShouldNotBe(null);
		}

		[Test(Description = @"Tests global and statement level comments. ")]
		public void TestGlobalAndStatementLevelComments()
		{
			const string query1 = "-- Statement title\nSELECT /*+ gather_plan_statistics */ 1 FROM DUAL";
			var result = Parser.Parse(query1);

			var commentNodes = result.Comments.ToArray();
			commentNodes.Length.ShouldBe(2);
			commentNodes[0].Token.Value.ShouldBe("-- Statement title\n");
			commentNodes[0].ParentNode.ShouldBe(null);
			commentNodes[1].Token.Value.ShouldBe("/*+ gather_plan_statistics */");
			commentNodes[1].ParentNode.ShouldNotBe(null);
		}

		[Test(Description = @"Tests quoted notation values. ")]
		public void TestQuotedNotationValues()
		{
			const string query1 = "SELECT NQ'|Value|', Q'_Value_' FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var terminals = statement.AllTerminals.ToArray();
			terminals.Length.ShouldBe(6);
			terminals[1].Token.Value.ShouldBe("NQ'|Value|'");
			terminals[3].Token.Value.ShouldBe("Q'_Value_'");
		}

		[Test(Description = @"Tests timestamp <value> AT TIME ZONE <value>. ")]
		public void TestTimestampAtTimeZone()
		{
			const string query1 = "SELECT TIMESTAMP '1999-10-29 01:30:00' AT TIME ZONE 'US/Pacific' FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests year to month interval. ")]
		public void TestYearToMonthInterval()
		{
			const string query1 = "SELECT INTERVAL '5-3' YEAR TO MONTH + INTERVAL'20' MONTH FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests day to second interval. ")]
		public void TestDayToSecondInterval()
		{
			const string query1 = "SELECT INTERVAL '4 5:12:10.2222' DAY(1) TO SECOND(3) + INTERVAL '1234.5678' SECOND(9) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"")]
		public void TestSelectListWhenEnteringNewColumnsBeforeFromTerminal()
		{
			const string query1 = @"SELECT NAME, /* missing expression 1 */, /* missing expression 2 */ FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			statement.RootNode.ChildNodes.Count.ShouldBe(1);
			var rootNode = statement.RootNode;

			var terminals = rootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(6);

			var commentNodes = result.Comments.ToArray();
			commentNodes.Length.ShouldBe(2);
			commentNodes[0].Token.Value.ShouldBe("/* missing expression 1 */");
			commentNodes[0].ParentNode.ShouldNotBe(null);
			commentNodes[1].Token.Value.ShouldBe("/* missing expression 2 */");
			commentNodes[1].ParentNode.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestSelectListWhenEnteringMathExpressionsBeforeFromTerminal()
		{
			const string query1 = @"SELECT NAME, 1 + /* missing expression */ FROM SELECTION";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

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
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestWithLevelAsTableAlias()
		{
			const string query1 = @"SELECT 1 FROM DUAL LEVEL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestEnteringSecondStatement()
		{
			const string query1 = @"SELECT 1 FROM DUAL; S";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(2);
			var statements = result.ToArray();
			statements[0].ParseStatus.ShouldBe(ParseStatus.Success);
			statements[0].SourcePosition.IndexStart.ShouldBe(0);
			statements[0].SourcePosition.IndexEnd.ShouldBe(18);
			statements[0].SourcePosition.Length.ShouldBe(19);
			
			statements[1].ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
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
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			const string query2 = @"SELECT 1 + CASE WHEN FROM DUAL";
			result = Parser.Parse(query2);

			result.Count.ShouldBe(1);
			statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestCountAsterisk()
		{
			const string query1 = @"SELECT COUNT(*), COUNT(ALL *) OVER (PARTITION BY 1 ORDER BY 1) FROM DUAL";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[TestCase("LEFT", Terminals.Left)]
		[TestCase("RIGHT", Terminals.Right)]
		[TestCase("FULL", Terminals.Full)]
		public void TestLeftJoinClauseWhereLeftMustNotBeRecognizedAsAlias(string joinType, string terminalId)
		{
			var query1 = $"SELECT NULL FROM SELECTION {joinType} JOIN RESPONDENTBUCKET RB ON SELECTION.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID";
			var result = Parser.Parse(query1);

			result.Count.ShouldBe(1);
			var statement = result.Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
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
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

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

		[Test(Description = @"")]
		public void TestJoinKeywordAsAlias()
		{
			const string statement1 = @"SELECT JOIN.* FROM ""CaseSensitiveTable"" JOIN";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var terminals = statement.RootNode.Terminals.ToArray();
			terminals.Length.ShouldBe(7);
			terminals[1].Token.Value.ShouldBe("JOIN");
			terminals[1].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[6].Token.Value.ShouldBe("JOIN");
			terminals[6].Id.ShouldBe(Terminals.ObjectAlias);
		}

		[Test(Description = @"")]
		public void TestParsingCancellation()
		{
			var cancellationTokenSource = new CancellationTokenSource();

			using (var reader = File.OpenText(@"TestFiles\SqlStatements1.sql"))
			{
				var parsingStopwatch = Stopwatch.StartNew();
				var task = Parser.ParseAsync(OracleTokenReader.Create(reader), cancellationTokenSource.Token);

				var cancellationStopwatch = Stopwatch.StartNew();
				cancellationTokenSource.Cancel();

				var exception = Assert.Throws<AggregateException>(() => task.Wait(CancellationToken.None));
				exception.InnerException.ShouldBeTypeOf<TaskCanceledException>();

				parsingStopwatch.Stop();
				cancellationStopwatch.Stop();

				Trace.WriteLine($"Parsing successfully cancelled; parse time: {parsingStopwatch.ElapsedMilliseconds} ms; cancellation time: {cancellationStopwatch.ElapsedMilliseconds} ms");
			}
		}

		[Test(Description = @"")]
		public void TestInvalidCastFunction()
		{
			const string statement1 = @"SELECT CAST(Respondent_ID AS NUMBER(16) FROM RESPONDENT";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestInvalidCastFunctionFollowedByChainingClause()
		{
			const string statement1 = @"SELECT CAST(Respondent_ID AS NUMBER(16), Respondent_ID FROM RESPONDENT";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestNegativeScaleInDataType()
		{
			const string statement1 = @"SELECT CAST(99 AS NUMBER(5,-2)) FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestTimestampWithLocalTimeZoneDataType()
		{
			const string statement1 = @"SELECT CAST(SYSDATE AS TIMESTAMP (9) WITH LOCAL TIME ZONE) FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestIntervalDataTypes()
		{
			const string statement1 = @"SELECT CAST('4-8' AS INTERVAL YEAR(9) TO MONTH), CAST('2 12:34:45.6789' AS INTERVAL DAY (9) TO SECOND (9)) FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestOtherDataTypes()
		{
			const string statement1 =
@"SELECT
	CAST(1 AS INT), CAST(1.3 AS DEC(*, 1)), CAST(1.2 AS DOUBLE PRECISION), CAST(1.4 AS REAL), CAST('X' AS NATIONAL CHARACTER VARYING (30)), --CAST(NULL AS BFILE)
	CAST('Y' AS CHARACTER (1)), CAST(ROWID AS UROWID(16)), CAST('ABC' AS CHAR VARYING (3)), CAST('Z' AS NCHAR VARYING (6))
FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestDatabaseLinkWithDomainName()
		{
			const string statement1 = @"CREATE SYNONYM emp_table FOR oe.employees@remote.us.oracle.com@instance";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestQuotedStringWithApostrophe()
		{
			const string statement1 = @"SELECT q'|quoted'string|' FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestDatetimeAtTimeZoneClause()
		{
			const string statement1 = @"SELECT SYSTIMESTAMP AT TIME ZONE SESSIONTIMEZONE, TIMESTAMP'2014-10-04 12:32:34.123456' AT TIME ZONE DBTIMEZONE, SYSTIMESTAMP AT TIME ZONE 'America/Los_Angeles', DUMMY AT LOCAL FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestComplexModelClause()
		{
			const string statement1 =
@"WITH PRIME_CANDIDATES(PRIME_CANDIDATE, NUMBER_RANGE) AS
(SELECT PRIME_CANDIDATE, CAST(POWER(10, CEIL(LOG(10, PRIME_CANDIDATE))) AS NUMBER) NUMBER_RANGE FROM
    (SELECT CAST(LEVEL AS BINARY_DOUBLE) PRIME_CANDIDATE FROM DUAL CONNECT BY LEVEL <= 10000))
SELECT
    ROW_NUMBER,
    PRIME_NUMBER,
    PRIME_PRODUCT,
    ROUND(POWER(10, CASE WHEN PRIME_PRODUCT_APPROX < 1 THEN PRIME_PRODUCT_APPROX ELSE REMAINDER(PRIME_PRODUCT_APPROX, TRUNC(PRIME_PRODUCT_APPROX)) END), TRUNC(PRIME_PRODUCT_APPROX)) || 'E+' || TRUNC(PRIME_PRODUCT_APPROX) PRIME_PRODUCT_APPROX,
    TOTAL_PRIMES,
    '<' || 2 || ', ' || (NUMBER_RANGE - 1) || '>' NUMBER_RANGE,
    PRIMES_WITHIN_SAME_ORDER,
    PERCENT_WITHIN_SAME_ORDER
FROM
    (SELECT
        ROWNUM ROW_NUMBER,
        PRIME_CANDIDATE PRIME_NUMBER,
        NUMBER_RANGE
    FROM
        PRIME_CANDIDATES
    WHERE
        PRIME_CANDIDATE > 1 AND
        NOT EXISTS
            (SELECT PRIME_CANDIDATE FROM PRIME_CANDIDATES T WHERE PRIME_CANDIDATES.PRIME_CANDIDATE > T.PRIME_CANDIDATE AND T.PRIME_CANDIDATE > 1 AND MOD(PRIME_CANDIDATES.PRIME_CANDIDATE, T.PRIME_CANDIDATE) = 0)
    ORDER BY
        PRIME_NUMBER
    ) PRIME_NUMBER_DATA
MODEL
	RETURN UPDATED ROWS
    DIMENSION BY (ROW_NUMBER)
    MEASURES
    (
        PRIME_NUMBER,
        NUMBER_RANGE,
        CAST(NULL AS INTEGER) PRIME_PRODUCT,
        CAST(NULL AS NUMBER) PRIME_PRODUCT_APPROX,
        CAST(NULL AS INTEGER) TOTAL_PRIMES,
        CAST(NULL AS INTEGER) PRIMES_WITHIN_SAME_ORDER,
        CAST(NULL AS NUMBER) PERCENT_WITHIN_SAME_ORDER
    )
    RULES
    (
        PRIME_PRODUCT[1] = PRIME_NUMBER[CV()],
        PRIME_PRODUCT[ROW_NUMBER > 1] =
            CASE
                WHEN LOG(10, PRIME_PRODUCT[CV() - 1]) >= 38 THEN NULL
                ELSE PRIME_PRODUCT[CV() - 1] * PRIME_NUMBER[CV()]
            END,
        PRIME_PRODUCT_APPROX[ANY] = SUM(LOG(10, PRIME_NUMBER)) OVER (ORDER BY PRIME_NUMBER), -- sum using analytics 
        TOTAL_PRIMES[ANY] = COUNT(*) OVER (),
        PRIMES_WITHIN_SAME_ORDER[ANY] = COUNT(*) OVER (ORDER BY NUMBER_RANGE), -- ranges 2 - 9, 2 - 99, 2 - 999, ...
        PERCENT_WITHIN_SAME_ORDER[ANY] = ROUND(PRIMES_WITHIN_SAME_ORDER[CV()] / NUMBER_RANGE[CV()] * 100, 2)
    )";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestModelClauseWithIterateClause()
		{
			const string statement1 =
@"SELECT
    NEXT_DAY
    (
        LAST_DAY(ADD_MONTHS(TRUNC(SYSDATE, 'y'), cell)) - 7,
        TO_CHAR(TO_DATE('29-jan-1927', 'dd-mon-yyyy'), 'DAY') -- Saturday
    ) LAST_SATURDAY_IN_MONTH
FROM
    DUAL
MODEL
    RETURN ALL ROWS
    DIMENSION BY (0 dimension_column)
    MEASURES (0 cell)
    RULES ITERATE (12)
    (
        cell[Iteration_Number] = Iteration_Number
    )";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestModelClauseWithDimensionFromToWithDecrement()
		{
			const string statement1 =
@"SELECT * FROM DUAL
MODEL
    DIMENSION BY (0 dimension_column)
    MEASURES (0 cell)
    RULES ITERATE (12) (
        cell[FOR dimension_column FROM 9 TO 5 DECREMENT 2] = 1
    )";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestModelClauseWithDimensionInLiteralList()
		{
			const string statement1 =
@"SELECT * FROM DUAL
MODEL
    DIMENSION BY (0 dimension_column)
    MEASURES (0 cell)
    RULES ITERATE (12) (
        cell[FOR dimension_column IN (0, 10)] = 1
    )";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestModelClauseWithOtherClauses()
		{
			const string statement1 =
@"SELECT
	COUNTRY, PROD, YEAR, S
FROM
	SALES_VIEW_REF
MODEL
	PARTITION BY (COUNTRY)
	DIMENSION BY (PROD, YEAR)
	MEASURES (SALE S)
	IGNORE NAV
	UNIQUE DIMENSION
	RULES UPSERT SEQUENTIAL ORDER (
		S[PROD = 'Mouse Pad', YEAR = 2000] = S['Mouse Pad', 1998] + S['Mouse Pad', 1999],
		S['Standard Mouse', 2001] = S['Standard Mouse', 2000],
		S['Standard Mouse', 2002] = SUM(S)['Mouse Pad', YEAR BETWEEN CV() - 2 AND CV() - 1]
    )
ORDER BY
	COUNTRY, PROD, YEAR";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestModelClauseWithDimensionAndMeasuresWithAsAlias()
		{
			const string statement1 =
@"SELECT 1 FROM DUAL
MODEL
	DIMENSION BY (0 AS KEY)
	MEASURES (
    	CAST(NULL AS VARCHAR2(4000)) AS M1,
    	CAST(NULL AS VARCHAR2(4000)) AS M2
	)
	RULES UPDATE (
    	M1[ANY] = 'x',
    	M2[ANY] = 'x'
	)";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestModelClauseWithRuleWithCountAsterisk()
		{
			const string statement1 =
@"SELECT
	*
FROM DUAL
MODEL
	DIMENSION BY (0 D)
	MEASURES (0 M1, 0 M2)
	RULES (M1[ANY] = COUNT(*)[D BETWEEN 0 AND 1])";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestComplexHierarchicalQuery()
		{
			const string statement1 =
@"SELECT
    CONNECT_BY_ROOT ID STARTELEMENTID,
    CONNECT_BY_ROOT ELEMENTNAME STARTELEMENT,
    ID, PARENT_ID, LEVEL - 1 DEPTH, ELEMENTNAME,
    SYS_CONNECT_BY_PATH(ELEMENTNAME, '/') TREEPATH,
    AMOUNT,
    CONNECT_BY_ISCYCLE ISCYCLE,
    CONNECT_BY_ISLEAF ISLEAF
FROM
    HIEARCHYTEST
WHERE
    LEVEL < 4
START WITH
    ID = 8
CONNECT BY NOCYCLE PRIOR
    PARENT_ID = ID
ORDER BY
    LEVEL DESC";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestCharacterFunctionWithNationalCharacterSet()
		{
			const string statement1 = @"SELECT CHR(219 USING NCHAR_CS) FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestXmlSerializeFunction()
		{
			const string statement1 = @"SELECT XMLSERIALIZE(CONTENT XMLTYPE('<Root>value</Root>').GETCLOBVAL() AS CLOB ENCODING 'UTF-8' VERSION '1.0' INDENT SIZE = 2 SHOW DEFAULTS) || '' FROM DUAL";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestCommentWithInvalidGrammarWithOverlappingChildNodes()
		{
			const string statement1 = "SELECT NULL FROM (SELECT NULL FROM DUAL WHERE DUMMY = 'X'--77918\nAN";

			var statements = Parser.Parse(statement1);
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
		}

		[Test(Description = @"")]
		public void TestSqlPlusSeparator()
		{
			const string statement1 = "SELECT NULL FROM DUAL\n/\nSELECT NULL FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			statements.Length.ShouldBe(2);
			var statement = statements[0].Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			statement = statements[1].Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestCursorFunction()
		{
			const string statement1 = "SELECT CURSOR(SELECT * FROM DUAL) CURSOR_OUTPUT FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestXmlCastFunction()
		{
			const string statement1 = "SELECT XMLCAST(XMLQUERY('/r/v' PASSING XMLTYPE('<r><c>id</c><v>value 1</v></r>') RETURNING CONTENT) AS VARCHAR2(100)) || ' Value' FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestXmlExistsFunction()
		{
			const string statement1 = "SELECT * FROM DUAL WHERE XMLEXISTS('/r[c=\"id\"]' PASSING XMLTYPE('<r><c>id</c><v>value 1</v></r>')) AND 1 = 1";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestIsJsonCondition()
		{
			const string statement1 = "SELECT * FROM DUAL WHERE DUMMY IS NOT JSON LAX WITHOUT UNIQUE KEYS AND 1 = 1";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestJsonExistsFunction()
		{
			const string statement1 = "SELECT * FROM DUAL WHERE JSON_EXISTS('[{first: \"Jeff\"}, {last: \"Williams\"}]', '$[1].first' FALSE ON ERROR) AND 1 = 1";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestJsonQueryFunction()
		{
			const string statement1 = "SELECT JSON_QUERY('[{\"a\": 100}, {\"b\": 200}, {\"c\": 300}]', '$[0]' RETURNING VARCHAR2(100) PRETTY ASCII WITH CONDITIONAL WRAPPER EMPTY ON ERROR) || '' AS VALUE FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestJsonTableFunction()
		{
			const string statement1 =
@"SELECT
	*
FROM
	JSON_TABLE(
	  '{""PONumber""             : 1600,
		""Reference""            : ""ABULL-20140421"",
		""Requestor""            : ""Alexis Bull"",
		""User""                 : ""ABULL"",
		""CostCenter""           : ""A50"",
		""ShippingInstructions"" : {""name""   : ""Alexis Bull"",
									""Address"": {""street""     : ""200 Sporting Green"",
												  ""city""       : ""South San Francisco"",
												  ""state""      : ""CA"",
												  ""zipCode""    : 99236,
												  ""country""    : ""United States of America""},
									""Phone"" : [{""type""       : ""Office"", ""number"" : ""909-555-7307""},
												 {""type""       : ""Mobile"", ""number"" : ""415-555-1234""}]},
       ""Special Instructions"" : null,
       ""AllowPartialShipment"" : true,
       ""LineItems"" : [{""ItemNumber"" : 1,
                       ""Part"" : {""Description"" : ""One Magic Christmas"",
                                 ""UnitPrice""   : 19.95,
                                 ""UPCCode""     : 13131092899},
                       ""Quantity"" : 9.0},
                      {""ItemNumber"" : 2,
                       ""Part"" : {""Description"" : ""Lethal Weapon"",
                                 ""UnitPrice""   : 19.95,
                                 ""UPCCode""     : 85391628927},
                       ""Quantity"" : 5.0}]}',
		'$.ShippingInstructions.Phone[*]'
		DEFAULT 'invalid data' ON ERROR
		COLUMNS (
			seq$ FOR ORDINALITY,
			phone_type VARCHAR2(10) PATH '$.type',
			phone_num VARCHAR2(20) FORMAT JSON WITH CONDITIONAL WRAPPER PATH '$.number' EMPTY ON ERROR,
			has_zip VARCHAR2(5) EXISTS PATH '$.ShippingInstructions.Address.zipCode' FALSE ON ERROR
		)
	) AS T";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestJsonValueFunction()
		{
			const string statement1 = "SELECT JSON_VALUE('{firstname: \"John\"}', '$.lastname' DEFAULT 'No last name found' ON ERROR) || '' LAST_NAME FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestIncompleteInlineView()
		{
			const string statement1 = "SELECT * FROM DUAL D1 JOIN (SELECT DUMMY FROM DUAL) X";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			var terminals = statement.RootNode.Terminals.ToArray();
			terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[4].Id.ShouldBe(Terminals.ObjectAlias);
			terminals[5].Id.ShouldBe(Terminals.Join);
			terminals[8].Id.ShouldBe(Terminals.Identifier);
		}

		[Test(Description = @"")]
		public void TestCollectionFunction()
		{
			const string statement1 = "SELECT COLLECT(UNIQUE ROWNUM || 'X' ORDER BY ROWNUM + 1 DESC NULLS LAST, 1) || NULL FROM SELECTION WHERE ROWNUM <= 10";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestFunctionSupportingWithinGroupClause()
		{
			const string statement1 = "SELECT CUME_DIST(1, 1) WITHIN GROUP (ORDER BY 0, 1), RANK(1) WITHIN GROUP (ORDER BY NULL), DENSE_RANK(1) WITHIN GROUP (ORDER BY NULL), PERCENTILE_CONT(0) WITHIN GROUP (ORDER BY 1), PERCENTILE_DISC(0) WITHIN GROUP (ORDER BY 1) FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestObsoleteTheClause()
		{
			const string statement1 = "SELECT * FROM THE(SELECT SQLPAD.PIPELINED_FUNCTION() FROM DUAL)";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestTableCollectionExpressionWithSubquery()
		{
			const string statement1 = "SELECT * FROM TABLE(SELECT SQLPAD.PIPELINED_FUNCTION() FROM DUAL)(+) AS OF TIMESTAMP SYSDATE - 0.01 ALIAS";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestSelectIntoScalarVariable()
		{
			const string statement1 = "SELECT DUMMY, DUMMY INTO :X, :Y FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestSelectIntoCollectionVariable()
		{
			const string statement1 = "SELECT SELECTION_ID, NAME BULK COLLECT INTO :X, :Y FROM SELECTION";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestUserFunction()
		{
			const string statement1 = "SELECT USER FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestBasicRowPatternMatchingClause()
		{
			const string statement1 =
@"SELECT
	*
FROM
	Ticker
	MATCH_RECOGNIZE (
		 PARTITION BY symbol
		 ORDER BY tstamp
		 MEASURES
			STRT.tstamp AS start_tstamp,
			LAST(DOWN.tstamp) AS bottom_tstamp,
			LAST(UP.tstamp) AS end_tstamp
		 ONE ROW PER MATCH
		 AFTER MATCH SKIP TO LAST UP
		 PATTERN (STRT DOWN+ UP+)
		 DEFINE
			DOWN AS DOWN.price < PREV(DOWN.price),
			UP AS UP.price > PREV(UP.price)
	) MR
ORDER BY
	MR.symbol,
	MR.start_tstamp";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestRowPatternMatchingClauseWithNavigationFunction()
		{
			const string statement1 =
@"SELECT
	M.Symbol, M.Tstamp, M.Matchno, M.Classfr, M.Price, M.Avgp
FROM Ticker MATCH_RECOGNIZE (
     PARTITION BY Symbol
     ORDER BY tstamp
     MEASURES FINAL AVG(S.Price) AS Avgp,
              CLASSIFIER() AS Classfr,
              MATCH_NUMBER() AS Matchno
     ALL ROWS PER MATCH
     AFTER MATCH SKIP TO LAST B
     PATTERN ( {- A -} B+ {- C+ -} )
     SUBSET S = (A,B)
     DEFINE
        A AS A.Price >= 10,
        B AS B.Price > PREV(B.Price),
        C AS C.Price <= PREV(C.Price)
)  M
ORDER BY symbol, tstamp";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestNotBetweenCondition()
		{
			const string statement1 = @"SELECT * FROM SELECTION WHERE SELECTION_ID NOT BETWEEN 0 AND 3000000";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestParenthesisWrappedSubqueryConcatenatedWithAnotherSubquery()
		{
			const string statement1 = @"(SELECT * FROM DUAL) UNION ALL SELECT * FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestTrimSpecificGrammar()
		{
			const string statement1 = @"SELECT TRIM(BOTH 'X' || NULL FROM 'XVODKAX') FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestChrSpecificGrammar()
		{
			const string statement1 = @"SELECT CHR(50 + 0 USING NCHAR_CS) FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestNewKeywordForObjectTypeConstructor()
		{
			const string statement1 = @"SELECT NEW HUSQVIK.TEST_TYPE(10) X FROM DUAL";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestGroupValueComparisonWithoutSetOperator()
		{
			const string statement1 = @"SELECT * FROM DUAL WHERE (DUMMY, DUMMY) = (SELECT DUMMY, DUMMY FROM DUAL)";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		[Test(Description = @"")]
		public void TestHavingWithoutGroupBy()
		{
			const string statement1 = @"SELECT COUNT(*) FROM DUAL HAVING COUNT(*) = 1";

			var statements = Parser.Parse(statement1).ToArray();
			var statement = statements.Single().Validate();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
		}

		public class PlSql
		{
			[Test(Description = @"")]
			public void TestAnonymousPlSqlBlock()
			{
				const string statement1 =
@"<<LABEL1>>
DECLARE
	TYPE type1 IS RECORD (field1 NUMBER(4) NOT NULL := 10, field2 VARCHAR2(30) NOT NULL DEFAULT 'Administration', field3 NUMBER(6) := 200);
	record1 type1;
	record2 record1%TYPE;

	constant1 CONSTANT NUMBER NOT NULL := 123;
	variable1 NUMBER NOT NULL := 456;
	exception1 EXCEPTION;

	cursor1 SYS_REFCURSOR;
	cursor2 SYS_REFCURSOR;
	dummy1 VARCHAR2(4000);

	CURSOR cursor3(selection_id IN NUMBER, PROJECT_ID NUMBER) RETURN selection%ROWTYPE;
	CURSOR cursor4(selection_id VARCHAR2) RETURN record1%TYPE;

	SUBTYPE double_digit IS PLS_INTEGER RANGE 10..99;
	SUBTYPE special_clob IS CLOB CHARACTER SET NCHAR_CS NOT NULL;

	TYPE refCursorType1 IS REF CURSOR RETURN selection%ROWTYPE; -- strong type
	TYPE refCursorType2 IS REF CURSOR;							-- weak type

	refCursor1 refCursorType1; -- strong cursor variable
	refCursor2 refCursorType2; -- weak cursor variable

	TYPE associativeArrayType1 IS TABLE OF NUMBER INDEX BY VARCHAR2(64); -- associative array type indexed by VARCHAR2
	TYPE associativeArrayType2 IS TABLE OF NUMBER INDEX BY STRING(64); -- associative array type indexed by STRING
	TYPE varrayType1 IS VARRAY(4) OF VARCHAR2(15); -- varying array type
	TYPE nestedTableType1 IS TABLE OF type1; -- nested table type
BEGIN
	NULL;
	<<LABEL1>>
	DBMS_OUTPUT.PUT_LINE(a => 'Current time: ' || SYSTIMESTAMP());
	UPDATE T SET ID = NULL WHERE 1 = 0;
	FOR i IN 1..10 LOOP
		DBMS_OUTPUT.PUT_LINE(a => 'i = ' || i);
	END LOOP;
	WHILE 0 + 1 = 1 + 1 LOOP
		EXIT;
	END LOOP;

	IF SQL%ISOPEN THEN DBMS_OUTPUT.PUT_LINE(a => 'Open');
	ELSIF SQL%FOUND THEN DBMS_OUTPUT.PUT_LINE(a => 'Found');
	ELSIF SQL%NOTFOUND THEN DBMS_OUTPUT.PUT_LINE(a => 'Not found');
	END IF;

	CASE variable1
		WHEN 456 THEN DBMS_OUTPUT.PUT_LINE(a => 'Default');
		WHEN 457 THEN DBMS_OUTPUT.PUT_LINE(a => 'Non-default');
		ELSE DBMS_OUTPUT.PUT_LINE(a => 'Fallback');
	END CASE;

	CASE
		WHEN variable1 = 456 THEN DBMS_OUTPUT.PUT_LINE(a => 'Default');
		WHEN variable1 = 457 THEN DBMS_OUTPUT.PUT_LINE(a => 'Non-default');
		ELSE DBMS_OUTPUT.PUT_LINE(a => 'Fallback');
	END CASE;

	OPEN cursor1 FOR 'SELECT DUMMY FROM DUAL';
	OPEN cursor2 FOR SELECT DUMMY FROM DUAL;
	OPEN cursor3(123, 456);
 
	LOOP
		FETCH cursor1 INTO dummy1;
		EXIT WHEN cursor1%NOTFOUND;
	END LOOP;

	FOR item IN (SELECT dummy FROM DUAL)
	LOOP
		DBMS_OUTPUT.PUT_LINE('DUMMY = ' || item.dummy);
	END LOOP;
END;";

				var statement = (OracleStatement)Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
				statement.IsPlSql.ShouldBe(true);
			}

			[Test(Description = @"")]
			public void TestDummyPlSql2()
			{
				const string statement1 =
@"BEGIN NULL; END;
/
BEGIN NULL; END;
/
";

				var statements = Parser.Parse(statement1).Cast<OracleStatement>().ToList();
				statements.ForEach(s =>
				{
					s.Validate();
					s.ParseStatus.ShouldBe(ParseStatus.Success);
					s.IsPlSql.ShouldBe(true);
				});
			}

			[Test(Description = @"")]
			public void TestPlSqlAndSqlCombined()
			{
				const string statement1 =
@"BEGIN NULL; END;

SELECT * FROM DUAL";

				var statements = Parser.Parse(statement1).ToList();
				statements.Count.ShouldBe(2);

				statements.ForEach(s =>
				{
					s.Validate();
					s.ParseStatus.ShouldBe(ParseStatus.Success);
				});
			}

			[Test(Description = @"")]
			public void TestCreatePipelinedFunction()
			{
				const string statement1 =
@"CREATE FUNCTION GENERATEDAYS(DATE_FROM IN DATE, DATE_TO IN DATE, p3 VARCHAR2 DEFAULT NULL, p4 SYS.DUAL%ROWTYPE, p5 STRING) RETURN SYS.ODCIDATELIST PIPELINED
IS
	DATE_COLLECTION$ SYS.ODCIDATELIST;
	exception1 EXCEPTION;
	PRAGMA AUTONOMOUS_TRANSACTION;
	--PRAGMA RESTRICT_REFERENCES(DEFAULT, TRUST, WNPS);
	PRAGMA EXCEPTION_INIT(exception1, -1553);
	--PRAGMA SERIALLY_REUSABLE;
	PRAGMA UDF;

	variable1 STRING(30) DEFAULT 'ABC';
	constant1 CONSTANT STRING(30) NOT NULL DEFAULT 'DEF';
BEGIN
    SELECT
    	DAY BULK COLLECT INTO DATE_COLLECTION$
    FROM (
        SELECT TRUNC(DATE_FROM + COUNTER) DAY
        FROM (
        	SELECT (LEVEL - 1) COUNTER FROM DUAL
        		CONNECT BY LEVEL <= (TRUNC(DATE_TO) - TRUNC(DATE_FROM))
        )
    )
    WHERE DAY < TRUNC(DATE_TO);

    IF (DATE_COLLECTION$ IS NOT NULL AND DATE_COLLECTION$.COUNT > 0) THEN
        FOR I IN 1..DATE_COLLECTION$.COUNT
        LOOP
            PIPE ROW (DATE_COLLECTION$(I));
        END LOOP;
    END IF;

	EXECUTE IMMEDIATE 'SELECT :P1, :P2 FROM DUAL' USING DATE_FROM, DATE_TO;
END;";

				var statement = (OracleStatement)Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
				statement.IsPlSql.ShouldBe(true);
			}

			[Test(Description = @"")]
			public void TestCreateFunction()
			{
				const string statement1 =
@"CREATE OR REPLACE EDITIONABLE FUNCTION TEST_FUNCTION RETURN VARCHAR2 RESULT_CACHE RELIES_ON (HUSQVIK.SELECTION, PROJECT) IS BEGIN RETURN NULL; END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestCreateProcedure()
			{
				const string statement1 =
@"CREATE OR REPLACE NONEDITIONABLE PROCEDURE TEST_PROCEDURE AUTHID DEFINER ACCESSIBLE BY (TYPE HUSQVIK.TEST_TYPE, TEST_FUNCTION) IS EXTERNAL;";

				var statement = (OracleStatement)Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
				statement.IsPlSql.ShouldBe(true);
			}

			[Test(Description = @"")]
			public void TestCreatePackage()
			{
				const string statement1 =
@"CREATE OR REPLACE PACKAGE emp_mgmt AS
	FUNCTION hire (last_name VARCHAR2, job_id VARCHAR2, manager_id NUMBER, salary NUMBER, commission_pct NUMBER, department_id NUMBER)
		RETURN NUMBER; 
	FUNCTION create_dept(department_id NUMBER, location_id NUMBER)
		RETURN NUMBER; 
	
	PROCEDURE remove_emp(employee_id NUMBER);
	PROCEDURE remove_dept(department_id NUMBER);
	PROCEDURE increase_sal(employee_id NUMBER, salary_incr NUMBER);
	PROCEDURE increase_comm(employee_id NUMBER, comm_incr NUMBER);

	no_comm EXCEPTION; 
	no_sal EXCEPTION; 
END emp_mgmt;";

				var statement = (OracleStatement)Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
				statement.IsPlSql.ShouldBe(true);
			}

			[Test(Description = @"")]
			public void TestCreatePackageBody()
			{
				const string statement1 =
@"CREATE OR REPLACE PACKAGE BODY test_package_body IS
	
	CURSOR cursor1(selection_id IN NUMBER, PROJECT_ID NUMBER) RETURN selection%ROWTYPE IS SELECT * FROM selection;
	
	PROCEDURE p1 IS
	BEGIN
		dbms_output.put_line('procedure executed. ');
	END p1;

	FUNCTION f1 RETURN NUMBER IS
		recordType1 SYS.DUAL%ROWTYPE;
	BEGIN
		dbms_output.put_line('function executed. ');
		RETURN 0;
	END f1;

	PROCEDURE p2 IS
	BEGIN
		NULL;
	END p1;
	
BEGIN
	dbms_output.put_line('package initialized. ');
	PRAGMA INLINE(p2, 'NO');
	PRAGMA UDF;
EXCEPTION
	WHEN ZERO_DIVIDE THEN
		DBMS_OUTPUT.PUT_LINE('division by zero');
	WHEN OTHERS THEN
		DBMS_OUTPUT.PUT_LINE('It''s broken. ');
		RAISE;
END test_package_body;";

				var statement = (OracleStatement)Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
				statement.IsPlSql.ShouldBe(true);
			}

			[Test(Description = @"")]
			public void TestForAllStatement()
			{
				const string statement1 =
@"BEGIN
	FORALL i IN INDICES OF cust_tab SAVE EXCEPTIONS
		INSERT INTO valid_orders (cust_name, amount)
		VALUES (cust_tab(i), amount_tab(i));

	FORALL i IN VALUES OF rejected_order_tab
		INSERT INTO rejected_orders (cust_name, amount)
		VALUES (cust_tab(i), amount_tab(i));
END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestProcedureCall()
			{
				const string statement1 = @"BEGIN P1(1); END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.RootNode.Terminals.ToArray();
				terminals.Length.ShouldBe(8);
				terminals[0].IsReservedWord.ShouldBe(true);
				terminals[6].IsReservedWord.ShouldBe(true);
			}

			[Test(Description = @"")]
			public void TestNestedProcedureCall()
			{
				const string statement1 = @"DECLARE PROCEDURE P1(P1 NUMBER) IS BEGIN NULL; END; BEGIN P1(1); END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestCaseAndEndLabelCombination()
			{
				const string statement1 = @"BEGIN CASE 0 WHEN 0 THEN NULL; END CASE; END LABEL;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestRowCountCursorFunction()
			{
				const string statement1 = @"DECLARE x NUMBER; BEGIN x := SQL%ROWCOUNT; END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestBulkRowCountCursorFunction()
			{
				const string statement1 = @"DECLARE x NUMBER; BEGIN x := SQL%BULK_ROWCOUNT(1); END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestVariableDeclarationAfterCursorDefinition()
			{
				const string statement1 = @"DECLARE CURSOR c1 IS SELECT * FROM DUAL; x NUMBER; BEGIN NULL; END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestCursorDefinitionAfterProcedureDefiniton()
			{
				const string statement1 = @"DECLARE PROCEDURE P1 IS BEGIN NULL; END; CURSOR c1 IS SELECT * FROM DUAL; BEGIN NULL; END;";

				var statement = Parser.Parse(statement1).First().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
				statement.SourcePosition.IndexStart.ShouldBe(0);
			}

			[Test(Description = @"")]
			public void TestRowIdParameterAndVariable()
			{
				const string statement1 = @"CREATE OR REPLACE PROCEDURE P1 (P ROWID) IS V ROWID; BEGIN NULL; END;";

				var statement = Parser.Parse(statement1).First().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestExecuteImmediateWithExpressionParameter()
			{
				const string statement1 = @"BEGIN EXECUTE IMMEDIATE 'SELECT ' || (1 + 1) || ' FROM DUAL'; END;";

				var statement = Parser.Parse(statement1).First().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestCollectionTargetWithinIntoClause()
			{
				const string statement1 =
@"DECLARE
	TEST_ARRAY SYS.ODCIVARCHAR2LIST := SYS.ODCIVARCHAR2LIST();
BEGIN
	TEST_ARRAY.EXTEND;
	SELECT DUMMY INTO TEST_ARRAY(TEST_ARRAY.LAST) FROM DUAL;
END;";

				var statement = Parser.Parse(statement1).First().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestMultipleStatementsWithinCaseBranches()
			{
				const string statement1 =
@"BEGIN
	CASE 6
		WHEN 6 THEN
			DBMS_OUTPUT.PUT_LINE('Branch 6-1');
			DBMS_OUTPUT.PUT_LINE('Branch 6-2');
		WHEN 12 THEN
			DBMS_OUTPUT.PUT_LINE('Branch 12-1');
			DBMS_OUTPUT.PUT_LINE('Branch 12-2');
		ELSE
			DBMS_OUTPUT.PUT_LINE('Else 1');
			DBMS_OUTPUT.PUT_LINE('Else 2');
	END CASE;
END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestRecordWithCollectionAttributeAssignment()
			{
				const string statement1 =
@"CREATE PROCEDURE test_procedure
IS
	TYPE t_inner_record IS RECORD(i1 VARCHAR2(200), i2 VARCHAR2(200));
	TYPE t_inner_record_collection IS TABLE OF t_inner_record INDEX BY PLS_INTEGER;
	TYPE t_record IS RECORD(v1 VARCHAR2(200), v2 t_inner_record, v3 t_inner_record_collection);
	record t_record;
	
	FUNCTION get_record (p IN NUMBER) RETURN t_record
	IS
		inner_record t_record;
	BEGIN
		inner_record.v1 := 'XXX';
		inner_record.v2.i1 := 'YYY';
		inner_record.v3(1).i2 := 'ZZZ';
		RETURN inner_record;
	END;
BEGIN
	record := get_record(1);
	DBMS_OUTPUT.PUT_LINE(get_record(1).v2.i1);	
	DBMS_OUTPUT.PUT_LINE(get_record(1).v3(1).i2);	
END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestWithPlSqlDeclaration()
			{
				const string statement1 =
@"WITH
	PROCEDURE CHECK_INPUT(P NUMBER) IS BEGIN DBMS_OUTPUT.PUT_LINE('Parameter value ' || P || ' checked. '); END;
	FUNCTION F1(P IN NUMBER) RETURN NUMBER AS BEGIN CHECK_INPUT(P); RETURN DBMS_RANDOM.VALUE + P; END;
	FUNCTION F2(P IN NUMBER) RETURN NUMBER AS BEGIN CHECK_INPUT(P); RETURN 5 * P + P / 2; END;
	CTE AS (SELECT 5 VAL FROM DUAL)
SELECT F1(VAL), F2(VAL + 1) FROM CTE";

				var statements = Parser.Parse(statement1).ToArray();
				var statement = statements.Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestBindVariableAsAssignmentTarget()
			{
				const string statement1 =
@"BEGIN :TARGET := NULL; END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestDynamicTypesInRecordTypeDefinition()
			{
				const string statement1 =
@"CREATE OR REPLACE PACKAGE test_package AS 
	TYPE test_type IS RECORD (c1 selection.name%type, c2 selection.selection_id%type);
END DEPARTMENT_INFO;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestRefCursorUsingParameters()
			{
				const string statement1 =
@"DECLARE
	c1 SYS_REFCURSOR;
	c2 SYS_REFCURSOR;
	c3 SYS_REFCURSOR;
	statementText VARCHAR2(4000) := 'SELECT * FROM DUAL';
BEGIN
	OPEN c1 FOR :statementText;
	OPEN c2 FOR 'SELECT * FROM DUAL';
	OPEN c3 FOR statementText;
END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestForLoopWithInnerIfStatement()
			{
				const string statement1 =
@"BEGIN
	FOR i IN 1..2 LOOP
		IF 1 = 1 THEN
			NULL;
		ELSIF 2 = 2 THEN
			NULL;
		END IF;
	END LOOP;
END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestNewKeywordForObjectTypeConstructor()
			{
				const string statement1 =
@"DECLARE
	x TEST_TYPE;
BEGIN
	x := NEW TEST_TYPE(1);
END;";

				var statement = Parser.Parse(statement1).Single().Validate();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			public class Triggers
			{
				[Test(Description = @"")]
				public void TestCreateSimpleTrigger()
				{
					const string statement1 =
@"CREATE OR REPLACE TRIGGER GenerateStudentID
  BEFORE INSERT OR UPDATE OF id, FirstName ON lecturer
  REFERENCING new AS new_student
  FOR EACH ROW
DECLARE
  testException EXCEPTION;
BEGIN
  SELECT 20001
    INTO :new_student.ID
    FROM dual;

  :new_student.FirstName := :new_student.LastName;
  :new_student.LastName := NULL;

  EXCEPTION
    WHEN NO_DATA_FOUND
      THEN RAISE;
END GenerateStudentID;";

					var statement = Parser.Parse(statement1).Single().Validate();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateInsteadOfTrigger()
				{
					const string statement1 =
@"CREATE OR REPLACE TRIGGER ioft_insert_role_perm
INSTEAD OF INSERT
  ON role_permission_view
  FOR EACH ROW
DECLARE
  x INTEGER;
BEGIN
  IF x = 0 THEN
    INSERT INTO permission_code (pcode, pcode_description, mod_user_id, mod_user_date)
    VALUES (:NEW.pcode, 'New Code', USER, SYSDATE);
  END IF;

  IF x = 0 THEN
    INSERT INTO dept_code (dept_code, dept_name)
    VALUES (:NEW.dept_code, 'New Dept');
  END IF;

  INSERT INTO user_role (dept_code, pcode, mod_user_id)
  VALUES (:NEW.dept_code, :NEW.pcode, 'Morgan');
END ioft_insert_role_perm;";

					var statement = Parser.Parse(statement1).Single().Validate();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateCompoundTrigger()
				{
					const string statement1 =
@"create or replace trigger check_raise_on_avg
for update of sal on emp
COMPOUND TRIGGER
  Twelve_Percent        constant number:=0.12;
  TYPE Department_Salaries_t  IS TABLE OF Emp.Sal%TYPE
                                INDEX BY VARCHAR2(80);
  Department_Avg_Salaries     Department_Salaries_t;
  TYPE Sal_t             IS TABLE OF Emp.Sal%TYPE;
  Avg_Salaries                Sal_t;
  TYPE Deptno_t       IS TABLE OF Emp.Deptno%TYPE;
  Department_IDs              Deptno_t;

  BEFORE STATEMENT IS
  BEGIN
    SELECT               AVG(e.Sal), NVL(e.Deptno, -1)
      BULK COLLECT INTO  Avg_Salaries, Department_IDs
      FROM               Emp e
      GROUP BY           e.Deptno;
    FOR j IN 1..Department_IDs.COUNT() LOOP
      Department_Avg_Salaries(Department_IDs(j)) := Avg_Salaries(j);
    END LOOP;
  END BEFORE STATEMENT;

  AFTER EACH ROW IS
  BEGIN
    IF :NEW.Sal - :Old.Sal >
      Twelve_Percent*Department_Avg_Salaries(:NEW.Deptno)
    THEN
      Raise_Application_Error(-20000, 'Raise too large');
    END IF;
  END AFTER EACH ROW;
END Check_Raise_On_Avg;";

					var statement = Parser.Parse(statement1).Single().Validate();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}
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

			[Test(Description = @"")]
			public void TestInvalidExpressionEndingWithComma()
			{
				var isRuleValid = Parser.IsRuleValid(NonTerminals.Expression, "PROJECT_ID,");
				isRuleValid.ShouldBe(false);
			}
		}

		public class TerminalCandidates
		{
			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterSelectColumnWithAlias()
			{
				const string statement1 = @"SELECT 1 A";
				var node = Parser.Parse(statement1).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).OrderBy(t => t.Id).ToArray();

				terminalCandidates.Length.ShouldBe(4);
				terminalCandidates[0].Id.ShouldBe(Terminals.Bulk);
				terminalCandidates[0].FollowingMandatoryCandidates.Count.ShouldBe(1);
				terminalCandidates[0].FollowingMandatoryCandidates[0].ShouldBe(Terminals.Collect);
				terminalCandidates[1].Id.ShouldBe(Terminals.Comma);
				terminalCandidates[2].Id.ShouldBe(Terminals.From);
				terminalCandidates[3].Id.ShouldBe(Terminals.Into);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterSelectColumnWithLiteral()
			{
				const string statement2 = @"SELECT 1";
				var node = Parser.Parse(statement2).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).Select(c => c.Id).OrderBy(t => t).ToArray();

				var expectedTerminals = new[] { Terminals.As, Terminals.Bulk, Terminals.ColumnAlias, Terminals.Comma, Terminals.From, Terminals.Into, Terminals.MathDivide, Terminals.MathFactor, Terminals.MathMinus, Terminals.MathPlus, Terminals.OperatorConcatenation };
				terminalCandidates.ShouldBe(expectedTerminals);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterSelectColumnWithObjectPrefix()
			{
				const string statement2 = @"SELECT OBJECT_PREFIX.";
				var node = Parser.Parse(statement2).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).Select(c => c.Id).OrderBy(t => t).ToArray();

				// TODO: Wrong, at least ObjectIdentifier should be available in addition
				var expectedTerminals = new[] { Terminals.Asterisk, Terminals.Identifier, Terminals.RowIdPseudoColumn };
				terminalCandidates.ShouldBe(expectedTerminals);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesWithNullNode()
			{
				var terminalCandidates = Parser.GetTerminalCandidates(null).Select(c => c.Id).OrderBy(t => t).ToArray();

				var expectedTerminals = new[] { Terminals.Alter, Terminals.Analyze, Terminals.Begin, Terminals.Call, Terminals.Comment, Terminals.Commit, Terminals.Create, Terminals.Declare, Terminals.Delete, Terminals.Drop, Terminals.Explain, Terminals.Grant, Terminals.Insert, Terminals.LeftLabelMarker, Terminals.LeftParenthesis, Terminals.Lock, Terminals.Merge, Terminals.Purge, Terminals.Rename, Terminals.Rollback, Terminals.Savepoint, Terminals.Select, Terminals.Set, Terminals.Truncate, Terminals.Update, Terminals.With };
				terminalCandidates.ShouldBe(expectedTerminals);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterValidStatementWhenStartingAdditionalJoinClause()
			{
				const string statement1 = @"SELECT * FROM SELECTION S FULL";
				var node = Parser.Parse(statement1).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).Select(c => c.Id).OrderBy(t => t).ToArray();
				var expectedTerminals = new[] { Terminals.Join, Terminals.Outer };
				terminalCandidates.ShouldBe(expectedTerminals);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterObjectIdentifierInFromClause()
			{
				const string statement1 = @"SELECT * FROM SELECTION";
				var node = Parser.Parse(statement1).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).Select(c => c.Id).OrderBy(t => t).ToArray();

				var expectedTerminals =
					new[]
					{
						Terminals.As,
						Terminals.AtCharacter,
						Terminals.Comma,
						Terminals.Connect,
						Terminals.Cross,
						Terminals.Fetch,
						Terminals.For,
						Terminals.Full,
						Terminals.Group,
						Terminals.Having,
						Terminals.Inner,
						Terminals.Intersect,
						Terminals.Join,
						Terminals.Left,
						Terminals.MatchRecognize,
						Terminals.Model,
						Terminals.Natural,
						Terminals.ObjectAlias,
						Terminals.Offset,
						Terminals.Order,
						Terminals.Outer,
						Terminals.Partition,
						Terminals.Pivot,
						Terminals.Right,
						Terminals.Sample,
						Terminals.SetMinus,
						Terminals.Start,
						Terminals.Subpartition,
						Terminals.Union,
						Terminals.Unpivot,
						Terminals.Versions,
						Terminals.Where
					};

				terminalCandidates.ShouldBe(expectedTerminals);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAfterDotWithinNestedExpression()
			{
				const string statement1 = @"SELECT CASE WHEN S.";
				var node = Parser.Parse(statement1).Single().RootNode.LastTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).Select(c => c.Id).OrderBy(t => t).ToArray();
				var expectedTerminals = new[] { Terminals.Identifier, Terminals.RowIdPseudoColumn };
				terminalCandidates.ShouldBe(expectedTerminals);
			}

			[Test(Description = @""), Ignore]
			public void TestTerminalCandidatesAfterLeftParenthesisWithMultiplePotentialExpressionSequnces()
			{
				const string statement1 = @"SELECT EXTRACT()";
				var terminal = Parser.Parse(statement1).Single().RootNode.Terminals.ToArray()[2];
				terminal.Id.ShouldBe(Terminals.LeftParenthesis);

				var terminalCandidates = Parser.GetTerminalCandidates(terminal).Select(c => c.Id).OrderBy(t => t).ToArray();
				
				terminalCandidates.ShouldContain(Terminals.Day);
			}

			[Test(Description = @"")]
			public void TestTerminalCandidatesAtNotLastNodeInTheParsedTree()
			{
				const string statement1 = @"SELECT D FROM DUAL";
				var node = Parser.Parse(statement1).Single().RootNode.FirstTerminalNode;
				var terminalCandidates = Parser.GetTerminalCandidates(node).Select(c => c.Id).OrderBy(t => t).ToArray();

				var expectedTerminals =
					new[]
					{
						Terminals.All,
						Terminals.Asterisk,
						Terminals.Avg,
						Terminals.Case,
						Terminals.Cast,
						Terminals.CharacterCode,
						Terminals.Collect,
						Terminals.Colon,
						Terminals.ConnectByRoot,
						Terminals.Count,
						Terminals.CumulativeDistribution,
						Terminals.Cursor,
						Terminals.Date,
						Terminals.DenseRank,
						Terminals.Distinct,
						Terminals.Extract,
						Terminals.FirstValue,
						Terminals.Identifier,
						Terminals.Interval,
						Terminals.JsonQuery,
						Terminals.JsonValue,
						Terminals.Lag,
						Terminals.LastValue,
						Terminals.Lead,
						Terminals.LeftParenthesis,
						Terminals.Level,
						Terminals.ListAggregation,
						Terminals.MathMinus,
						Terminals.MathPlus,
						Terminals.Max,
						Terminals.Min,
						Terminals.Null,
						Terminals.NumberLiteral,
						Terminals.ObjectIdentifier,
						Terminals.PercentileContinuousDistribution,
						Terminals.PercentileDiscreteDistribution,
						Terminals.Prior,
						Terminals.Rank,
						Terminals.RowIdPseudoColumn,
						Terminals.RowNumberPseudoColumn,
						Terminals.SchemaIdentifier,
						Terminals.StandardDeviation,
						Terminals.StringLiteral,
						Terminals.Sum,
						Terminals.SystemDate,
						Terminals.Timestamp,
						Terminals.Treat,
						Terminals.Trim,
						Terminals.Unique,
						Terminals.User,
						Terminals.Variance,
						Terminals.XmlAggregate,
						Terminals.XmlCast,
						Terminals.XmlColumnValue,
						Terminals.XmlElement,
						Terminals.XmlForest,
						Terminals.XmlParse,
						Terminals.XmlQuery,
						Terminals.XmlRoot,
						Terminals.XmlSerialize
					};

				terminalCandidates.ShouldBe(expectedTerminals);
			}
		}

		public class Transaction
		{
			public class Commit
			{
				[Test(Description = @"")]
				public void TestCommitStatement()
				{
					const string statement1 = @"COMMIT COMMENT 'In-doubt transaction Code 36, Call (415) 555-2637' WRITE NOWAIT;";
					var result = Parser.Parse(statement1);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(5);

					terminals[0].Id.ShouldBe(Terminals.Commit);
					terminals[1].Id.ShouldBe(Terminals.Force);
					terminals[2].Id.ShouldBe(Terminals.StringLiteral);
					terminals[3].Id.ShouldBe(Terminals.Comma);
					terminals[4].Id.ShouldBe(Terminals.IntegerLiteral);
				}
			}

			public class Rollback
			{
				[Test(Description = @"")]
				public void TestRollbackStatement()
				{
					const string statementText = @"ROLLBACK TO SAVEPOINT savepoint_name";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(4);

					terminals[0].Id.ShouldBe(Terminals.Rollback);
					terminals[1].Id.ShouldBe(Terminals.To);
					terminals[2].Id.ShouldBe(Terminals.Savepoint);
					terminals[3].Id.ShouldBe(Terminals.Identifier);
				}

				[Test(Description = @"")]
				public void TestRollbackStatementForceDistributedTransaction()
				{
					const string statementText = @"ROLLBACK WORK FORCE '25.32.87';";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(4);

					terminals[0].Id.ShouldBe(Terminals.Rollback);
					terminals[1].Id.ShouldBe(Terminals.Work);
					terminals[2].Id.ShouldBe(Terminals.Force);
					terminals[3].Id.ShouldBe(Terminals.StringLiteral);
				}
			}

			public class Savepoint
			{
				[Test(Description = @"")]
				public void TestSavepointStatement()
				{
					const string statementText = @"SAVEPOINT savepoint_name";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(2);

					terminals[0].Id.ShouldBe(Terminals.Savepoint);
					terminals[1].Id.ShouldBe(Terminals.Identifier);
				}
			}

			public class SetTransaction
			{
				[Test(Description = @"")]
				public void TestSetReadOnlyTransactionWithName()
				{
					const string statement1 = @"SET TRANSACTION READ ONLY NAME 'Toronto'";
					var statement = Parser.Parse(statement1).Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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

			public class SetConstraint
			{
				[Test(Description = @"")]
				public void TestSetAllConstraintsImmediate()
				{
					const string statement1 = @"SET CONSTRAINTS ALL IMMEDIATE";
					var statement = Parser.Parse(statement1).Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestSetNamedConstraintsDeferred()
				{
					const string statement1 = @"SET CONSTRAINTS emp_job_nn, emp_salary_min, hr.jhist_dept_fk@remote DEFERRED";
					var statement = Parser.Parse(statement1).Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}
		}

		public class Dml
		{
			public class Delete
			{
				[Test(Description = @"")]
				public void TestDeleteWithReturningAndErrorLogging()
				{
					const string statement1 = @"DELETE FROM TEST_TABLE RETURNING COLUMN1, COLUMN2 INTO :C1, :C2 LOG ERRORS INTO SCHEMA.LOG_TABLE ('Delete statement') REJECT LIMIT 10";
					var statements = Parser.Parse(statement1);
					statements.Count.ShouldBe(1);
					var statement = statements.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(28);
				}

				[Test(Description = @"")]
				public void TestMergeWithObjectPrefixes()
				{
					const string statement1 = @"MERGE INTO HUSQVIK.T USING SYS.DUAL ON (1 = 1) WHEN MATCHED THEN UPDATE SET HUSQVIK.T.PARENT_ID = SYS.DUAL.DUMMY WHERE 1 = 0 WHEN NOT MATCHED THEN INSERT (ID, PARENT_ID) VALUES (NULL, NULL) WHERE 1 = 0";
					var statements = Parser.Parse(statement1);
					statements.Count.ShouldBe(1);
					var statement = statements.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(55);
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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(37);
				}
			}
		}

		public class Drop
		{
			public class DropTable
			{
				[Test(Description = @"")]
				public void TestDropTablePurge()
				{
					const string statementText = @"DROP TABLE HUSQVIK.SELECTION PURGE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(6);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Table);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[5].Id.ShouldBe(Terminals.Purge);
				}

				[Test(Description = @"")]
				public void TestDropTableCascadeConstraints()
				{
					const string statementText = @"DROP TABLE SELECTION CASCADE CONSTRAINTS;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(5);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Table);
					terminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Cascade);
					terminals[4].Id.ShouldBe(Terminals.Constraints);
				}
			}

			public class DropIndex
			{
				[Test(Description = @"")]
				public void TestDropIndexForce()
				{
					const string statementText = @"DROP INDEX HUSQVIK.PK_SELECTION FORCE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(6);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Index);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[5].Id.ShouldBe(Terminals.Force);
				}

				[Test(Description = @"")]
				public void TestDropIndexOnline()
				{
					const string statementText = @"DROP INDEX PK_SELECTION ONLINE;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(4);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Index);
					terminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Online);
				}
			}

			public class DropView
			{
				[Test(Description = @"")]
				public void TestDropViewCascadeConstraints()
				{
					const string statementText = @"DROP VIEW HUSQVIK.SELECTION CASCADE CONSTRAINTS;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(7);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.View);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[5].Id.ShouldBe(Terminals.Cascade);
					terminals[6].Id.ShouldBe(Terminals.Constraints);
				}

				[Test(Description = @"")]
				public void TestDropMaterializedViewPreserveTable()
				{
					const string statementText = @"DROP MATERIALIZED VIEW HUSQVIK.MV_SELECTION PRESERVE TABLE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(8);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Materialized);
					terminals[2].Id.ShouldBe(Terminals.View);
					terminals[3].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[4].Id.ShouldBe(Terminals.Dot);
					terminals[5].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[6].Id.ShouldBe(Terminals.Preserve);
					terminals[7].Id.ShouldBe(Terminals.Table);
				}

				[Test(Description = @"")]
				public void TestDropMaterializedViewWithObsoleteSnapshotKeyword()
				{
					const string statementText = @"DROP SNAPSHOT MV_SELECTION";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(3);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Snapshot);
					terminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
				}
			}

			public class DropPackage
			{
				[Test(Description = @"")]
				public void TestDropPackage()
				{
					const string statementText = @"DROP PACKAGE BODY HUSQVIK.SQLPAD";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(6);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Package);
					terminals[2].Id.ShouldBe(Terminals.Body);
					terminals[3].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[4].Id.ShouldBe(Terminals.Dot);
					terminals[5].Id.ShouldBe(Terminals.ObjectIdentifier);
				}
			}

			public class DropCluster
			{
				[Test(Description = @"")]
				public void TestDropPackage()
				{
					const string statementText = @"DROP CLUSTER HUSQVIK.TEST_CLUSTER INCLUDING TABLES CASCADE CONSTRAINTS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(9);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Cluster);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[5].Id.ShouldBe(Terminals.Including);
					terminals[6].Id.ShouldBe(Terminals.Tables);
					terminals[7].Id.ShouldBe(Terminals.Cascade);
					terminals[8].Id.ShouldBe(Terminals.Constraints);
				}
			}

			public class DropMaterializedViewLog
			{
				[Test(Description = @"")]
				public void TestDropMaterializedViewLog()
				{
					const string statementText = @"DROP MATERIALIZED VIEW LOG ON HUSQVIK.TEST_CLUSTER";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(8);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Materialized);
					terminals[2].Id.ShouldBe(Terminals.View);
					terminals[3].Id.ShouldBe(Terminals.Log);
					terminals[4].Id.ShouldBe(Terminals.On);
					terminals[5].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[6].Id.ShouldBe(Terminals.Dot);
					terminals[7].Id.ShouldBe(Terminals.ObjectIdentifier);
				}
			}

			public class DropOther
			{
				[Test(Description = @"")]
				public void TestDropFunction()
				{
					const string statementText = @"DROP FUNCTION HUSQVIK.SQLPAD_FUNCTION";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(5);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Function);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
				}

				[Test(Description = @"")]
				public void TestDropProcedure()
				{
					const string statementText = @"DROP PROCEDURE HUSQVIK.TEST_PROCEDURE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(5);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Procedure);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
				}

				[Test(Description = @"")]
				public void TestDropSequence()
				{
					const string statementText = @"DROP SEQUENCE HUSQVIK.TEST_SEQ";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(5);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Sequence);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
				}

				[Test(Description = @"")]
				public void TestDropContext()
				{
					const string statementText = @"DROP CONTEXT TEST_CONTEXT";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(3);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Context);
					terminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
				}

				[Test(Description = @"")]
				public void TestDropDirectory()
				{
					const string statementText = @"DROP DIRECTORY TEST_DIRECTORY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(3);

					terminals[0].Id.ShouldBe(Terminals.Drop);
					terminals[1].Id.ShouldBe(Terminals.Directory);
					terminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
				}
			}
		}

		public class LockTable
		{
			[Test(Description = @"")]
			public void TestLockTable()
			{
				const string statementText = @"LOCK TABLE HUSQVIK.TEST_TABLE1@REMOTE, TEST_TABLE2 PARTITION (P1) IN EXCLUSIVE MODE WAIT 6";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(18);

				terminals[0].Id.ShouldBe(Terminals.Lock);
				terminals[1].Id.ShouldBe(Terminals.Table);
				terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
				terminals[3].Id.ShouldBe(Terminals.Dot);
				terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[5].Id.ShouldBe(Terminals.AtCharacter);
				terminals[6].Id.ShouldBe(Terminals.DatabaseLinkIdentifier);
				terminals[7].Id.ShouldBe(Terminals.Comma);
				terminals[8].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[9].Id.ShouldBe(Terminals.Partition);
				terminals[10].Id.ShouldBe(Terminals.LeftParenthesis);
				terminals[11].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[12].Id.ShouldBe(Terminals.RightParenthesis);
				terminals[13].Id.ShouldBe(Terminals.In);
				terminals[14].Id.ShouldBe(Terminals.Exclusive);
				terminals[15].Id.ShouldBe(Terminals.Mode);
				terminals[16].Id.ShouldBe(Terminals.Wait);
				terminals[17].Id.ShouldBe(Terminals.IntegerLiteral);
			}
		}

		public class RenameSchemaObject
		{
			[Test(Description = @"")]
			public void TestLockTable()
			{
				const string statementText = @"RENAME TEST_TABLE1 TO TEST_TABLE2";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(4);

				terminals[0].Id.ShouldBe(Terminals.Rename);
				terminals[1].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[2].Id.ShouldBe(Terminals.To);
				terminals[3].Id.ShouldBe(Terminals.ObjectIdentifier);
			}
		}

		public class Comment
		{
			[Test(Description = @"")]
			public void TestCommentOnTable()
			{
				const string statementText = @"COMMENT ON TABLE HUSQVIK.TEST_TABLE IS 'Test comment'";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(8);

				terminals[0].Id.ShouldBe(Terminals.Comment);
				terminals[1].Id.ShouldBe(Terminals.On);
				terminals[2].Id.ShouldBe(Terminals.Table);
				terminals[3].Id.ShouldBe(Terminals.SchemaIdentifier);
				terminals[4].Id.ShouldBe(Terminals.Dot);
				terminals[5].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[6].Id.ShouldBe(Terminals.Is);
				terminals[7].Id.ShouldBe(Terminals.StringLiteral);
			}

			[Test(Description = @"")]
			public void TestCommentOnColumn()
			{
				const string statementText = @"COMMENT ON COLUMN HUSQVIK.TEST_TABLE.COLUMN1 IS 'Test column comment'";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(10);

				terminals[0].Id.ShouldBe(Terminals.Comment);
				terminals[1].Id.ShouldBe(Terminals.On);
				terminals[2].Id.ShouldBe(Terminals.Column);
				terminals[3].Id.ShouldBe(Terminals.SchemaIdentifier);
				terminals[4].Id.ShouldBe(Terminals.Dot);
				terminals[5].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[6].Id.ShouldBe(Terminals.Dot);
				terminals[7].Id.ShouldBe(Terminals.Identifier);
				terminals[8].Id.ShouldBe(Terminals.Is);
				terminals[9].Id.ShouldBe(Terminals.StringLiteral);
			}
		}

		public class Create
		{
			public class CreateTable
			{
				[Test(Description = @"")]
				public void TestBasicCreateTable()
				{
					const string statementText =
						@"CREATE TABLE test_table (
  id NUMBER(6) PRIMARY KEY,
  parent_id NUMBER(6) CHECK (id <> 0) REFERENCES test_table (id) ON DELETE SET NULL,
  first_name VARCHAR2(20) VISIBLE,
  last_name VARCHAR2(25) NOT NULL,
  email VARCHAR2(25) CONSTRAINT nn_test_table_email NOT NULL UNIQUE,
  hire_date DATE DEFAULT SYSDATE,
  salary NUMBER(8, 2) CONSTRAINT nn_test_table_salary NOT NULL,
  CONSTRAINT chk_test_table_salary CHECK (salary > 0),
  CONSTRAINT uq_test_table_email UNIQUE (email)
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(92);

					terminals[85].Id.ShouldBe(Terminals.Constraint);
				}

				[Test(Description = @"")]
				public void TestPhysicalPropertiesClause()
				{
					const string statementText =
						@"CREATE TABLE HUSQVIK.SELECTION (
	SELECTION_ID NUMBER
)
SEGMENT CREATION IMMEDIATE
PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255
LOGGING
STORAGE (
	INITIAL 65536 NEXT 1048576 MINEXTENTS 1 MAXEXTENTS 2147483645
	PCTINCREASE 0 FREELISTS 1 FREELIST GROUPS 1
	BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT
)
TABLESPACE TBS_HQ_PDB";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestTablePropertiesClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	ID
)
NOCACHE RESULT_CACHE (MODE FORCE) PARALLEL NOROWDEPENDENCIES DISABLE ROW MOVEMENT NO FLASHBACK ARCHIVE
NOLOGGING
AS
SELECT * FROM DUAL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestConstraintStateClause()
				{
					const string statementText = @"CREATE TABLE TEST_TABLE (ID NUMBER CHECK (VAL2 IN ('A', 'B', 'C')) DEFERRABLE INITIALLY DEFERRED NORELY ENABLE)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestSimpleCreateTableAsSelect()
				{
					const string statementText = @"CREATE TABLE XXX COLUMN STORE COMPRESS FOR ARCHIVE HIGH NO ROW LEVEL LOCKING AS SELECT * FROM DUAL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateGlobalTemporaryTable()
				{
					const string statementText = @"CREATE GLOBAL TEMPORARY TABLE XXX (VAL NUMBER) ON COMMIT PRESERVE ROWS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestGeneratedIdentityClause()
				{
					const string statementText =
						@"CREATE TABLE TESTTABLE (
    ID1 NUMBER GENERATED BY DEFAULT ON NULL AS IDENTITY (START WITH 42 INCREMENT BY 1000 NOCYCLE NOORDER CACHE 100 NOMINVALUE NOMAXVALUE),
    ID2 NUMBER DEFAULT SEQ_TEST.NEXTVAL,
    STARTDATE DATE NOT NULL,
    ENDDATE DATE NULL,
    PERIOD FOR VALID(STARTDATE, ENDDATE),
    HIDDENCOLUMN VARCHAR2(100) INVISIBLE
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestEncryptColumnClause()
				{
					const string statementText =
						@"CREATE TABLE ENCRYPTIONTEST (
     FIRST_NAME VARCHAR2(128),
     LAST_NAME VARCHAR2(128),
     EMPID NUMBER,
     SALARY NUMBER(12, 2) ENCRYPT USING 'AES256' IDENTIFIED BY oracle 'SHA-1' SALT
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestExplicitHeapOrganizationClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	VAL NUMBER
)
SEGMENT CREATION IMMEDIATE
ORGANIZATION HEAP
LOGGING
NOCOMPRESS
NOROWDEPENDENCIES
TABLESPACE TBS_HQ_PDB";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestExplicitIndexpOrganizationClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	ID1 NUMBER,
	ID2 NUMBER,
	DATA VARCHAR2(255),
	CONSTRAINT PK_TEST_TABLE PRIMARY KEY (ID1, ID2)
)
ORGANIZATION INDEX
TABLESPACE TBS_HQ_PDB
INCLUDING DATA
COMPRESS 1
NOMAPPING
PCTTHRESHOLD 2 
STORAGE (INITIAL 4K)
OVERFLOW STORAGE (INITIAL 4K)
TABLESPACE TBS_MSSM";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestSupplementalLoggingClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	ID NUMBER,
	DATA1 VARCHAR2(255) UNIQUE,
	DATA2 VARCHAR2(255),
	DATA3 VARCHAR2(255),
	DATA4 VARCHAR2(255),
	DATA5 VARCHAR2(255),
	DATA6 VARCHAR2(255),
	CONSTRAINT PK_TEST_TABLE PRIMARY KEY (ID),
	SUPPLEMENTAL LOG DATA (PRIMARY KEY, UNIQUE) COLUMNS,
	SUPPLEMENTAL LOG GROUP TEST_SUPPLEMENTAL_LOG_GROUP1 (DATA2, DATA3, DATA4) ALWAYS,
	SUPPLEMENTAL LOG GROUP TEST_SUPPLEMENTAL_LOG_GROUP2 (DATA5 NO LOG, DATA6)
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestLargeObjectStorageClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	LOB_COLUMN1 CLOB,
	LOB_COLUMN2 CLOB,
	LOB_COLUMN3 BLOB
)
LOB (LOB_COLUMN1, LOB_COLUMN2)
STORE AS SECUREFILE (
	TABLESPACE TBS_HQ_PDB
	CHUNK 4000
	DECRYPT
	PCTVERSION 10
	STORAGE (INITIAL 6144)
	KEEP_DUPLICATES
	CACHE READS FILESYSTEM_LIKE_LOGGING
)
LOB (LOB_COLUMN3)
STORE AS BASICFILE TEST_LOB_SEGMENT (
	DISABLE STORAGE IN ROW
	TABLESPACE TBS_HQ_PDB
	CHUNK 4000
	STORAGE (INITIAL 6144)
	CACHE
	LOGGING
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestVariableElementArrayColumnProperties()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	VARRAY_COLUMN1 SYS.ODCIVARCHAR2LIST,
	VARRAY_COLUMN2 HUSQVIK.ALERT_COLLECTION
)
VARRAY VARRAY_COLUMN1
	NOT SUBSTITUTABLE AT ALL LEVELS
	STORE AS SECUREFILE LOB (
		ENABLE STORAGE IN ROW CHUNK 8192 CACHE COMPRESS MEDIUM DEDUPLICATE
			STORAGE(BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT)
		)
VARRAY VARRAY_COLUMN2
	IS OF (ONLY HUSQVIK.ALERT)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestNestedTableColumnProperties()
				{
					const string statementText =
						@"CREATE TABLE HUSQVIK.TEST_TABLE (
	NESTED_TABLE_COLUMN HUSQVIK.ALERT_COLLECTION 
)
SEGMENT CREATION IMMEDIATE
PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255 
NOCOMPRESS LOGGING
STORAGE (
	INITIAL 65536 NEXT 1048576 MINEXTENTS 1 MAXEXTENTS 2147483645
	PCTINCREASE 0 FREELISTS 1 FREELIST GROUPS 1
	BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT
)
TABLESPACE TBS_HQ_PDB 
NESTED TABLE NESTED_TABLE_COLUMN
	STORE AS NT_NESTED_TABLE_COLUMN	(
		PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255 LOGGING
		STORAGE (
			INITIAL 65536 NEXT 1048576 MINEXTENTS 1 MAXEXTENTS 2147483645
			PCTINCREASE 0 FREELISTS 1 FREELIST GROUPS 1
			BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT
		)
		TABLESPACE TBS_HQ_PDB
	)
	RETURN AS VALUE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestUsingCreateIndexClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	ID NUMBER PRIMARY KEY USING INDEX (CREATE INDEX PK_TEST_TABLE ON TEST_TABLE (ID)),
	VALUE NUMBER,
	CONSTRAINT IX_TEST_TABLE UNIQUE (VALUE) USING INDEX (CREATE UNIQUE INDEX IX_TEST_TABLE_VALUE ON TEST_TABLE (VALUE)),
	DATA VARCHAR2(255) CONSTRAINT UQ_TEST_TABLE_DATA UNIQUE USING INDEX PCTFREE 20 STORAGE (INITIAL 8M) REVERSE
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestObjectPropertiesClause()
				{
					const string statementText = @"CREATE TABLE EMPLOYEES_OBJ_T OF EMPLOYEES_TYP (E_NO PRIMARY KEY) OBJECT IDENTIFIER IS PRIMARY KEY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestVirtualColumnClause()
				{
					const string statementText = @"CREATE TABLE TEST_TABLE (C1 NUMBER, C2 NUMBER, C3 VISIBLE GENERATED ALWAYS AS ((C1 + C2) * (C1 * C2)) VIRTUAL EVALUATE USING NULL EDITION UNUSABLE BEGINNING WITH EDITION TEST_EDITION CONSTRAINT NN_VIRTUAL_C3 NOT NULL)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestRangePartitioningClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (
	PROD_ID NUMBER(6),
	CUST_ID NUMBER,
	TIME_IDB DATE,
	CHANNEL_ID CHAR(1),
	PROMO_ID NUMBER(6),
	QUANTITY_SOLD NUMBER(3),
	AMOUNT_SOLD NUMBER(10, 2)
)
PARTITION BY RANGE (TIME_ID) (
   PARTITION SALES_Q1_2000 VALUES LESS THAN (TO_DATE('01-APR-2000', 'DD-MON-YYYY')),
   PARTITION SALES_Q2_2000 VALUES LESS THAN (TO_DATE('01-JUL-2000', 'DD-MON-YYYY')),
   PARTITION SALES_Q3_2000 VALUES LESS THAN (TO_DATE('01-OCT-2000', 'DD-MON-YYYY')),
   PARTITION SALES_Q4_2000 VALUES LESS THAN (MAXVALUE)
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestListPartitioningClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (
	CUSTOMER_ID NUMBER(6),
	CUST_FIRST_NAME VARCHAR2(20),
	CUST_LAST_NAME VARCHAR2(20),
	NLS_TERRITORY VARCHAR2(30),
	CUST_EMAIL VARCHAR2(40)
)
PARTITION BY LIST (NLS_TERRITORY) (
	PARTITION ASIA VALUES ('CHINA', 'THAILAND'),
	PARTITION EUROPE VALUES ('GERMANY', 'ITALY', 'SWITZERLAND'),
	PARTITION WEST VALUES ('AMERICA'),
	PARTITION EAST VALUES ('INDIA'),
	PARTITION REST VALUES (DEFAULT)
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestHashPartitioningClauseWithPartitionsByQuantity()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (
	PRODUCT_ID NUMBER(6) PRIMARY KEY,
	PRODUCT_NAME VARCHAR2(50),
	PRODUCT_DESCRIPTION VARCHAR2(2000),
	CATEGORY_ID NUMBER(2),
	WEIGHT_CLASS NUMBER(1),
	WARRANTY_PERIOD INTERVAL YEAR TO MONTH,
	SUPPLIER_ID NUMBER(6),
	PRODUCT_STATUS VARCHAR2(20),
	LIST_PRICE NUMBER(8, 2),
	MIN_PRICE NUMBER(8, 2),
	CATALOG_URL VARCHAR2(50),
	CONSTRAINT PRODUCT_STATUS_LOV_DEMO
		CHECK (PRODUCT_STATUS IN ('orderable', 'planned', 'under development', 'obsolete'))
) 
PARTITION BY HASH (PRODUCT_ID)
PARTITIONS 4 STORE IN (TABLESPACE_1, TABLESPACE_2, TABLESPACE_3, TABLESPACE_4)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestObsoleteComputeStatisticsInConstraintUsingIndexClause()
				{
					const string statementText = @"CREATE TABLE TEST_TABLE (ID NUMBER PRIMARY KEY USING INDEX COMPUTE STATISTICS)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestReferencePartitioningClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (
	ORDER_ID NUMBER(12) PRIMARY KEY,
    LINE_ITEM_ID NUMBER(3),
    PRODUCT_ID NUMBER(6) NOT NULL,
    UNIT_PRICE NUMBER(8,2),
    QUANTITY NUMBER(8),
    CONSTRAINT FK_PRODUCT_ID
    	FOREIGN KEY (PRODUCT_ID) REFERENCES HASH_PRODUCTS(PRODUCT_ID)
)
PARTITION BY REFERENCE (FK_PRODUCT_ID)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestSystemPartitioningClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (C1 NUMBER, C2 NUMBER)
PARTITION BY SYSTEM (
   PARTITION PARTITION_1 TABLESPACE TABLESPACE_1,
   PARTITION PARTITION_2 TABLESPACE TABLESPACE_2,
   PARTITION PARTITION_3 TABLESPACE TABLESPACE_3
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCompositeRangeHashPartitioningClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (
	PROD_ID NUMBER(6),
	CUST_ID NUMBER,
	TIME_ID DATE,
	CHANNEL_ID CHAR(1),
	PROMO_ID NUMBER(6),
	QUANTITY_SOLD  NUMBER(3),
	AMOUNT_SOLD NUMBER(10, 2)
)
PARTITION BY RANGE (TIME_ID)
SUBPARTITION BY HASH (CHANNEL_ID) (
	PARTITION SALES_Q1_2000 VALUES LESS THAN (TO_DATE('01-APR-2000','DD-MON-YYYY')),
	PARTITION SALES_Q2_2000 VALUES LESS THAN (TO_DATE('01-JUL-2000','DD-MON-YYYY'))
    SUBPARTITIONS 8,
   	PARTITION SALES_Q3_2000 VALUES LESS THAN (TO_DATE('01-OCT-2000','DD-MON-YYYY')) (
   		SUBPARTITION CH_C,
		SUBPARTITION CH_I,
		SUBPARTITION CH_P),
	PARTITION SALES_Q4_2000 VALUES LESS THAN (MAXVALUE)
	SUBPARTITIONS 4
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCompositeRangeListPartitioningClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (
	CUSTOMER_ID NUMBER(6),
	CUST_FIRST_NAME VARCHAR2(20),
	CUST_LAST_NAME VARCHAR2(20),
	NLS_TERRITORY VARCHAR2(30),
	CREDIT_LIMIT NUMBER(9, 2)
) 
PARTITION BY RANGE (CREDIT_LIMIT)
	SUBPARTITION BY LIST (NLS_TERRITORY)
	SUBPARTITION TEMPLATE (
		SUBPARTITION EAST VALUES ('CHINA', 'JAPAN', 'INDIA', 'THAILAND'),
		SUBPARTITION WEST VALUES ('AMERICA', 'GERMANY', 'ITALY', 'SWITZERLAND'),
		SUBPARTITION OTHER VALUES (DEFAULT)
	)
(
	PARTITION P1 VALUES LESS THAN (1000),
    PARTITION P2 VALUES LESS THAN (2500),
    PARTITION P3 VALUES LESS THAN (MAXVALUE)
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestObsololeteCompressForClause()
				{
					const string statementText = @"CREATE TABLE TEST_PARTITION_TABLE (C1 NUMBER) COMPRESS FOR DIRECT_LOAD OPERATIONS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestEnableDisableConstraintClause()
				{
					const string statementText = @"CREATE TABLE TEST_TABLE (ID NUMBER CONSTRAINT PK_TEST_PARTITION_TABLE PRIMARY KEY, VAL NUMBER CONSTRAINT UQ_TEST_PARTITION_TABLE_VAL UNIQUE) ENABLE NOVALIDATE PRIMARY KEY DISABLE NOVALIDATE UNIQUE (VAL) CASCADE DROP INDEX";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestPartitionLargeObjectStorageClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_PARTITION_TABLE (
	C3 NUMBER,
	C4 VARCHAR2(30),
	C25 BLOB
)
PARTITION BY RANGE (C3)
	SUBPARTITION BY LIST (C4)
	SUBPARTITION TEMPLATE (
		SUBPARTITION SP_C4_V1 VALUES ('Key 1'),
	    SUBPARTITION SP_C4_V2 VALUES ('Key 2'),
	    SUBPARTITION SP_C4_VDEFAULT VALUES (DEFAULT)
	)
(
	PARTITION P_C3_10 VALUES LESS THAN (10)
	SEGMENT CREATION DEFERRED
	PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255 
  	STORAGE (BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT)
	TABLESPACE TBS_HQ_PDB
	LOB (C25) STORE AS SECUREFILE (
  		DISABLE STORAGE IN ROW CHUNK 8192
		CACHE READS LOGGING NOCOMPRESS KEEP_DUPLICATES 
  		STORAGE (BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT)
  	), 
	PARTITION P_C3_99 VALUES LESS THAN (99) 
	LOB (C25) STORE AS SECUREFILE (
		DISABLE STORAGE IN ROW CHUNK 8192
		CACHE READS LOGGING NOCOMPRESS KEEP_DUPLICATES 
		STORAGE (BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT)
	)
	NOCOMPRESS
	SEGMENT CREATION DEFERRED
	INDEXING OFF
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestInMemoryClause()
				{
					const string statementText =
						@"CREATE TABLE TEST_TABLE (
	C1 NUMBER,
	C2 VARCHAR2(128),
	C3 VARCHAR2(128),
	C4 VARCHAR2(128)
)
NOLOGGING
INMEMORY
	DISTRIBUTE BY ROWID RANGE
	PRIORITY CRITICAL
	NO DUPLICATE
	INMEMORY MEMCOMPRESS FOR QUERY HIGH (C2, C3)
	NO INMEMORY (C1)
	INMEMORY MEMCOMPRESS FOR CAPACITY HIGH (C4)
TABLESPACE TBS_HQ_PDB";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAttributeClusteringClause()
				{
					const string statementText = @"CREATE TABLE TEST_TABLE (C1 NUMBER, C2 NUMBER, C3 NUMBER) CLUSTERING BY INTERLEAVED ORDER ((C1, C2), (C3)) YES ON LOAD NO ON DATA MOVEMENT WITHOUT MATERIALIZED ZONEMAP";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestObsoleteRecoverableClause()
				{
					const string statementText = @"CREATE TABLE TEST_TABLE (C) UNRECOVERABLE TABLESPACE TBS_HQ_PDB AS SELECT * FROM DUAL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestInformationLifecycleManagementClause()
				{
					const string statementText = @"CREATE TABLE TEST_TABLE (C NUMBER) ILM ADD POLICY TIER TO TBS_HQ_PDB READ ONLY SEGMENT AFTER 2 YEARS OF CREATION";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateExternalTableUsingDataPump()
				{
					const string statementText =
@"CREATE TABLE DATAPUMP_TMP
ORGANIZATION EXTERNAL (
	TYPE ORACLE_DATAPUMP DEFAULT DIRECTORY TMP
	ACCESS PARAMETERS (
		COMPRESSION ENABLED HIGH
		ENCRYPTION DISABLED
		VERSION '12.1'
		LOGFILE TMP: 'tmp_table.dmp.log'
	)
	LOCATION ('tmp_table.dmp')
)
AS
SELECT * FROM ALL_OBJECTS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateExternalTableUsingDataLoader()
				{
					const string statementText =
@"CREATE TABLE HUSQVIK.SAMPLINGLOGS (
   TIMESTAMP TIMESTAMP(4),
   MESSAGE VARCHAR2(4000),
   OTHER VARCHAR2(4000),
   VODKA VARCHAR2(255),
   LOB_COLUMN BLOB
)
ORGANIZATION EXTERNAL (
    TYPE ORACLE_LOADER
    DEFAULT DIRECTORY SAMPLINGLOGS
    ACCESS PARAMETERS (
        RECORDS DELIMITED BY NEWLINE
        PREPROCESSOR PREPROCESSORS: 'GetSamplingQueueLogContents.cmd'
        NOBADFILE NODISCARDFILE NOLOGFILE
		LOAD WHEN ((OTHER != '0') AND OTHER = BLANKS) OR MESSAGE != BLANKS AND (1:40) != ""-""
		DATE_CACHE 5000
        SKIP 1
		STRING SIZES ARE IN BYTES
		--FIELD_NAMES NONE
		READSIZE 1048576
        DISABLE_DIRECTORY_LINK_CHECK
        FIELDS TERMINATED BY '|' OPTIONALLY ENCLOSED BY ""'""
        MISSING FIELD VALUES ARE NULL
        REJECT ROWS WITH ALL NULL FIELDS
        (
            ""TIMESTAMP"" CHAR DATE_FORMAT TIMESTAMP MASK 'YYYY-MM-DD HH24:MI:SS.FF4',
            MESSAGE,
            OTHER DEFAULTIF MESSAGE = BLANKS,
            TMP VARRAWC(32000) NULLIF MESSAGE = BLANKS
        )
        COLUMN TRANSFORMS (
            VODKA FROM CONCAT(CONSTANT 'value 1, ', ""TIMESTAMP"", CONSTANT ', value2'),
            LOB_COLUMN FROM LOBFILE (TMP) FROM (SAMPLINGLOGS)
        )
    )
    LOCATION ('SamplingQueueService_2014-10-21_Info.txt')
)
--PARALLEL
REJECT LIMIT UNLIMITED";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateIndex
			{
				[Test(Description = @"")]
				public void TestCreateSequence()
				{
					const string statementText =
						@"CREATE INDEX HUSQVIK.IX_MEASUREVALUES
ON MEASUREVALUES (USAGEPOINT_ID, MEASURETIMESTAMP)
COMPRESS 1
LOCAL
UNUSABLE
NOLOGGING";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateView
			{
				[Test(Description = @"")]
				public void TestCreateSimpleView()
				{
					const string statementText =
						@"CREATE VIEW TEST_VIEW
(
	SELECTION_ID,
	NAME,
	PROJECT_ID,
	RESPONDENTBUCKET_ID,
	GUID UNIQUE RELY DISABLE NOVALIDATE,
	CONSTRAINT PK_TEST_VIEW PRIMARY KEY (SELECTION_ID) RELY DISABLE NOVALIDATE
) AS
SELECT SELECTION_ID, NAME, PROJECT_ID, RESPONDENTBUCKET_ID, SYS_GUID() GUID FROM SELECTION";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateViewWithoutColumnList()
				{
					const string statementText = @"CREATE VIEW TEST_VIEW AS SELECT * FROM DUAL WITH READ ONLY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateCluster
			{
				[Test(Description = @"")]
				public void TestCreateCluster()
				{
					const string statementText =
						@"CREATE CLUSTER SALES (
	AMOUNT_SOLD NUMBER,
	PROD_ID NUMBER
)
HASHKEYS 100000
HASH IS (AMOUNT_SOLD * 10 + PROD_ID)
SIZE 300
TABLESPACE TEST_TABLESPACE
PARTITION BY RANGE (AMOUNT_SOLD) (
	PARTITION P1 VALUES LESS THAN (1000),
	PARTITION P2 VALUES LESS THAN (2000),
	PARTITION P3 VALUES LESS THAN (3000),
	PARTITION PM VALUES LESS THAN (MAXVALUE)
)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateMaterializedViewLog
			{
				[Test(Description = @"")]
				public void CreateSimpleMaterializedViewLog()
				{
					const string statementText =
						@"CREATE MATERIALIZED VIEW LOG ON TEST_TABLE 
WITH ROWID, SEQUENCE (LIST_PRICE, MIN_PRICE, CATEGORY_ID), PRIMARY KEY, OBJECT ID
INCLUDING NEW VALUES";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void CreateMaterializedViewLogWithPurgeClause()
				{
					const string statementText =
						@"CREATE MATERIALIZED VIEW LOG ON TEST_TABLE
PCTFREE 5
TABLESPACE TABLESPACE_01
STORAGE (INITIAL 10K)
PURGE REPEAT INTERVAL '5' DAY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void CreateMaterializedViewLogWithImmediateAsynchronousPurge()
				{
					const string statementText =
						@"CREATE MATERIALIZED VIEW LOG ON TEST_TABLE PURGE IMMEDIATE ASYNCHRONOUS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void CreateMaterializedViewLogForSynchronousRefreshClause()
				{
					const string statementText = @"CREATE MATERIALIZED VIEW LOG ON TEST_TABLE FOR SYNCHRONOUS REFRESH USING TEST_STAGING_LOG";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateSequence
			{
				[Test(Description = @"")]
				public void TestCreateSequence()
				{
					const string statementText = @"CREATE SEQUENCE HUSQVIK.TEST_SEQUNCE GLOBAL NOKEEP NOORDER NOCACHE NOCYCLE MINVALUE - 1000000 MAXVALUE -1 START WITH -1 INCREMENT BY -1;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(24);

					terminals[0].Id.ShouldBe(Terminals.Create);
					terminals[1].Id.ShouldBe(Terminals.Sequence);
					terminals[2].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Dot);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[5].Id.ShouldBe(Terminals.Global);
					terminals[6].Id.ShouldBe(Terminals.NoKeep);
					terminals[7].Id.ShouldBe(Terminals.NoOrder);
					terminals[8].Id.ShouldBe(Terminals.NoCache);
					terminals[9].Id.ShouldBe(Terminals.NoCycle);
					terminals[10].Id.ShouldBe(Terminals.MinimumValue);
					terminals[11].Id.ShouldBe(Terminals.MathMinus);
					terminals[12].Id.ShouldBe(Terminals.IntegerLiteral);
					terminals[13].Id.ShouldBe(Terminals.MaximumValue);
					terminals[14].Id.ShouldBe(Terminals.MathMinus);
					terminals[15].Id.ShouldBe(Terminals.IntegerLiteral);
					terminals[16].Id.ShouldBe(Terminals.Start);
					terminals[17].Id.ShouldBe(Terminals.With);
					terminals[18].Id.ShouldBe(Terminals.MathMinus);
					terminals[19].Id.ShouldBe(Terminals.IntegerLiteral);
					terminals[20].Id.ShouldBe(Terminals.Increment);
					terminals[21].Id.ShouldBe(Terminals.By);
					terminals[22].Id.ShouldBe(Terminals.MathMinus);
					terminals[23].Id.ShouldBe(Terminals.IntegerLiteral);
				}
			}

			public class CreateSynonym
			{
				[Test(Description = @"")]
				public void TestCreateSynonym()
				{
					const string statementText = @"CREATE OR REPLACE NONEDITIONABLE PUBLIC SYNONYM CUSTOMER FOR HUSQVIK.CUSTOMER@DBLINK;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(13);

					terminals[0].Id.ShouldBe(Terminals.Create);
					terminals[1].Id.ShouldBe(Terminals.Or);
					terminals[2].Id.ShouldBe(Terminals.Replace);
					terminals[3].Id.ShouldBe(Terminals.NonEditionable);
					terminals[4].Id.ShouldBe(Terminals.Public);
					terminals[5].Id.ShouldBe(Terminals.Synonym);
					terminals[6].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[7].Id.ShouldBe(Terminals.For);
					terminals[8].Id.ShouldBe(Terminals.SchemaIdentifier);
					terminals[9].Id.ShouldBe(Terminals.Dot);
					terminals[10].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[11].Id.ShouldBe(Terminals.AtCharacter);
					terminals[12].Id.ShouldBe(Terminals.DatabaseLinkIdentifier);
				}
			}

			public class CreateDirectory
			{
				[Test(Description = @"")]
				public void TestCreateDirectory()
				{
					const string statementText = @"CREATE OR REPLACE DIRECTORY TEST_DIRECTORY AS '\\host\directory\subdirectory'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(7);

					terminals[0].Id.ShouldBe(Terminals.Create);
					terminals[1].Id.ShouldBe(Terminals.Or);
					terminals[2].Id.ShouldBe(Terminals.Replace);
					terminals[3].Id.ShouldBe(Terminals.Directory);
					terminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[5].Id.ShouldBe(Terminals.As);
					terminals[6].Id.ShouldBe(Terminals.StringLiteral);
				}
			}

			public class CreateProfile
			{
				[Test(Description = @"")]
				public void TestCreateProfile()
				{
					const string statementText = @"CREATE PROFILE TEST_PROFILE LIMIT COMPOSITE_LIMIT DEFAULT PRIVATE_SGA DEFAULT PASSWORD_GRACE_TIME DEFAULT PASSWORD_VERIFY_FUNCTION VERIFY_FUNCTION CONTAINER = ALL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateTablespace
			{
				[Test(Description = @"")]
				public void TestCreateUndoTablespace()
				{
					const string statementText = @"CREATE UNDO TABLESPACE undots1 DATAFILE 'undotbs_1a.dbf' SIZE 10M AUTOEXTEND ON RETENTION GUARANTEE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateComplexTablespace()
				{
					const string statementText =
@"CREATE SMALLFILE TABLESPACE encrypt_ts
	DATAFILE
		'$ORACLE_HOME/dbs/encrypt_df1.dbf' SIZE 1M REUSE AUTOEXTEND ON,
		'$ORACLE_HOME/dbs/encrypt_df2.dbf' SIZE 1M REUSE AUTOEXTEND OFF
	ENCRYPTION USING 'AES256'
	DEFAULT STORAGE (ENCRYPT INITIAL 2m MAXSIZE 100m)
	EXTENT MANAGEMENT LOCAL UNIFORM SIZE 1M
	SEGMENT SPACE MANAGEMENT AUTO
	LOGGING
	ONLINE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateSimpleBigFileTemporaryTablespace()
				{
					const string statementText = @"CREATE BIGFILE TEMPORARY TABLESPACE omf_ts1";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateTablespaceWithUnnamedDataFileOptions()
				{
					const string statementText = @"CREATE TABLESPACE omf_ts2 DATAFILE AUTOEXTEND OFF";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateUser
			{
				[Test(Description = @"")]
				public void TestCreateUser()
				{
					const string statementText =
@"CREATE USER sidney 
    IDENTIFIED BY out_standing1 
    DEFAULT TABLESPACE example 
    QUOTA 10M ON example 
    TEMPORARY TABLESPACE temp
    QUOTA 5M ON system 
    PROFILE app_user
	ACCOUNT UNLOCK
	ENABLE EDITIONS
	CONTAINER = ALL
    PASSWORD EXPIRE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateRole
			{
				[Test(Description = @"")]
				public void TestCreateRole()
				{
					const string statementText = @"CREATE ROLE warehouse_user IDENTIFIED GLOBALLY CONTAINER = ALL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateRollbackSegment
			{
				[Test(Description = @"")]
				public void TestCreateRollbackSegment()
				{
					const string statementText = @"CREATE ROLLBACK SEGMENT rollback_segment TABLESPACE tablespace1 STORAGE (INITIAL 16M)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateRestorePoint
			{
				[Test(Description = @"")]
				public void TestCreateRestorePoint()
				{
					const string statementText = @"CREATE RESTORE POINT restore_point AS OF SCN 12345 GUARANTEE FLASHBACK DATABASE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateParameterFile
			{
				[Test(Description = @"")]
				public void TestCreateParameterFileFromServerParameterFile()
				{
					const string statementText = @"CREATE PFILE = 'my_init.ora' FROM SPFILE = 's_params.ora'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateParameterFileFromMemory()
				{
					const string statementText = @"CREATE PFILE FROM MEMORY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateServerParameterFile
			{
				[Test(Description = @"")]
				public void TestCreateServerParameterFileFromParameterFile()
				{
					const string statementText = @"CREATE SPFILE = 'my_init.ora' FROM PFILE = 's_params.ora'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestCreateServerParameterFileFromMemory()
				{
					const string statementText = @"CREATE SPFILE FROM MEMORY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class CreateAuthorizationSchema
			{
				[Test(Description = @""), Ignore]
				public void TestCreateAuthorizationSchema()
				{
					const string statementText =
@"CREATE SCHEMA AUTHORIZATION test_schema
	CREATE VIEW test_view AS SELECT * FROM DUAL
	CREATE TABLE test_table (test_column NUMBER)
	GRANT REFERENCES (employee_id), UPDATE (employee_id, salary, commission_pct) ON hr.employees TO oe
	GRANT READ ON DIRECTORY bfile_dir TO hr WITH GRANT OPTION
	GRANT ALL ON bonuses TO hr WITH GRANT OPTION
	--GRANT CREATE ANY MATERIALIZED VIEW, ALTER ANY MATERIALIZED VIEW, DROP ANY MATERIALIZED VIEW, QUERY REWRITE, GLOBAL QUERY REWRITE TO dw_manager WITH ADMIN OPTION";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}
		}

		public class Alter
		{
			public class AlterProgram
			{
				[Test(Description = @"")]
				public void TestAlterProcedureNonEditionable()
				{
					const string statementText = @"ALTER PROCEDURE procedure1 NONEDITIONABLE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterProcedureCompile()
				{
					const string statementText = @"ALTER PROCEDURE procedure1 COMPILE DEBUG PLSCOPE_SETTINGS = 'IDENTIFIERS:ALL' PLSQL_CODE_TYPE=INTERPRETED PLSQL_OPTIMIZE_LEVEL=1 PLSQL_WARNINGS ='ENABLE:(5000,5001,5002)', 'DISABLE:(6000,6001)' PLSQL_CCFLAGS = 'debug:TRUE' REUSE SETTINGS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterPackageCompile()
				{
					const string statementText = @"ALTER PACKAGE package1 COMPILE DEBUG BODY;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterSystem
			{
				[Test(Description = @"")]
				public void TestAlterSystemArchiveLogLogFile()
				{
					const string statementText = @"ALTER SYSTEM ARCHIVE LOG INSTANCE 'TEST_INSTANCE' LOGFILE 'TEST_LOGFILE' USING BACKUP CONTROLFILE TO 'TEST_LOCATION'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemDisableRestrictedSession()
				{
					const string statementText = @"ALTER SYSTEM DISABLE RESTRICTED SESSION";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemSetEncryptionWalletOpen()
				{
					const string statementText = @"ALTER SYSTEM SET ENCRYPTION WALLET OPEN IDENTIFIED BY ""password""";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemSetEncryptionKey()
				{
					const string statementText = @"ALTER SYSTEM SET ENCRYPTION KEY IDENTIFIED BY ""password""";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemSetEncryptionWalletClose()
				{
					const string statementText = @"ALTER SYSTEM SET ENCRYPTION WALLET CLOSE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemFlushRedo()
				{
					const string statementText = @"ALTER SYSTEM FLUSH REDO TO DB_NAME NO CONFIRM APPLY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemSwitchLogFile()
				{
					const string statementText = @"ALTER SYSTEM SWITCH LOGFILE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemDisableDistributedRecovery()
				{
					const string statementText = @"ALTER SYSTEM DISABLE DISTRIBUTED RECOVERY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemRelocateClient()
				{
					const string statementText = @"ALTER SYSTEM RELOCATE CLIENT 'ClientName'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemReset()
				{
					const string statementText = @"ALTER SYSTEM RESET p1 SID = '*' SCOPE = SPFILE p2 SID = '*' SCOPE = SPFILE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemShutdownDispatcher()
				{
					const string statementText = @"ALTER SYSTEM SHUTDOWN IMMEDIATE 'D 001'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemQuiesceRestricted()
				{
					const string statementText = @"ALTER SYSTEM QUIESCE RESTRICTED";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemStartRollingMigration()
				{
					const string statementText = @"ALTER SYSTEM START ROLLING MIGRATION TO 'ASM_version'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemStopRollingPatch()
				{
					const string statementText = @"ALTER SYSTEM STOP ROLLING PATCH";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemCheckpointLocal()
				{
					const string statementText = @"ALTER SYSTEM CHECKPOINT LOCAL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemCheckDatafilesGlobal()
				{
					const string statementText = @"ALTER SYSTEM CHECK DATAFILES GLOBAL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemRegister()
				{
					const string statementText = @"ALTER SYSTEM REGISTER";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemDisconnectSession()
				{
					const string statementText = @"ALTER SYSTEM DISCONNECT SESSION 'SID, SERIAL#' POST_TRANSACTION IMMEDIATE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSystemKillSession()
				{
					const string statementText = @"ALTER SYSTEM KILL SESSION 'SID, SERIAL#, @1' NOREPLAY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				// TODO: Missing tests
			}

			public class AlterSession
			{
				[Test(Description = @"")]
				public void TestAlterSessionParameters()
				{
					const string statementText = @"ALTER SESSION SET statistics_level = ALL SQL_TRACE = TRUE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(9);

					terminals[0].Id.ShouldBe(Terminals.Alter);
					terminals[1].Id.ShouldBe(Terminals.Session);
					terminals[2].Id.ShouldBe(Terminals.Set);
					terminals[3].Id.ShouldBe(Terminals.Identifier);
					terminals[4].Id.ShouldBe(Terminals.MathEquals);
					terminals[5].Id.ShouldBe(Terminals.Identifier);
					terminals[6].Id.ShouldBe(Terminals.Identifier);
					terminals[7].Id.ShouldBe(Terminals.MathEquals);
					terminals[8].Id.ShouldBe(Terminals.Identifier);
				}

				[Test(Description = @"")]
				public void TestAlterSessionResumableDistributedTransaction()
				{
					const string statementText = @"ALTER SESSION ENABLE RESUMABLE TIMEOUT 60 NAME 'Distributed transaction'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionSyncWithPrimary()
				{
					const string statementText = @"ALTER SESSION SYNC WITH PRIMARY";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionGuard()
				{
					const string statementText = @"ALTER SESSION DISABLE GUARD";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionProcedureCommit()
				{
					const string statementText = @"ALTER SESSION DISABLE COMMIT IN PROCEDURE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionForceParallel()
				{
					const string statementText = @"ALTER SESSION FORCE PARALLEL QUERY PARALLEL 8";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionCloseDatabaseLink()
				{
					const string statementText = @"ALTER SESSION CLOSE DATABASE LINK TEST_DB_LINK";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionNumericParameter()
				{
					const string statementText = @"ALTER SESSION SET SORT_AREA_SIZE=1073741824;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionSetEvents()
				{
					const string statementText = @"ALTER SESSION SET EVENTS '10053 trace name context forever, level 2'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterSessionAdviseNothing()
				{
					const string statementText = @"ALTER SESSION ADVISE NOTHING";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterProfile
			{
				[Test(Description = @"")]
				public void TestAlterProfile()
				{
					const string statementText = @"ALTER PROFILE TEST_PROFILE LIMIT COMPOSITE_LIMIT DEFAULT PRIVATE_SGA DEFAULT PASSWORD_GRACE_TIME DEFAULT PASSWORD_VERIFY_FUNCTION VERIFY_FUNCTION CONTAINER = ALL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(15);

					terminals[0].Id.ShouldBe(Terminals.Alter);
					terminals[1].Id.ShouldBe(Terminals.Profile);
					terminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
					terminals[3].Id.ShouldBe(Terminals.Limit);
					terminals[4].Id.ShouldBe(Terminals.CompositeLimit);
					terminals[5].Id.ShouldBe(Terminals.Default);
					terminals[6].Id.ShouldBe(Terminals.PrivateSystemGlobalArea);
					terminals[7].Id.ShouldBe(Terminals.Default);
					terminals[8].Id.ShouldBe(Terminals.PasswordGraceTime);
					terminals[9].Id.ShouldBe(Terminals.Default);
					terminals[10].Id.ShouldBe(Terminals.PasswordVerifyFunction);
					terminals[11].Id.ShouldBe(Terminals.Identifier);
					terminals[12].Id.ShouldBe(Terminals.Container);
					terminals[13].Id.ShouldBe(Terminals.MathEquals);
					terminals[14].Id.ShouldBe(Terminals.All);
				}
			}

			public class AlterRole
			{
				[Test(Description = @"")]
				public void TestAlterRole()
				{
					const string statementText = @"ALTER ROLE dw_manager IDENTIFIED USING hr.admin CONTAINER = CURRENT";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterRollbackSegment
			{
				[Test(Description = @"")]
				public void TestAlterRollbackSegment()
				{
					const string statementText = @"ALTER ROLLBACK SEGMENT rollback_segment SHRINK TO 128M";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterResourceCost
			{
				[Test(Description = @"")]
				public void TestAlterResourceCost()
				{
					const string statementText = @"ALTER RESOURCE COST PRIVATE_SGA 0 LOGICAL_READS_PER_SESSION 0 CONNECT_TIME 0 CPU_PER_SESSION 0";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterView
			{
				[Test(Description = @"")]
				public void TestAlterViewAddConstraint()
				{
					const string statementText = @"ALTER VIEW tmp ADD CONSTRAINT constraint_name PRIMARY KEY (dummy) RELY DISABLE NOVALIDATE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterViewDropConstraint()
				{
					const string statementText = @"ALTER VIEW tmp DROP UNIQUE (dummy)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterViewCompile()
				{
					const string statementText = @"ALTER VIEW tmp COMPILE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterViewReadWrite()
				{
					const string statementText = @"ALTER VIEW tmp READ WRITE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterViewNonEditionable()
				{
					const string statementText = @"ALTER VIEW tmp NONEDITIONABLE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterTablespace
			{
				[Test(Description = @"")]
				public void TestAlterTablespaceEndBackup()
				{
					const string statementText = @"ALTER TABLESPACE tbs_01 END BACKUP";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceOffline()
				{
					const string statementText = @"ALTER TABLESPACE tbs_02 OFFLINE IMMEDIATE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceRenameDatafile()
				{
					const string statementText = @"ALTER TABLESPACE tbs_02 RENAME DATAFILE 'diskb:tbs_f5.dbf' TO 'diska:tbs_f5.dbf'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceAddDatafile()
				{
					const string statementText = @"ALTER TABLESPACE tbs_03 ADD DATAFILE 'tbs_f04.dbf' SIZE 100K AUTOEXTEND ON NEXT 10K MAXSIZE 100K";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceAddTempfile()
				{
					const string statementText = @"ALTER TABLESPACE temp_demo ADD TEMPFILE 'temp05.dbf' SIZE 5 AUTOEXTEND ON";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceDropTempfile()
				{
					const string statementText = @"ALTER TABLESPACE temp_demo DROP TEMPFILE 'temp05.dbf'";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceShrinkTempfile()
				{
					const string statementText = @"ALTER TABLESPACE temp_demo SHRINK SPACE KEEP 10M";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceAddUnnamedDatafile()
				{
					const string statementText = @"ALTER TABLESPACE omf_ts1 ADD DATAFILE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceNologging()
				{
					const string statementText = @"ALTER TABLESPACE tbs_03 NOLOGGING";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTablespaceRetentionNoGuarantee()
				{
					const string statementText = @"ALTER TABLESPACE undots1 RETENTION NOGUARANTEE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterTable
			{
				[Test(Description = @"")]
				public void TestAlterTableDisableAllTriggers()
				{
					const string statementText = @"ALTER TABLE test_table DISABLE ALL TRIGGERS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableRenameColumn()
				{
					const string statementText = @"ALTER TABLE test_table RENAME COLUMN old_name TO new_name";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDeallocateUnused()
				{
					const string statementText = @"ALTER TABLE test_table DEALLOCATE UNUSED KEEP 1G";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAddColumns()
				{
					const string statementText = @"ALTER TABLE countries ADD (duty_pct NUMBER(2, 2) CHECK (duty_pct < 10.5), visa_needed VARCHAR2(3))";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAddVirtualColumnWithoutExplicitTypeDefinition()
				{
					const string statementText = @"ALTER TABLE emp2 ADD (income AS (salary + (salary * commission_pct)))";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAddSingleVirtualColumnWithoutExplicitTypeDefinition()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE ADD C3 AS (C1 + (C1 * C2))";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableModifySingleColumnInvisible()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE MODIFY C3 INVISIBLE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);

					var terminals = statement.AllTerminals.ToArray();
					terminals.Length.ShouldBe(6);

					terminals[5].Id.ShouldBe(Terminals.Invisible);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropSingleColumn()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE DROP COLUMN C3 INVALIDATE CASCADE CONSTRAINTS CHECKPOINT 123";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropMultipleColumns()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE DROP (C2, C3)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableSetSingleColumnUnused()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE SET UNUSED COLUMN C3 CASCADE CONSTRAINTS ONLINE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropColumnsContinue()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE DROP COLUMNS CONTINUE CHECKPOINT 123";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropUnusedColumns()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE DROP UNUSED COLUMNS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAddPeriod()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE ADD (PERIOD FOR C5 (C3, C4))";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropPeriod()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE DROP (PERIOD FOR C4)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableModifyColumnType()
				{
					const string statementText = @"ALTER TABLE countries MODIFY (duty_pct NUMBER(3, 2))";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableModifySegmentAttributes()
				{
					const string statementText = @"ALTER TABLE employees PCTFREE 30 PCTUSED 60";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableModifyColumnEncryption()
				{
					const string statementText = @"ALTER TABLE employees MODIFY (salary ENCRYPT USING 'AES256' 'NOMAC')";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableRenameConstraint()
				{
					const string statementText = @"ALTER TABLE customers RENAME CONSTRAINT cust_fname_nn TO cust_firstname_nn;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropPrimaryKey()
				{
					const string statementText = @"ALTER TABLE departments DROP PRIMARY KEY CASCADE;";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropConstraint()
				{
					const string statementText = @"ALTER TABLE departments DROP CONSTRAINT pk_dept CASCADE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableDropUniqueConstraint()
				{
					const string statementText = @"ALTER TABLE departments DROP UNIQUE (c1, c2, c3)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableNoMinimizeRecordsPerBlockAndUpgrade()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE NOMINIMIZE RECORDS_PER_BLOCK UPGRADE NOT INCLUDING DATA";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAllocateExtent()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE ALLOCATE EXTENT (SIZE 1E DATAFILE 'datafile01.dat' INSTANCE 1)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAddDropSupplementalLogging()
				{
					const string statementText =
@"ALTER TABLE TEST_TABLE
	ADD SUPPLEMENTAL LOG GROUP G2 (C1 NO LOG, C2) ALWAYS
	ADD SUPPLEMENTAL LOG DATA (FOREIGN KEY, UNIQUE) COLUMNS
	DROP SUPPLEMENTAL LOG DATA (PRIMARY KEY) COLUMNS
	DROP SUPPLEMENTAL LOG GROUP G1";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAddConstraint()
				{
					const string statementText = @"ALTER TABLE COUNTRY ADD CONSTRAINT PK_COUNTRY PRIMARY KEY (COUNTRY_ID)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
				
				[Test(Description = @"")]
				public void TestAlterTableCombineTableAttributeColumnAttributeAndConstraintClause()
				{
					const string statementText = @"ALTER TABLE tmp CACHE ROW ARCHIVAL ADD (c2 NUMBER) ADD CONSTRAINT pk_tmp PRIMARY KEY (dummy) NOLOGGING ADD (c3 NUMBER) READ ONLY ADD CONSTRAINT uq_tmp UNIQUE (dummy, c2)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterTableAddConstraintWhileTyping()
				{
					const string statement1 = @"ALTER TABLE pracovnici ADD CONSTRAINT fk_pracovnici_zarizenia REF";

					var statement = Parser.Parse(statement1).Single();
					statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);
				}

				/*[Test(Description = @"")]
				public void TestAlterTableAlterXmlSchemaAllowAnySchema()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE NOCACHE ALLOW ANYSCHEMA";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
				
				[Test(Description = @"")]
				public void TestAlterTableAlterXmlSchemaDisallowNonSchema()
				{
					const string statementText = @"ALTER TABLE TEST_TABLE NOCACHE DISALLOW NONSCHEMA";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}*/
			}

			public class AlterUser
			{
				[Test(Description = @"")]
				public void TestBasicAlterUser()
				{
					const string statementText = @"ALTER USER sidney IDENTIFIED BY second_2nd_pwd DEFAULT TABLESPACE example PROFILE new_profile DEFAULT ROLE ALL EXCEPT dw_manager TEMPORARY TABLESPACE tbs_grp_01 REMOVE CONTAINER_DATA = (C1, C2) FOR TEST_SCHEMA.TEST_OBJECT";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterMultipleUsersGrantConnectThroughEnterpriseUsers()
				{
					const string statementText = @"ALTER USER u1, u2 GRANT CONNECT THROUGH ENTERPRISE USERS";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterMultipleUsersGrantConnectThroughUserWithRoles()
				{
					const string statementText = @"ALTER USER u1, u2 GRANT CONNECT THROUGH proxy_user WITH ROLE ALL EXCEPT r1, r2 AUTHENTICATION REQUIRED";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}

			public class AlterMaterializedView
			{
				[Test(Description = @"")]
				public void TestAlterMaterializedViewCompile()
				{
					const string statementText = @"ALTER MATERIALIZED VIEW order_data COMPILE";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"Undocumented")]
				public void TestAlterMaterializedViewAddConstraint()
				{
					const string statementText = @"ALTER MATERIALIZED VIEW TEST_MV ADD CONSTRAINT PK_TEST_MV PRIMARY KEY (ID) ADD CONSTRAINT UQ_TEST_MV_LEADING_ZEROES UNIQUE (LEADING_ZEROES)";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"Undocumented")]
				public void TestAlterMaterializedViewModifyColumns()
				{
					const string statementText = @"ALTER MATERIALIZED VIEW TEST_MV MODIFY ID NUMBER(10) NOT NULL MODIFY NUMBER_AS_NUMBER NOT NULL MODIFY NUMBER_AS_CHAR NOT NULL";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}

				[Test(Description = @"")]
				public void TestAlterMaterializedRefreshCompleteStartWithNext()
				{
					const string statementText =
@"ALTER MATERIALIZED VIEW emp_data
	REFRESH COMPLETE
	START WITH TRUNC(SYSDATE + 1) + 12/24
	NEXT SYSDATE + 1";

					var result = Parser.Parse(statementText);

					result.Count.ShouldBe(1);
					var statement = result.Single();
					statement.ParseStatus.ShouldBe(ParseStatus.Success);
				}
			}
		}

		public class Truncate
		{
			[Test(Description = @"")]
			public void TestTruncateTableMinimal()
			{
				const string statementText = @"TRUNCATE TABLE TEST_TABLE";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(3);

				terminals[0].Id.ShouldBe(Terminals.Truncate);
				terminals[1].Id.ShouldBe(Terminals.Table);
				terminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
			}

			[Test(Description = @"")]
			public void TestTruncateTableMaximal()
			{
				const string statementText = @"TRUNCATE TABLE TEST_SCHEMA.TEST_TABLE PURGE MATERIALIZED VIEW LOG REUSE STORAGE CASCADE;";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}
		}

		public class Analyze
		{
			[Test(Description = @"")]
			public void TestAnalyzeTableDeleteStatistics()
			{
				const string statementText = @"ANALYZE TABLE orders DELETE STATISTICS";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}
			
			[Test(Description = @"")]
			public void TestAnalyzeIndexValidateStructure()
			{
				const string statementText = @"ANALYZE INDEX inv_product_ix VALIDATE STRUCTURE";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestAnalyzeTableListChainedRows()
			{
				const string statementText = @"ANALYZE TABLE orders LIST CHAINED ROWS INTO chained_rows";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}

			[Test(Description = @"")]
			public void TestAnalyzeClusterValidateStructureCascade()
			{
				const string statementText = @"ANALYZE CLUSTER personnel VALIDATE STRUCTURE CASCADE COMPLETE ONLINE";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);
			}
		}

		public class Purge
		{
			[Test(Description = @"")]
			public void TestPurge()
			{
				const string statementText = @"PURGE TABLESPACE TEST_TABLESPACE USER TEST_USER;";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(5);

				terminals[0].Id.ShouldBe(Terminals.Purge);
				terminals[1].Id.ShouldBe(Terminals.Tablespace);
				terminals[2].Id.ShouldBe(Terminals.Identifier);
				terminals[3].Id.ShouldBe(Terminals.User);
				terminals[4].Id.ShouldBe(Terminals.Identifier);
			}
		}

		public class ExplainPlan
		{
			[Test(Description = @"")]
			public void TestExplainPlan()
			{
				const string statementText = @"EXPLAIN PLAN SET STATEMENT_ID = 'dummyStatementId' INTO PLAN_TABLE FOR SELECT * FROM DUAL";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(13);

				terminals[0].Id.ShouldBe(Terminals.Explain);
				terminals[1].Id.ShouldBe(Terminals.Plan);
				terminals[2].Id.ShouldBe(Terminals.Set);
				terminals[3].Id.ShouldBe(Terminals.StatementId);
				terminals[4].Id.ShouldBe(Terminals.MathEquals);
				terminals[5].Id.ShouldBe(Terminals.StringLiteral);
				terminals[6].Id.ShouldBe(Terminals.Into);
				terminals[7].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[8].Id.ShouldBe(Terminals.For);
				terminals[9].Id.ShouldBe(Terminals.Select);
				terminals[10].Id.ShouldBe(Terminals.Asterisk);
				terminals[11].Id.ShouldBe(Terminals.From);
				terminals[12].Id.ShouldBe(Terminals.ObjectIdentifier);
			}
		}

		public class Call
		{
			[Test(Description = @"")]
			public void TestCallStatement()
			{
				const string statementText = @"CALL HUSQVIK.RET_WAREHOUSE_TYP(WAREHOUSE_TYP(234, 'Warehouse 234')).ret_name() INTO :x INDICATOR :y;";

				var result = Parser.Parse(statementText);

				result.Count.ShouldBe(1);
				var statement = result.Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var terminals = statement.AllTerminals.ToArray();
				terminals.Length.ShouldBe(22);

				terminals[0].Id.ShouldBe(Terminals.Call);
				terminals[1].Id.ShouldBe(Terminals.ObjectIdentifier);
				terminals[2].Id.ShouldBe(Terminals.Dot);
				terminals[3].Id.ShouldBe(Terminals.Identifier);
				terminals[4].Id.ShouldBe(Terminals.LeftParenthesis);
				terminals[5].Id.ShouldBe(Terminals.Identifier);
				terminals[6].Id.ShouldBe(Terminals.LeftParenthesis);
				terminals[7].Id.ShouldBe(Terminals.NumberLiteral);
				terminals[8].Id.ShouldBe(Terminals.Comma);
				terminals[9].Id.ShouldBe(Terminals.StringLiteral);
				terminals[10].Id.ShouldBe(Terminals.RightParenthesis);
				terminals[11].Id.ShouldBe(Terminals.RightParenthesis);
				terminals[12].Id.ShouldBe(Terminals.Dot);
				terminals[13].Id.ShouldBe(Terminals.MemberFunctionIdentifier);
				terminals[14].Id.ShouldBe(Terminals.LeftParenthesis);
				terminals[15].Id.ShouldBe(Terminals.RightParenthesis);
				terminals[16].Id.ShouldBe(Terminals.Into);
				terminals[17].Id.ShouldBe(Terminals.Colon);
				terminals[18].Id.ShouldBe(Terminals.BindVariableIdentifier);
				terminals[19].Id.ShouldBe(Terminals.Indicator);
				terminals[20].Id.ShouldBe(Terminals.Colon);
				terminals[21].Id.ShouldBe(Terminals.IndicatorVariableIdentifier);
			}
		}

		public class CanAddPairCharacter
		{
			[Test(Description = @"")]
			public void TestCanAddPairCharacter()
			{
				Parser.CanAddPairCharacter("n'VODKA", '\'').ShouldBe(false);
				Parser.CanAddPairCharacter("nq'VODKA", '\'').ShouldBe(false);
				Parser.CanAddPairCharacter("q'VODKA", '\'').ShouldBe(false);
				Parser.CanAddPairCharacter("'VODKA", '\'').ShouldBe(false);
				Parser.CanAddPairCharacter("VODKA", '\'').ShouldBe(true);
				Parser.CanAddPairCharacter("nqVODKA", '\'').ShouldBe(true);
				Parser.CanAddPairCharacter("qVODKA", '\'').ShouldBe(true);
				Parser.CanAddPairCharacter("'", '\'').ShouldBe(false);
			}
		}

		private static OracleTokenReader CreateTokenReader(string sqlText)
		{
			Trace.WriteLine("SQL text: " + sqlText);

			return OracleTokenReader.Create(new StringReader(sqlText));
		}
    }
}
