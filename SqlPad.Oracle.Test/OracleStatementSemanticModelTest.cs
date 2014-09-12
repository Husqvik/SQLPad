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
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[0].IsAsterisk.ShouldBe(true);
			columns[1].ColumnReferences.Count.ShouldBe(1);
			columns[1].ExplicitDefinition.ShouldBe(false);
			columns[1].ColumnReferences.Count.ShouldBe(1);
			columns[1].ColumnReferences.First().ColumnNodeObjectReferences.Count.ShouldBe(1);
			columns[1].ColumnReferences.First().ColumnNodeObjectReferences.Single().ShouldBe(objectReference);
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
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[0].IsDirectReference.ShouldBe(true);
			columns[0].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].ExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectReference.ShouldBe(true);
			columns[1].ColumnDescription.Type.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].ExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectReference.ShouldBe(false);
			columns[2].ColumnDescription.Type.ShouldBe(null); // TODO: Add column expression type resolving

			var outerBlock = queryBlocks[1];
			outerBlock.ObjectReferences.Count.ShouldBe(1);
			var innerTableReference = outerBlock.ObjectReferences.Single();
			innerTableReference.Type.ShouldBe(ReferenceType.InlineView);
			outerBlock.Columns.Count.ShouldBe(3);
			columns = outerBlock.Columns.ToArray();
			columns[0].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[0].IsDirectReference.ShouldBe(true);
			columns[0].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].ExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectReference.ShouldBe(true);
			columns[1].ColumnDescription.Type.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].ExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectReference.ShouldBe(true);
			columns[2].ColumnDescription.Type.ShouldBe(null); // TODO: Add column expression type resolving
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
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[1].ExplicitDefinition.ShouldBe(false);
			columns[1].ColumnDescription.Type.ShouldBe("NUMBER");

			var outerBlock = queryBlocks[1];
			outerBlock.ObjectReferences.Count.ShouldBe(1);
			var innerTableReference = outerBlock.ObjectReferences.Single();
			innerTableReference.Type.ShouldBe(ReferenceType.InlineView);
			outerBlock.Columns.Count.ShouldBe(5);
			columns = outerBlock.Columns.ToArray();
			columns[0].IsAsterisk.ShouldBe(true);
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[1].ExplicitDefinition.ShouldBe(false);
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
	}
}