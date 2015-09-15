using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.SemanticModel;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OraclePlSqlStatementSemanticModelTest
	{
		[Test]
		public void TestInitializationNullStatement()
		{
			Assert.Throws<ArgumentNullException>(() => OraclePlSqlStatementSemanticModel.Build(null, null, TestFixture.DatabaseModel));
		}

		[Test]
		public void TestInitializationWithNonPlSqlStatement()
		{
			const string sqlText = @"SELECT * FROM DUAL";
			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(sqlText).Single();

			Assert.Throws<ArgumentException>(() => OraclePlSqlStatementSemanticModel.Build(sqlText, statement, TestFixture.DatabaseModel));
		}

		[Test]
		public void TestBasicInitialization()
		{
			const string plsqlText =
@"CREATE OR REPLACE FUNCTION TEST_FUNCTION(p1 IN NUMBER DEFAULT 0, p2 IN OUT VARCHAR2, p3 OUT NOCOPY CLOB) RETURN RAW
IS
	TYPE test_type IS RECORD (attribute1 NUMBER, attribute2 VARCHAR2(255));
	TYPE test_table_type IS TABLE OF test_type;
	
	test_variable1 NUMBER;
	test_variable2 VARCHAR2(100);
	test_constant1 CONSTANT VARCHAR2(30) := 'Constant 1';

	PROCEDURE TEST_INNER_PROCEDURE(p1 test_type)
	IS
	BEGIN
		NULL;
	END;
BEGIN
	SELECT COUNT(*) INTO test_variable1 FROM DUAL;
	SELECT NULL, 'String value' INTO test_variable1, test_variable2 FROM DUAL;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OraclePlSqlStatementSemanticModel.Build(plsqlText, statement, TestFixture.DatabaseModel);
		}
	}
}
