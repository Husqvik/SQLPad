using System;
using System.Linq;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleStatementSemanticModelTest
	{
		private static readonly OracleSqlParser Parser = OracleSqlParser.Instance;

		[Test(Description = @"")]
		public void TestInitializationWithNullStatement()
		{
			var exception = Assert.Throws<ArgumentNullException>(() => OracleStatementSemanticModel.Build(null, null, null));
			exception.ParamName.ShouldBe("statement");
		}

		[Test(Description = @"")]
		public void TestInitializationWithNullDatabaseModel()
		{
			var statement = (OracleStatement)Parser.Parse("SELECT NULL FROM DUAL").Single();
			var exception = Assert.Throws<ArgumentNullException>(() => OracleStatementSemanticModel.Build(null, statement, null));
			exception.ParamName.ShouldBe("databaseModel");
		}

		[Test(Description = @"")]
		public void TestUnitializedDatabaseModel()
		{
			var statement = (OracleStatement)Parser.Parse("SELECT NULL FROM DUAL").Single();
			var semanticModel = OracleStatementSemanticModel.Build(null, statement, new OracleTestDatabaseModel(false));
			semanticModel.HasDatabaseModel.ShouldBe(false);
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.HasDatabaseModel.ShouldBe(true);
			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(3);

			var queryBlocks = semanticModel.QueryBlocks.ToArray();
			queryBlocks[0].Alias.ShouldBe("CTE1");
			queryBlocks[0].NormalizedAlias.ShouldBe("\"CTE1\"");
			queryBlocks[0].ObjectReferences.Count.ShouldBe(1);
			queryBlocks[0].Columns.Count.ShouldBe(3);
			queryBlocks[1].Alias.ShouldBe("CTE2");
			queryBlocks[1].NormalizedAlias.ShouldBe("\"CTE2\"");
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.ToArray();
			columnReferences.Length.ShouldBe(6);
			var orderByName = columnReferences[5];
			orderByName.Placement.ShouldBe(StatementPlacement.OrderBy);
			orderByName.ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestColumnResolvedAmbiguousInOrderByClauseWhenSelectContainsAsteriskForRowSourcesWithSameColumnName()
		{
			const string query1 = @"SELECT SELECTION.*, SELECTION.* FROM SELECTION ORDER BY NAME";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.ToArray();
			columnReferences.Length.ShouldBe(11);
			var orderByName = columnReferences[10];
			orderByName.Placement.ShouldBe(StatementPlacement.OrderBy);
			orderByName.ColumnNodeColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestColumnResolvedAmbiguousInOrderByClauseWhenNotReferencedInSelectClause()
		{
			const string query1 = @"SELECT NULL FROM DUAL D1 JOIN DUAL D2 ON D1.DUMMY = D2.DUMMY ORDER BY DUMMY";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.OrderBy(r => r.ColumnNode.SourcePosition.IndexStart).ToArray();
			columnReferences.Length.ShouldBe(3);
			var orderByDummy = columnReferences[2];
			orderByDummy.Placement.ShouldBe(StatementPlacement.OrderBy);
			orderByDummy.ColumnNodeColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestFromClauseWithObjectOverDatabaseLink()
		{
			const string query1 = @"SELECT * FROM SELECTION@HQ_PDB_LOOPBACK";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var databaseModel = new OracleTestDatabaseModel { CurrentDatabaseDomainNameInternal = null };

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, databaseModel);
			semanticModel.StatementText.ShouldBe(query1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithMissingAliasedColumnExpression()
		{
			const string query1 = @"SELECT SQL_CHILD_NUMBER, , PREV_CHILD_NUMBER FROM V$SESSION";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhileTypingXmlTableColumnDataType()
		{
			const string query1 = @"SELECT * FROM XMLTABLE ('/root' PASSING XMLTYPE ('<root>value</root>') COLUMNS VALUE V";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhileTypingRecursiveCommonTableExpression()
		{
			const string query1 =
@"WITH sampleData(c1, c2) AS (
	SELECT 0, 0 FROM DUAL
	UNION ALL
	SELECT 0, , FROM sampleData WHERE c2 = 0
)
SELECT * FROM sampleData";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			Assert.DoesNotThrow(() => OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel));
		}

		[Test(Description = @"")]
		public void TestModelBuildWhileTypingInlineSubquerySelectList()
		{
			const string query1 = @"SELECT * FROM (SELECT HUSQVIK. FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			Assert.DoesNotThrow(() => OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel));
		}

		[Test(Description = @"")]
		public void TestSimpleInsertValuesStatementModelBuild()
		{
			const string query1 = @"INSERT INTO HUSQVIK.SELECTION(NAME) VALUES ('Dummy selection')";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.SchemaObject.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestSimpleDeleteStatementModelBuild()
		{
			const string query1 = @"DELETE SELECTION WHERE SELECTION_ID = 0";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.SchemaObject.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestUpdateOfSubqueryModelBuild()
		{
			const string query1 = @"UPDATE (SELECT * FROM SELECTION) SET NAME = 'Dummy selection' WHERE SELECTION_ID = 0";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.SchemaObject.ShouldBe(null);
			semanticModel.MainObjectReferenceContainer.MainObjectReference.Type.ShouldBe(ReferenceType.InlineView);
			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestUnfinishedInsertModelBuild()
		{
			const string query1 = @"INSERT INTO";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestInsertColumnListIdentiferMatchingFunctionName()
		{
			const string query1 = @"INSERT INTO SELECTION(SESSIONTIMEZONE) SELECT * FROM SELECTION";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainObjectReferenceContainer.MainObjectReference.ShouldNotBe(null);
			semanticModel.MainObjectReferenceContainer.ProgramReferences.Count.ShouldBe(1);
			var functionReference = semanticModel.MainObjectReferenceContainer.ProgramReferences.Single();
			functionReference.Metadata.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestLiteralColumnDataTypeResolution()
		{
			const string query1 = @"SELECT UNIQUE 123.456, '123.456', N'123.456', 123., 1.2E+1, DATE'2014-10-03', TIMESTAMP'2014-10-03 23:15:43.777' FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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
		public void TestLiteralColumnDataTypeResolutionInConcatenatedQueryBlocks()
		{
			const string query1 =
@"WITH tmp(C1, C2, C3, C4) AS (
	SELECT 'X', N'X', NULL, 'X' FROM DUAL UNION ALL
	SELECT 'XX', NULL, DUMMY, DBMS_RANDOM.STRING('X', 16) FROM DUAL
)
SELECT C1, C2, C3, C4 FROM tmp";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainQueryBlock.Columns.Count.ShouldBe(4);
			semanticModel.MainQueryBlock.Columns[0].NormalizedName.ShouldBe("\"C1\"");
			semanticModel.MainQueryBlock.Columns[0].ColumnDescription.ShouldNotBe(null);
			semanticModel.MainQueryBlock.Columns[0].ColumnDescription.FullTypeName.ShouldBe("CHAR(2)");
			semanticModel.MainQueryBlock.Columns[0].ColumnDescription.Nullable.ShouldBe(false);
			semanticModel.MainQueryBlock.Columns[1].NormalizedName.ShouldBe("\"C2\"");
			semanticModel.MainQueryBlock.Columns[1].ColumnDescription.ShouldNotBe(null);
			semanticModel.MainQueryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("NCHAR(1)");
			semanticModel.MainQueryBlock.Columns[1].ColumnDescription.Nullable.ShouldBe(true);
			semanticModel.MainQueryBlock.Columns[2].NormalizedName.ShouldBe("\"C3\"");
			semanticModel.MainQueryBlock.Columns[2].ColumnDescription.ShouldNotBe(null);
			semanticModel.MainQueryBlock.Columns[2].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(1 BYTE)");
			semanticModel.MainQueryBlock.Columns[2].ColumnDescription.Nullable.ShouldBe(true);
			semanticModel.MainQueryBlock.Columns[3].NormalizedName.ShouldBe("\"C4\"");
			semanticModel.MainQueryBlock.Columns[3].ColumnDescription.ShouldNotBe(null);
			semanticModel.MainQueryBlock.Columns[3].ColumnDescription.FullTypeName.ShouldBe(String.Empty);
			semanticModel.MainQueryBlock.Columns[3].ColumnDescription.Nullable.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestAliasedRecursiveQueryReference()
		{
			const string query1 =
@"with recursion(dummy) as 
(
     select 1 from dual 
     union all 
     select r.dummy + 1
     from dual
     join recursion r on r.dummy <= 5
)
select * from recursion";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var resursiveQueryBlock = semanticModel.QueryBlocks.Where(qb => qb.Type == QueryBlockType.CommonTableExpression).Skip(1).Single();
			resursiveQueryBlock.ObjectReferences.Count.ShouldBe(2);
			var objectReferences = resursiveQueryBlock.ObjectReferences.ToArray();
			var recursiveObjectReference = objectReferences[1];
			recursiveObjectReference.ObjectNode.Token.Value.ShouldBe("recursion");
			recursiveObjectReference.AliasNode.Token.Value.ShouldBe("r");
			recursiveObjectReference.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = resursiveQueryBlock.AllColumnReferences.ToList();
			columnReferences.Count.ShouldBe(2);
			columnReferences[0].ValidObjectReference.ShouldBe(recursiveObjectReference);
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ValidObjectReference.ShouldBe(recursiveObjectReference);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestLiteralColumnDataTypeResolutionAccessedFromInlineView()
		{
			const string query1 = @"SELECT CONSTANT1, CONSTANT2 FROM (SELECT DISTINCT 123.456 CONSTANT1, 654.321 AS CONSTANT2 FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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
		public void TestUnusedXmlTableColumnRedundantTerminals()
		{
			const string query1 =
@"SELECT
    NULL
FROM
    XMLTABLE('/root'
        PASSING XMLTYPE('<root>value</root>')
        COLUMNS
            VALUE1 VARCHAR2(30) PATH '.',
            VALUE2 VARCHAR2(30) PATH '.'
    )";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(1);
			var group1 = semanticModel.RedundantSymbolGroups.First();
			group1.Count.ShouldBe(8);
			group1[0].Id.ShouldBe(Terminals.Comma);
			group1[1].Id.ShouldBe(Terminals.ColumnAlias);
			group1[1].Token.Value.ShouldBe("VALUE2");
			group1[2].Id.ShouldBe(Terminals.Varchar2);
			group1[3].Id.ShouldBe(Terminals.LeftParenthesis);
			group1[4].Id.ShouldBe(Terminals.IntegerLiteral);
			group1[5].Id.ShouldBe(Terminals.RightParenthesis);
			group1[6].Id.ShouldBe(Terminals.Path);
			group1[7].Id.ShouldBe(Terminals.StringLiteral);
		}

		[Test(Description = @"")]
		public void TestUnusedJsonTableColumnRedundantTerminals()
		{
			const string query1 =
@"SELECT
    VALUE2
FROM
    JSON_TABLE('{ key: ""value"" }', '$'
        COLUMNS(
            VALUE1 VARCHAR2(30) PATH '$.key',
            VALUE2 VARCHAR2(30) PATH '$.key',
            VALUE3 VARCHAR2(30) PATH '$.key'
        )
    )";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(2);
			var group1 = semanticModel.RedundantSymbolGroups.First();
			var group2 = semanticModel.RedundantSymbolGroups.Last();
			group1.Count.ShouldBe(8);
			group1[0].Id.ShouldBe(Terminals.Comma);
			group1[1].Id.ShouldBe(Terminals.ColumnAlias);
			group1[1].Token.Value.ShouldBe("VALUE3");
			group1[2].Id.ShouldBe(Terminals.Varchar2);
			group1[3].Id.ShouldBe(Terminals.LeftParenthesis);
			group1[4].Id.ShouldBe(Terminals.IntegerLiteral);
			group1[5].Id.ShouldBe(Terminals.RightParenthesis);
			group1[6].Id.ShouldBe(Terminals.Path);
			group1[7].Id.ShouldBe(Terminals.StringLiteral);
			group1.RedundancyType.ShouldBe(RedundancyType.UnusedColumn);

			group2.Count.ShouldBe(8);
			group2[0].Id.ShouldBe(Terminals.ColumnAlias);
			group2[0].Token.Value.ShouldBe("VALUE1");
			group2[1].Id.ShouldBe(Terminals.Varchar2);
			group2[2].Id.ShouldBe(Terminals.LeftParenthesis);
			group2[3].Id.ShouldBe(Terminals.IntegerLiteral);
			group2[4].Id.ShouldBe(Terminals.RightParenthesis);
			group2[5].Id.ShouldBe(Terminals.Path);
			group2[6].Id.ShouldBe(Terminals.StringLiteral);
			group2[7].Id.ShouldBe(Terminals.Comma);
			group2.RedundancyType.ShouldBe(RedundancyType.UnusedColumn);
		}

		[Test(Description = @"")]
		public void TestUnusedCommonTableExpression()
		{
			const string query1 =
@"WITH CTE1(DUMMY) AS (
	SELECT 0 DUMMY FROM DUAL
	UNION ALL
	SELECT 1 + DUMMY FROM CTE1 WHERE DUMMY < 5
)
SEARCH DEPTH FIRST BY DUMMY SET SEQ#
SELECT * FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var cteQueryBlock = semanticModel.QueryBlocks.First(qb => qb.Type == QueryBlockType.CommonTableExpression);
			cteQueryBlock.RecursiveSearchClause.ShouldNotBe(null);
			semanticModel.MainQueryBlock.RecursiveSearchClause.ShouldBe(null);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(1);
			var terminalGroup = semanticModel.RedundantSymbolGroups.Single();
			terminalGroup.Count.ShouldBe(32);
			terminalGroup[0].Token.Value.ShouldBe("WITH");
			terminalGroup[0].Token.Index.ShouldBe(0);
			terminalGroup[31].Token.Value.ShouldBe("SEQ#");
			terminalGroup[31].Token.Index.ShouldBe(142);
		}

		[Test(Description = @"")]
		public void TestUnusedCommonTableExpressionWhenReferenced()
		{
			const string query1 = @"WITH CTE AS (SELECT DUMMY DUMMY1, 2 DUMMY2 FROM DUAL) SELECT * FROM CTE";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestMultipleUnusedCommonTableExpression()
		{
			const string query1 =
@"WITH CTE1(DUMMY) AS (
	SELECT 0 DUMMY FROM DUAL
	UNION ALL
	SELECT 1 + DUMMY FROM CTE1 WHERE DUMMY < 5
)
SEARCH DEPTH FIRST BY DUMMY SET SEQ#
CYCLE DUMMY SET CYCLE TO 1 DEFAULT 0,
CTE_DUMMY AS (
	SELECT * FROM DUAL
)
SELECT * FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var cteQueryBlock = semanticModel.QueryBlocks.First(qb => qb.Type == QueryBlockType.CommonTableExpression);
			cteQueryBlock.RecursiveSearchClause.ShouldNotBe(null);
			cteQueryBlock.RecursiveCycleClause.ShouldNotBe(null);
			semanticModel.MainQueryBlock.RecursiveCycleClause.ShouldBe(null);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(2);
			var terminalGroups = semanticModel.RedundantSymbolGroups.ToArray();
			terminalGroups[0].Count.ShouldBe(40);
			terminalGroups[0][0].Token.Value.ShouldBe("CTE1");
			terminalGroups[0][0].Token.Index.ShouldBe(5);
			terminalGroups[0][39].Token.Value.ShouldBe(",");
			terminalGroups[0][39].Token.Index.ShouldBe(184);
			terminalGroups[1].Count.ShouldBe(9);
			terminalGroups[1][0].Token.Value.ShouldBe(",");
			terminalGroups[1][0].Token.Index.ShouldBe(184);
			terminalGroups[1][8].Token.Value.ShouldBe(")");
			terminalGroups[1][8].Token.Index.ShouldBe(224);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWithAllQueryBlockColumns()
		{
			const string query1 = @"SELECT 1 FROM (SELECT 1 C1, 1 C2, DUMMY C3 FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWhenInMultisetInClause()
		{
			const string query1 = @"SELECT * FROM DUAL WHERE (DUMMY, DUMMY) IN (SELECT DUMMY, DUMMY FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWithAsteriskReference()
		{
			const string query1 = @"SELECT * FROM (SELECT 1 C1, 2 C2 FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnRedundantTerminalsWithCommonTableExpression()
		{
			const string query1 =
@"WITH sampleData(c1, c2) AS (
	SELECT 0, 0 FROM DUAL
	UNION ALL
	SELECT c1 + 1, 0 FROM sampleData WHERE c1 <= 3
)
SELECT NULL FROM sampleData";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestCommonTableExpressionNameColumnReferenceFromAnotherCommonTableExpression()
		{
			const string query1 =
@"WITH t1 (c) AS (SELECT 0 FROM DUAL),
t2 AS (SELECT c FROM t1)
SELECT c FROM t2";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(3);
			var queryBlock = semanticModel.QueryBlocks.Single(qb => qb.Alias =="t2");
			queryBlock.Columns.Count.ShouldBe(1);
			queryBlock.Columns[0].ColumnReferences.Count.ShouldBe(1);
			queryBlock.Columns[0].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			queryBlock.Columns[0].ColumnReferences[0].ValidObjectReference.ShouldNotBe(null);
        }

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithSchemaQualifiedFunction()
		{
			const string query1 = @"SELECT DUMMY, HUSQVIK.SQLPAD_FUNCTION FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(2);
			redundantTerminals[0].Id.ShouldBe(Terminals.ObjectIdentifier);
			redundantTerminals[0].Token.Value.ShouldBe("HUSQVIK");
			redundantTerminals[1].Id.ShouldBe(Terminals.Dot);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithObjectQualifiedPseudoColumn()
		{
			const string query1 = @"SELECT T.ORA_ROWSCN, T.ROWID FROM ""CaseSensitiveTable"" T";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(4);
			redundantTerminals[0].Id.ShouldBe(Terminals.ObjectIdentifier);
			redundantTerminals[0].Token.Value.ShouldBe("T");
			redundantTerminals[1].Id.ShouldBe(Terminals.Dot);
			redundantTerminals[2].Id.ShouldBe(Terminals.ObjectIdentifier);
			redundantTerminals[2].Token.Value.ShouldBe("T");
			redundantTerminals[3].Id.ShouldBe(Terminals.Dot);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithConcatenatedSubquery()
		{
			const string query1 = @"SELECT DUMMY, DUMMY FROM DUAL UNION SELECT DUMMY, DUMMY FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithCorrelatedSubquery()
		{
			const string query1 = @"SELECT (SELECT 1 FROM DUAL D WHERE DUMMY = SYS.DUAL.DUMMY) VAL FROM SYS.DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsWithDistinctSubquery()
		{
			const string query1 = @"SELECT COUNT(*) FROM (SELECT DISTINCT X, Y FROM COORDINATES)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantTerminalsOfUnreferencedAsteriskClause()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUAL.*, SELECTION.* FROM DUAL, SELECTION)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(4);
		}

		[Test(Description = @"")]
		public void TestUnusedColumnAndRedundantQualifierCombined()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUAL.*, HUSQVIK.SELECTION.NAME FROM DUAL, HUSQVIK.SELECTION)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(2);
			semanticModel.RedundantSymbolGroups.SelectMany(g => g).Count().ShouldBe(8);
		}

		[Test(Description = @"")]
		public void TestRedundantColumnAlias()
		{
			const string query1 = @"SELECT DUAL.DUMMY DUMMY, DUMMY DUMMY FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var terminalGroups = semanticModel.RedundantSymbolGroups.Where(g => g.RedundancyType == RedundancyType.RedundantColumnAlias).ToArray();
			terminalGroups.Length.ShouldBe(2);
			terminalGroups[0].Count.ShouldBe(1);
			terminalGroups[0][0].Token.Index.ShouldBe(18);
			terminalGroups[0][0].Token.Value.ShouldBe("DUMMY");

			terminalGroups[1].Count.ShouldBe(1);
			terminalGroups[1][0].Token.Index.ShouldBe(31);
			terminalGroups[1][0].Token.Value.ShouldBe("DUMMY");
		}

		[Test(Description = @"")]
		public void TestRedundantColumnAliasWithinInlineView()
		{
			const string query1 = @"SELECT C1 FROM (SELECT NULL C1, DUMMY DUMMY FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var terminalGroups = semanticModel.RedundantSymbolGroups.ToArray();
			terminalGroups.Length.ShouldBe(1);
			terminalGroups[0].Count.ShouldBe(3);
			terminalGroups[0][0].Token.Index.ShouldBe(30);
			terminalGroups[0][0].Token.Value.ShouldBe(",");
			terminalGroups[0][1].Token.Index.ShouldBe(32);
			terminalGroups[0][1].Token.Value.ShouldBe("DUMMY");
			terminalGroups[0][2].Token.Index.ShouldBe(38);
			terminalGroups[0][2].Token.Value.ShouldBe("DUMMY");
		}

		[Test(Description = @"")]
		public void TestRedundantObjectAlias()
		{
			const string query1 = @"SELECT DUMMY FROM DUAL DUAL, SYS.DUAL DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var terminalGroups = semanticModel.RedundantSymbolGroups.Where(g => g.RedundancyType == RedundancyType.RedundantObjectAlias).ToArray();
			terminalGroups.Length.ShouldBe(2);
			terminalGroups[0].Count.ShouldBe(1);
			terminalGroups[0][0].Token.Index.ShouldBe(23);
			terminalGroups[0][0].Token.Value.ShouldBe("DUAL");

			terminalGroups[1].Count.ShouldBe(1);
			terminalGroups[1][0].Token.Index.ShouldBe(38);
			terminalGroups[1][0].Token.Value.ShouldBe("DUAL");
		}

		[Test(Description = @"")]
		public void TestCommonTableExpressionColumnNameList()
		{
			const string query1 = @"WITH GENERATOR(C1, C2, C3, C4) AS (SELECT 1, DUAL.*, 3, DUMMY FROM DUAL) SELECT C1, C2, C3, C4 FROM GENERATOR";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var functionReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			functionReferences.Length.ShouldBe(3);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithQuotedDatabaseLinkName()
		{
			const string query1 = @"SELECT * FROM DUAL@""SQLSERVERDB.STOCKHOLM.CINT.COM""";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestAsteriskNotRedundantInCorrelatedSubquery()
		{
			const string query1 = @"SELECT * FROM SELECTION WHERE SELECTIONNAME IN (SELECT * FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantCommonTableExpressionWithConcatenatedQueryBlock()
		{
			const string query1 =
@"WITH CTE(C1, C2) AS (
	SELECT 1, 2 FROM DUAL UNION ALL
	SELECT 1, 2 FROM DUAL
)
SELECT * FROM CTE";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestRedundantDoubleDefinedCommonTableExpression()
		{
			const string query1 = @"WITH CTE AS (SELECT 1 C1 FROM DUAL), CTE AS (SELECT 1 C2 FROM DUAL) SELECT * FROM CTE";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var redundantTerminals = semanticModel.RedundantSymbolGroups.SelectMany(g => g).OrderBy(t => t.SourcePosition.IndexStart).ToArray();
			redundantTerminals.Length.ShouldBe(10);
		}

		[Test(Description = @"")]
		public void TestFullyQualifiedTableOverDatabaseLink()
		{
			const string query1 = @"SELECT * FROM HUSQVIK.SELECTION@HQ_PDB_LOOPBACK";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var objectReferences = semanticModel.QueryBlocks.Single().ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(1);
			objectReferences[0].SchemaObject.ShouldBe(null);
			objectReferences[0].DatabaseLink.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithSimpleDatabaseLinkIncludingInstanceName()
		{
			const string query1 = @"SELECT * FROM DUAL, DUAL@""HQ_PDB@LOOPBACK""";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var objectReferences = semanticModel.QueryBlocks.Single().ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithDateLiteralWithInvalidQuotedString()
		{
			const string query1 = @"SELECT DATE q'2014-10-04' FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithTimestampLiteralWithInvalidQuotedString()
		{
			const string query1 = @"SELECT TIMESTAMP q'2014-10-04' FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhenTypingUpdateSubqueryStatement()
		{
			const string query1 = @"UPDATE (S)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			Assert.DoesNotThrow(() => OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel));
		}

		[Test(Description = @"")]
		public void TestModelBuildWhenTypingInlineViewInOuterJoinClause()
		{
			const string query1 =
@"SELECT
    *
FROM
    DUAL
    LEFT JOIN (
        SELEC DUMMY FROM DUAL
    ) TMP
    ON DUAL.DUMMY = TMP.DUMMY";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			Assert.DoesNotThrow(() => OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel));
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestModelBuildWithScalarSubQueryAsPlSqlParameter()
		{
			const string query1 =
@"BEGIN
	DBMS_OUTPUT.PUT_VALUE((SELECT COUNT(*) FROM DUAL));
END;";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			Assert.DoesNotThrow(() => OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel));
		}

		[Test(Description = @"")]
		public void TestModelBuildWithUnfinishedSqlModelMeasure()
		{
			const string query1 = @"SELECT * FROM (SELECT * FROM DUAL) MODEL DIMENSION BY (0 C1) MEASURES (0 C2, , 0 C3) RULES (C2[ANY] = 0)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhileTypingSchemaQualifiedObjectWithinUpdateStatement()
		{
			const string query1 = @"UPDATE HUSQVIK.";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);
			semanticModel.MainQueryBlock.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhenSubqueryWrappedInParenthesis()
		{
			const string query1 = @"WITH SOURCE_DATA AS (SELECT DUMMY FROM DUAL) (SELECT DUMMY FROM SOURCE_DATA)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);
			semanticModel.QueryBlocks.Count.ShouldBe(2);
		}

		[Test(Description = @"")]
		public void TestStoredProcedureIsIgnoredInSql()
		{
			const string query1 = @"SELECT SQLPAD.SQLPAD_PROCEDURE() FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var programReferences = semanticModel.QueryBlocks.Single().AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(1);
			programReferences[0].Metadata.ShouldBe(null);
			programReferences[0].SchemaObject.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestSpecificGrammarFunctionInOrderByClause()
		{
			const string query1 = @"SELECT NULL FROM DUAL ORDER BY COUNT(*)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			var objectReferences = queryBlock.ObjectReferences.ToArray();
			objectReferences.Length.ShouldBe(2);
			objectReferences[0].RootNode.ShouldNotBe(null);
			objectReferences[0].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.Table);
			objectReferences[0].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.ObjectAlias);
			objectReferences[0].RootNode.LastTerminalNode.Token.Value.ShouldBe("T1");
			objectReferences[0].ShouldBeTypeOf<OracleTableCollectionReference>();
			var tableCollectionReference = (OracleTableCollectionReference)objectReferences[0];
			tableCollectionReference.RowSourceReference.ShouldBeTypeOf<OracleProgramReference>();
			objectReferences[1].RootNode.ShouldNotBe(null);
			objectReferences[1].RootNode.FirstTerminalNode.Id.ShouldBe(Terminals.Table);
			objectReferences[1].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.ObjectAlias);
			objectReferences[1].RootNode.LastTerminalNode.Token.Value.ShouldBe("T2");
			objectReferences[1].ShouldBeTypeOf<OracleTableCollectionReference>();
			tableCollectionReference = (OracleTableCollectionReference)objectReferences[1];
			tableCollectionReference.RowSourceReference.ShouldBeTypeOf<OracleTypeReference>();

			var typeReferences = queryBlock.AllTypeReferences.ToArray();
			typeReferences.Length.ShouldBe(1);
			typeReferences[0].RootNode.ShouldNotBe(null);
			var terminals = typeReferences[0].RootNode.Terminals.ToArray();
			terminals[0].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[0].Token.Value.ShouldBe("SYS");
			terminals[2].Id.ShouldBe(Terminals.Identifier);
			terminals[2].Token.Value.ShouldBe("ODCIRAWLIST");
			typeReferences[0].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.RightParenthesis);
			typeReferences[0].ParameterListNode.ShouldNotBe(null);
			typeReferences[0].ParameterReferences.ShouldNotBe(null);
			typeReferences[0].ParameterReferences.Count.ShouldBe(3);

			var programReferences = queryBlock.AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(4);
			programReferences[0].RootNode.ShouldNotBe(null);
			terminals = programReferences[0].RootNode.Terminals.ToArray();
			terminals[0].Id.ShouldBe(Terminals.ObjectIdentifier);
			terminals[0].Token.Value.ShouldBe("DBMS_XPLAN");
			terminals[2].Id.ShouldBe(Terminals.Identifier);
			terminals[2].Token.Value.ShouldBe("DISPLAY_CURSOR");
			programReferences[0].RootNode.LastTerminalNode.Id.ShouldBe(Terminals.RightParenthesis);
			programReferences[0].ParameterListNode.ShouldNotBe(null);
			programReferences[0].ParameterReferences.ShouldNotBe(null);
			programReferences[0].ParameterReferences.Count.ShouldBe(3);
			programReferences[1].Metadata.ShouldNotBe(null);
			programReferences[1].Metadata.Identifier.Name.ShouldBe("\"HEXTORAW\"");
			programReferences[2].Metadata.ShouldNotBe(null);
			programReferences[2].Metadata.Identifier.Name.ShouldBe("\"HEXTORAW\"");
			programReferences[3].Metadata.ShouldNotBe(null);
			programReferences[3].Metadata.Identifier.Name.ShouldBe("\"HEXTORAW\"");

			queryBlock.Columns.Count.ShouldBe(3);
		}

		[Test(Description = @"")]
		public void TestTableCollectionExpressionColumnUsingPackageFunction()
		{
			const string query1 = @"SELECT COLUMN_VALUE FROM TABLE(SQLPAD.PIPELINED_FUNCTION(SYSDATE, SYSDATE))";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.QueryBlocks.Single();
			queryBlock.ObjectReferences.Count.ShouldBe(1);
			var objectReference = queryBlock.ObjectReferences.First();
			objectReference.ShouldBeTypeOf<OracleTableCollectionReference>();
			var tableCollectionReference = (OracleTableCollectionReference)objectReference;
			tableCollectionReference.RowSourceReference.ShouldBeTypeOf<OracleProgramReference>();
			((OracleProgramReference)tableCollectionReference.RowSourceReference).Metadata.ShouldNotBe(null);

			queryBlock.ProgramReferences.Count.ShouldBe(1);
			queryBlock.ProgramReferences.Single().Metadata.ShouldNotBe(null);

			queryBlock.Columns.Count.ShouldBe(1);
			queryBlock.Columns[0].ColumnReferences.Count.ShouldBe(1);
			queryBlock.Columns[0].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestXmlTableReference()
		{
			const string query1 = @"SELECT * FROM XMLTABLE('for $i in $RSS_DATA/rss/channel/item return $i' PASSING HTTPURITYPE('http://servis.idnes.cz/rss.asp?c=zpravodaj').GETXML() AS RSS_DATA COLUMNS SEQ# FOR ORDINALITY, TITLE VARCHAR2(4000) PATH 'title', DESCRIPTION CLOB PATH 'description') T";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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
			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestCommonTableExpressionDataTypePropagationToInnerSubquery()
		{
			const string query1 = @"WITH SOURCE_DATA AS (SELECT DUMMY FROM DUAL) SELECT NULL FROM (SELECT DUMMY FROM SOURCE_DATA)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(3);

			var queryBlock = semanticModel.QueryBlocks.OrderByDescending(qb => qb.RootNode.Level).First();
			var columns = queryBlock.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].ColumnDescription.Name.ShouldBe("\"DUMMY\"");
			columns[0].ColumnDescription.Nullable.ShouldBe(false);
			columns[0].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(1 BYTE)");
		}
		
		[Test(Description = @"")]
		public void TestXmlTableReferenceWithoutColumnListSpecification()
		{
			const string query1 = @"SELECT * FROM XMLTABLE('for $i in $RSS_DATA/rss/channel/item return $i' PASSING HTTPURITYPE('http://servis.idnes.cz/rss.asp?c=zpravodaj').GETXML() AS RSS_DATA)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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
			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestSqlModelDimensionReferenceDataTypeFromInlineView()
		{
			const string query1 =
@"SELECT
	DUMMY, P
FROM
	(SELECT DUMMY FROM DUAL)
	MODEL
		PARTITION BY (DUMMY P)
		DIMENSION BY (DUMMY)
		MEASURES (0 MEASURE)
		RULES (
			MEASURE[ANY] = 0
	    )";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ModelReference.SourceReferenceContainer.ColumnReferences.Count.ShouldBe(2);
			queryBlock.ModelReference.SourceReferenceContainer.ColumnReferences[0].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(1 BYTE)");
			queryBlock.ModelReference.SourceReferenceContainer.ColumnReferences[1].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(1 BYTE)");

			queryBlock.Columns.Count.ShouldBe(2);
			queryBlock.Columns[0].NormalizedName.ShouldBe("\"DUMMY\"");
			queryBlock.Columns[0].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(1 BYTE)");
			queryBlock.Columns[1].NormalizedName.ShouldBe("\"P\"");
			queryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(1 BYTE)");
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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
		public void TestSqlModelOutputColumnTypeDefinedByCast()
		{
			const string query1 =
@"SELECT
	M_1
FROM
	DUAL
	MODEL
		DIMENSION BY (0 AS KEY)
		MEASURES (
			CAST(NULL AS VARCHAR2(4000)) AS M_1
		)
		RULES UPDATE (
			M_1[ANY] = 'x'
		)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.Columns.Count.ShouldBe(1);
			queryBlock.Columns[0].NormalizedName.ShouldBe("\"M_1\"");
			queryBlock.Columns[0].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(4000)");
		}

		[Test(Description = @"")]
		public void TestRecursiveCommonTableExpression()
		{
			const string query1 = @"WITH GENERATOR(VAL) AS (SELECT 1 FROM DUAL UNION ALL SELECT VAL + 1 FROM GENERATOR WHERE VAL <= 10) SELECT VAL FROM	GENERATOR";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var mainObjectReference = semanticModel.MainObjectReferenceContainer.MainObjectReference;
			mainObjectReference.ShouldNotBe(null);

			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(2);
			var sourceColumn = semanticModel.MainObjectReferenceContainer.ColumnReferences[1];
			sourceColumn.ColumnNodeColumnReferences.Count.ShouldBe(1);
			sourceColumn.ObjectNodeObjectReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestWithDefinedFunctionReferences()
		{
			const string query1 =
@"WITH
	FUNCTION F1(p IN NUMBER) RETURN NUMBER AS BEGIN RETURN DBMS_RANDOM.VALUE; END;
	FUNCTION COUNT(p IN NUMBER) RETURN NUMBER AS BEGIN RETURN DBMS_RANDOM.VALUE; END;
SELECT F1(ROWNUM) RESULT1, COUNT(ROWNUM), F2(ROWNUM), T.* FROM (
	WITH
		FUNCTION F2(p IN NUMBER) RETURN NUMBER AS BEGIN RETURN F1(p); END;
		FUNCTION ""f 3""(p IN NUMBER) RETURN NUMBER AS BEGIN RETURN F1(p); END;
	SELECT F2(NULL) RESULT2, F1(NULL), (select ""f 3""(null) from dual) FROM DUAL
) T";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(3);
			var queryBlocks = semanticModel.QueryBlocks.OrderBy(qb => qb.RootNode.SourcePosition.IndexStart).ToArray();

			queryBlocks[0].Type.ShouldBe(QueryBlockType.Normal);
			queryBlocks[0].Alias.ShouldBe(null);
			var programReferences = queryBlocks[0].AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(3);
			programReferences[0].Metadata.ShouldNotBe(null);
			programReferences[1].Metadata.ShouldNotBe(null);
			programReferences[1].Metadata.Identifier.Name.ShouldBe("\"COUNT\"");
			programReferences[1].Metadata.Type.ShouldBe(ProgramType.Function);
			programReferences[1].Metadata.IsAggregate.ShouldBe(true);
			programReferences[2].Metadata.ShouldBe(null);

			queryBlocks[1].Type.ShouldBe(QueryBlockType.Normal);
			queryBlocks[1].Alias.ShouldBe("T");
			programReferences = queryBlocks[1].AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(2);
			programReferences[0].Metadata.ShouldNotBe(null);
			programReferences[1].Metadata.ShouldNotBe(null);

			queryBlocks[2].Type.ShouldBe(QueryBlockType.ScalarSubquery);
			queryBlocks[2].Alias.ShouldBe(null);
			programReferences = queryBlocks[2].AllProgramReferences.ToArray();
			programReferences.Length.ShouldBe(1);
			programReferences[0].Metadata.ShouldNotBe(null);
			programReferences[0].Metadata.Identifier.Name.ShouldBe("\"f 3\"");
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ShouldNotBe(null);

			var columnReferences = queryBlock.ColumnReferences.Where(c => c.Placement == StatementPlacement.TableReference).ToArray();
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

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ShouldNotBe(null);

			var columnReferences = queryBlock.ColumnReferences.Where(c => c.Placement == StatementPlacement.TableReference).ToArray();
			columnReferences.Length.ShouldBe(2);
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnReferencesInConnectByClause()
		{
			const string query1 = @"SELECT NULL FROM SELECTION CONNECT BY SELECTION_ID > PROJECT_ID";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

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
		public void TestColumnReferencesInRecursiveSearchClauseAndInSubquery()
		{
			const string query1 =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 5
)
SEARCH DEPTH FIRST BY VAL SET SEQ#
SELECT * FROM CTE";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);
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

		[Test(Description = @"")]
		public void TestOuterApplyColumnReference()
		{
			const string query1 = @"SELECT DUMMY C1, C2 FROM DUAL T1 OUTER APPLY (SELECT DUMMY C2 FROM DUAL T2 WHERE DUMMY <> T1.DUMMY) T2";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var queryBlock = semanticModel.MainQueryBlock;
			queryBlock.ShouldNotBe(null);

			queryBlock.ObjectReferences.Count.ShouldBe(2);
			var dualTableReference = queryBlock.ObjectReferences.First();
			var crossApliedTableReference = queryBlock.ObjectReferences.Last();
			crossApliedTableReference.QueryBlocks.Count.ShouldBe(1);
			var crossApliedQueryBlock = crossApliedTableReference.QueryBlocks.Single();
			crossApliedTableReference.IsOuterJoined.ShouldBe(true);
			crossApliedQueryBlock.ColumnReferences.Count.ShouldBe(2);
			crossApliedQueryBlock.ColumnReferences[1].FullyQualifiedObjectName.ShouldBe(OracleObjectIdentifier.Create(null, "T1"));
			crossApliedQueryBlock.ColumnReferences[1].ObjectNodeObjectReferences.Count.ShouldBe(1);
			crossApliedQueryBlock.ColumnReferences[1].ObjectNodeObjectReferences.Single().ShouldBe(dualTableReference);
			crossApliedQueryBlock.ColumnReferences[1].ColumnNodeObjectReferences.Count.ShouldBe(1);
			crossApliedQueryBlock.ColumnReferences[1].ColumnNodeObjectReferences.Single().ShouldBe(dualTableReference);
			crossApliedQueryBlock.ColumnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestInsertIntoSelfReferenceWithinValuesClause()
		{
			const string query1 = @"INSERT INTO PROJECT (PROJECT_ID) VALUES (PROJECT_ID)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.InsertTargets.Count.ShouldBe(1);
			var insertTarget = semanticModel.InsertTargets.Single();
			insertTarget.ColumnReferences.Count.ShouldBe(2);
			var columnReferences = insertTarget.ColumnReferences.OrderBy(c => c.ColumnNode.SourcePosition.IndexStart).ToArray();
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestPartitionAndSubpartitionReferences()
		{
			const string sqlText = @"SELECT * FROM INVOICES PARTITION (P2015), INVOICES PARTITION (P2016), INVOICES SUBPARTITION (P2015_PRIVATE), INVOICES SUBPARTITION (P2016_ENTERPRISE), INVOICES PARTITION (P2015_PRIVATE), INVOICES SUBPARTITION (P2015)";
			var statement = (OracleStatement)Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(sqlText, statement, TestFixture.DatabaseModel);
			semanticModel.QueryBlocks.Count.ShouldBe(1);
			var queryBlock = semanticModel.MainQueryBlock;

			queryBlock.ObjectReferences.Count.ShouldBe(6);
			var objectReferences = queryBlock.ObjectReferences.ToList();
			objectReferences.ForEach(r => r.PartitionReference.ShouldNotBe(null));
			
			objectReferences[0].PartitionReference.Partition.ShouldNotBe(null);
			objectReferences[0].PartitionReference.DataObjectReference.ShouldNotBe(null);
			objectReferences[0].PartitionReference.Partition.Name.ShouldBe("\"P2015\"");
			objectReferences[0].PartitionReference.RootNode.ShouldNotBe(null);
			objectReferences[0].PartitionReference.RootNode.FirstTerminalNode.Token.Value.ShouldBe("PARTITION");
			objectReferences[0].PartitionReference.RootNode.LastTerminalNode.Token.Value.ShouldBe(")");
			objectReferences[1].PartitionReference.Partition.ShouldBe(null);
			objectReferences[2].PartitionReference.Partition.ShouldNotBe(null);
			objectReferences[3].PartitionReference.Partition.ShouldBe(null);
			objectReferences[4].PartitionReference.Partition.ShouldBe(null);
			objectReferences[5].PartitionReference.Partition.ShouldBe(null);
		}

		[Test(Description = @"")]
		public void TestMergeSemantics()
		{
			const string sqlText =
@"MERGE INTO SELECTION TARGET
USING SELECTION SOURCE
ON (TARGET.SELECTION_ID = SOURCE.SELECTION_ID AND SOURCE.DUMMY = 0 AND DUMMY.DUMMY = DBMS_RANDOM.NORMAL)
WHEN MATCHED THEN
	UPDATE SET SELECTION_ID = CASE WHEN SOURCE.SELECTION_ID = TARGET.SELECTION_ID AND SOURCE.DUMMY = DBMS_RANDOM.NORMAL AND DUMMY.DUMMY = 0 THEN NULL END
		WHERE SOURCE.SELECTION_ID = TARGET.SELECTION_ID AND SOURCE.DUMMY = 0 AND DUMMY.DUMMY = DBMS_RANDOM.NORMAL
	DELETE
		WHERE SOURCE.SELECTION_ID <> TARGET.SELECTION_ID AND SOURCE.DUMMY = 0 AND DUMMY.DUMMY = DBMS_RANDOM.NORMAL
WHEN NOT MATCHED THEN
	INSERT (SELECTION_ID, NAME) VALUES (TEST_SEQ.NEXTVAL, DBMS_RANDOM.STRING('X', 50))
		WHERE SOURCE.SELECTION_ID = TARGET.SELECTION_ID AND SOURCE.DUMMY = 0 AND DUMMY.DUMMY = 0
LOG ERRORS INTO ERRORLOG ('Statement identifier') REJECT LIMIT UNLIMITED";
			
			var statement = (OracleStatement)Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(sqlText, statement, TestFixture.DatabaseModel);
			semanticModel.MainObjectReferenceContainer.ObjectReferences.Count.ShouldBe(3);
			var objectReferences = semanticModel.MainObjectReferenceContainer.ObjectReferences.ToArray();
			objectReferences[0].SchemaObject.ShouldNotBe(null);
			objectReferences[1].SchemaObject.ShouldNotBe(null);
			objectReferences[2].SchemaObject.ShouldBe(null);

			semanticModel.MainObjectReferenceContainer.ColumnReferences.Count.ShouldBe(24);
			var columnReferences = semanticModel.MainObjectReferenceContainer.ColumnReferences.ToList();
			columnReferences[0].ObjectNodeObjectReferences.Count.ShouldBe(1);
			columnReferences[0].ColumnNodeObjectReferences.Count.ShouldBe(1);
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[0].ObjectNode.Token.Value.ShouldBe("TARGET");
			columnReferences[0].ColumnNode.Token.Value.ShouldBe("SELECTION_ID");
			columnReferences[1].ObjectNodeObjectReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeObjectReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ObjectNode.Token.Value.ShouldBe("SOURCE");
			columnReferences[1].ColumnNode.Token.Value.ShouldBe("SELECTION_ID");
			columnReferences[2].ObjectNodeObjectReferences.Count.ShouldBe(1);
			columnReferences[2].ColumnNodeObjectReferences.Count.ShouldBe(0);
			columnReferences[2].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[2].ObjectNode.Token.Value.ShouldBe("SOURCE");
			columnReferences[2].ColumnNode.Token.Value.ShouldBe("DUMMY");
			columnReferences[3].ObjectNodeObjectReferences.Count.ShouldBe(0);
			columnReferences[3].ColumnNodeObjectReferences.Count.ShouldBe(0);
			columnReferences[3].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[3].ObjectNode.Token.Value.ShouldBe("DUMMY");
			columnReferences[3].ColumnNode.Token.Value.ShouldBe("DUMMY");
			columnReferences[21].ObjectNodeObjectReferences.Count.ShouldBe(1);
			columnReferences[21].ColumnNodeObjectReferences.Count.ShouldBe(1);
			columnReferences[21].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[21].ObjectNode.Token.Value.ShouldBe("TARGET");
			columnReferences[21].ColumnNode.Token.Value.ShouldBe("SELECTION_ID");
			columnReferences[23].ObjectNodeObjectReferences.Count.ShouldBe(0);
			columnReferences[23].ColumnNodeObjectReferences.Count.ShouldBe(0);
			columnReferences[23].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[23].ObjectNode.Token.Value.ShouldBe("DUMMY");
			columnReferences[23].ColumnNode.Token.Value.ShouldBe("DUMMY");

			semanticModel.MainObjectReferenceContainer.ProgramReferences.Count.ShouldBe(5);
			var programReferences = semanticModel.MainObjectReferenceContainer.ProgramReferences.ToList();
			programReferences.ForEach(p => p.Metadata.ShouldNotBe(null));

			semanticModel.MainObjectReferenceContainer.SequenceReferences.Count.ShouldBe(1);
			var sequenceReferences = semanticModel.MainObjectReferenceContainer.SequenceReferences.ToArray();
			sequenceReferences[0].SchemaObject.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestErrorLogTableReferencesInMultiTableInsert()
		{
			const string sqlText =
@"INSERT ALL
    WHEN 1 = 1 THEN
        INTO T1(ID) VALUES (VAL)
			LOG ERRORS INTO T1_ERRLOG ('Statement identifier')
    WHEN VAL > 5 THEN
        INTO T2(ID) VALUES (VAL)
			LOG ERRORS INTO T2_ERRLOG ('Statement identifier')
        INTO T3(ID) VALUES (VAL)
			LOG ERRORS INTO T3_ERRLOG ('Statement identifier')
    WHEN VAL IN (5, 6) THEN
        INTO T4 (ID) VALUES (VAL)
			LOG ERRORS INTO T4_ERRLOG ('Statement identifier')
SELECT LEVEL VAL FROM DUAL CONNECT BY LEVEL <= 10";

			var statement = (OracleStatement)Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(sqlText, statement, TestFixture.DatabaseModel);
			semanticModel.InsertTargets.Count.ShouldBe(4);
			var insertTargets = semanticModel.InsertTargets.ToArray();
			insertTargets[0].ObjectReferences.Count.ShouldBe(2);
			insertTargets[0].ColumnReferences.Count.ShouldBe(2);
			insertTargets[1].ObjectReferences.Count.ShouldBe(2);
			insertTargets[1].ColumnReferences.Count.ShouldBe(3);
			insertTargets[2].ObjectReferences.Count.ShouldBe(2);
			insertTargets[2].ColumnReferences.Count.ShouldBe(2);
			insertTargets[3].ObjectReferences.Count.ShouldBe(2);
			insertTargets[3].ColumnReferences.Count.ShouldBe(3);
		}

		[Test(Description = @"")]
		public void TestFlashbackAsOfPseudocolumnReferences()
		{
			const string query1 = @"SELECT SELECTION_ID, ORA_ROWSCN, VERSIONS_STARTTIME, VERSIONS_ENDTIME, VERSIONS_STARTSCN, VERSIONS_ENDSCN, VERSIONS_OPERATION, VERSIONS_XID FROM SELECTION AS OF TIMESTAMP TIMESTAMP'2015-05-08 00:00:00'";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.ToArray();
			columnReferences.Length.ShouldBe(8);

			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[2].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[3].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[4].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[5].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[6].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[7].ColumnNodeColumnReferences.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestFlashbackVersionsPseudocolumnReferences()
		{
			const string query1 = @"SELECT SELECTION_ID, ORA_ROWSCN, VERSIONS_STARTTIME, VERSIONS_ENDTIME, VERSIONS_STARTSCN, VERSIONS_ENDSCN, VERSIONS_OPERATION, VERSIONS_XID FROM SELECTION VERSIONS BETWEEN SCN MINVALUE AND MAXVALUE AS OF TIMESTAMP TIMESTAMP'2015-05-08 00:00:00'";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columnReferences = semanticModel.QueryBlocks.Single().AllColumnReferences.ToArray();
			columnReferences.Length.ShouldBe(8);

			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences[2].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[3].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[4].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[5].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[6].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[7].ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestColumnReferenceInTableClause()
		{
			const string query1 = @"SELECT (SELECT LISTAGG(COLUMN_VALUE, ', ') WITHIN GROUP (ORDER BY ROWNUM) FROM TABLE(SELECTION_NAMES)) FROM (SELECT CAST(COLLECT(SELECTIONNAME) AS SYS.ODCIRAWLIST) SELECTION_NAMES FROM SELECTION WHERE ROWNUM <= 5)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(3);

			var queryBlock = semanticModel.QueryBlocks.Single(qb => qb.Type == QueryBlockType.ScalarSubquery);
			var columnReferences = queryBlock.AllColumnReferences.ToList();
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[0].ColumnDescription.ShouldNotBe(null);
			columnReferences[0].ColumnDescription.FullTypeName.ShouldBe("RAW(2000)");
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnDescription.ShouldNotBe(null);
			columnReferences[1].ColumnDescription.FullTypeName.ShouldBe("SYS.ODCIRAWLIST");
		}
		
		[Test(Description = @"")]
		public void TestColumnReferenceInTableClauseUsingDynamicCollectionType()
		{
			const string query1 = @"SELECT (SELECT LISTAGG(COLUMN_VALUE, ', ') WITHIN GROUP (ORDER BY ROWNUM) FROM TABLE(SELECTION_NAMES)) FROM (SELECT COLLECT(SELECTIONNAME) SELECTION_NAMES FROM SELECTION WHERE ROWNUM <= 5)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(3);

			var queryBlock = semanticModel.QueryBlocks.Single(qb => qb.Type == QueryBlockType.ScalarSubquery);
			var columnReferences = queryBlock.AllColumnReferences.ToList();
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[0].ColumnDescription.ShouldNotBe(null);
			columnReferences[0].ColumnDescription.FullTypeName.ShouldBe(String.Empty);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnDescription.ShouldNotBe(null);
			columnReferences[1].ColumnDescription.DataType.ShouldBe(OracleDataType.DynamicCollectionType);
		}
		
		[Test(Description = @"")]
		public void TestModelBuildWhenXmlTableCombinedWithFlashbackAndPivotClauses()
		{
			const string query1 =
@"SELECT
	*
FROM ((
    XMLTABLE(
        'let $language := $RSS_DATA/rss/channel/language
         for $item in $RSS_DATA/rss/channel/item
         return
         	<data>
         		{$language}
         		{$item/category}
         		{$item/pubDate}
         		{$item/title}
         		{$item/description}
         		{$item/link}
         	</data>'
        PASSING
            HTTPURITYPE('http://www.novinky.cz/rss2/vase-zpravy').GETXML() AS RSS_DATA
        COLUMNS
        	LANGUAGE VARCHAR2(10) PATH 'language'
	) AS RSSDATA
	AS OF TIMESTAMP SYSDATE)
	PIVOT (COUNT(RSSDATA.LANGUAGE) FOR(LANGUAGE) IN ('cs')) RESULT)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestPivotTableColumnReference()
		{
			const string query1 =
@"SELECT
	INVITES_COUNT,
	EXTERNAL_COUNT,
	REUSE_COUNT,
	NONE_COUNT,
	DIRECT_COUNT,
	INVITES_MAX,
	EXTERNAL_MAX,
	REUSE_MAX,
	NONE_MAX,
	DIRECT_MAX,
	DUMMY,
	SUPPLYCHANNEL
FROM (
		(SELECT MOD(LEVEL, 5) SUPPLYCHANNEL, DUMMY FROM DUAL CONNECT BY LEVEL <= 999) SOURCE_DATA
		PIVOT (
			COUNT(SUPPLYCHANNEL) AS COUNT,
			MAX(SOURCE_DATA.SUPPLYCHANNEL) AS MAX
			FOR (SUPPLYCHANNEL)
				IN (0 AS INVITES, 1 AS EXTERNAL, 2 AS REUSE, 3 AS NONE, 4 AS DIRECT)
		) PIVOT_TABLE
	)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);

			var columnReferences = semanticModel.MainQueryBlock.AllColumnReferences.ToList();
			columnReferences.Count.ShouldBe(15);

			var unknownColumn = columnReferences[11];
			unknownColumn.Name.ShouldBe("SUPPLYCHANNEL");
			unknownColumn.SelectListColumn.ShouldNotBe(null);
			unknownColumn.ColumnNodeColumnReferences.Count.ShouldBe(0);
			columnReferences.Where(c => c != unknownColumn).ToList().ForEach(c => c.ColumnNodeColumnReferences.Count.ShouldBe(1));

			semanticModel.MainQueryBlock.ObjectReferences.Count.ShouldBe(1);
			var pivotTableReference = (OraclePivotTableReference)semanticModel.MainQueryBlock.ObjectReferences.Single();
			pivotTableReference.FullyQualifiedObjectName.Name.ShouldBe("PIVOT_TABLE");
			pivotTableReference.SourceReference.Type.ShouldBe(ReferenceType.InlineView);
			pivotTableReference.SourceReference.FullyQualifiedObjectName.Name.ShouldBe("SOURCE_DATA");
			var pivotSourceColumnReferences = pivotTableReference.SourceReferenceContainer.ColumnReferences.ToList();
			pivotSourceColumnReferences.Count.ShouldBe(3);
			pivotSourceColumnReferences.ForEach(c => c.Name.ShouldBe("SUPPLYCHANNEL"));
			pivotSourceColumnReferences.ForEach(c => c.ColumnNodeColumnReferences.Count.ShouldBe(1));
			
			var programReferences = pivotTableReference.SourceReferenceContainer.ProgramReferences.ToArray();
			programReferences.Length.ShouldBe(2);
			programReferences[0].Name.ShouldBe("COUNT");
			programReferences[0].Metadata.ShouldNotBe(null);
			programReferences[1].Name.ShouldBe("MAX");
			programReferences[1].Metadata.ShouldNotBe(null);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestUnpivotTableColumnReference()
		{
			const string query1 =
@"SELECT STATUS, SUPPLY_CHANNEL_PRINT, SELECTION_COUNT, INVITES_COUNT, EXTERNAL_COUNT, REUSE_COUNT, NONE_COUNT, DIRECT_COUNT
FROM (
	SELECT 1 STATUS, 37 INVITES_COUNT, 0 EXTERNAL_COUNT, 0 REUSE_COUNT, 0 NONE_COUNT, 0 DIRECT_COUNT FROM DUAL UNION ALL
	SELECT 2, 1665171, 82, 853, 96, 93 FROM DUAL UNION ALL
	SELECT 5, 9000, 0, 0, 0, 0 FROM DUAL UNION ALL
	SELECT 3, 729779, 1200, 39385, 2525, 788 FROM DUAL UNION ALL
	SELECT 0, 10876, 0, 0, 0, 0 FROM DUAL) SOURCE_DATA
	UNPIVOT INCLUDE NULLS (
		SELECTION_COUNT 
		FOR SUPPLY_CHANNEL_PRINT IN (
			UNDEFINED_COUNT AS 'Undefined',
			INVITES_COUNT AS 'Invite',
			EXTERNAL_COUNT AS 'External',
			REUSE_COUNT AS 'Reuse',
			NONE_COUNT AS 'None',
			DIRECT_COUNT AS 'Direct')
	) UNPIVOT_TABLE";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(6);

			var columnReferences = semanticModel.MainQueryBlock.AllColumnReferences.ToList();
			columnReferences.Count.ShouldBe(14);

			columnReferences
				.ForEach(c =>
				{
					var index = columnReferences.IndexOf(c);
					var expectedReferenceCount = index < 3 || index >= 9 ? 1 : 0;
					c.ColumnNodeColumnReferences.Count.ShouldBe(expectedReferenceCount);

					if (index < 8)
					{
						c.SelectListColumn.ShouldNotBe(null);
					}
					else
					{
						c.SelectListColumn.ShouldBe(null);
					}
				});

			semanticModel.MainQueryBlock.Columns.Count.ShouldBe(8);
			semanticModel.MainQueryBlock.Columns[1].NormalizedName.ShouldBe("\"SUPPLY_CHANNEL_PRINT\"");
			semanticModel.MainQueryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(9)");

			semanticModel.MainQueryBlock.ObjectReferences.Count.ShouldBe(1);
			var unpivotTableReference = (OraclePivotTableReference)semanticModel.MainQueryBlock.ObjectReferences.Single();
			unpivotTableReference.FullyQualifiedObjectName.Name.ShouldBe("UNPIVOT_TABLE");
			unpivotTableReference.SourceReference.Type.ShouldBe(ReferenceType.InlineView);
			unpivotTableReference.SourceReference.FullyQualifiedObjectName.Name.ShouldBe("SOURCE_DATA");
			
			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestUnpivotTableColumnNullability()
		{
			const string query1 =
@"SELECT
	*
FROM
	dual
	UNPIVOT (
		x FOR c IN (
			dummy AS NULL,
			dummy AS 'X'
		)
	)
WHERE c IS NULL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainQueryBlock.ObjectReferences.Count.ShouldBe(1);
			var unpivotTableReference = (OraclePivotTableReference)semanticModel.MainQueryBlock.ObjectReferences.Single();
			unpivotTableReference.Columns.Count.ShouldBe(2);
			unpivotTableReference.Columns[0].Name.ShouldBe("\"C\"");
			unpivotTableReference.Columns[0].Nullable.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhileTypingIncludeNullsInUnpivotClause()
		{
			const string query1 =
@"SELECT
	*
FROM
	dual
	UNPIVOT INCLUDE N (
		x FOR c IN (
			dummy AS 'X'
		)
	)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();

			Assert.DoesNotThrow(() => OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel));
		}

		[Test(Description = @"")]
		public void TestUnpivotTableWithAllowedNulls()
		{
			const string query1 =
@"SELECT
	*
FROM
	dual
	UNPIVOT INCLUDE NULLS (
		x FOR c IN (
			dummy AS 'X'
		)
	)
WHERE c IS NULL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainQueryBlock.ObjectReferences.Count.ShouldBe(1);
			var unpivotTableReference = (OraclePivotTableReference)semanticModel.MainQueryBlock.ObjectReferences.Single();
			unpivotTableReference.Columns.Count.ShouldBe(2);
			unpivotTableReference.Columns[1].Name.ShouldBe("\"X\"");
			unpivotTableReference.Columns[1].Nullable.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestModelBuildWhenTypingUnpivotClause()
		{
			const string query1 =
@"SELECT
	*
FROM (
	SELECT 0 VAL1, 0 VAL2 FROM DUAL)
	UNPIVOT INCLUDE NULLS (
		VAL 
	FOR PLACEHOLDER IN (
		VAL1 AS 0,
		VAL2 AS )
	)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			Assert.DoesNotThrow(() => OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel));
		}

		[Test(Description = @"")]
		public void TestAsteriskColumnReferencesWithJoinUsingMultipleColumns()
		{
			const string query1 = @"SELECT * FROM SELECTION T1 JOIN SELECTION T2 USING (PROJECT_ID, NAME)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columns = semanticModel.MainQueryBlock.Columns;
			columns.Count.ShouldBe(7);
			columns[0].IsAsterisk.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[1].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("T1");
			columns[2].NormalizedName.ShouldBe("\"SELECTION_ID\"");
			columns[2].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("T1");
			columns[3].NormalizedName.ShouldBe("\"PROJECT_ID\"");
			columns[3].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("T1");
			columns[4].NormalizedName.ShouldBe("\"NAME\"");
			columns[4].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("T1");
			columns[5].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[5].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("T2");
			columns[6].NormalizedName.ShouldBe("\"SELECTION_ID\"");
			columns[6].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("T2");

			var columnReferences = semanticModel.MainQueryBlock.ColumnReferences;
			columnReferences.Count.ShouldBe(2);
			columnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columnReferences[1].ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestAsteriskColumnReferencesUsingNaturalJoin()
		{
			const string query1 = @"SELECT * FROM SELECTION S1 NATURAL JOIN SELECTION S2";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columns = semanticModel.MainQueryBlock.Columns;
			columns.Count.ShouldBe(5);
			columns[0].IsAsterisk.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[1].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columns[1].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("S1");
			columns[2].NormalizedName.ShouldBe("\"SELECTION_ID\"");
			columns[2].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columns[2].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("S1");
			columns[3].NormalizedName.ShouldBe("\"PROJECT_ID\"");
			columns[3].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columns[3].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("S1");
			columns[4].NormalizedName.ShouldBe("\"NAME\"");
			columns[4].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columns[4].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("S1");
		}

		[Test(Description = @"")]
		public void TestDirectColumnReferenceWithJoinUsingMultipleColumns()
		{
			const string query1 = @"SELECT RESPONDENTBUCKET_ID, SELECTION_ID, PROJECT_ID, NAME FROM SELECTION JOIN RESPONDENTBUCKET USING (PROJECT_ID, RESPONDENTBUCKET_ID)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var columns = semanticModel.MainQueryBlock.Columns;
			columns.Count.ShouldBe(4);
			columns[0].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[0].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columns[0].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("SELECTION");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_ID\"");
			columns[1].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columns[1].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("SELECTION");
			columns[2].NormalizedName.ShouldBe("\"PROJECT_ID\"");
			columns[2].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			columns[2].ColumnReferences[0].ValidObjectReference.FullyQualifiedObjectName.Name.ShouldBe("SELECTION");
			columns[3].NormalizedName.ShouldBe("\"NAME\"");
			columns[3].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(2);
			columns[3].ColumnReferences[0].ValidObjectReference.ShouldBe(null);
        }

		[Test(Description = @"")]
		public void TestModelBuildWithPivotedInlineViewWithAsterisk()
		{
			const string query1 =
@"SELECT
	*
FROM (
		(SELECT * FROM SELECTION)
		PIVOT (
			COUNT(PROJECT_ID) PROJECT_COUNT
			FOR (PROJECT_ID)
				IN (0)
		) PIVOT_TABLE
	)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);

			semanticModel.MainQueryBlock.Columns.Count.ShouldBe(5);
			semanticModel.MainQueryBlock.Columns[0].IsAsterisk.ShouldBe(true);
			semanticModel.MainQueryBlock.Columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			semanticModel.MainQueryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("NUMBER(9)");
			semanticModel.MainQueryBlock.Columns[2].NormalizedName.ShouldBe("\"SELECTION_ID\"");
			semanticModel.MainQueryBlock.Columns[2].ColumnDescription.FullTypeName.ShouldBe("NUMBER(9)");
			semanticModel.MainQueryBlock.Columns[3].NormalizedName.ShouldBe("\"NAME\"");
			semanticModel.MainQueryBlock.Columns[3].ColumnDescription.FullTypeName.ShouldBe("VARCHAR2(50 BYTE)");
			semanticModel.MainQueryBlock.Columns[4].NormalizedName.ShouldBe("\"0_PROJECT_COUNT\"");
			semanticModel.MainQueryBlock.Columns[4].ColumnDescription.FullTypeName.ShouldBe(String.Empty);
		}

		[Test(Description = @"")]
		public void TestPivotXmlTableReference()
		{
			const string query1 =
@"SELECT
	*
FROM (
	SELECT STATUS ""Long test column name STATUS"", SUPPLYCHANNEL ""Long test column SUPPLYCHANNEL"" FROM SELECTION
	)
	PIVOT XML (
		COUNT(*) AS COUNT
		FOR (""Long test column name STATUS"", ""Long test column SUPPLYCHANNEL"")
		IN (ANY, ANY)
	)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);

			semanticModel.MainQueryBlock.Columns.Count.ShouldBe(2);
			semanticModel.MainQueryBlock.Columns[0].IsAsterisk.ShouldBe(true);
			semanticModel.MainQueryBlock.Columns[1].NormalizedName.ShouldBe("\"Long test column name STAT_XML\"");
			semanticModel.MainQueryBlock.Columns[1].ColumnDescription.FullTypeName.ShouldBe("SYS.XMLTYPE");
		}

		[Test(Description = @"")]
		public void TestOrderBySelectListReferenceWithMultipleSourceTableReferences()
		{
			const string query1 = @"SELECT T1.DUMMY FROM DUAL T1, DUAL T2 ORDER BY DUMMY";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainQueryBlock.ColumnReferences.Count.ShouldBe(1);
			semanticModel.MainQueryBlock.ColumnReferences[0].ColumnNodeObjectReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestColumnReferencesFromTableTypeUsingSynonym()
		{
			const string query1 = @"SELECT COLUMN_VALUE FROM TABLE(RAWLIST('AA', 'FF'))";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.MainQueryBlock.Columns.Count.ShouldBe(1);
			semanticModel.MainQueryBlock.Columns[0].ColumnReferences.Count.ShouldBe(1);
			semanticModel.MainQueryBlock.Columns[0].ColumnReferences[0].ColumnNodeObjectReferences.Count.ShouldBe(1);

			semanticModel.MainQueryBlock.ObjectReferences.Count.ShouldBe(1);
			var objectReference = semanticModel.MainQueryBlock.ObjectReferences.Single();
			objectReference.ShouldBeTypeOf<OracleTableCollectionReference>();
			var tableCollectionExpressionReference = (OracleTableCollectionReference)objectReference;
			tableCollectionExpressionReference.RowSourceReference.ShouldBeTypeOf<OracleTypeReference>();
		}

		[Test(Description = @"")]
		public void TestPivotTableReferenceWithinInlineView()
		{
			const string query1 =
@"SELECT
    UNANSWERED
FROM (
    SELECT
        UNANSWERED
    FROM
        (SELECT 1 COUNTRY_ID, 0 STATUS FROM DUAL)
        PIVOT (
            COUNT(*)
            FOR (STATUS) IN (
                0 AS UNANSWERED
            )
        ) RESPONDENTS
	)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(3);

			semanticModel.MainQueryBlock.Columns.Count.ShouldBe(1);
			semanticModel.MainQueryBlock.Columns[0].ColumnReferences.Count.ShouldBe(1);
			semanticModel.MainQueryBlock.Columns[0].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);

			var inlineView = semanticModel.MainQueryBlock.ObjectReferences.Single().QueryBlocks.Single();
			inlineView.Columns.Count.ShouldBe(1);
			inlineView.Columns[0].ColumnReferences.Count.ShouldBe(1);
			inlineView.Columns[0].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
			inlineView.ObjectReferences.Count.ShouldBe(1);
			var objectReference = inlineView.ObjectReferences.Single();
			objectReference.ShouldBeTypeOf<OraclePivotTableReference>();
		}

		[Test(Description = @"")]
		public void TestSubqueryAsCursorParameter()
		{
			const string query1 = @"SELECT * FROM TABLE(SQLPAD.CURSOR_FUNCTION(0, CURSOR(SELECT SELECTION_ID, NAME, PROJECT_ID, RESPONDENTBUCKET_ID FROM SELECTION), NULL))";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestParenthesisWrappedConcatenatedSubquery()
		{
			const string query1 = @"(SELECT DUMMY, DUMMY FROM DUAL) UNION ALL (SELECT DUMMY, DUMMY FROM DUAL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			semanticModel.QueryBlocks.Count.ShouldBe(2);
			semanticModel.MainQueryBlock.PrecedingConcatenatedQueryBlock.ShouldBe(null);
			semanticModel.MainQueryBlock.FollowingConcatenatedQueryBlock.ShouldNotBe(null);
			semanticModel.MainQueryBlock.FollowingConcatenatedQueryBlock.PrecedingConcatenatedQueryBlock.ShouldNotBe(null);
            semanticModel.RedundantSymbolGroups.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnReferenceInScalarSubqueryWithinJoinCondition()
		{
			const string query1 =
@"SELECT
	NULL
FROM
	SELECTION S1
	JOIN SELECTION S2 ON S1.SELECTION_ID = S2.SELECTION_ID
		AND S2.PROJECT_ID >= (SELECT MIN(PROJECT_ID) FROM SELECTION WHERE NAME IS NOT NULL)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var columnIdentifier = statement.AllTerminals.Single(t => t.Token.Value == "NAME");
			var columnReference = semanticModel.GetColumnReference(columnIdentifier);
			columnReference.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestHiddenColumnReferences()
		{
			const string query1 = @"SELECT HIDDEN_COLUMN, INVISIBLE_COLUMN FROM (SELECT ""CaseSensitiveTable"".*, HIDDEN_COLUMN INVISIBLE_COLUMN FROM ""CaseSensitiveTable"")";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var columns = semanticModel.MainQueryBlock.Columns;
			columns.Count.ShouldBe(2);
			columns[0].NormalizedName.ShouldBe("\"HIDDEN_COLUMN\"");
			columns[0].ColumnReferences.Count.ShouldBe(1);
			columns[0].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(0);
			columns[1].NormalizedName.ShouldBe("\"INVISIBLE_COLUMN\"");
			columns[1].ColumnReferences.Count.ShouldBe(1);
			columns[1].ColumnReferences[0].ColumnNodeColumnReferences.Count.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void TestCastedColumnDataType()
		{
			const string query1 = @"SELECT ((CAST(NULL AS RAW(32)))) COL1, CAST(NULL AS RAW(32)) + 1 COL2, CAST(COLLECT(HEXTORAW('FF')) AS SYS.ODCIRAWLIST) COL3 FROM DUAL";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var columns = semanticModel.MainQueryBlock.Columns;
			columns.Count.ShouldBe(3);
			columns[0].NormalizedName.ShouldBe("\"COL1\"");
			columns[0].ColumnDescription.ShouldNotBe(null);
			columns[0].ColumnDescription.FullTypeName.ShouldBe("RAW(32)");
			columns[0].ColumnDescription.Nullable.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"COL2\"");
			columns[1].ColumnDescription.ShouldNotBe(null);
			columns[1].ColumnDescription.FullTypeName.ShouldBe(String.Empty);
			columns[1].ColumnDescription.Nullable.ShouldBe(true);
			columns[2].NormalizedName.ShouldBe("\"COL3\"");
			columns[2].ColumnDescription.ShouldNotBe(null);
			columns[2].ColumnDescription.FullTypeName.ShouldBe("SYS.ODCIRAWLIST");
			columns[2].ColumnDescription.Nullable.ShouldBe(true);
			columns[2].DataTypeReferences.Count.ShouldBe(1);
			columns[2].DataTypeReferences.Single().SchemaObject.ShouldNotBe(null);
		}

		[Test(Description = @"")]
		public void TestApplyReferenceConstraints()
		{
			const string query1 = @"SELECT * FROM TARGETGROUP";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var columnHeaders =
				new[]
				{
					new ColumnHeader { Name = "PROJECT_ID" },
					new ColumnHeader { Name = "TARGETGROUP_ID" }
				};

			var childReferenceDataSources = semanticModel.ApplyReferenceConstraints(columnHeaders);

			var referenceDataSources = columnHeaders[0].ParentReferenceDataSources;
			referenceDataSources.ShouldNotBe(null);
			referenceDataSources.Count.ShouldBe(1);
			var referenceDataSource = referenceDataSources.First();
			referenceDataSource.ObjectName.ShouldBe("HUSQVIK.PROJECT");
			referenceDataSource.ConstraintName.ShouldBe("HUSQVIK.FK_TARGETGROUP_PROJECT");

			var executionModel = referenceDataSource.CreateExecutionModel(new object[] { 123 });
			executionModel.BindVariables.Count.ShouldBe(1);
			executionModel.BindVariables[0].Name.ShouldBe("KEY0");
			executionModel.BindVariables[0].Value.ShouldBe(123);
			executionModel.StatementText.ShouldBe("SELECT * FROM HUSQVIK.PROJECT WHERE \"PROJECT_ID\" = :KEY0");

			childReferenceDataSources.Count.ShouldBe(1);
			var respondentBucketTargetGroupReferenceDataSource = childReferenceDataSources.First();
			respondentBucketTargetGroupReferenceDataSource.ObjectName.ShouldBe("HUSQVIK.RESPONDENTBUCKET");
			respondentBucketTargetGroupReferenceDataSource.ConstraintName.ShouldBe("HUSQVIK.FK_RESPONDENTBUCKET_TARGETGROUP");
			respondentBucketTargetGroupReferenceDataSource.ColumnHeaders.Count.ShouldBe(1);

			executionModel = respondentBucketTargetGroupReferenceDataSource.CreateExecutionModel(new object[] { 456 });
			executionModel.BindVariables.Count.ShouldBe(1);
			executionModel.BindVariables[0].Name.ShouldBe("KEY0");
			executionModel.BindVariables[0].Value.ShouldBe(456);
			executionModel.StatementText.ShouldBe("SELECT * FROM HUSQVIK.RESPONDENTBUCKET WHERE \"TARGETGROUP_ID\" = :KEY0");
		}

		[Test(Description = @"")]
		public void TestApplyReferenceConstraintsUsingOnlyNonPhysicalColumns()
		{
			const string query1 = @"SELECT * FROM (SELECT ROWNUM VAL FROM DUAL) T1";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);

			var columnHeaders = new[] { new ColumnHeader { Name = "VAL" } };

			var childReferenceDataSources = semanticModel.ApplyReferenceConstraints(columnHeaders);
			childReferenceDataSources.Count.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestMergeUsingAliasedSubquery()
		{
			const string query1 =
@"MERGE INTO DUAL T1
USING (SELECT DUMMY FROM DUAL) T2
ON (T1.DUMMY = T2.DUMMY)
WHEN MATCHED THEN UPDATE SET DUMMY = T2.DUMMY
WHEN NOT MATCHED THEN INSERT VALUES (T2.DUMMY)";

			var statement = (OracleStatement)Parser.Parse(query1).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var semanticModel = OracleStatementSemanticModel.Build(query1, statement, TestFixture.DatabaseModel);
			var columnReferences = semanticModel.MainObjectReferenceContainer.ColumnReferences.ToList();
			columnReferences.Count.ShouldBe(5);
			columnReferences.ForEach(c => c.ColumnNodeColumnReferences.Count.ShouldBe(1));
        }
	}
}
