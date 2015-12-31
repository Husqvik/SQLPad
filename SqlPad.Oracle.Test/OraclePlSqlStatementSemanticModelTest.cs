using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OraclePlSqlStatementSemanticModelTest
	{
		[Test]
		public void TestInitializationNullStatement()
		{
			Assert.Throws<ArgumentNullException>(() => new OraclePlSqlStatementSemanticModel(null, null, TestFixture.DatabaseModel));
		}

		[Test]
		public void TestInitializationWithNonPlSqlStatement()
		{
			const string sqlText = @"SELECT * FROM DUAL";
			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(sqlText).Single();

			Assert.Throws<ArgumentException>(() => new OraclePlSqlStatementSemanticModel(sqlText, statement, TestFixture.DatabaseModel));
		}

		private const string TestPlSqlProgramBase =
@"
	TYPE test_type1 IS RECORD (attribute1 NUMBER, attribute2 VARCHAR2(255));
	TYPE test_table_type1 IS TABLE OF test_type;
	
	test_variable1 NUMBER;
	test_variable2 VARCHAR2(100);
	test_constant1 CONSTANT VARCHAR2(30) NOT NULL := 'Constant 1';

	PROCEDURE TEST_INNER_PROCEDURE(p1 test_type)
	IS
		test_variable3 VARCHAR2(100) NOT NULL DEFAULT 'value3';
	BEGIN
		test_variable3 := 'modified value3';
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
		RAISE test_exception1;
		EXCEPTION WHEN test_exception1 THEN NULL;
	END;
BEGIN
	dbms_output.put_line(item => test_constant1);
	SELECT COUNT(*) INTO test_variable1 FROM DUAL;
	SELECT NULL, 'String value' INTO test_variable1, test_variable2 FROM DUAL;
END;";

		[Test]
		public void TestBasicInitialization()
		{
			var plsqlText = $"CREATE OR REPLACE FUNCTION TEST_FUNCTION(p1 IN NUMBER DEFAULT 0, p2 IN OUT VARCHAR2, p3 OUT NOCOPY CLOB) RETURN RAW IS {TestPlSqlProgramBase}";
			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var expectedObjectIdentifier = OracleObjectIdentifier.Create("HUSQVIK", "TEST_FUNCTION");

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);
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

			AssertMainProgram(mainProgram, expectedObjectIdentifier);
		}

		private static void AssertMainProgram(OraclePlSqlProgram mainProgram, OracleObjectIdentifier expectedObjectIdentifier)
		{
			mainProgram.Variables.Count.ShouldBe(3);
			mainProgram.Variables[0].Name.ShouldBe("\"TEST_VARIABLE1\"");
			mainProgram.Variables[0].IsConstant.ShouldBe(false);
			mainProgram.Variables[0].Nullable.ShouldBe(true);
			mainProgram.Variables[0].DefaultExpression.ShouldBe(null);
			mainProgram.Variables[0].DataTypeNode.ShouldNotBe(null);
			mainProgram.Variables[0].DataTypeNode.FirstTerminalNode.Id.ShouldBe(Terminals.Number);
			mainProgram.Variables[0].DataTypeNode.LastTerminalNode.Id.ShouldBe(Terminals.Number);
			mainProgram.Variables[1].Name.ShouldBe("\"TEST_VARIABLE2\"");
			mainProgram.Variables[1].IsConstant.ShouldBe(false);
			mainProgram.Variables[1].Nullable.ShouldBe(true);
			mainProgram.Variables[1].DefaultExpression.ShouldBe(null);
			mainProgram.Variables[1].DataTypeNode.ShouldNotBe(null);
			mainProgram.Variables[1].DataTypeNode.FirstTerminalNode.Id.ShouldBe(Terminals.Varchar2);
			mainProgram.Variables[1].DataTypeNode.LastTerminalNode.Id.ShouldBe(Terminals.RightParenthesis);
			mainProgram.Variables[2].Name.ShouldBe("\"TEST_CONSTANT1\"");
			mainProgram.Variables[2].IsConstant.ShouldBe(true);
			mainProgram.Variables[2].Nullable.ShouldBe(false);
			mainProgram.Variables[2].DefaultExpression.ShouldNotBe(null);
			mainProgram.Exceptions.Count.ShouldBe(0);
			mainProgram.PlSqlVariableReferences.Count.ShouldBe(1);
			mainProgram.PlSqlExceptionReferences.Count.ShouldBe(0);
			mainProgram.Types.Count.ShouldBe(2);
			mainProgram.Types[0].Name.ShouldBe("\"TEST_TYPE1\"");
			mainProgram.Types[1].Name.ShouldBe("\"TEST_TABLE_TYPE1\"");
			mainProgram.SubPrograms.Count.ShouldBe(2);
			mainProgram.ChildModels.Count.ShouldBe(2);

			mainProgram.ProgramReferences.Count.ShouldBe(1);
			var programReference = mainProgram.ProgramReferences.First();
			programReference.Name.ShouldBe("put_line");
			programReference.ObjectNode.Token.Value.ShouldBe("dbms_output");
			programReference.ParameterListNode.ShouldNotBe(null);
			programReference.ParameterReferences.Count.ShouldBe(1);
			programReference.ParameterReferences[0].OptionalIdentifierTerminal.Token.Value.ShouldBe("item");
			programReference.ParameterReferences[0].ParameterNode.LastTerminalNode.Token.Value.ShouldBe("test_constant1");

			mainProgram.SubPrograms[0].ObjectIdentifier.ShouldBe(expectedObjectIdentifier);
			mainProgram.SubPrograms[0].Name.ShouldBe("\"TEST_INNER_PROCEDURE\"");
			mainProgram.SubPrograms[0].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[0].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[0].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[0].ReturnParameter.ShouldBe(null);
			mainProgram.SubPrograms[0].Variables.Count.ShouldBe(1);
			mainProgram.SubPrograms[0].Variables[0].Name.ShouldBe("\"TEST_VARIABLE3\"");
			mainProgram.SubPrograms[0].Variables[0].IsConstant.ShouldBe(false);
			mainProgram.SubPrograms[0].Variables[0].Nullable.ShouldBe(false);
			mainProgram.SubPrograms[0].Variables[0].DefaultExpression.ShouldNotBe(null);
			mainProgram.SubPrograms[0].Exceptions.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].PlSqlVariableReferences.Count.ShouldBe(1);
			mainProgram.SubPrograms[0].PlSqlExceptionReferences.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].SubPrograms.Count.ShouldBe(0);
			mainProgram.SubPrograms[0].ChildModels.Count.ShouldBe(0);

			mainProgram.SubPrograms[1].ObjectIdentifier.ShouldBe(expectedObjectIdentifier);
			mainProgram.SubPrograms[1].Name.ShouldBe("\"TEST_INNER_FUNCTION\"");
			mainProgram.SubPrograms[1].Parameters.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].Parameters[0].Name.ShouldBe("\"P1\"");
			mainProgram.SubPrograms[1].Parameters[0].Direction.ShouldBe(ParameterDirection.Input);
			mainProgram.SubPrograms[1].ReturnParameter.ShouldNotBe(null);
			mainProgram.SubPrograms[1].ReturnParameter.DataTypeNode.ShouldNotBe(null);
			mainProgram.SubPrograms[1].ReturnParameter.DefaultExpression.ShouldBe(null);
			mainProgram.SubPrograms[1].ReturnParameter.Nullable.ShouldBe(true);
			mainProgram.SubPrograms[1].ReturnParameter.Direction.ShouldBe(ParameterDirection.ReturnValue);
			mainProgram.SubPrograms[1].Variables.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].Exceptions.Count.ShouldBe(1);
			mainProgram.SubPrograms[1].Exceptions[0].Name.ShouldBe("\"TEST_EXCEPTION1\"");
			mainProgram.SubPrograms[1].PlSqlExceptionReferences.Count.ShouldBe(2);
			mainProgram.SubPrograms[1].PlSqlExceptionReferences.ForEach(r => r.Name.ShouldBe("test_exception1"));
			mainProgram.SubPrograms[1].PlSqlVariableReferences.Count.ShouldBe(0);
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
			mainProgram.SubPrograms[1].SubPrograms[0].Exceptions.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].PlSqlVariableReferences.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].PlSqlExceptionReferences.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].Types.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].SubPrograms.Count.ShouldBe(0);
			mainProgram.SubPrograms[1].SubPrograms[0].ChildModels.Count.ShouldBe(1);
		}

		[Test]
		public void TestInitializationWithAnonymousBlock()
		{
			var plsqlText = $"DECLARE {TestPlSqlProgramBase}";
			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);
			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ObjectIdentifier.ShouldBe(OracleObjectIdentifier.Empty);
			mainProgram.RootNode.ShouldBe(statement.RootNode[0]);

			mainProgram.Parameters.Count.ShouldBe(0);

			AssertMainProgram(mainProgram, OracleObjectIdentifier.Empty);
		}

		[Test]
		public void TestInitializationNestedAnonymousBlock()
		{
			const string plsqlText =
@"BEGIN
	DECLARE
		n NUMBER;
	BEGIN
		SELECT NULL INTO n FROM DUAL;
	END;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);
			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ObjectIdentifier.ShouldBe(OracleObjectIdentifier.Empty);
			mainProgram.Variables.Count.ShouldBe(0);
			mainProgram.SubPrograms.Count.ShouldBe(1);
			mainProgram.SubPrograms[0].ObjectIdentifier.ShouldBe(OracleObjectIdentifier.Empty);
			mainProgram.SubPrograms[0].RootNode.SourcePosition.IndexStart.ShouldBe(8);
			mainProgram.SubPrograms[0].RootNode.SourcePosition.Length.ShouldBe(68);
			mainProgram.SubPrograms[0].Variables.Count.ShouldBe(1);
			mainProgram.SubPrograms[0].Variables[0].Name.ShouldBe("\"N\"");
			mainProgram.SubPrograms[0].ChildModels.Count.ShouldBe(1);
		}

		[Test]
		public void TestSelectListRedundancy()
		{
			const string plsqlText =
@"DECLARE
	c1 NUMBER;
	c2 NUMBER;
BEGIN
	SELECT NULL, NULL INTO c1, c2 FROM DUAL;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);
			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);

			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ChildModels.Count.ShouldBe(1);
			mainProgram.ChildModels[0].RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test]
		public void TestLocalReferences()
		{
			const string plsqlText =
@"CREATE OR REPLACE PROCEDURE test_procedure(test_parameter1 IN VARCHAR2)
IS
    test_variable1 VARCHAR2(255);
BEGIN
    test_variable1 := nvl(test_variable1, 'x') || test_parameter1 || test_parameter2;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);

			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ColumnReferences.Count.ShouldBe(0);
			mainProgram.PlSqlVariableReferences.Count.ShouldBe(4);
			var localReferences = mainProgram.PlSqlVariableReferences.OrderBy(r => r.IdentifierNode.SourcePosition.IndexStart).ToArray();
			localReferences[0].Variables.Count.ShouldBe(1);
			localReferences[1].Variables.Count.ShouldBe(1);
			localReferences[2].Variables.Count.ShouldBe(1);
			localReferences[3].Variables.Count.ShouldBe(0);

			mainProgram.ProgramReferences.Count.ShouldBe(1);
			var programReference = mainProgram.ProgramReferences.First();
			programReference.Metadata.ShouldNotBe(null);
		}

		[Test]
		public void TestAnonymousPlSqlBlockLocalReferences()
		{
			const string plsqlText =
@"DECLARE
    test_variable1 VARCHAR2(255);
BEGIN
    test_variable1 := test_variable1 || test_parameter1;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);

			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ColumnReferences.Count.ShouldBe(0);
			mainProgram.PlSqlVariableReferences.Count.ShouldBe(3);
			var localReferences = mainProgram.PlSqlVariableReferences.OrderBy(r => r.IdentifierNode.SourcePosition.IndexStart).ToArray();
			localReferences[0].Variables.Count.ShouldBe(1);
			localReferences[1].Variables.Count.ShouldBe(1);
			localReferences[2].Variables.Count.ShouldBe(0);
		}

		[Test]
		public void TestCursorVariableQueryBlockRetrieval()
		{
			const string plsqlText =
@"DECLARE
    CURSOR test_cursor IS SELECT * FROM DUAL;
BEGIN
	FOR c IN (SELECT * FROM DUAL) LOOP
		NULL;
	END LOOP;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);
			semanticModel.QueryBlocks.Count.ShouldBe(2);
		}

		[Test]
		public void TestComplexStatementProgramReference()
		{
			const string plsqlText =
@"BEGIN
	FOR i IN 1..2 LOOP
		dbms_output.put_line(a => 'x');
	END LOOP;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);

			semanticModel.Programs.Count.ShouldBe(1);
			var mainProgram = semanticModel.Programs[0];
			mainProgram.ProgramReferences.Count.ShouldBe(1);
			var putLineReference = mainProgram.ProgramReferences.First();
			putLineReference.OwnerNode.ShouldBe(null);
			putLineReference.ObjectNode.Token.Value.ShouldBe("dbms_output");
			putLineReference.ProgramIdentifierNode.Token.Value.ShouldBe("put_line");
			putLineReference.Metadata.ShouldNotBe(null);
			putLineReference.ParameterListNode.ShouldNotBe(null);
			putLineReference.ParameterReferences.Count.ShouldBe(1);
		}

		[Test]
		public void TestPlSqlBlockDeclarationLabels()
		{
			const string plsqlText =
@"<<test_label1>>
<<test_label2>>
BEGIN
	<<test_label3>>
	<<test_label4>>
	BEGIN
		<<test_label5>>
		NULL;
	END;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);

			semanticModel.Programs.Count.ShouldBe(1);
			semanticModel.Programs[0].Labels.Count.ShouldBe(2);
			semanticModel.Programs[0].SubPrograms.Count.ShouldBe(1);
			semanticModel.Programs[0].SubPrograms[0].Labels.Count.ShouldBe(2);
		}

		[Test]
		public void TestScopeQualifiedPlSqlReferences()
		{
			const string plsqlText =
@"CREATE OR REPLACE PROCEDURE test_procedure
IS
    dummy VARCHAR2(2);
BEGIN
    SELECT dummy INTO dummy FROM dual WHERE dummy = test_procedure.dummy OR dummy = undefined_scope.dummy;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);

			semanticModel.Programs.Count.ShouldBe(1);
			var plSqlVariableReferences = semanticModel.Programs[0].PlSqlVariableReferences.ToArray();
			plSqlVariableReferences.Length.ShouldBe(2);
			plSqlVariableReferences[0].ObjectNode.Token.Value.ShouldBe("test_procedure");
			plSqlVariableReferences[0].Variables.Count.ShouldBe(1);
			plSqlVariableReferences[1].ObjectNode.Token.Value.ShouldBe("undefined_scope");
			plSqlVariableReferences[1].Variables.Count.ShouldBe(0);
		}

		[Test]
		public void TestScopeQualifiedPlSqlReferencesUsingLabel()
		{
			const string plsqlText =
@"<<test_plsql_block>>
DECLARE
    dummy VARCHAR2(2);
BEGIN
    SELECT dummy INTO dummy FROM dual WHERE dummy = test_plsql_block.dummy OR dummy = undefined_scope.dummy;
END;";

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);

			semanticModel.Programs.Count.ShouldBe(1);
			var plSqlVariableReferences = semanticModel.Programs[0].PlSqlVariableReferences.ToArray();
			plSqlVariableReferences.Length.ShouldBe(2);
			plSqlVariableReferences[0].ObjectNode.Token.Value.ShouldBe("test_plsql_block");
			plSqlVariableReferences[0].Variables.Count.ShouldBe(1);
			plSqlVariableReferences[1].ObjectNode.Token.Value.ShouldBe("undefined_scope");
			plSqlVariableReferences[1].Variables.Count.ShouldBe(0);
		}

		[Test]
		public void TestExceptionReferences()
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

			var statement = (OracleStatement)OracleSqlParser.Instance.Parse(plsqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OraclePlSqlStatementSemanticModel(plsqlText, statement, TestFixture.DatabaseModel).Build(CancellationToken.None);

			semanticModel.Programs.Count.ShouldBe(1);
			var plSqlExceptionReferences = semanticModel.Programs[0].PlSqlExceptionReferences.ToArray();
			plSqlExceptionReferences.Length.ShouldBe(4);
			plSqlExceptionReferences[0].IdentifierNode.Token.Value.ShouldBe("test_exception");
			plSqlExceptionReferences[0].Exceptions.Count.ShouldBe(1);
			plSqlExceptionReferences[1].IdentifierNode.Token.Value.ShouldBe("undefined_exception");
			plSqlExceptionReferences[1].Exceptions.Count.ShouldBe(0);
			plSqlExceptionReferences[2].IdentifierNode.Token.Value.ShouldBe("test_exception");
			plSqlExceptionReferences[2].Exceptions.Count.ShouldBe(1);
			plSqlExceptionReferences[3].IdentifierNode.Token.Value.ShouldBe("undefined_exception");
			plSqlExceptionReferences[3].Exceptions.Count.ShouldBe(0);
		}
	}
}
