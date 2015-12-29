using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OraclePlSqlStatementValidatorTest
	{
		private static readonly OracleSqlParser Parser = OracleSqlParser.Instance;

		[Test(Description = @"")]
		public void TestTableNodeValidityWithQuotedNotations()
		{
			const string plsqlText =
@"BEGIN
	FOR i IN 1..2 LOOP
		dbms_output.put_line(a => 'x');
	END LOOP;
END;";

			var statement = Parser.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = OracleStatementValidatorTest.BuildValidationModel(plsqlText, statement);
			validationModel.ProgramNodeValidity.Values.Count(v => !v.IsRecognized).ShouldBe(0);
		}
	}
}