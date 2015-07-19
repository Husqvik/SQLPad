using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.Test
{
	public class MiscellaneousTest
	{
		[TestFixture]
		public class OracleExtensionsTest
		{
			[Test(Description = @"")]
			public void TestQuotedToSimpleIdentifierStartingWithNonLetter()
			{
				const string quotedIdentifier = "\"_IDENTIFIER\"";
				var simpleIdentifier = quotedIdentifier.ToSimpleIdentifier();
				simpleIdentifier.ShouldBe(quotedIdentifier);
			}

			[Test(Description = @"")]
			public void TestQuotedToSimpleIdentifierContainingDash()
			{
				const string quotedIdentifier = "\"DASH-COLUMN\"";
				var simpleIdentifier = quotedIdentifier.ToSimpleIdentifier();
				simpleIdentifier.ShouldBe(quotedIdentifier);
			}

			[Test(Description = @"")]
			public void TestQuotedToSimpleIdentifierOfSingleLetterIdentifier()
			{
				const string quotedIdentifier = "\"x\"";
				var simpleIdentifier = quotedIdentifier.ToSimpleIdentifier();
				simpleIdentifier.ShouldBe(quotedIdentifier);
			}
		}

		[TestFixture]
		public class OracleStatementTest
		{
			private static readonly OracleSqlParser Parser = OracleSqlParser.Instance;

			[Test(Description = @"")]
			public void TestTryGetPlSqlUnitNameFromCreateProcedure()
			{
				var statement = Parser.Parse("CREATE PROCEDURE TEST_SCHEMA.TEST_PROCEDURE")[0];

				OracleObjectIdentifier identifier;
				OracleStatement.TryGetPlSqlUnitName(statement, out identifier).ShouldBe(true);
				identifier.Owner.ShouldBe("TEST_SCHEMA");
				identifier.Name.ShouldBe("TEST_PROCEDURE");
			}

			[Test(Description = @"")]
			public void TestTryGetPlSqlUnitNameFromCreateFunction()
			{
				var statement = Parser.Parse("CREATE FUNCTION TEST_SCHEMA.TEST_FUNCTION")[0];

				OracleObjectIdentifier identifier;
				OracleStatement.TryGetPlSqlUnitName(statement, out identifier).ShouldBe(true);
				identifier.Owner.ShouldBe("TEST_SCHEMA");
				identifier.Name.ShouldBe("TEST_FUNCTION");
			}

			[Test(Description = @"")]
			public void TestTryGetPlSqlUnitNameFromCreateTable()
			{
				var statement = Parser.Parse("CREATE TABLE TEST_SCHEMA.TEST_TABLE")[0];

				OracleObjectIdentifier identifier;
				OracleStatement.TryGetPlSqlUnitName(statement, out identifier).ShouldBe(false);
			}
		}
	}
}