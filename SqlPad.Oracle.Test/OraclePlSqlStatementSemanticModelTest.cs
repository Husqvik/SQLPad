using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DataDictionary;
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
	TYPE test_type1 IS RECORD (attribute1 NUMBER, attribute2 VARCHAR2(255));
	TYPE test_table_type1 IS TABLE OF test_type;
	
	test_variable1 NUMBER;
	test_variable2 VARCHAR2(100);
	test_constant1 CONSTANT VARCHAR2(30) := 'Constant 1';

	PROCEDURE TEST_INNER_PROCEDURE(p1 test_type)
	IS
	BEGIN
		NULL;
	END;

	FUNCTION TEST_INNER_FUNCTION(p1 test_table_type) RETURN NUMBER
	IS
		test_exception1 EXCEPTION;

		PROCEDURE TEST_NESTED_PROCEDURE(p1 VARCHAR2)
		IS
		BEGIN
			SELECT dummy INTO x FROM DUAL;
		END;
	BEGIN
		SELECT dummy INTO x FROM DUAL;
	END;
BEGIN
	SELECT COUNT(*) INTO test_variable1 FROM DUAL;
	SELECT NULL, 'String value' INTO test_variable1, test_variable2 FROM DUAL;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var expectedObjectIdentifier = OracleObjectIdentifier.Create("HUSQVIK", "TEST_FUNCTION");

			var semanticModel = OraclePlSqlStatementSemanticModel.Build(plsqlText, statement, TestFixture.DatabaseModel);
			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ObjectIdentifier.ShouldBe(expectedObjectIdentifier);
			mainProgram.Name.ShouldBe("\"TEST_FUNCTION\"");
			mainProgram.Parameters.Count.ShouldBe(3);
			mainProgram.Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.Parameters[1].Name.ShouldBe("\"P2\"");
			mainProgram.Parameters[1].Direction.ShouldBe(ParameterDirection.InputOutput);
			mainProgram.Parameters[2].Name.ShouldBe("\"P3\"");
			mainProgram.Parameters[2].Direction.ShouldBe(ParameterDirection.Output);
			mainProgram.ReturnParameter.ShouldNotBe(null);
			mainProgram.Variables.Count.ShouldBe(3);
			mainProgram.Variables[0].Name.ShouldBe("\"TEST_VARIABLE1\"");
			mainProgram.Variables[0].IsConstant.ShouldBe(false);
			mainProgram.Variables[0].IsException.ShouldBe(false);
			mainProgram.Variables[1].Name.ShouldBe("\"TEST_VARIABLE2\"");
			mainProgram.Variables[1].IsConstant.ShouldBe(false);
			mainProgram.Variables[1].IsException.ShouldBe(false);
			mainProgram.Variables[2].Name.ShouldBe("\"TEST_CONSTANT1\"");
			mainProgram.Variables[2].IsConstant.ShouldBe(true);
			mainProgram.Variables[2].IsException.ShouldBe(false);
			mainProgram.Types.Count.ShouldBe(2);
			mainProgram.Types[0].Name.ShouldBe("\"TEST_TYPE1\"");
			mainProgram.Types[1].Name.ShouldBe("\"TEST_TABLE_TYPE1\"");
			mainProgram.SubPrograms.Count.ShouldBe(2);
			mainProgram.ChildModels.Count.ShouldBe(2);

			mainProgram.SubPrograms[0].ObjectIdentifier.ShouldBe(expectedObjectIdentifier);
			mainProgram.SubPrograms[0].Name.ShouldBe("\"TEST_INNER_PROCEDURE\"");
			mainProgram.SubPrograms[0].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[0].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[0].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[0].ReturnParameter.ShouldBe(null);
			mainProgram.SubPrograms[0].Variables.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].SubPrograms.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].ChildModels.Count.ShouldBe(0);

			mainProgram.SubPrograms[1].ObjectIdentifier.ShouldBe(expectedObjectIdentifier);
			mainProgram.SubPrograms[1].Name.ShouldBe("\"TEST_INNER_FUNCTION\"");
			mainProgram.SubPrograms[1].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[1].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[1].ReturnParameter.ShouldNotBe(null);
			mainProgram.SubPrograms[1].Variables.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].Variables[0].Name.ShouldBe("\"TEST_EXCEPTION1\"");
			mainProgram.SubPrograms[1].Variables[0].IsConstant.ShouldBe(false);
			mainProgram.SubPrograms[1].Variables[0].IsException.ShouldBe(true);
			mainProgram.SubPrograms[1].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].ChildModels.Count.ShouldBe(1);

			mainProgram.SubPrograms[1].SubPrograms[0].ObjectIdentifier.ShouldBe(expectedObjectIdentifier);
			mainProgram.SubPrograms[1].SubPrograms[0].Name.ShouldBe("\"TEST_NESTED_PROCEDURE\"");
			mainProgram.SubPrograms[1].SubPrograms[0].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].SubPrograms[0].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[1].SubPrograms[0].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[1].SubPrograms[0].ReturnParameter.ShouldBe(null);
			mainProgram.SubPrograms[1].SubPrograms[0].Variables.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].SubPrograms.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].ChildModels.Count.ShouldBe(1);
		}

		[Test]
		public void TestInitializationWithAnonymousBlock()
		{
			const string plsqlText =
@"DECLARE
	TYPE test_type1 IS RECORD (attribute1 NUMBER, attribute2 VARCHAR2(255));
	TYPE test_table_type1 IS TABLE OF test_type;
	
	test_variable1 NUMBER;
	test_variable2 VARCHAR2(100);
	test_constant1 CONSTANT VARCHAR2(30) := 'Constant 1';

	PROCEDURE TEST_INNER_PROCEDURE(p1 test_type)
	IS
	BEGIN
		NULL;
	END;

	FUNCTION TEST_INNER_FUNCTION(p1 test_table_type) RETURN NUMBER
	IS
		test_exception1 EXCEPTION;

		PROCEDURE TEST_NESTED_PROCEDURE(p1 VARCHAR2)
		IS
		BEGIN
			SELECT dummy INTO x FROM DUAL;
		END;
	BEGIN
		SELECT dummy INTO x FROM DUAL;
	END;
BEGIN
	SELECT COUNT(*) INTO test_variable1 FROM DUAL;
	SELECT NULL, 'String value' INTO test_variable1, test_variable2 FROM DUAL;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OraclePlSqlStatementSemanticModel.Build(plsqlText, statement, TestFixture.DatabaseModel);
			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ObjectIdentifier.ShouldBe(OracleObjectIdentifier.Empty);
			mainProgram.RootNode.ShouldBe(statement.RootNode);
			mainProgram.Variables.Count.ShouldBe(3);
			mainProgram.Variables[0].Name.ShouldBe("\"TEST_VARIABLE1\"");
			mainProgram.Variables[0].IsConstant.ShouldBe(false);
			mainProgram.Variables[0].IsException.ShouldBe(false);
			mainProgram.Variables[1].Name.ShouldBe("\"TEST_VARIABLE2\"");
			mainProgram.Variables[1].IsConstant.ShouldBe(false);
			mainProgram.Variables[1].IsException.ShouldBe(false);
			mainProgram.Variables[2].Name.ShouldBe("\"TEST_CONSTANT1\"");
			mainProgram.Variables[2].IsConstant.ShouldBe(true);
			mainProgram.Variables[2].IsException.ShouldBe(false);
			mainProgram.Types.Count.ShouldBe(2);
			mainProgram.Types[0].Name.ShouldBe("\"TEST_TYPE1\"");
			mainProgram.Types[1].Name.ShouldBe("\"TEST_TABLE_TYPE1\"");
			mainProgram.SubPrograms.Count.ShouldBe(2);
			mainProgram.ChildModels.Count.ShouldBe(2);

			mainProgram.SubPrograms[0].Name.ShouldBe("\"TEST_INNER_PROCEDURE\"");
			mainProgram.SubPrograms[0].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[0].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[0].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[0].ReturnParameter.ShouldBe(null);
			mainProgram.SubPrograms[0].Variables.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].SubPrograms.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].ChildModels.Count.ShouldBe(0);

			mainProgram.SubPrograms[1].Name.ShouldBe("\"TEST_INNER_FUNCTION\"");
			mainProgram.SubPrograms[1].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[1].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[1].ReturnParameter.ShouldNotBe(null);
			mainProgram.SubPrograms[1].Variables.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].Variables[0].Name.ShouldBe("\"TEST_EXCEPTION1\"");
			mainProgram.SubPrograms[1].Variables[0].IsConstant.ShouldBe(false);
			mainProgram.SubPrograms[1].Variables[0].IsException.ShouldBe(true);
			mainProgram.SubPrograms[1].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].ChildModels.Count.ShouldBe(1);

			mainProgram.SubPrograms[1].SubPrograms[0].Name.ShouldBe("\"TEST_NESTED_PROCEDURE\"");
			mainProgram.SubPrograms[1].SubPrograms[0].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].SubPrograms[0].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[1].SubPrograms[0].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[1].SubPrograms[0].ReturnParameter.ShouldBe(null);
			mainProgram.SubPrograms[1].SubPrograms[0].Variables.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].SubPrograms.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].ChildModels.Count.ShouldBe(1);
		}
	}
}
