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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			lagFunction.ParameterReferences.Count.ShouldBe(3);
		}

		[Test(Description = @"")]
		public void TestBasicColumnTypes()
		{
			const string query1 = @"SELECT RESPONDENTBUCKET_ID, SELECTION_NAME, MY_NUMBER_COLUMN FROM (SELECT RESPONDENTBUCKET_ID, NAME SELECTION_NAME, 1 MY_NUMBER_COLUMN FROM SELECTION)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			columns[0].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].HasExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectReference.ShouldBe(true);
			columns[1].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].HasExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectReference.ShouldBe(false);
			columns[2].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("NUMBER");
			columns[2].ColumnDescription.FullTypeName.ShouldBe("NUMBER");

			var outerBlock = queryBlocks[1];
			outerBlock.ObjectReferences.Count.ShouldBe(1);
			var innerTableReference = outerBlock.ObjectReferences.Single();
			innerTableReference.Type.ShouldBe(ReferenceType.InlineView);
			outerBlock.Columns.Count.ShouldBe(3);
			columns = outerBlock.Columns.ToArray();
			columns[0].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[0].HasExplicitDefinition.ShouldBe(true);
			columns[0].IsDirectReference.ShouldBe(true);
			columns[0].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].HasExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectReference.ShouldBe(true);
			columns[1].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].HasExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectReference.ShouldBe(true);
			columns[2].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("NUMBER");
			columns[2].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
		}

		[Test(Description = @"")]
		public void TestAsteriskExposedColumnTypes()
		{
			const string query1 = @"SELECT * FROM (SELECT * FROM SELECTION)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			columns[1].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("NUMBER");

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
			columns[1].ColumnDescription.DataType.FullyQualifiedName.Name.ShouldBe("NUMBER");
		}

		[Test(Description = @"")]
		public void TestFunctionParameterListForComplexExpressionParameter()
		{
			const string query1 = @"SELECT MAX(CASE WHEN 'Option' IN ('Value1', 'Value2') THEN 1 END) FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			maxFunction.ParameterReferences.ShouldNotBe(null);
			maxFunction.ParameterReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestColumnNotAmbiguousInOrderByClauseWhenSelectContainsAsterisk()
		{
			const string query1 = @"SELECT * FROM SELECTION ORDER BY NAME";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
		public void TestColumnResolvedAmbiguousInOrderByClauseWhenNotReferencedInSelectClause()
		{
			const string query1 = @"SELECT NULL FROM DUAL D1 JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY ORDER BY DUMMY";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.OrderBy(r => r.ColumnNode.SourcePosition.IndexStart).ToArray();
			columnReferences.Length.ShouldBe(3);
			var orderByDummy = columnReferences[2];
			orderByDummy.Placement.ShouldBe(QueryBlockPlacement.OrderBy);
			orderByDummy.ColumnNodeColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestFromClauseWithObjectOverDatabaseLink()
		{
			const string query1 = @"SELECT * FROM SELECTION@HQ_PDB_LOOPBACK";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

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
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var databaseModel = new OracleTestDatabaseModel { CurrentDatabaseDomainNameInternal = null };

			var semanticModel = new OracleStatementSemanticModel(query1, statement, databaseModel);
			semanticModel.StatementText.ShouldBe(query1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithMissingAliasedColumnExpression()
		{
			const string query1 = @"SELECT SQL_CHILD_NUMBER, , PREV_CHILD_NUMBER FROM V$SESSION";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhileTypingXmlTableColumnDataType()
		{
			const string query1 = @"SELECT * FROM XMLTABLE ('/root' PASSING XMLTYPE ('<root>value</root>') COLUMNS VALUE V";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

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
			functionReference.ParameterReferences.ShouldBe(null);
			functionReference.RootNode.FirstTerminalNode.Token.Value.ShouldBe("SYS");
			functionReference.RootNode.LastTerminalNode.Token.Value.ShouldBe("VALUE");
		}

		[Test(Description = @"")]
		public void TestRedundantTerminals()
		{
			const string query1 = @"SELECT HUSQVIK.SELECTION.SELECTION_ID, SELECTION.NAME, RESPONDENTBUCKET.TARGETGROUP_ID, RESPONDENTBUCKET.NAME FROM HUSQVIK.SELECTION LEFT JOIN RESPONDENTBUCKET ON SELECTION.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID, SYS.DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
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
			columns[0].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			columns[1].ColumnDescription.FullTypeName.ShouldBe("CHAR(7)");
			columns[2].ColumnDescription.FullTypeName.ShouldBe("NCHAR(7)");
			columns[3].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			columns[4].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			columns[5].ColumnDescription.FullTypeName.ShouldBe("DATE");
			columns[6].ColumnDescription.FullTypeName.ShouldBe("TIMESTAMP(9)");
		}

		[Test(Description = @"")]
		public void TestLiteralColumnDataTypeResolutionAccessedFromInlineView()
		{
			const string query1 = @"SELECT CONSTANT1, CONSTANT2 FROM (SELECT DISTINCT 123.456 CONSTANT1, 654.321 AS CONSTANT2 FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
			var queryBlock = semanticModel.QueryBlocks.First();
			queryBlock.HasDistinctResultSet.ShouldBe(true);
			queryBlock.Columns.Count.ShouldBe(2);
			queryBlock.Columns[0].ColumnDescription.ShouldNotBe(null);
			queryBlock.Columns[0].ColumnDescription.Name.ShouldBe("\"CONSTANT1\"");
			queryBlock.Columns[0].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			queryBlock.Columns[1].ColumnDescription.ShouldNotBe(null);
			queryBlock.Columns[1].ColumnDescription.Name.ShouldBe("\"CONSTANT2\"");
			queryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
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
			columns.ForEach(c => c.ColumnDescription.DataType.ShouldBe(OracleDataType.Empty));
			columns.ForEach(c => c.ColumnDescription.FullTypeName.ShouldBe(String.Empty));
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

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
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

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
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

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(3);
			redundantTerminals[0].Id.ShouldBe(Terminals.NumberLiteral);
			redundantTerminals[1].Id.ShouldBe(Terminals.ColumnAlias);
			redundantTerminals[2].Id.ShouldBe(Terminals.Comma);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWhenCombinedWithCommonTableExpressionUsingInlineView()
		{
			const string query1 = @"WITH CTE AS (SELECT VAL FROM (SELECT 1 VAL FROM DUAL)) SELECT 1 OUTPUT_COLUMN, VAL FROM (SELECT VAL FROM CTE)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWithAsteriskReference()
		{
			const string query1 = @"SELECT * FROM (SELECT 1 C1, 2 C2 FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithSchemaQualifiedFunction()
		{
			const string query1 = @"SELECT DUMMY, HUSQVIK.SQLPAD_FUNCTION FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(2);
			redundantTerminals[0].Id.ShouldBe(Terminals.ObjectIdentifier);
			redundantTerminals[0].Token.Value.ShouldBe("HUSQVIK");
			redundantTerminals[1].Id.ShouldBe(Terminals.Dot);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithConcatenatedSubquery()
		{
			const string query1 = @"SELECT DUMMY, DUMMY FROM DUAL UNION SELECT DUMMY, DUMMY FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithCorrelatedSubquery()
		{
			const string query1 = @"SELECT (SELECT 1 FROM DUAL D WHERE DUMMY = SYS.DUAL.DUMMY) VAL FROM SYS.DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithDistinctSubquery()
		{
			const string query1 = @"SELECT COUNT(*) FROM (SELECT DISTINCT X, Y FROM COORDINATES)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsOfUnreferencedAsteriskClause()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUAL.*, SELECTION.* FROM DUAL, SELECTION)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(4);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnAndRedundantQualifierCombined()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUAL.*, HUSQVIK.SELECTION.NAME FROM DUAL, HUSQVIK.SELECTION)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(2);
			semanticModel.RedundantSymbolGroups.SelectMany(g => g).Count().ShouldBe(8);
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

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithMultipleAnalyticFunctionsWithinSameExpression()
		{
			const string query1 = @"SELECT SUM(COUNT(*) OVER (ORDER BY NULL) / COUNT(*) OVER (ORDER BY NULL) FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var functionReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			functionReferences.Length.ShouldBe(3);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithQuotedDatabaseLinkName()
		{
			const string query1 = @"SELECT * FROM DUAL@""SQLSERVERDB.STOCKHOLM.CINT.COM""";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestAsteriskNotRedundantInCorrelatedSubquery()
		{
			const string query1 = @"SELECT * FROM SELECTION WHERE SELECTIONNAME IN (SELECT * FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestFullyQualifiedTableOverDatabaseLink()
		{
			const string query1 = @"SELECT * FROM HUSQVIK.SELECTION@HQ_PDB_LOOPBACK";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var objectReferences = semanticModel.QueryBlocks.Single().ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			objectReferences[0].SchemaObject.ShouldBe(null);
			objectReferences[0].DatabaseLink.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithSimpleDatabaseLinkIncludingInstanceName()
		{
			const string query1 = @"SELECT * FROM DUAL, DUAL@""HQ_PDB@LOOPBACK""";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var objectReferences = semanticModel.QueryBlocks.Single().ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithDateLiteralWithInvalidQuotedString()
		{
			const string query1 = @"SELECT DATE q'2014-10-04' FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithTimestampLiteralWithInvalidQuotedString()
		{
			const string query1 = @"SELECT TIMESTAMP q'2014-10-04' FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithUnfinishedSqlModelRule()
		{
			const string query1 =
@"SELECT
	*
FROM (SELECT 1 C1, 2 C2 FROM DUAL)
MODEL

	DIMENSION BY (C1)
	MEASURES (C2 MEASURE1)
	RULES (
		MEASURE1[ANY] = DBMS_RANDOM.VALUE(), DBMS_RANDOM
    )";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithUnfinishedSqlModelMeasure()
		{
			const string query1 = @"SELECT * FROM (SELECT * FROM DUAL) MODEL DIMENSION BY (0 C1) MEASURES (0 C2, , 0 C3) RULES (C2[ANY] = 0)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhileTypingSchemaQualifiedObjectWithinUpdateStatement()
		{
			const string query1 = @"UPDATE HUSQVIK.";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);
			semanticModel.MainQueryBlock.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestStoredProcedureIsIgnoredInSql()
		{
			const string query1 = @"SELECT SQLPAD.SQLPAD_PROCEDURE() FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var programReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(1);
			programReferences[0].Metadata.ShouldBe(null);
			programReferences[0].SchemaObject.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestSpecificGrammarFunctionInOrderByClause()
		{
			const string query1 = @"SELECT NULL FROM DUAL ORDER BY COUNT(*)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var programReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(1);
			var programReference = programReferences[0];
			programReference.FunctionIdentifierNode.Id.ShouldBe(Terminals.Count);
			programReference.ObjectNode.ShouldBe(null);
			programReference.OwnerNode.ShouldBe(null);
			programReference.AnalyticClauseNode.ShouldBe(null);
			programReference.SelectListColumn.ShouldBe(null);
			programReference.ParameterListNode.ShouldNotBe(null);
			programReference.ParameterReferences.ShouldNotBe(null);
			programReference.ParameterReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestListAggregationFunction()
		{
			const string query1 = @"SELECT LISTAGG(ROWNUM, ', ') WITHIN GROUP (ORDER BY ROWNUM) FROM DUAL";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var programReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(1);
			var programReference = programReferences[0];
			programReference.FunctionIdentifierNode.Id.ShouldBe(Terminals.ListAggregation);
			programReference.ObjectNode.ShouldBe(null);
			programReference.OwnerNode.ShouldBe(null);
			programReference.AnalyticClauseNode.ShouldBe(null);
			programReference.SelectListColumn.ShouldNotBe(null);
			programReference.ParameterListNode.ShouldNotBe(null);
			programReference.ParameterReferences.ShouldNotBe(null);
			programReference.ParameterReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestTableCollectionExpressionProgramReferences()
		{
			const string query1 = @"SELECT * FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST ADVANCED')) T1, TABLE(SYS.ODCIRAWLIST(HEXTORAW('ABCDEF'), HEXTORAW('A12345'), HEXTORAW('F98765'))) T2";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			var objectReferences = queryBlock.ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(2);
			objectReferences[0].RootNode.ShouldNotBe(null);
			objectReferences[0].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.Table);
			objectReferences[0].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.ObjectAlias);
			objectReferences[0].RootNode.LastTerminalNode.Token.Value.ShouldBe("T1");
			objectReferences[1].RootNode.ShouldNotBe(null);
			objectReferences[1].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.Table);
			objectReferences[1].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.ObjectAlias);
			objectReferences[1].RootNode.LastTerminalNode.Token.Value.ShouldBe("T2");

			var programReferences = queryBlock.AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(5);
			programReferences[0].RootNode.ShouldNotBe(null);
			programReferences[0].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.ObjectIdentifier);
			programReferences[0].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.RightParenthesis);
			programReferences[0].ParameterListNode.ShouldNotBe(null);
			programReferences[0].ParameterReferences.ShouldNotBe(null);
			programReferences[0].ParameterReferences.Count.ShouldBe(3);
			programReferences[1].RootNode.ShouldNotBe(null);
			programReferences[1].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.ObjectIdentifier);
			programReferences[1].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.RightParenthesis);
			programReferences[1].ParameterListNode.ShouldNotBe(null);
			programReferences[1].ParameterReferences.ShouldNotBe(null);
			programReferences[1].ParameterReferences.Count.ShouldBe(3);
			programReferences[2].Metadata.ShouldNotBe(null);
			programReferences[2].Metadata.Identifier.Name.ShouldBe("\"HEXTORAW\"");
			programReferences[3].Metadata.ShouldNotBe(null);
			programReferences[3].Metadata.Identifier.Name.ShouldBe("\"HEXTORAW\"");
			programReferences[4].Metadata.ShouldNotBe(null);
			programReferences[4].Metadata.Identifier.Name.ShouldBe("\"HEXTORAW\"");

			queryBlock.Columns.Count.ShouldBe(3);
		}

		[Test(Description = @"")]
		public void TestTableCollectionExpressionColumnUsingPackageFunction()
		{
			const string query1 = @"SELECT COLUMN_VALUE FROM TABLE(SQLPAD.PIPELINED_FUNCTION(SYSDATE, SYSDATE)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			queryBlock.ObjectReferences.Count.ShouldBe(1);
			var objectReference = queryBlock.ObjectReferences.First();
			objectReference.ShouldBeTypeOf<OracleTableCollectionReference>();
			var tableCollectionReference = (OracleTableCollectionReference)objectReference;
			tableCollectionReference.RowSourceFunctionReference.ShouldNotBe(null);
			tableCollectionReference.RowSourceFunctionReference.Metadata.ShouldNotBe(null);

			queryBlock.Columns.Count.ShouldBe(1);
			queryBlock.Columns[0].ColumnReferences.Count.ShouldBe(1);
			queryBlock.Columns[0].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestXmlTableReference()
		{
			const string query1 = @"SELECT * FROM XMLTABLE('for $i in $RSS_DATA/rss/channel/item return $i' PASSING HTTPURITYPE('http://servis.idnes.cz/rss.asp?c=zpravodaj').GETXML() AS RSS_DATA COLUMNS SEQ# FOR ORDINALITY, TITLE VARCHAR2(4000) PATH 'title', DESCRIPTION CLOB PATH 'description') T";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			var objectReferences = queryBlock.ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			objectReferences[0].RootNode.ShouldNotBe(null);
			objectReferences[0].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.XmlTable);
			objectReferences[0].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.ObjectAlias);
			objectReferences[0].RootNode.LastTerminalNode.Token.Value.ShouldBe("T");
			objectReferences[0].Columns.Count.ShouldBe(3);
			var columns = objectReferences[0].Columns.ToArray();
			columns[0].Name.ShouldBe("\"SEQ#\"");
			columns[0].Nullable.ShouldBe(true);
			columns[0].FullTypeName.ShouldBe("NUMBER");
			columns[1].Name.ShouldBe("\"TITLE\"");
			columns[1].Nullable.ShouldBe(true);
			columns[1].FullTypeName.ShouldBe("VARCHAR2(4000)");
			columns[2].Name.ShouldBe("\"DESCRIPTION\"");
			columns[2].Nullable.ShouldBe(true);
			columns[2].FullTypeName.ShouldBe("CLOB");

			queryBlock.Columns.Count.ShouldBe(4);
		}

		[Test(Description = @"")]
		public void TestXmlTableReferenceWithoutColumnListSpecification()
		{
			const string query1 = @"SELECT * FROM XMLTABLE('for $i in $RSS_DATA/rss/channel/item return $i' PASSING HTTPURITYPE('http://servis.idnes.cz/rss.asp?c=zpravodaj').GETXML() AS RSS_DATA)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			var columns = queryBlock.Columns.ToArray();
			columns.Length.ShouldBe(2);
			columns[0].IsAsterisk.ShouldBe(true);
			columns[1].ColumnDescription.Name.ShouldBe("\"COLUMN_VALUE\"");
			columns[1].ColumnDescription.Nullable.ShouldBe(true);
			columns[1].ColumnDescription.FullTypeName.ShouldBe("SYS.XMLTYPE");
		}

		[Test(Description = @"")]
		public void TestJsonTableReference()
		{
			const string query1 =
@"SELECT
	*
FROM
	JSON_TABLE(
	'{LineItems : [{ItemNumber : 1,
                    Part       : {Description : ""One Magic Christmas"",
                                 UnitPrice    : 19.95,
                                 UPCCode      : 13131092899},
                    Quantity   : 9.0,
                    CorrelationIds: [""CorrelationId1"", ""CorrelationId2""]},
                   {ItemNumber : 2,
                    Part       : {Description : ""Lethal Weapon"",
                                  UnitPrice   : 19.95,
                                  UPCCode     : 85391628927},
                    Quantity   : 5.0,
                    CorrelationIds: [""CorrelationId3"", ""CorrelationId4""]}]}',
		'$.LineItems[*]'
		DEFAULT 'invalid data' ON ERROR
		COLUMNS (
			SEQ# FOR ORDINALITY,
			ITEM_NUMBER NUMBER PATH '$.ItemNumber',
			QUANTITY VARCHAR2 PATH '$.Quantity',
			NONEXISTING VARCHAR2(20) PATH '$.NonExisting' DEFAULT 'Not found' ON ERROR,
			HAS_UPCCODE VARCHAR2(5) EXISTS PATH '$.Part.UPCCode' FALSE ON ERROR,
			NESTED PATH '$.CorrelationIds[*]'
         	COLUMNS (
         		NESTED_VALUE VARCHAR2(16) PATH '$',
				HAS_NESTED_VALUE VARCHAR2(5) EXISTS PATH '$' FALSE ON ERROR)
		)
	)
AS T";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			var objectReferences = queryBlock.ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			objectReferences[0].RootNode.ShouldNotBe(null);
			objectReferences[0].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.JsonTable);
			objectReferences[0].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.ObjectAlias);
			objectReferences[0].RootNode.LastTerminalNode.Token.Value.ShouldBe("T");
			objectReferences[0].Columns.Count.ShouldBe(7);
			var columns = objectReferences[0].Columns.ToArray();
			columns[0].Name.ShouldBe("\"SEQ#\"");
			columns[0].FullTypeName.ShouldBe("NUMBER");
			columns[1].Name.ShouldBe("\"ITEM_NUMBER\"");
			columns[1].FullTypeName.ShouldBe("NUMBER");
			columns[2].Name.ShouldBe("\"QUANTITY\"");
			columns[2].FullTypeName.ShouldBe("VARCHAR2(4000)");
			columns[3].Name.ShouldBe("\"NONEXISTING\"");
			columns[3].FullTypeName.ShouldBe("VARCHAR2(20)");
			columns[4].Name.ShouldBe("\"HAS_UPCCODE\"");
			columns[4].FullTypeName.ShouldBe("VARCHAR2(5)");
			columns[5].Name.ShouldBe("\"NESTED_VALUE\"");
			columns[5].FullTypeName.ShouldBe("VARCHAR2(16)");
			columns[6].Name.ShouldBe("\"HAS_NESTED_VALUE\"");
			columns[6].FullTypeName.ShouldBe("VARCHAR2(5)");

			queryBlock.Columns.Count.ShouldBe(8);
		}

		[Test(Description = @"")]
		public void TestSqlModelReference()
		{
			const string query1 =
@"SELECT
	*
FROM
	DUAL
MODEL
	DIMENSION BY (0 AS KEY)
	MEASURES
	(
		'Default Value' AS C1,
		CAST(NULL AS VARCHAR2(4000)) AS C2,
		CAST(NULL AS VARCHAR2(4000)) AS C3
	)
	RULES UPDATE
	(
		C1[ANY] = 'x',
		C2[ANY] = 'x',
		C3[ANY] = 'x'
	)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			var objectReferences = queryBlock.ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			objectReferences[0].ShouldBeTypeOf<OracleSqlModelReference>();
			objectReferences[0].Columns.Count.ShouldBe(4);
			
			queryBlock.Columns.Count.ShouldBe(5);
			queryBlock.Columns[0].IsAsterisk.ShouldBe(true);
			queryBlock.Columns[1].NormalizedName.ShouldBe("\"KEY\"");
			queryBlock.Columns[1].ColumnDescription.DataType.ShouldNotBe(null);
			queryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			queryBlock.Columns[2].NormalizedName.ShouldBe("\"C1\"");
			queryBlock.Columns[2].ColumnDescription.DataType.ShouldNotBe(null);
			queryBlock.Columns[2].ColumnDescription.FullTypeName.ShouldBe("CHAR(13)");
			queryBlock.Columns[2].IsDirectReference.ShouldBe(true);
			queryBlock.Columns[3].NormalizedName.ShouldBe("\"C2\"");
			queryBlock.Columns[3].ColumnDescription.DataType.ShouldNotBe(null);
			queryBlock.Columns[3].IsDirectReference.ShouldBe(true);
			queryBlock.Columns[4].NormalizedName.ShouldBe("\"C3\"");
			queryBlock.Columns[4].IsDirectReference.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestSqlModelReferenceInnerReferences()
		{
			const string query1 =
@"SELECT
	*
FROM (SELECT 1 C1, 2 C2, 3 C3 FROM DUAL)
MODEL
	PARTITION BY (C1, C4)
	DIMENSION BY (C2, C5)
	MEASURES (C3 MEASURE1, DBMS_RANDOM.VALUE() MEASURE2, C6, XMLTYPE('<root/>') MEASURE4)
	RULES (
		MEASURE1[ANY, ANY] = C6[CV(C2), CV(C4)],
		MEASURE2[ANY, ANY] = MEASURE5[DBMS_RANDOM.VALUE(), DBMS_RANDOM.VALUE],
		MEASURE3[C1 > C5, ANY] = AVG(NVL(MEASURE1, 0))[C2 BETWEEN 0 AND 1, C4 BETWEEN 0 AND 1],
		MEASURE4[NVL(C1, 0), NULL] = XMLTYPE('<root/>')
    )";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
			var outerQueryBlock = semanticModel.QueryBlocks.OrderBy(qb => qb.RootNode.Level).First();
			outerQueryBlock.Columns.Count.ShouldBe(9);

			var objectReferences = outerQueryBlock.ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			objectReferences[0].ShouldBeTypeOf<OracleSqlModelReference>();
			objectReferences[0].Columns.Count.ShouldBe(8);

			outerQueryBlock.ModelReference.ShouldNotBe(null);
			var sourceReferenceContainer = outerQueryBlock.ModelReference.SourceReferenceContainer;
			sourceReferenceContainer.ColumnReferences.Count.ShouldBe(6);
			sourceReferenceContainer.ProgramReferences.Count.ShouldBe(1);
			sourceReferenceContainer.TypeReferences.Count.ShouldBe(1);

			var dimensionReferenceContainer = outerQueryBlock.ModelReference.DimensionReferenceContainer;
			dimensionReferenceContainer.ColumnReferences.Count.ShouldBe(7);
			dimensionReferenceContainer.ColumnReferences[0].ColumnNode.Token.Value.ShouldBe("C2");
			dimensionReferenceContainer.ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			dimensionReferenceContainer.ColumnReferences[1].ColumnNode.Token.Value.ShouldBe("C4");
			dimensionReferenceContainer.ColumnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(0);
			
			dimensionReferenceContainer.ProgramReferences.Count.ShouldBe(5);
			dimensionReferenceContainer.TypeReferences.Count.ShouldBe(0);

			var measuresReferenceContainer = outerQueryBlock.ModelReference.MeasuresReferenceContainer;
			measuresReferenceContainer.ColumnReferences.Count.ShouldBe(7);
			measuresReferenceContainer.ColumnReferences[0].ColumnNode.Token.Value.ShouldBe("MEASURE1");
			measuresReferenceContainer.ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			measuresReferenceContainer.ColumnReferences[1].ColumnNode.Token.Value.ShouldBe("C6");
			measuresReferenceContainer.ColumnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
			measuresReferenceContainer.ColumnReferences[4].ColumnNode.Token.Value.ShouldBe("MEASURE3");
			measuresReferenceContainer.ColumnReferences[4].ColumnNodeColumnReferences.Count.ShouldBe(0);

			measuresReferenceContainer.ProgramReferences.Count.ShouldBe(2);
			measuresReferenceContainer.TypeReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestSqlModelOutputColumns()
		{
			const string query1 =
@"SELECT
	*
FROM (SELECT 1 C1, 2 C2, 3 C3 FROM DUAL)
MODEL
	PARTITION BY (C1 P1, 'PARTITION2' P2)
	DIMENSION BY (C2 D1, N'DIMENSION2' D2)
	MEASURES (C3 M1)
	RULES (
		M1[D1 IS ANY, (D2) IS ANY] = DBMS_RANDOM.VALUE
    )";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
			var outerQueryBlock = semanticModel.QueryBlocks.OrderBy(qb => qb.RootNode.Level).First();
			outerQueryBlock.Columns.Count.ShouldBe(6);
			outerQueryBlock.Columns[0].IsAsterisk.ShouldBe(true);
			outerQueryBlock.Columns[1].NormalizedName.ShouldBe("\"P1\"");
			outerQueryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			outerQueryBlock.Columns[2].NormalizedName.ShouldBe("\"P2\"");
			outerQueryBlock.Columns[2].ColumnDescription.FullTypeName.ShouldBe("CHAR(10)");
			outerQueryBlock.Columns[3].NormalizedName.ShouldBe("\"D1\"");
			outerQueryBlock.Columns[3].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
			outerQueryBlock.Columns[4].NormalizedName.ShouldBe("\"D2\"");
			outerQueryBlock.Columns[4].ColumnDescription.FullTypeName.ShouldBe("NCHAR(10)");
			outerQueryBlock.Columns[5].NormalizedName.ShouldBe("\"M1\"");
		}

		[Test(Description = @"")]
		public void TestRecursiveCommonTableExpression()
		{
			const string query1 = @"WITH GENERATOR(VAL) AS (SELECT 1 FROM DUAL UNION ALL SELECT VAL + 1 FROM GENERATOR WHERE VAL <= 10) SELECT VAL FROM	GENERATOR";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(3);
			var queryBlocks = semanticModel.QueryBlocks.OrderBy(qb => qb.RootNode.SourcePosition.IndexStart).ToArray();
			queryBlocks[0].ColumnReferences.Count.ShouldBe(0);
			queryBlocks[0].ObjectReferences.Count.ShouldBe(1);
			queryBlocks[0].ObjectReferences.First().Type.ShouldBe(ReferenceType.SchemaObject);

			queryBlocks[1].ColumnReferences.Count.ShouldBe(1);
			var columnReference = queryBlocks[1].ColumnReferences[0];
			columnReference.ColumnNodeObjectReferences.Count.ShouldBe(1);
			columnReference.ColumnNodeObjectReferences.First().Type.ShouldBe(ReferenceType.CommonTableExpression);
			queryBlocks[1].Columns.Count.ShouldBe(1);
			queryBlocks[1].Columns[0].ColumnReferences.Count.ShouldBe(1);
			columnReference = queryBlocks[1].Columns[0].ColumnReferences[0];
			columnReference.ColumnNodeObjectReferences.Count.ShouldBe(1);
			columnReference.ColumnNodeObjectReferences.First().Type.ShouldBe(ReferenceType.CommonTableExpression);
			
			queryBlocks[1].ObjectReferences.Count.ShouldBe(1);
			queryBlocks[1].ObjectReferences.First().Type.ShouldBe(ReferenceType.CommonTableExpression);
		}

		[Test(Description = @"")]
		public void TestFullyQualifiedColumnNameResolutionInUpdateStatement()
		{
			const string query1 = @"UPDATE HUSQVIK.SELECTION SET SELECTION.PROJECT_ID = HUSQVIK.SELECTION.PROJECT_ID WHERE 1 = 0";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var mainObjectReference = semanticModel.MainObjectReferenceContainer.MainObjectReference;
			mainObjectReference.ShouldNotBe(null);

			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(2);
			var sourceColumn = semanticModel.MainObjectReferenceContainer.ColumnReferences[1];
			sourceColumn.ColumnNodeColumnReferences.Count.ShouldBe(1);
			sourceColumn.ObjectNodeObjectReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestXmlTablePassingExpressionWithInaccessibleReference()
		{
			const string query1 =
@"SELECT
	*
FROM
	(SELECT XMLTYPE('<value>value 1</value>') XML_DATA1 FROM DUAL)
	CROSS JOIN
		XMLTABLE('/root' PASSING '<root>' || XML_DATA1 || XML_DATA2 || '</root>')
	CROSS JOIN
		(SELECT XMLTYPE('<value>value 2</value>') XML_DATA2 FROM DUAL)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ShouldNotBe(null);

			var columnReferences = queryBlock.ColumnReferences.Where(c => c.Placement == QueryBlockPlacement.TableReference).ToArray();
			columnReferences.Length.ShouldBe(2);
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestJsonTablePassingExpressionWithInaccessibleReference()
		{
			const string query1 =
@"SELECT
	*
FROM
	(SELECT '""Value 1"", ""Value 2"", ""Value 3""' JSON_DATA1 FROM DUAL)
	CROSS JOIN
		JSON_TABLE(
			'[' || JSON_DATA1 || ', ' || JSON_DATA2 || ']', '$[*]'
			DEFAULT 'invalid data' ON ERROR
			COLUMNS (
				SEQ$ FOR ORDINALITY,
				VALUE VARCHAR2 PATH '$'
			)
		)
	CROSS JOIN (
		SELECT '""Value 4"", ""Value 5"", ""Value 6""' JSON_DATA2 FROM DUAL
	)";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ShouldNotBe(null);

			var columnReferences = queryBlock.ColumnReferences.Where(c => c.Placement == QueryBlockPlacement.TableReference).ToArray();
			columnReferences.Length.ShouldBe(2);
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnReferencesInConnectByClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION CONNECT BY SELECTION_ID > PROJECT_ID";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ShouldNotBe(null);

			var selectionTableIdentifier = OracleObjectIdentifier.Create(null, "SELECTION");

			queryBlock.ColumnReferences.Count.ShouldBe(2);
			queryBlock.ColumnReferences[0].ValidObjectReference.ShouldNotBe(null);
			queryBlock.ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.ShouldBe(selectionTableIdentifier);
			queryBlock.ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			queryBlock.ColumnReferences[0].ColumnNodeColumnReferences.Single().Name.ShouldBe("\"SELECTION_ID\"");
			queryBlock.ColumnReferences[1].ValidObjectReference.ShouldNotBe(null);
			queryBlock.ColumnReferences[1].ValidObjectReference.FullyQualifiedObjectName.ShouldBe(selectionTableIdentifier);
			queryBlock.ColumnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
			queryBlock.ColumnReferences[1].ColumnNodeColumnReferences.Single().Name.ShouldBe("\"PROJECT_ID\"");
		}

		[Test(Description = @"")]
		public void TestColumnReferencesInRecursiveSearchFirstClauseAndInSubquery()
		{
			const string query1 =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 5
)
SEARCH DEPTH FIRST BY VAL SET SEQ#
SELECT * FROM CTE";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = new OracleStatementSemanticModel(query1, statement, TestFixture.DatabaseModel);
			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ShouldNotBe(null);

			queryBlock.Columns.Count.ShouldBe(3);
			queryBlock.Columns[0].IsAsterisk.ShouldBe(true);
			queryBlock.Columns[1].NormalizedName.ShouldBe("\"VAL\"");
			queryBlock.Columns[2].NormalizedName.ShouldBe("\"SEQ#\"");
			queryBlock.Columns[2].ColumnDescription.FullTypeName.ShouldBe("NUMBER");

			queryBlock.ObjectReferences.Count.ShouldBe(1);
			var cteQueryBlock = queryBlock.ObjectReferences.Single().QueryBlocks.Single();
			cteQueryBlock.Columns.Count.ShouldBe(2);
			cteQueryBlock.Columns[0].NormalizedName.ShouldBe("\"VAL\"");
			cteQueryBlock.Columns[1].NormalizedName.ShouldBe("\"SEQ#\"");
			cteQueryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("NUMBER");
		}
	}
}
