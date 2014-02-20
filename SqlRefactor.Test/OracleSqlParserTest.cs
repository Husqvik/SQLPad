using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlRefactor.Test
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
		public void Test2()
		{
			const string sqlText = @"SELECT NULL FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));
			
			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var terminals = result.Single().TokenCollection.SelectMany(i => i.Terminals).ToList();

			terminals.Count.ShouldBe(4);
			terminals[0].Id.ShouldBe("Select");
			terminals[1].Id.ShouldBe("Null");
			terminals[2].Id.ShouldBe("From");
			terminals[3].Id.ShouldBe("Identifier");
			terminals[3].Value.Value.ShouldBe("DUAL");
		}

		[Test(Description = @"Tests query with fully qualified names and aliases. ")]
		public void Test3()
		{
			const string sqlText = @"SELECT NULL AS "">=;+Alias/*--^"", SYS.DUAL.DUMMY FROM SYS.DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.ShouldNotBe(null);
			result.Count.ShouldBe(1);
			var terminals = result.Single().TokenCollection.SelectMany(i => i.Terminals).ToList();

			terminals.Count.ShouldBe(14);
			terminals[0].Id.ShouldBe("Select");
			terminals[1].Id.ShouldBe("Null");
			terminals[2].Id.ShouldBe("As");
			terminals[3].Id.ShouldBe("Alias");
			terminals[3].Value.Value.ShouldBe("\">=;+Alias/*--^\"");
			terminals[4].Id.ShouldBe("Comma");
			terminals[5].Id.ShouldBe("Identifier");
			terminals[5].Value.Value.ShouldBe("SYS");
			terminals[6].Id.ShouldBe("Dot");
			terminals[7].Id.ShouldBe("Identifier");
			terminals[7].Value.Value.ShouldBe("DUAL");
			terminals[8].Id.ShouldBe("Dot");
			terminals[9].Id.ShouldBe("Identifier");
			terminals[9].Value.Value.ShouldBe("DUMMY");
			terminals[10].Id.ShouldBe("From");
			terminals[11].Id.ShouldBe("Identifier");
			terminals[11].Value.Value.ShouldBe("SYS");
			terminals[12].Id.ShouldBe("Dot");
			terminals[13].Id.ShouldBe("Identifier");
			terminals[13].Value.Value.ShouldBe("DUAL");
		}

		[Test(Description = @"Tests complex case expression with literals. ")]
		public void Test4()
		{
			const string sqlText = @"SELECT CASE WHEN 1 = 2 THEN 'True1' WHEN 1 = 0 THEN 'True2' ELSE 'False' END - (1 + 2) XXX FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			var terminals = result.Single().TokenCollection.SelectMany(i => i.Terminals).ToList();

			terminals.Count.ShouldBe(26);
			terminals[0].Id.ShouldBe("Select");
			terminals[1].Id.ShouldBe("Case");
			terminals[2].Id.ShouldBe("When");
			terminals[3].Id.ShouldBe("NumberLiteral");
			terminals[3].Value.Value.ShouldBe("1");

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests complex mathematic expressions. ")]
		public void Test5()
		{
			const string sqlText = @"SELECT CASE (1 * (0 + 0)) WHEN (2 + 0) THEN DUAL.DUMMY || 'xxx' ELSE 'a' || ('b' || 'c') END FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			var terminals = result.Single().TokenCollection.SelectMany(i => i.Terminals).ToList();

			terminals.Count.ShouldBe(34);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void Test6()
		{
			const string sqlText = @"SELECT 1 1 FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().TokenCollection.Count.ShouldBe(0);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void Test7()
		{
			var sqlText = @"SELECT 1FF F FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().TokenCollection.Count.ShouldBe(0);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			sqlText = @"SELECT . FROM DUAL";
			result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().TokenCollection.Count.ShouldBe(0);
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
			const string sqlText = @"with x as (select 1 from d) SELECT * FROM D;";
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
			const string sqlText = @"select 1 from dual union all select 1 from dual union select 1 from dual minus select 1 from dual intersect select 1 from dual";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with group by and having clauses. ")]
		public void Test13()
		{
			const string query1 = @"select 1 from dual group by 1 having 1 = 1";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"select 1 from dual having 1 = 1";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with for update clause. ")]
		public void Test14()
		{
			const string query1 = @"select 1 from dual for update skip locked";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"select 1 from dual alias for update nowait";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query3 = @"select 1 from dual alias for update wait 10e-0";
			result = _oracleSqlParser.Parse(CreateTokenReader(query3));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query4 = @"select 1 from dual alias for update wait -1";
			result = _oracleSqlParser.Parse(CreateTokenReader(query4));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests simple queries with IS (NOT) <terminal> conditions. ")]
		public void Test15()
		{
			const string query1 = @"select 1 from dual where null is null and 1 is not null";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions

			const string query2 = @"select 'a' NaN, 'b' AS INFINITE from dual where 1 is not NaN and 1 is not infinite";
			result = _oracleSqlParser.Parse(CreateTokenReader(query2));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests empty token set. ")]
		public void Test16()
		{
			const string query1 = @"--select 1 from dual where null is null and 1 is not null";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(1);
			result.Single().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
			result.Single().TokenCollection.Count.ShouldBe(0);
		}

		[Test(Description = @"Tests correct recovery after invalid statement. ")]
		public void Test17()
		{
			const string query1 = @"/*invalid statement */ select 1 from dual+;/* valid statement */ select 1 from dual";
			var result = _oracleSqlParser.Parse(CreateTokenReader(query1));

			result.Count.ShouldBe(2);
			result.First().ProcessingResult.ShouldBe(NonTerminalProcessingResult.SequenceNotFound);
			result.First().TokenCollection.Count.ShouldBe(1);
			result.First().TokenCollection.Single().TerminalCount.ShouldBe(4);

			result.Last().ProcessingResult.ShouldBe(NonTerminalProcessingResult.Success);
			result.Last().TokenCollection.Count.ShouldBe(1);
			result.Last().TokenCollection.Single().TerminalCount.ShouldBe(4);
		}

		private OracleTokenReader CreateTokenReader(string sqlText)
		{
			Trace.WriteLine("SQL text: " + sqlText);

			return OracleTokenReader.Create(new StringReader(sqlText));
		}
    }
}

