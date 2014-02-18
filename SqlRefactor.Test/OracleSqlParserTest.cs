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
			terminals[3].Id.ShouldBe("Identifier");
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

			result.Count.ShouldBe(0);

			// TODO: Precise assertions
		}

		[Test(Description = @"Tests query. ")]
		public void Test7()
		{
			var sqlText = @"SELECT 1FF F FROM DUAL";
			var result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(0);

			sqlText = @"SELECT . FROM DUAL";
			result = _oracleSqlParser.Parse(CreateTokenReader(sqlText));

			result.Count.ShouldBe(0);

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

		private OracleTokenReader CreateTokenReader(string sqlText)
		{
			Trace.WriteLine("SQL text: " + sqlText);

			return OracleTokenReader.Create(new StringReader(sqlText));
		}
    }
}

