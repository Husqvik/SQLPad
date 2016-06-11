using System;
using NUnit.Framework;
using Oracle.DataAccess.Types;
using Shouldly;
using SqlPad.Oracle.DatabaseConnection;
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

		[TestFixture]
		public class OracleValueAggregatorTest
		{
			[Test]
			public void TestNumberAggregation()
			{
				var aggregator = new OracleValueAggregator();
				aggregator.AddValue(1);
				aggregator.AddValue(2);

				aggregator.AggregatedValuesAvailable.ShouldBe(true);
				aggregator.LimitValuesAvailable.ShouldBe(true);
				aggregator.Average.ShouldBe(new OracleNumber(new OracleDecimal(1.5m)));
				aggregator.Sum.ShouldBe(new OracleNumber(new OracleDecimal(3m)));
				aggregator.Minimum.ShouldBe(new OracleNumber(new OracleDecimal(1m)));
				aggregator.Maximum.ShouldBe(new OracleNumber(new OracleDecimal(2m)));
				aggregator.Mode.ShouldBe(null);
				//aggregator.Median.ShouldBe(new OracleNumber(new OracleDecimal(1.5m)));
				aggregator.Count.ShouldBe(2);
				aggregator.DistinctCount.ShouldBe(2);
			}

			[Test]
			public void TestDateAggregation()
			{
				var aggregator = new OracleValueAggregator();
				aggregator.AddValue(new DateTime(2016, 6, 11));
				aggregator.AddValue(new DateTime(2016, 6, 12));

				aggregator.AggregatedValuesAvailable.ShouldBe(false);
				aggregator.LimitValuesAvailable.ShouldBe(true);
				aggregator.Average.ShouldBe(null);
				aggregator.Sum.ShouldBe(null);
				aggregator.Minimum.ShouldBe(new OracleDateTime(2016, 6, 11, 0, 0, 0));
				aggregator.Maximum.ShouldBe(new OracleDateTime(2016, 6, 12, 0, 0, 0));
				aggregator.Mode.ShouldBe(null);
				aggregator.Median.ShouldBe(null);
				aggregator.Count.ShouldBe(2);
				aggregator.DistinctCount.ShouldBe(2);
			}

			[Test]
			public void TestIntervalAggregation()
			{
				var aggregator = new OracleValueAggregator();
				var oneYear = new OracleIntervalYearToMonth(new OracleIntervalYM(1, 0));
				aggregator.AddValue(oneYear);
				aggregator.AddValue(oneYear);
				var twoYear = new OracleIntervalYearToMonth(new OracleIntervalYM(2, 0));
				aggregator.AddValue(twoYear);
				aggregator.AddValue(twoYear);

				aggregator.AggregatedValuesAvailable.ShouldBe(true);
				aggregator.LimitValuesAvailable.ShouldBe(true);
				aggregator.Average.ShouldBe(new OracleIntervalYearToMonth(new OracleIntervalYM(1, 6)));
				aggregator.Sum.ShouldBe(new OracleIntervalYearToMonth(new OracleIntervalYM(6, 0)));
				aggregator.Minimum.ShouldBe(oneYear);
				aggregator.Maximum.ShouldBe(twoYear);
				aggregator.Mode.ShouldBe(null);
				//aggregator.Median.ShouldBe(value);
				aggregator.Count.ShouldBe(4);
				aggregator.DistinctCount.ShouldBe(2);
			}

			[Test]
			public void TestMultipleTypes()
			{
				var aggregator = new OracleValueAggregator();
				aggregator.AddValue(1);
				aggregator.AddValue(1);
				aggregator.AddValue("string");
				aggregator.AddValue("string");
				aggregator.AddValue(null);

				aggregator.AggregatedValuesAvailable.ShouldBe(false);
				aggregator.LimitValuesAvailable.ShouldBe(false);
				aggregator.Average.ShouldBe(null);
				aggregator.Sum.ShouldBe(null);
				aggregator.Minimum.ShouldBe(null);
				aggregator.Maximum.ShouldBe(null);
				aggregator.Mode.ShouldBe(null);
				aggregator.Median.ShouldBe(null);
				aggregator.Count.ShouldBe(4);
				aggregator.DistinctCount.ShouldBe(null);
			}
		}
	}
}
