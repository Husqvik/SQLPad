using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	[TestFixture]
    public class OracleSqlParserTest
    {
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();

		[Test(Description = @"")]
		public void Test1()
		{
			using (var reader = File.OpenText(@"TestFiles\SqlStatements1.sql"))
			{
				var result = _oracleSqlParser.Parse(OracleTokenReader.Create(reader));
				result.ShouldNotBe(null);

				var i = 0;
				foreach (var statement in result)
				{
					Trace.WriteLine(++i + ". statement parse status: " + statement.ProcessingResult);
				}
			}
		}

		[Test(Description = @"Tests trivial query. ")]
		public void TestTrivialQuery()
		{
			const string sqlText = @"SELECT NULL FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));
			
			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var terminals = result.Single().NodeCollection.SelectMany(i => i.Terminals).ToList();

			terminals.Count.ShouldBe(4);
			terminals[0].Id.ShouldBe(OracleGrammarDescription.Terminals.Select);
			terminals[0].SourcePosition.IndexStart.ShouldBe(0);
			terminals[0].SourcePosition.IndexEnd.ShouldBe(5);
			terminals[1].Id.ShouldBe(OracleGrammarDescription.Terminals.Null);
			terminals[1].SourcePosition.IndexStart.ShouldBe(7);
			terminals[1].SourcePosition.IndexEnd.ShouldBe(10);
			terminals[2].Id.ShouldBe(OracleGrammarDescription.Terminals.From);
			terminals[2].SourcePosition.IndexStart.ShouldBe(12);
			terminals[2].SourcePosition.IndexEnd.ShouldBe(15);
			terminals[3].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[3].Token.Value.ShouldBe("DUAL");
			terminals[3].SourcePosition.IndexStart.ShouldBe(17);
			terminals[3].SourcePosition.IndexEnd.ShouldBe(20);
		}

		[Test(Description = @"Tests query with fully qualified names and aliases. ")]
		public void TestQueryWithFullyQualifiedNamesAndAliases()
		{
			const string sqlText = @"SELECT NULL AS "">=;+Alias/*--^"", SYS.DUAL.DUMMY FROM SYS.DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var rootToken = result.Single().NodeCollection.Single();
			var terminals = rootToken.Terminals.ToList();

			terminals.Count.ShouldBe(14);
			terminals[0].Id.ShouldBe(OracleGrammarDescription.Terminals.Select);
			terminals[1].Id.ShouldBe(OracleGrammarDescription.Terminals.Null);
			terminals[2].Id.ShouldBe(OracleGrammarDescription.Terminals.As);
			terminals[3].Id.ShouldBe(OracleGrammarDescription.Terminals.Alias);
			terminals[3].Token.Value.ShouldBe("\">=;+Alias/*--^\"");
			terminals[4].Id.ShouldBe(OracleGrammarDescription.Terminals.Comma);
			terminals[5].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[5].Token.Value.ShouldBe("SYS");
			terminals[6].Id.ShouldBe(OracleGrammarDescription.Terminals.Dot);
			terminals[7].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[7].Token.Value.ShouldBe("DUAL");
			terminals[8].Id.ShouldBe(OracleGrammarDescription.Terminals.Dot);
			terminals[9].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[9].Token.Value.ShouldBe("DUMMY");
			terminals[10].Id.ShouldBe(OracleGrammarDescription.Terminals.From);
			terminals[11].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[11].Token.Value.ShouldBe("SYS");
			terminals[12].Id.ShouldBe(OracleGrammarDescription.Terminals.Dot);
			terminals[13].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[13].Token.Value.ShouldBe("DUAL");
		}

		[Test(Description = @"Tests complex case expression with literals. ")]
		public void Test4()
		{
			const string sqlText = @"SELECT CASE WHEN 1 = 2 THEN 'True1' WHEN 1 = 0 THEN 'True2' ELSE 'False' END - (1 + 2) XXX FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			var terminals = result.Single().NodeCollection.SelectMany(i => i.Terminals).ToList();

			terminals.Count.ShouldBe(26);
			terminals[0].Id.ShouldBe(OracleGrammarDescription.Terminals.Select);
			terminals[1].Id.ShouldBe(OracleGrammarDescription.Terminals.Case);
			terminals[2].Id.ShouldBe(OracleGrammarDescription.Terminals.When);
			terminals[3].Id.ShouldBe(OracleGrammarDescription.Terminals.NumberLiteral);
			terminals[3].Token.Value.ShouldBe("1");

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests complex mathematic expressions. ")]
		public void Test5()
		{
			const string sqlText = @"SELECT CASE (1 * (0 + 0)) WHEN (2 + 0) THEN DUAL.DUMMY || 'xxx' ELSE 'a' || ('b' || 'c') END FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			var terminals = result.Single().NodeCollection.SelectMany(i => i.Terminals).ToList();

			terminals.Count.ShouldBe(34);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void Test6()
		{
			const string query1 = @"SELECT 1 1 FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().NodeCollection.Count.ShouldBe(0);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 AS FROM T1";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void Test7()
		{
			var sqlText = @"SELECT 1FF F FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().NodeCollection.Count.ShouldBe(0);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			sqlText = @"SELECT . FROM DUAL";
			result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().NodeCollection.Count.ShouldBe(0);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void Test8()
		{
			const string sqlText = @"SELECT 1 FROM T1;SELECT 2 FROM T2";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(2);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void Test9()
		{
			const string sqlText = @"SELECT 1 FROM DUAL,";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple query with common table expression. ")]
		public void Test10()
		{
			const string sqlText = @"WITH X(A, B, C) AS (SELECT 1 FROM D) SELECT * FROM D;";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple query with IN and BETWEEN clauses. ")]
		public void Test11()
		{
			const string sqlText = @"SELECT 1 FROM DUAL WHERE 1 IN (1, 2, 3) AND 4 NOT IN (5, 6, 7) AND (8 + 0) BETWEEN (0 * 0) AND (9 - 0);";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries connected by set operations. ")]
		public void Test12()
		{
			const string sqlText = @"SELECT 1 FROM DUAL UNION ALL SELECT 1 FROM DUAL UNION SELECT 1 FROM DUAL MINUS SELECT 1 FROM DUAL INTERSECT SELECT 1 FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with group by and having clauses. ")]
		public void Test13()
		{
			const string query1 = @"SELECT 1 FROM DUAL GROUP BY 1, DUMMY HAVING 1 = 1";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL HAVING 1 = 1";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with for update clause. ")]
		public void Test14()
		{
			const string query1 = @"SELECT 1 FROM DUAL FOR UPDATE SKIP LOCKED";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE NOWAIT";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query3 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE WAIT 10E-0";
			result = _oracleSqlParser.Parse(CreateTokenReader(query3));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query4 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE WAIT -1";
			result = _oracleSqlParser.Parse(CreateTokenReader(query4));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions

			const string query5 = @"SELECT 1 FROM DUAL ALIAS FOR UPDATE OF SCHEMA.OBJECT.COLUMN1, COLUMN2, OBJECT.COLUMN3 WAIT 1";
			result = _oracleSqlParser.Parse(CreateTokenReader(query5));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with IS (NOT) <terminal> conditions. ")]
		public void Test15()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE NULL IS NULL AND 1 IS NOT NULL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 'A' NAN, 'B' AS INFINITE FROM DUAL WHERE 1 IS NOT NAN AND 1 IS NOT INFINITE";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests empty token set. ")]
		public void Test16()
		{
			const string query1 = @"--SELECT 1 FROM DUAL WHERE NULL IS NULL AND 1 IS NOT NULL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
			result.Single().NodeCollection.Count.ShouldBe(0);
		}

		[Test(Description = @"Tests correct recovery after invalid statement. ")]
		public void Test17()
		{
			const string query1 = @"/*invalid statement */ SELECT 1 FROM DUAL+;/* valid statement */ SELECT 1 FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(2);
			result.First().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);
			result.First().NodeCollection.Count.ShouldBe(1);
			result.First().NodeCollection.Single().Terminals.Count().ShouldBe(4);

			result.Last().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
			result.Last().NodeCollection.Count.ShouldBe(1);
			result.Last().NodeCollection.Single().Terminals.Count().ShouldBe(4);
		}

		[Test(Description = @"Tests EXISTS and NOT EXISTS. ")]
		public void Test18()
		{
			const string query1 = @"SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM DUAL) THEN 'FALSE' ELSE 'TRUE' END FROM DUAL WHERE EXISTS (SELECT 1 FROM DUAL) AND EXISTS (SELECT 2 FROM DUAL)";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests flashback clauses. ")]
		public void Test19()
		{
			const string query1 = @"SELECT 1 FROM T1 VERSIONS BETWEEN SCN MINVALUE AND MAXVALUE AS OF SCN 123";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			const string query2 = @"SELECT * FROM T1 VERSIONS BETWEEN TIMESTAMP TIMESTAMP'2014-02-20 00:00:00' AND DATE'2014-02-20'";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests chained asterisk clause. ")]
		public void Test20()
		{
			const string query1 = @"SELECT T1.*, 1, T1.*, 2, T1.*, 3 FROM T1";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests LIKE clause. ")]
		public void Test21()
		{
			const string query1 = @"SELECT CASE WHEN 'abc' LIKE 'a%' OR '123' LIKEC '1%' OR '456' LIKE2 '4%' OR '789' LIKE4 '%7%' ESCAPE '\' THEN 'true' END FROM DUAL WHERE 'def' LIKE 'd%' ESCAPE '\'";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests IN clause with subquery. ")]
		public void Test22()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE 1 NOT IN (WITH XXX AS (SELECT 1 FROM DUAL) SELECT 1 FROM DUAL DD) AND EXISTS (WITH XXX AS (SELECT 1 FROM DUAL) SELECT 1 FROM DUAL DD)";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests scalar subquery clause. ")]
		public void Test23()
		{
			const string query1 = @"SELECT 1 C1, (SELECT (SELECT 2 FROM DUAL) FROM DUAL) C2, (SELECT 3 FROM DUAL) C3 FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests join clauses. ")]
		public void Test24()
		{
			//const string query1 = @"SELECT * FROM T1 CROSS JOIN T2 ALIAS";
			const string query1 = @"SELECT * FROM T1 CROSS JOIN T2 JOIN T3 ON 1 = 1 INNER JOIN T4 USING (ID) NATURAL JOIN T5 FULL OUTER JOIN T6 ALIAS ON T5.ID = T6.ID, V$SESSION";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			var terminals = result.Single().NodeCollection.SelectMany(n => n.Terminals).ToArray();
			terminals.Length.ShouldBe(38);
			terminals[3].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[3].ParentNode.Id.ShouldBe(OracleGrammarDescription.NonTerminals.QueryTableExpression);
			terminals[3].Token.Value.ShouldBe("T1");
			terminals[4].Id.ShouldBe(OracleGrammarDescription.Terminals.Cross);
			terminals[5].Id.ShouldBe(OracleGrammarDescription.Terminals.Join);
			terminals[7].Id.ShouldBe(OracleGrammarDescription.Terminals.Join);
			terminals[9].Id.ShouldBe(OracleGrammarDescription.Terminals.On);
			terminals[13].Id.ShouldBe(OracleGrammarDescription.Terminals.Inner);
			terminals[14].Id.ShouldBe(OracleGrammarDescription.Terminals.Join);
			terminals[16].Id.ShouldBe(OracleGrammarDescription.Terminals.Using);
			terminals[20].Id.ShouldBe(OracleGrammarDescription.Terminals.Natural);
			terminals[21].Id.ShouldBe(OracleGrammarDescription.Terminals.Join);
			terminals[23].Id.ShouldBe(OracleGrammarDescription.Terminals.Full);
			terminals[24].Id.ShouldBe(OracleGrammarDescription.Terminals.Outer);
			terminals[25].Id.ShouldBe(OracleGrammarDescription.Terminals.Join);
			terminals[26].Id.ShouldBe(OracleGrammarDescription.Terminals.Identifier);
			terminals[27].Id.ShouldBe(OracleGrammarDescription.Terminals.Alias);
		}

		[Test(Description = @"Tests order by clause. ")]
		public void Test25()
		{
			const string query1 = @"SELECT 1 FROM DUAL ORDER BY 1 DESC, DUMMY ASC NULLS LAST, (SELECT DBMS_RANDOM.VALUE FROM DUAL)";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests hierarchical query clauses. ")]
		public void Test26()
		{
			const string query1 = @"SELECT LEVEL FROM DUAL CONNECT BY NOCYCLE LEVEL <= 5 AND 1 = 1 START WITH 1 = 1";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			const string query2 = @"SELECT LEVEL FROM DUAL START WITH 1 = 1 CONNECT BY NOCYCLE LEVEL <= 5 AND 1 = 1";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
		}

		[Test(Description = @"Tests group by rollup, cube and grouping sets clauses. ")]
		public void Test27()
		{
			const string query1 = @"SELECT 1 FROM DUAL GROUP BY 1, 2, (3 || DUMMY), ROLLUP(1, (1, 2 || DUMMY)), CUBE(2, (3, 4 + 2))";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL GROUP BY GROUPING SETS (ROLLUP(DUMMY), 1, DUMMY, (1, DUMMY), ROLLUP(DUMMY), CUBE(DUMMY)), GROUPING SETS (1), CUBE(1), ROLLUP(1, DUMMY);";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests ANY and ALL operators with expression list and subquery. ")]
		public void Test28()
		{
			const string query1 = @"SELECT 1 FROM DUAL WHERE DUMMY = ANY(1, 2, 3) OR DUMMY = ALL(SELECT * FROM DUAL)";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"SELECT 1 FROM DUAL WHERE (1, 2, 3) = ALL(SELECT 1, 2, 3 FROM DUAL)";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests sample clause. ")]
		public void Test29()
		{
			const string query1 = @"SELECT * FROM COUNTRY SAMPLE BLOCK (0.1) SEED (1)";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests nested subquery. ")]
		public void Test30()
		{
			const string query1 = @"SELECT * FROM (SELECT LEVEL ORDERED_COLUMN FROM DUAL CONNECT BY LEVEL <= 5 ORDER BY ORDERED_COLUMN DESC) WHERE ROWNUM <= 3";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests incorrectly nested factored subquery. ")]
		public void Test31()
		{
			const string query1 = @"WITH TEST AS (WITH T2 AS (SELECT 1 FROM DUAL) SELECT * FROM T2) SELECT * FROM TEST";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query with multiple tables in from clause; also tests '$' character within standard identifier. ")]
		public void Test32()
		{
			const string query1 = @"SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		private static OracleTokenReader CreateTokenReader(string sqlText)
		{
			Trace.WriteLine("SQL text: " + sqlText);

			return OracleTokenReader.Create(new StringReader(sqlText));
		}
    }
}
