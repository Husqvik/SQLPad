using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
    public class OracleTokenReaderTest
    {
		[Test(Description = @"Tests '(' and '""' characters as tokens separators. ")]
		public void Test1()
		{
			var tokens = GetTokenValuesFromOracleSql("select('x')as\"x\"from(dual\"d\")");
			tokens.ShouldBe(new [] { "select", "(", "'x'", ")", "as", "\"x\"", "from", "(", "dual", "\"d\"", ")" });
		}

		[Test(Description = "Tests quoted string (q'?<string>?' where ? is character used for marking start and end of the string) literal. ")]
		public void Test2()
		{
			var tokens = GetTokenValuesFromOracleSql("select q'|x|'as\"x\"from(dual\"d\")");
			tokens.ShouldBe(new [] { "select", "q'|x|'", "as", "\"x\"", "from", "(", "dual", "\"d\"", ")" });
		}

		[Test(Description = "Tests numeric literal and quoted identifiers for column name and table name. ")]
		public void Test3()
		{
			var tokens = GetTokenValuesFromOracleSql("select(1)\"x\"from(dual\"d\")");
			tokens.ShouldBe(new [] { "select", "(", "1", ")", "\"x\"", "from", "(", "dual", "\"d\"", ")" });
		}

		[Test(Description = "Tests string literal and quoted identifiers for column name (without as keyword) and table name. ")]
		public void Test4()
		{
			var tokens = GetTokenValuesFromOracleSql("select('x')\"x\"from(dual\"d\")");
			tokens.ShouldBe(new [] { "select", "(", "'x'", ")", "\"x\"", "from", "(", "dual", "\"d\"", ")" });
		}

		[Test(Description = "Tests comment character starting combinations ('--', '/*') in string literal and quoted identifiers. ")]
		public void Test5()
		{
			var tokens = GetTokenValuesFromOracleSql("select '--' \"/*x\"from(dual)");
			tokens.ShouldBe(new [] { "select", "'--'", "\"/*x\"", "from", "(", "dual", ")" });
		}

		[Test(Description = "Tests comment character combinations in string literal and quoted identifiers. ")]
		public void Test6()
		{
			var tokens = GetTokenValuesFromOracleSql("select '/*--' \"--x*/\"from(dual)");
			tokens.ShouldBe(new [] { "select", "'/*--'", "\"--x*/\"", "from", "(", "dual", ")" });
		}

		[Test(Description = "Tests starting and trailing white space. ")]
		public void Test7()
		{
			var tokens = GetTokenValuesFromOracleSql("   \r\n  \t\t  \r\n   select 'x' \"x\"from dual     \r\n    \t\t     \t     ");
			tokens.ShouldBe(new [] { "select", "'x'", "\"x\"", "from", "dual" });
		}

		[Test(Description = "Tests block comments. ")]
		public void Test8()
		{
			var tokens = GetTokenValuesFromOracleSql("select/**/1/**/from/**/dual");
			tokens.ShouldBe(new [] { "select", "1", "from", "dual" });
		}

		[Test(Description = "Tests line comments. ")]
		public void Test9()
		{
			var tokens = GetTokenValuesFromOracleSql("select--\r\n1--\r\nfrom--\r\ndual");
			tokens.ShouldBe(new [] { "select", "1", "from", "dual" });
		}

		[Test(Description = "Tests line breaks in string literal. ")]
		public void Test10()
		{
			var tokens = GetTokenValuesFromOracleSql("select 'some\r\n--\r\n/*\r\n*/\r\nthing'\r\nfrom dual");
			tokens.ShouldBe(new [] { "select", "'some\r\n--\r\n/*\r\n*/\r\nthing'", "from", "dual" });
		}

		[Test(Description = "Tests multiple statements separated by ';'. ")]
		public void Test11()
		{
			var tokens = GetTokenValuesFromOracleSql("select null c1 from dual       \t;\r\n\r\nselect null c2 from dual;   \t\r\n    select null c3 from dual");
			tokens.ShouldBe(new[] { "select", "null", "c1", "from", "dual", ";", "select", "null", "c2", "from", "dual", ";", "select", "null", "c3", "from", "dual" });
		}

		[Test(Description = "Tests distinct and asterisk symbols. ")]
		public void Test12()
		{
			var tokens = GetTokenValuesFromOracleSql("select d.*from dual d");
			tokens.ShouldBe(new[] { "select", "d", ".", "*", "from", "dual", "d" });
		}

		[Test(Description = "Tests distinct and asterisk symbols. ")]
		public void Test13()
		{
			var tokens = GetTokenValuesFromOracleSql("select count(d.dummy),''from dual d");
			tokens.ShouldBe(new[] { "select", "count", "(", "d", ".", "dummy", ")", ",", "''", "from", "dual", "d" });
		}

		[Test(Description = "Tests apostrophe in string literal and quoted identifier. ")]
		public void Test14()
		{
			var tokens = GetTokenValuesFromOracleSql("select '''' \"''apostrophe''\"from dual");
			tokens.ShouldBe(new[] { "select", "''''", "\"''apostrophe''\"", "from", "dual" });
		}

		[Test(Description = "Tests apostrophes in string literal in between of other keywords without spaces. ")]
		public void Test15()
		{
			var tokens = GetTokenValuesFromOracleSql("select''''from dual");
			tokens.ShouldBe(new[] { "select", "''''", "from", "dual" });
		}

		[Test(Description = "Tests special cases of literals. ")]
		public void Test16()
		{
			var tokens = GetTokenValuesFromOracleSql("select n'*', N'*', Nq'|*|', q'|*|' from dual");
			tokens.ShouldBe(new[] { "select", "n'*'", ",", "N'*'", ",", "Nq'|*|'", ",", "q'|*|'", "from", "dual" });
		}

		[Test(Description = "Tests invalid special cases of literals. ")]
		public void Test17()
		{
			var tokens = GetTokenValuesFromOracleSql("select x'*', qn'|*|' from dual");
			tokens.ShouldBe(new[] { "select", "x", "'*'", ",", "qn", "'|*|'", "from", "dual" });
		}

		[Test(Description = "Tests token index positions within the input. ")]
		public void Test18()
		{
			var tokenIndexes = GetTokenIndexesFromOracleSql("select x'*', qn'|*|' from dual");
			tokenIndexes.ShouldBe(new[] { 0, 7, 8, 11, 13, 15, 21, 26 });
		}

		[Test(Description = "Tests token index positions within an input with the last remaining single character token. ")]
		public void Test19()
		{
			var tokenIndexes = GetTokenIndexesFromOracleSql("select d.*from dual d");
			tokenIndexes.ShouldBe(new[] { 0, 7, 8, 9, 10, 15, 20 });
		}

		[Test(Description = "Tests token index positions within an input with string literal and quoted identifier. ")]
		public void Test20()
		{
			var tokenIndexes = GetTokenIndexesFromOracleSql(" \t select '/*--' \"--x*/\"from(dual)");
			tokenIndexes.ShouldBe(new[] { 3, 10, 17, 24, 28, 29, 33 });
		}

		[Test(Description = "Tests OracleTokenReader constructor with null reader. ")]
		public void TestNullReader()
		{
			Assert.Throws<ArgumentNullException>(() => OracleTokenReader.Create((TextReader)null));
		}

		[Test(Description = "Tests OracleTokenReader disposing and string input. ")]
		public void TestUsingClauseWithStringParameter()
		{
			using(OracleTokenReader.Create(String.Empty))
			{ }
		}

		[Test(Description = "Tests longer comments with comment leading characters. ")]
		public void Test21()
		{
			var tokens = GetTokenValuesFromOracleSql(" \r\n \t select*/*this*is/a--block/comment*/\r\n--this-is/*a-line*commment\r\nfrom(dual)");
			tokens.ShouldBe(new[] { "select", "*", "from", "(", "dual", ")" });
		}

		[Test(Description = "Tests token index positions within an input with longer comments with comment leading characters. ")]
		public void Test22()
		{
			var tokenIndexes = GetTokenIndexesFromOracleSql(" \r\n \t select*/*this*is/a--block/comment*/\r\n--this-is/*a-line*commment\r\nfrom(dual)");
			tokenIndexes.ShouldBe(new[] { 6, 12, 71, 75, 76, 80 });
		}

		[Test(Description = "Tests unfinished comments. ")]
		public void Test23()
		{
			var tokens = GetTokenValuesFromOracleSql("select*from dual/*this is a comment");
			tokens.ShouldBe(new[] { "select", "*", "from", "dual" });

			tokens = GetTokenValuesFromOracleSql("--this is a comment\r\nselect*from dual");
			tokens.ShouldBe(new[] { "select", "*", "from", "dual" });
		}

		[Test(Description = "Tests simle CASE and WHERE clauses. ")]
		public void Test24()
		{
			const string testQuery = "select case when 1=1then 999else-999end from dual where 1>=1or 1<=1";
			var tokens = GetTokenValuesFromOracleSql(testQuery);
			tokens.ShouldBe(new[] { "select", "case", "when", "1", "=", "1", "then", "999", "else", "-", "999", "end", "from", "dual", "where", "1", ">=", "1", "or", "1", "<=", "1" });

			var tokenIndexes = GetTokenIndexesFromOracleSql(testQuery);
			tokenIndexes.ShouldBe(new[] { 0, 7, 12, 17, 18, 19, 20, 25, 28, 32, 33, 36, 40, 45, 50, 56, 57, 59, 60, 63, 64, 66 });
		}

		[Test(Description = "Tests special number literals. ")]
		public void Test25()
		{
			const string testQuery1 = "select 1DD, 1FF, 1., .1, 1.dd, .1ff, 999.888.777 from dual";
			var tokens = GetTokenValuesFromOracleSql(testQuery1);
			tokens.ShouldBe(new[] { "select", "1D", "D", ",", "1F", "F", ",", "1.", ",", ".1", ",", "1.d", "d", ",", ".1f", "f", ",", "999.888", ".", "777", "from", "dual" });

			var tokenIndexes = GetTokenIndexesFromOracleSql(testQuery1);
			tokenIndexes.ShouldBe(new[] { 0, 7, 9, 10, 12, 14, 15, 17, 19, 21, 23, 25, 28, 29, 31, 34, 35, 37, 44, 45, 49, 54 });

			// From here the numbers are invalid according to Toad.
			tokens = GetTokenValuesFromOracleSql("select.1ffrom dual");
			tokens.ShouldBe(new[] { "select", ".1f", "from", "dual" });

			const string testQuery2 = "select.1e+1f,.1e-10dfrom dual";
			tokens = GetTokenValuesFromOracleSql(testQuery2);
			tokens.ShouldBe(new[] { "select", ".1e+1f", ",", ".1e-10d", "from", "dual" });

			tokenIndexes = GetTokenIndexesFromOracleSql(testQuery2);
			tokenIndexes.ShouldBe(new[] { 0, 6, 12, 13, 20, 25 });

			tokens = GetTokenValuesFromOracleSql("select.1e+1xfrom dual");
			tokens.ShouldBe(new[] { "select", ".1e+1", "xfrom", "dual" });
		}

		[Test(Description = "Tests bind variable placeholders. ")]
		public void Test26()
		{
			const string testQuery1 = "select:1,:2,:\"3\"from/*:fake*/dual--fake";
			var tokens = GetTokenValuesFromOracleSql(testQuery1);
			tokens.ShouldBe(new[] { "select", ":1", ",", ":2", ",", ":\"3\"", "from", "dual" });
			
			var tokenIndexes = GetTokenIndexesFromOracleSql(testQuery1);
			tokenIndexes.ShouldBe(new[] { 0, 6, 8, 9, 11, 12, 16, 29 });

			const string testQuery2 = "select:1,:ABC,:2from dual";
			tokens = GetTokenValuesFromOracleSql(testQuery2);
			tokens.ShouldBe(new[] { "select", ":1", ",", ":ABC", ",", ":2from", "dual" });

			tokenIndexes = GetTokenIndexesFromOracleSql(testQuery2);
			tokenIndexes.ShouldBe(new[] { 0, 6, 8, 9, 13, 14, 21 });
		}

		[Test(Description = "Tests IN clause and not equals operators. ")]
		public void Test27()
		{
			const string testQuery = "select 1 from dual where 1 in (1, 2, 3) and 4 not in (5, 6, 7) and 8 != 9";
			var tokens = GetTokenValuesFromOracleSql(testQuery);
			tokens.ShouldBe(new[] { "select", "1", "from", "dual", "where", "1", "in", "(", "1", ",", "2", ",", "3", ")", "and", "4", "not", "in", "(", "5", ",", "6", ",", "7", ")", "and", "8", "!=", "9" });

			var tokenIndexes = GetTokenIndexesFromOracleSql(testQuery);
			tokenIndexes.ShouldBe(new[] { 0, 7, 9, 14, 19, 25, 27, 30, 31, 32, 34, 35, 37, 38, 40, 44, 46, 50, 53, 54, 55, 57, 58, 60, 61, 63, 67, 69, 72 });
		}

		[Test(Description = "Tests string literal as the last token. ")]
		public void Test28()
		{
			const string testQuery = "SELECT 1 FROM DUAL WHERE 'def' LIKE 'd'";
			var tokens = GetTokenValuesFromOracleSql(testQuery);
			tokens.ShouldBe(new[] { "SELECT", "1", "FROM", "DUAL", "WHERE", "'def'", "LIKE", "'d'" });

			var tokenIndexes = GetTokenIndexesFromOracleSql(testQuery);
			tokenIndexes.ShouldBe(new[] { 0, 7, 9, 14, 19, 25, 31, 36 });
		}

		[Test(Description = "Tests ANSI DATE and TIMESTAMP literals. ")]
		public void Test29()
		{
			const string testQuery = "SELECT DATE'2014-20-02', DATE		'2014-20-02', TIMESTAMP '2014-20-02 12:34:56.789' FROM DUAL";
			var tokens = GetTokenValuesFromOracleSql(testQuery);
			tokens.ShouldBe(new[] { "SELECT", "DATE", "'2014-20-02'", ",", "DATE", "'2014-20-02'", ",", "TIMESTAMP", "'2014-20-02 12:34:56.789'", "FROM", "DUAL" });

			var tokenIndexes = GetTokenIndexesFromOracleSql(testQuery);
			tokenIndexes.ShouldBe(new[] { 0, 7, 11, 23, 25, 31, 43, 45, 55, 81, 86 });
		}

		[Test(Description = "Tests quoted schema name. ")]
		public void Test30()
		{
			const string testQuery = @"SELECT NULL FROM ""SYS"".DUAL";
			var tokens = GetTokenValuesFromOracleSql(testQuery);
			tokens.ShouldBe(new[] { "SELECT", "NULL", "FROM", "\"SYS\"", ".", "DUAL" });

			var tokenIndexes = GetTokenIndexesFromOracleSql(testQuery);
			tokenIndexes.ShouldBe(new[] { 0, 7, 12, 17, 22, 23 });
		}

		private string[] GetTokenValuesFromOracleSql(string sqlText)
		{
			return GetTokensFromOracleSql(sqlText).Select(t => t.Value).ToArray();
		}

		private int[] GetTokenIndexesFromOracleSql(string sqlText)
		{
			return GetTokensFromOracleSql(sqlText).Select(t => t.Index).ToArray();
		}

		private static IEnumerable<OracleToken> GetTokensFromOracleSql(string sqlText)
		{
			Trace.WriteLine("SQL text: " + sqlText);

			using (var reader = new StringReader(sqlText))
			{
				var tokenReader = OracleTokenReader.Create(reader);
				return tokenReader.GetTokens().Cast<OracleToken>().ToArray();
			}
		}
    }
}
