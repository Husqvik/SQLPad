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

			var functionReferences = semanticModel.QueryBlocks.Single().AllFunctionReferences.ToArray();
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

			var functionReferences = semanticModel.QueryBlocks.Single().AllFunctionReferences.ToArray();
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
			selectionTableReference.Type.ShouldBe(TableReferenceType.PhysicalObject);
			innerBlock.Columns.Count.ShouldBe(3);
			var columns = innerBlock.Columns.ToArray();
			columns[0].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[0].IsDirectColumnReference.ShouldBe(true);
			columns[0].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].ExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectColumnReference.ShouldBe(true);
			columns[1].ColumnDescription.Type.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].ExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectColumnReference.ShouldBe(false);
			columns[2].ColumnDescription.Type.ShouldBe(null); // TODO: Add column expression type resolving

			var outerBlock = queryBlocks[1];
			outerBlock.ObjectReferences.Count.ShouldBe(1);
			var innerTableReference = outerBlock.ObjectReferences.Single();
			innerTableReference.Type.ShouldBe(TableReferenceType.InlineView);
			outerBlock.Columns.Count.ShouldBe(3);
			columns = outerBlock.Columns.ToArray();
			columns[0].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[0].IsDirectColumnReference.ShouldBe(true);
			columns[0].ColumnDescription.Type.ShouldBe("NUMBER");
			columns[1].NormalizedName.ShouldBe("\"SELECTION_NAME\"");
			columns[1].ExplicitDefinition.ShouldBe(true);
			columns[1].IsDirectColumnReference.ShouldBe(true);
			columns[1].ColumnDescription.Type.ShouldBe("VARCHAR2");
			columns[2].NormalizedName.ShouldBe("\"MY_NUMBER_COLUMN\"");
			columns[2].ExplicitDefinition.ShouldBe(true);
			columns[2].IsDirectColumnReference.ShouldBe(true);
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
			selectionTableReference.Type.ShouldBe(TableReferenceType.PhysicalObject);
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
			innerTableReference.Type.ShouldBe(TableReferenceType.InlineView);
			outerBlock.Columns.Count.ShouldBe(5);
			columns = outerBlock.Columns.ToArray();
			columns[0].IsAsterisk.ShouldBe(true);
			columns[0].ExplicitDefinition.ShouldBe(true);
			columns[1].NormalizedName.ShouldBe("\"RESPONDENTBUCKET_ID\"");
			columns[1].ExplicitDefinition.ShouldBe(false);
			columns[1].ColumnDescription.Type.ShouldBe("NUMBER");
		}
	}
}