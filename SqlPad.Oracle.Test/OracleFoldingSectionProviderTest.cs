using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleFoldingSectionProviderTest
    {
		private readonly OracleFoldingSectionProvider _provider = new OracleFoldingSectionProvider();

		[Test]
		public void TestSelectFoldingSections()
		{
			const string statement =
@"SELECT
	*
FROM
	(SELECT XMLTYPE('<value>value 1</value>') XML_DATA1 FROM DUAL)
	CROSS JOIN
		XMLTABLE('/root' PASSING '<root>' || XML_DATA1 || XML_DATA2 || '</root>')
	CROSS JOIN
		(SELECT XMLTYPE('<value>value 2</value>') XML_DATA2 FROM DUAL);";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(3);
			foldingSections[0].FoldingStart.ShouldBe(0);
			foldingSections[0].FoldingEnd.ShouldBe(251);
			foldingSections[0].IsNested.ShouldBeFalse();
			foldingSections[0].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderSubquery);

			foldingSections[1].FoldingStart.ShouldBe(20);
			foldingSections[1].FoldingEnd.ShouldBe(80);
			foldingSections[1].IsNested.ShouldBeTrue();
			foldingSections[1].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderSubquery);

			foldingSections[2].FoldingStart.ShouldBe(189);
			foldingSections[2].FoldingEnd.ShouldBe(249);
			foldingSections[2].IsNested.ShouldBeTrue();
			foldingSections[2].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderSubquery);
		}

		[Test]
		public void TestPlSqlBodyFoldingSections()
		{
			const string statement =
@"CREATE OR REPLACE PACKAGE BODY TEST_PACKAGE IS
	PROCEDURE procedure1 IS
	BEGIN
		BEGIN
			NULL;
		END;

		field1 := 123;
	END;
	
	FUNCTION function1 RETURN NUMBER IS
	BEGIN
		BEGIN
			NULL;
		END;

		RETURN 0;
		field2 := 123;
	END;

BEGIN
	DBMS_OUTPUT.PUT_LINE('Package state has been initialized. ');
END;";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(5);
			foldingSections[0].FoldingStart.ShouldBe(75);
			foldingSections[0].FoldingEnd.ShouldBe(134);
			foldingSections[0].IsNested.ShouldBeFalse();
			foldingSections[0].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections[1].FoldingStart.ShouldBe(84);
			foldingSections[1].FoldingEnd.ShouldBe(107);
			foldingSections[1].IsNested.ShouldBeTrue();
			foldingSections[1].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections[2].FoldingStart.ShouldBe(178);
			foldingSections[2].FoldingEnd.ShouldBe(250);
			foldingSections[2].IsNested.ShouldBeFalse();
			foldingSections[2].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections[3].FoldingStart.ShouldBe(187);
			foldingSections[3].FoldingEnd.ShouldBe(210);
			foldingSections[3].IsNested.ShouldBeTrue();
			foldingSections[3].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections[4].FoldingStart.ShouldBe(254);
			foldingSections[4].FoldingEnd.ShouldBe(329);
			foldingSections[4].IsNested.ShouldBeFalse();
			foldingSections[4].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);
		}

		[Test]
		public void TestPlSqlExceptionFoldingSections()
		{
			const string statement =
@"BEGIN
	DBMS_OUTPUT.PUT_LINE('Body output. ');
	
	EXCEPTION
		WHEN NO_DATA_FOUND
			THEN DBMS_OUTPUT.PUT_LINE('Specific exception output. ');
		WHEN OTHERS
			THEN DBMS_OUTPUT.PUT_LINE('Generic exception output. ');
END;";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(2);
			foldingSections[0].FoldingStart.ShouldBe(0);
			foldingSections[0].FoldingEnd.ShouldBe(227);
			foldingSections[0].IsNested.ShouldBeFalse();
			foldingSections[0].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections[1].FoldingStart.ShouldBe(52);
			foldingSections[1].FoldingEnd.ShouldBe(221);
			foldingSections[1].IsNested.ShouldBeTrue();
			foldingSections[1].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderException);
		}

		[Test]
		public void TestPlSqlBodyFoldingSectionWithLabeledEnding()
		{
			const string statement =
@"BEGIN
	BEGIN
		BEGIN
			NULL;
		END L1;
	END L2;
END L3;";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(3);
			foldingSections[0].FoldingStart.ShouldBe(0);
			foldingSections[0].FoldingEnd.ShouldBe(62);
			foldingSections[0].IsNested.ShouldBeFalse();
			foldingSections[0].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections[1].FoldingStart.ShouldBe(8);
			foldingSections[1].FoldingEnd.ShouldBe(53);
			foldingSections[1].IsNested.ShouldBeTrue();
			foldingSections[1].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections[2].FoldingStart.ShouldBe(17);
			foldingSections[2].FoldingEnd.ShouldBe(43);
			foldingSections[2].IsNested.ShouldBeTrue();
			foldingSections[2].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);
		}

		[Test]
		public void TestPlSqlBodyFoldingSectionWithStatementsWithSameEndingAsProgramUnit()
		{
			const string statement =
@"BEGIN
	IF TRUE
		THEN NULL;
	END IF;
	
	BEGIN
		LOOP
			EXIT;
		END LOOP;
		
		CASE 1
			WHEN 1 THEN NULL;
		END CASE;
	END;
END;";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(2);
			foldingSections[0].FoldingStart.ShouldBe(0);
			foldingSections[0].FoldingEnd.ShouldBe(143);
			foldingSections[0].IsNested.ShouldBeFalse();
			foldingSections[0].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);

			foldingSections.Length.ShouldBe(2);
			foldingSections[1].FoldingStart.ShouldBe(45);
			foldingSections[1].FoldingEnd.ShouldBe(137);
			foldingSections[1].IsNested.ShouldBeTrue();
			foldingSections[1].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);
		}

		[Test]
		public void TestInvalidPlSqlBodyFoldingSection()
		{
			const string statement =
@"DECLARE
BEGIN
END:";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(1);
			foldingSections[0].FoldingStart.ShouldBe(9);
			foldingSections[0].FoldingEnd.ShouldBe(19);
			foldingSections[0].IsNested.ShouldBeFalse();
			foldingSections[0].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);
		}

		[Test]
		public void TestCompoundTriggerFoldingSection()
		{
			const string statement =
@"CREATE OR REPLACE TRIGGER check_raise_on_avg
FOR UPDATE OF sal ON emp
COMPOUND TRIGGER

  BEFORE EACH ROW IS
  BEGIN
    NULL;
  END BEFORE EACH ROW;

  AFTER EACH ROW IS
  BEGIN
    NULL;
  END AFTER EACH ROW;
END;";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(2);
			foldingSections[0].FoldingStart.ShouldBe(116);
			foldingSections[0].FoldingEnd.ShouldBe(139);
			foldingSections[0].IsNested.ShouldBeFalse();
			foldingSections[0].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);
			foldingSections[1].FoldingStart.ShouldBe(183);
			foldingSections[1].FoldingEnd.ShouldBe(206);
			foldingSections[1].IsNested.ShouldBeFalse();
			foldingSections[1].Placeholder.ShouldBe(OracleFoldingSectionProvider.FoldingSectionPlaceholderPlSqlBlock);
		}
	}
}