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
		public void TestProgramNodeValidityWithinNestedStatement()
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

		[Test(Description = @"")]
		public void TestExceptionIdentifierValidities()
		{
			const string plsqlText =
@"DECLARE
    test_exception EXCEPTION;
BEGIN
    RAISE test_exception;
    RAISE undefined_exception;
    EXCEPTION
    	WHEN test_exception OR undefined_exception THEN NULL;
    	WHEN OTHERS THEN NULL;
END;";
			var statement = Parser.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = OracleStatementValidatorTest.BuildValidationModel(plsqlText, statement);
			var nodeValidities = validationModel.IdentifierNodeValidity.Values.ToArray();
			nodeValidities.Length.ShouldBe(2);
			nodeValidities[0].IsRecognized.ShouldBe(false);
			nodeValidities[0].Node.Token.Value.ShouldBe("undefined_exception");
			nodeValidities[1].IsRecognized.ShouldBe(false);
			nodeValidities[1].Node.Token.Value.ShouldBe("undefined_exception");
		}

		[Test(Description = @"")]
		public void TestOthersExceptionCombinedWithNamedException()
		{
			const string plsqlText =
@"DECLARE
    test_exception EXCEPTION;
BEGIN
    NULL;
    EXCEPTION
    	WHEN test_exception OR OTHERS THEN NULL;
END;";
			var statement = Parser.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = OracleStatementValidatorTest.BuildValidationModel(plsqlText, statement);
			validationModel.IdentifierNodeValidity.Count.ShouldBe(0);
			var nodeValidities = validationModel.InvalidNonTerminals.Values.ToArray();
			nodeValidities.Length.ShouldBe(1);
			nodeValidities[0].Node.Token.Value.ShouldBe("OTHERS");
			nodeValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.NoChoicesMayAppearWithChoiceOthersInExceptionHandler);
		}
	}
}
