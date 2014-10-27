using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleStatementSemanticModelTest
	{
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();

		[Test(Description = @"")]
		public void TestInitializationWithNullStatement()
		{
			var exception = Assert.Throws<ArgumentNullException>(() => new OracleStatementSemanticModel(null, null, null));
			exception.ParamName.ShouldBe("statement");
		}

		[Test(Description = @"")]
		public void TestInitializationWithNullDatabaseModel()
		{
			var statement = (OracleStatement)_oracleSqlParser.Parse("SELECT NULL FROM DUAL").Single();
			var exception = Assert.Throws<ArgumentNullException>(() => new OracleStatementSemanticModel(null, statement, null));
			exception.ParamName.ShouldBe("databaseModel");
		}

		[Test(Description = @"")]
		public void TestInitializationSimpleModel()
		{
			var statement = (OracleStatement)_oracleSqlParser.Parse("SELECT NULL FROM DUAL").Single();
			var semanticModel = new OracleStatementSemanticModel(null, statement);
			semanticModel.IsSimpleModel.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestInitializationWithStatementWithoutRootNode()
		{
			Assert.DoesNotThrow(() => new OracleStatementSemanticModel(null, new OracleStatement(), TestFixture.DatabaseModel));
		}

		[Test(Description = @"")]
		public void TestQueryBlockCommonTableExpressionReferences()
		{
			const string query1 =
@"WITH
	CTE1 AS (SELECT '' NAME, '' DESCRIPTION, 1 ID FROM DUAL),
	CTE2 AS (SELECT '' OTHER_NAME, '' OTHER_DESCRIPTION, 1 ID FROM DUAL)
SELECT
	*
FROM
	CTE1 JOIN CTE2 ON CTE1.ID = CTE2.ID";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.IsSimpleModel.ShouldBe(false);
			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(3);

			var queryBlocks = semanticModel.QueryBlocks.ToArray();
			queryBlocks[0].Alias.ShouldBe("CTE2");
			queryBlocks[0].NormalizedAlias.ShouldBe("\"CTE2\"");
			queryBlocks[0].ObjectReferences.Count.ShouldBe(1);
			queryBlocks[0].Columns.Count.ShouldBe(3);
			queryBlocks[1].Alias.ShouldBe("CTE1");
			queryBlocks[1].NormalizedAlias.ShouldBe("\"CTE1\"");
			queryBlocks[1].ObjectReferences.Count.ShouldBe(1);
			queryBlocks[1].Columns.Count.ShouldBe(3);
			queryBlocks[2].Alias.ShouldBe(null);
			queryBlocks[2].ObjectReferences.Count.ShouldBe(2);
			queryBlocks[2].Columns.Count.ShouldBe(7);
			var mainQueryBlockColumns = queryBlocks[2].Columns.ToArray();
			mainQueryBlockColumns[0].IsAsterisk.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestMultiTableReferences()
		{
			const string query1 = @"SELECT SELECTION.* FROM SELECTION JOIN HUSQVIK.PROJECT P ON SELECTION.PROJECT_ID = P.PROJECT_ID";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var queryBlock = semanticModel.QueryBlocks.Single();
			queryBlock.Alias.ShouldBe(null);
			queryBlock.ObjectReferences.Count.ShouldBe(2);
			queryBlock.Columns.Count.ShouldBe(5);
		}

		[Test(Description = @"")]
		public void TestImplicitColumnReferences()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var queryBlock = semanticModel.QueryBlocks.Single();
			queryBlock.Alias.ShouldBe(null);
			queryBlock.ObjectReferences.Count.ShouldBe(2);
			queryBlock.Columns.Count.ShouldBe(5);
		}

		[Test(Description = @"")]
		public void TestAllImplicitColumnReferences()
		{
			const string query1 = @"SELECT * FROM PROJECT";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var queryBlock = semanticModel.QueryBlocks.Single();
			queryBlock.ObjectReferences.Count.ShouldBe(1);
			var objectReference = queryBlock.ObjectReferences.Single();
			queryBlock.Columns.Count.ShouldBe(3);
			var columns = queryBlock.Columns.ToArray();
			columns[0].ColumnReferences.Count.ShouldBe(1);
			columns[0].HasExplicitDefinition.ShouldBe(true);
			columns[0].IsAsterisk.ShouldBe(true);
			columns[1].ColumnReferences.Count.ShouldBe(1);
			columns[1].HasExplicitDefinition.ShouldBe(false);
			columns[1].ColumnReferences.Count.ShouldBe(1);
			columns[1].ColumnReferences[0].ColumnNodeObjectReferences.Count.ShouldBe(1);
			columns[1].ColumnReferences[0].ColumnNodeObjectReferences.Single().ShouldBe(objectReference);
		}

		[Test(Description = @"")]
		public void TestGrammarSpecificAggregateFunctionRecognize()
		{
			const string query1 = @"SELECT COUNT(*) OVER (), AVG(1) OVER (), LAST_VALUE(DUMMY IGNORE NULLS) OVER () FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var functionReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			functionReferences.Length.ShouldBe(3);
			var countFunction = functionReferences[0];
			countFunction.FunctionIdentifierNode.Id.ShouldBe(Terminals.Count);
			countFunction.AnalyticClauseNode.ShouldNotBe(null);
			countFunction.SelectListColumn.ShouldNotBe(null);

			var avgFunction = functionReferences[1];
			avgFunction.FunctionIdentifierNode.Id.ShouldBe(Terminals.Avg);
			avgFunction.AnalyticClauseNode.ShouldNotBe(null);
			avgFunction.SelectListColumn.ShouldNotBe(null);

			var lastValueFunction = functionReferences[2];
			lastValueFunction.FunctionIdentifierNode.Id.ShouldBe(Terminals.LastValue);
			lastValueFunction.AnalyticClauseNode.ShouldNotBe(null);
			lastValueFunction.SelectListColumn.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestGrammarSpecifiAnalyticFunctionRecognize()
		{
			const string query1 = @"SELECT LAG(DUMMY, 1, 'Replace') IGNORE NULLS OVER (ORDER BY NULL) FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var functionReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			functionReferences.Length.ShouldBe(1);
			var lagFunction = functionReferences[0];
			lagFunction.FunctionIdentifierNode.Id.ShouldBe(Terminals.Lag);
			lagFunction.AnalyticClauseNode.ShouldNotBe(null);
			lagFunction.SelectListColumn.ShouldNotBe(null);
			lagFunction.ParameterListNode.ShouldNotBe(null);
			lagFunction.ParameterNodes.Count.ShouldBe(3);
		}

		[Test(Description = @"")]
		public void TestBasicColumnTypes()
		{
			const string query1 = @"SELECT RESPONDENTBUCKET_ID, SELECTION_NAME, MY_NUMBER_COLUMN FROM (SELECT RESPONDENTBUCKET_ID, NAME SELECTION_NAME, 1 MY_NUMBER_COLUMN FROM SELECTION)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);
			var queryBlocks = semanticModel.QueryBlocks.ToArray();
			queryBlocks.Length.ShouldBe(2);

			var innerBlock = queryBlocks[0];
			innerBlock.ObjectReferences.Count.ShouldBe(1);
			var selectionTableReference = innerBlock.ObjectReferences.Single();
			selectionTableReference.Type.ShouldBe(ReferenceType.SchemaObject);
			innerBlock.Columns.Count.ShouldBe(3);
			var columns = innerBlock.Columns.ToArray();
			columns[0].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[0].HasExplicitDefinition.ShouldBe(true);
			columns[0].IsDirectReference.ShouldBe(true);
			columns[0].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].HasExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectReference.ShouldBe(true);
			columns[1].ColumnDescription.Type.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].HasExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectReference.ShouldBe(false);
			columns[2].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[2].ColumnDescription.FullTypeName.ShouldBe("NUMBER(1)");

			var outerBlock = queryBlocks[1];
			outerBlock.ObjectReferences.Count.ShouldBe(1);
			var innerTableReference = outerBlock.ObjectReferences.Single();
			innerTableReference.Type.ShouldBe(ReferenceType.InlineView);
			outerBlock.Columns.Count.ShouldBe(3);
			columns = outerBlock.Columns.ToArray();
			columns[0].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[0].HasExplicitDefinition.ShouldBe(true);
			columns[0].IsDirectReference.ShouldBe(true);
			columns[0].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].HasExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectReference.ShouldBe(true);
			columns[1].ColumnDescription.Type.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].HasExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectReference.ShouldBe(true);
			columns[2].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[2].ColumnDescription.FullTypeName.ShouldBe("NUMBER(1)");
		}

		[Test(Description = @"")]
		public void TestAsteriskExposedColumnTypes()
		{
			const string query1 = @"SELECT * FROM (SELECT * FROM SELECTION)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);
			var queryBlocks = semanticModel.QueryBlocks.ToArray();
			queryBlocks.Length.ShouldBe(2);

			var innerBlock = queryBlocks[0];
			innerBlock.ObjectReferences.Count.ShouldBe(1);
			var selectionTableReference = innerBlock.ObjectReferences.Single();
			selectionTableReference.Type.ShouldBe(ReferenceType.SchemaObject);
			innerBlock.Columns.Count.ShouldBe(5);
			var columns = innerBlock.Columns.ToArray();
			columns[0].IsAsterisk.ShouldBe(true);
			columns[0].HasExplicitDefinition.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[1].HasExplicitDefinition.ShouldBe(false);
			columns[1].ColumnDescription.Type.ShouldBe("NUMBER");

			var outerBlock = queryBlocks[1];
			outerBlock.ObjectReferences.Count.ShouldBe(1);
			var innerTableReference = outerBlock.ObjectReferences.Single();
			innerTableReference.Type.ShouldBe(ReferenceType.InlineView);
			outerBlock.Columns.Count.ShouldBe(5);
			columns = outerBlock.Columns.ToArray();
			columns[0].IsAsterisk.ShouldBe(true);
			columns[0].HasExplicitDefinition.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[1].HasExplicitDefinition.ShouldBe(false);
			columns[1].ColumnDescription.Type.ShouldBe("NUMBER");
		}

		[Test(Description = @"")]
		public void TestFunctionParameterListForComplexExpressionParameter()
		{
			const string query1 = @"SELECT MAX(CASE WHEN 'Option' IN ('Value1', 'Value2') THEN 1 END) FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var functionReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			functionReferences.Length.ShouldBe(1);
			var maxFunction = functionReferences[0];
			maxFunction.FunctionIdentifierNode.Id.ShouldBe(Terminals.Max);
			maxFunction.AnalyticClauseNode.ShouldBe(null);
			maxFunction.SelectListColumn.ShouldNotBe(null);
			maxFunction.ParameterListNode.ShouldNotBe(null);
			maxFunction.ParameterNodes.ShouldNotBe(null);
			maxFunction.ParameterNodes.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestColumnNotAmbiguousInOrderByClauseWhenSelectContainsAsterisk()
		{
			const string query1 = @"SELECT * FROM SELECTION ORDER BY NAME";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.ToArray();
			columnReferences.Length.ShouldBe(6);
			var orderByName = columnReferences[5];
			orderByName.Placement.ShouldBe(QueryBlockPlacement.OrderBy);
			orderByName.ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestColumnResolvedAmbiguousInOrderByClauseWhenSelectContainsAsteriskForRowSourcesWithSameColumnName()
		{
			const string query1 = @"SELECT SELECTION.*, SELECTION.* FROM SELECTION ORDER BY NAME";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.ToArray();
			columnReferences.Length.ShouldBe(11);
			var orderByName = columnReferences[10];
			orderByName.Placement.ShouldBe(QueryBlockPlacement.OrderBy);
			orderByName.ColumnNodeColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestFromClauseWithObjectOverDatabaseLink()
		{
			const string query1 = @"SELECT * FROM SELECTION@HQ_PDB_LOOPBACK";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var objectReferences = semanticModel.QueryBlocks.Single().ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			var selectionTable = objectReferences[0];
			selectionTable.DatabaseLinkNode.ShouldNotBe(null);
			selectionTable.DatabaseLink.ShouldNotBe(null);
			selectionTable.DatabaseLink.FullyQualifiedName.Name.ShouldBe("HQ_PDB_LOOPBACK");
		}

		[Test(Description = @"")]
		public void TestDatabaseLinkWithoutDomain()
		{
			const string query1 = @"SELECT * FROM SELECTION@TESTHOST";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var objectReferences = semanticModel.QueryBlocks.Single().ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			var selectionTable = objectReferences[0];
			selectionTable.DatabaseLinkNode.ShouldNotBe(null);
			selectionTable.DatabaseLink.ShouldNotBe(null);
			selectionTable.DatabaseLink.FullyQualifiedName.Name.ShouldBe("TESTHOST.SQLPAD.HUSQVIK.COM@HQINSTANCE");
		}

		[Test(Description = @"")]
		public void TestDatabaseLinkWithNullDatabaseDomainSystemParameter()
		{
			const string query1 = @"SELECT * FROM SELECTION@TESTHOST";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var databaseModel = new OracleTestDatabaseModel();
			databaseModel.SystemParameters[OracleDatabaseModelBase.SystemParameterNameDatabaseDomain] = null;

			var semanticModel = new OracleStatementSemanticModel(query1, statement, databaseModel);
			semanticModel.StatementText.ShouldBe(query1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithMissingAliasedColumnExpression()
		{
			const string query1 = @"SELECT SQL_CHILD_NUMBER, , PREV_CHILD_NUMBER FROM V$SESSION";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestSimpleInsertValuesStatementModelBuild()
		{
			const string query1 = @"INSERT INTO HUSQVIK.SELECTION(NAME) VALUES ('Dummy selection')";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.InsertTargets.Count.ShouldBe(1);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldBe(null);
			var insertTarget = semanticModel.InsertTargets.First();
			insertTarget.ObjectReferences.Count.ShouldBe(1);
			insertTarget.ObjectReferences.First().SchemaObject.ShouldNotBe(null);
			insertTarget.ColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestSimpleUpdateStatementModelBuild()
		{
			const string query1 = @"UPDATE SELECTION SET NAME = 'Dummy selection' WHERE SELECTION_ID = 0";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.SchemaObject.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestSimpleDeleteStatementModelBuild()
		{
			const string query1 = @"DELETE SELECTION WHERE SELECTION_ID = 0";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.SchemaObject.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestUpdateOfSubqueryModelBuild()
		{
			const string query1 = @"UPDATE (SELECT * FROM SELECTION) SET NAME = 'Dummy selection' WHERE SELECTION_ID = 0";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.SchemaObject.ShouldBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.Type.ShouldBe(ReferenceType.InlineView);
			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestUnfinishedInsertModelBuild()
		{
			const string query1 = @"INSERT INTO";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestInsertColumnListIdentiferMatchingFunctionName()
		{
			const string query1 = @"INSERT INTO SELECTION(SESSIONTIMEZONE) SELECT * FROM SELECTION";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldBe(null);
			semanticModel.InsertTargets.Count.ShouldBe(1);
			var insertTarget = semanticModel.InsertTargets.First();
			insertTarget.ObjectReferences.Count.ShouldBe(1);
			insertTarget.ObjectReferences.First().SchemaObject.ShouldNotBe(null);
			insertTarget.ProgramReferences.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestFunctionTypeAndSequenceInInsertValuesClause()
		{
			const string query1 = @"INSERT INTO SELECTION (SELECTION_ID, NAME, RESPONDENTBUCKET_ID) VALUES (SQLPAD_FUNCTION, XMLTYPE(), TEST_SEQ.NEXTVAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.InsertTargets.Count.ShouldBe(1);
			var insertTarget = semanticModel.InsertTargets.First();
			insertTarget.ObjectReferences.Count.ShouldBe(1);
			var insertTargetDataObjectReference = insertTarget.ObjectReferences.First();
			insertTargetDataObjectReference.SchemaObject.ShouldNotBe(null);
			insertTarget.ProgramReferences.Count.ShouldBe(1);
			insertTarget.TypeReferences.Count.ShouldBe(1);
			insertTarget.SequenceReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestPackageFunctionReferenceProperties()
		{
			const string query1 = @"SELECT 1 + SYS.DBMS_RANDOM.VALUE + 1 FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var functionReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			functionReferences.Length.ShouldBe(1);
			var functionReference = functionReferences[0];
			functionReference.FunctionIdentifierNode.Token.Value.ShouldBe("VALUE");
			functionReference.ObjectNode.Token.Value.ShouldBe("DBMS_RANDOM");
			functionReference.OwnerNode.Token.Value.ShouldBe("SYS");
			functionReference.AnalyticClauseNode.ShouldBe(null);
			functionReference.SelectListColumn.ShouldNotBe(null);
			functionReference.ParameterListNode.ShouldBe(null);
			functionReference.ParameterNodes.ShouldBe(null);
			functionReference.RootNode.FirstTerminalNode.Token.Value.ShouldBe("SYS");
			functionReference.RootNode.LastTerminalNode.Token.Value.ShouldBe("VALUE");
		}

		[Test(Description = @"")]
		public void TestRedundantTerminals()
		{
			const string query1 = @"SELECT HUSQVIK.SELECTION.SELECTION_ID, SELECTION.NAME, RESPONDENTBUCKET.TARGETGROUP_ID, RESPONDENTBUCKET.NAME FROM HUSQVIK.SELECTION LEFT JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID, SYS.DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(8);
			redundantTerminals[0].Id.ShouldBe(Terminals.SchemaIdentifier);
			redundantTerminals[0].Token.Value.ShouldBe("HUSQVIK");
			redundantTerminals[0].SourcePosition.IndexStart.ShouldBe(7);
			redundantTerminals[1].Id.ShouldBe(Terminals.Dot);
			redundantTerminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
			redundantTerminals[2].Token.Value.ShouldBe("SELECTION");
			redundantTerminals[2].SourcePosition.IndexStart.ShouldBe(15);
			redundantTerminals[3].Id.ShouldBe(Terminals.Dot);
			redundantTerminals[4].Id.ShouldBe(Terminals.ObjectIdentifier);
			redundantTerminals[4].Token.Value.ShouldBe("RESPONDENTBUCKET");
			redundantTerminals[4].SourcePosition.IndexStart.ShouldBe(55);
			redundantTerminals[5].Id.ShouldBe(Terminals.Dot);
			redundantTerminals[6].Id.ShouldBe(Terminals.SchemaIdentifier);
			redundantTerminals[6].Token.Value.ShouldBe("HUSQVIK");
			redundantTerminals[6].SourcePosition.IndexStart.ShouldBe(115);
			redundantTerminals[7].Id.ShouldBe(Terminals.Dot);
		}

		[Test(Description = @"")]
		public void TestFunctionReferenceValidityInSetColumnValueClause()
		{
			const string query1 = @"UPDATE SELECTION SET NAME = SQLPAD_FUNCTION() WHERE SELECTION_ID = 0";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.ProgramReferences.Count.ShouldBe(1);
			var functionReference = semanticModel.MainObjectReferenceContainer.ProgramReferences.Single();
			functionReference.Metadata.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestLiteralColumnDataTypeResolution()
		{
			const string query1 = @"SELECT UNIQUE 123.456, '123.456', N'123.456', 123., 1.2E+1, DATE'2014-10-03', TIMESTAMP'2014-10-03 23:15:43.777' FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
			var queryBlock = semanticModel.QueryBlocks.First();
			queryBlock.Columns.Count.ShouldBe(7);
			queryBlock.HasDistinctResultSet.ShouldBe(true);
			var columns = queryBlock.Columns.ToList();
			columns.ForEach(c => c.ColumnDescription.ShouldNotBe(null));
			columns.ForEach(c => c.ColumnDescription.Nullable.ShouldBe(false));
			columns[0].ColumnDescription.FullTypeName.ShouldBe("NUMBER(6, 3)");
			columns[1].ColumnDescription.FullTypeName.ShouldBe("CHAR(7 CHAR)");
			columns[2].ColumnDescription.FullTypeName.ShouldBe("NCHAR(7)");
			columns[3].ColumnDescription.FullTypeName.ShouldBe("NUMBER(3)");
			columns[4].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			columns[5].ColumnDescription.FullTypeName.ShouldBe("DATE");
			columns[6].ColumnDescription.FullTypeName.ShouldBe("TIMESTAMP");
		}

		[Test(Description = @"")]
		public void TestLiteralColumnDataTypeResolutionAccessedFromInlineView()
		{
			const string query1 = @"SELECT CONSTANT FROM (SELECT DISTINCT 123.456 CONSTANT FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
			var queryBlock = semanticModel.QueryBlocks.First();
			queryBlock.Columns.Count.ShouldBe(1);
			queryBlock.HasDistinctResultSet.ShouldBe(true);
			var column = queryBlock.Columns.First();
			column.ColumnDescription.ShouldNotBe(null);
			column.ColumnDescription.Name.ShouldBe("\"CONSTANT\"");
			column.ColumnDescription.FullTypeName.ShouldBe("NUMBER(6, 3)");
		}

		[Test(Description = @"")]
		public void TestLiteralColumnDataTypeResolutionWithExpressions()
		{
			const string query1 = @"SELECT 1 + 1, 1.1 + 1.1, 'x' || 'y', DATE'2014-10-04' + 1, TIMESTAMP'2014-10-04 20:21:13' + INTERVAL '1' HOUR FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
			var queryBlock = semanticModel.QueryBlocks.First();
			queryBlock.HasDistinctResultSet.ShouldBe(false);
			queryBlock.Columns.Count.ShouldBe(5);
			var columns = queryBlock.Columns.ToList();
			columns.ForEach(c => c.ColumnDescription.ShouldNotBe(null));
			columns.ForEach(c => c.ColumnDescription.Nullable.ShouldBe(true));
			columns.ForEach(c => c.ColumnDescription.Type.ShouldBe(null));
			columns.ForEach(c => c.ColumnDescription.FullTypeName.ShouldBe(null));
		}

		[Test(Description = @"")]
		public void TestSequenceDatabaseLinkReference()
		{
			const string query1 = @"SELECT TEST_SEQ.NEXTVAL@SQLPAD.HUSQVIK.COM@HQINSTANCE, SQLPAD_FUNCTION@SQLPAD.HUSQVIK.COM@HQINSTANCE FROM DUAL@SQLPAD.HUSQVIK.COM@HQINSTANCE";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
			var queryBlock = semanticModel.QueryBlocks.First();
			var databaseLinkReferences = queryBlock.DatabaseLinkReferences.OrderBy(r => r.RootNode.SourcePosition.IndexStart).ToArray();
			databaseLinkReferences.Length.ShouldBe(4);
			databaseLinkReferences[0].ShouldBeTypeOf<OracleSequenceReference>();
			databaseLinkReferences[1].ShouldBeTypeOf<OracleColumnReference>();
			databaseLinkReferences[2].ShouldBeTypeOf<OracleProgramReference>();
			databaseLinkReferences[3].ShouldBeTypeOf<OracleDataObjectReference>();
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminals()
		{
			const string query1 = @"SELECT C1 FROM (SELECT 1 C1, 1 + 2 C2, DUMMY C3 FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(8);
			redundantTerminals[0].Id.ShouldBe(Terminals.Comma);
			redundantTerminals[1].Id.ShouldBe(Terminals.NumberLiteral);
			redundantTerminals[2].Id.ShouldBe(Terminals.MathPlus);
			redundantTerminals[3].Id.ShouldBe(Terminals.NumberLiteral);
			redundantTerminals[4].Id.ShouldBe(Terminals.ColumnAlias);
			redundantTerminals[5].Id.ShouldBe(Terminals.Comma);
			redundantTerminals[6].Id.ShouldBe(Terminals.Identifier);
			redundantTerminals[7].Id.ShouldBe(Terminals.ColumnAlias);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWithAllQueryBlockColumns()
		{
			const string query1 = @"SELECT 1 FROM (SELECT 1 C1, 1 C2, DUMMY C3 FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(6);
			redundantTerminals[0].Id.ShouldBe(Terminals.NumberLiteral);
			redundantTerminals[1].Id.ShouldBe(Terminals.ColumnAlias);
			redundantTerminals[2].Id.ShouldBe(Terminals.Comma);
			redundantTerminals[3].Id.ShouldBe(Terminals.NumberLiteral);
			redundantTerminals[4].Id.ShouldBe(Terminals.ColumnAlias);
			redundantTerminals[5].Id.ShouldBe(Terminals.Comma);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWithFirstRedundantColumn()
		{
			const string query1 = @"SELECT C2 FROM (SELECT 1 C1, 2 C2 FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(3);
			redundantTerminals[0].Id.ShouldBe(Terminals.NumberLiteral);
			redundantTerminals[1].Id.ShouldBe(Terminals.ColumnAlias);
			redundantTerminals[2].Id.ShouldBe(Terminals.Comma);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWithAsteriskReference()
		{
			const string query1 = @"SELECT * FROM (SELECT 1 C1, 2 C2 FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithConcatenatedSubquery()
		{
			const string query1 = @"SELECT DUMMY, DUMMY FROM DUAL UNION SELECT DUMMY, DUMMY FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithCorrelatedSubquery()
		{
			const string query1 = @"SELECT (SELECT 1 FROM DUAL D WHERE DUMMY = SYS.DUAL.DUMMY) VAL FROM SYS.DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsOfUnreferencedAsteriskClause()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUAL.*, SELECTION.* FROM DUAL, SELECTION)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantNodes.OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(4);
		}

		[Test(Description = @"")]
		public void TestCommonTableExpressionColumnNameList()
		{
			const string query1 = @"WITH GENERATOR(C1, C2, C3, C4) AS (SELECT 1, DUAL.*, 3, DUMMY FROM DUAL) SELECT C1, C2, C3, C4 FROM GENERATOR";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var columns = semanticModel.MainQueryBlock.Columns.ToList();
			columns.Count.ShouldBe(4);
			columns.ForEach(c => c.ColumnReferences.Count.ShouldBe(1));

			foreach (var column in columns)
			{
				var columnReference = column.ColumnReferences[0];
				columnReference.ColumnNodeObjectReferences.Count.ShouldBe(1);
				var tableReference = columnReference.ColumnNodeObjectReferences.Single();
				tableReference.FullyQualifiedObjectName.Name.ShouldBe("GENERATOR");
				tableReference.Type.ShouldBe(ReferenceType.CommonTableExpression);
				tableReference.QueryBlocks.Count.ShouldBe(1);
			}

			semanticModel.RedundantNodes.Count.ShouldBe(0);
		}
	}
}
