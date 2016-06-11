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
			[Test]
			public void TestQuotedToSimpleIdentifierStartingWithNonLetter()
			{
				const string quotedIdentifier = "\"_IDENTIFIER\"";
				var simpleIdentifier = quotedIdentifier.ToSimpleIdentifier();
				simpleIdentifier.ShouldBe(quotedIdentifier);
			}

			[Test]
			public void TestQuotedToSimpleIdentifierContainingDash()
			{
				const string quotedIdentifier = "\"DASH-COLUMN\"";
				var simpleIdentifier = quotedIdentifier.ToSimpleIdentifier();
				simpleIdentifier.ShouldBe(quotedIdentifier);
			}

			[Test]
			public void TestQuotedToSimpleIdentifierOfSingleLetterIdentifier()
			{
				const string quotedIdentifier = "\"x\"";
				var simpleIdentifier = quotedIdentifier.ToSimpleIdentifier();
				simpleIdentifier.ShouldBe(quotedIdentifier);
			}

			[Test]
			public void TestNormalStringPlainText()
			{
				const string literal = "'some''text'";
				var text = literal.ToPlainString();
				text.ShouldBe("some'text");
			}

			[Test]
			public void TestQuotedStringPlainText()
			{
				const string literal = "q'|some''text|'";
				var text = literal.ToPlainString();
				text.ShouldBe("some''text");
			}

			[Test]
			public void TestInvalidQuotedStringPlainText()
			{
				const string literal = "q'|some''text'";
				var text = literal.ToPlainString();
				text.ShouldBe("some''text");
			}
		}

		[TestFixture]
		public class OracleStatementTest
		{
			private static readonly OracleSqlParser Parser = OracleSqlParser.Instance;

			[Test]
			public void TestTryGetPlSqlUnitNameFromCreateProcedure()
			{
				var statement = Parser.Parse("CREATE PROCEDURE TEST_SCHEMA.TEST_PROCEDURE")[0];

				OracleObjectIdentifier identifier;
				OracleStatement.TryGetPlSqlUnitName(statement, out identifier).ShouldBe(true);
				identifier.Owner.ShouldBe("TEST_SCHEMA");
				identifier.Name.ShouldBe("TEST_PROCEDURE");
			}

			[Test]
			public void TestTryGetPlSqlUnitNameFromCreateFunction()
			{
				var statement = Parser.Parse("CREATE FUNCTION TEST_SCHEMA.TEST_FUNCTION")[0];

				OracleObjectIdentifier identifier;
				OracleStatement.TryGetPlSqlUnitName(statement, out identifier).ShouldBe(true);
				identifier.Owner.ShouldBe("TEST_SCHEMA");
				identifier.Name.ShouldBe("TEST_FUNCTION");
			}

			[Test]
			public void TestTryGetPlSqlUnitNameFromCreateTable()
			{
				var statement = Parser.Parse("CREATE TABLE TEST_SCHEMA.TEST_TABLE")[0];

				OracleObjectIdentifier identifier;
				OracleStatement.TryGetPlSqlUnitName(statement, out identifier).ShouldBe(false);
			}

			[Test]
			public void TestFeedbackMessage()
			{
				var statement = (OracleStatement)Parser.Parse("CREATE PROCEDURE TEST_SCHEMA.TEST_PROCEDURE")[0];
				var message = statement.BuildExecutionFeedbackMessage(null, false);

				message.ShouldBe("Procedure created. ");
			}

			[Test]
			public void TestCompilationErrorFeedbackMessage()
			{
				var statement = (OracleStatement)Parser.Parse("CREATE FUNCTION TEST_SCHEMA.TEST_FUNCTION")[0];
				var message = statement.BuildExecutionFeedbackMessage(null, true);

				message.ShouldBe("Function created with compilation errors. ");
			}
		}
	}
}