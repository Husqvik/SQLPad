using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleStatementSemanticModelTest
	{
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();

		[Test(Description = @"")]
		public void Test1()
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

			var semanticModel = new OracleStatementSemanticModel(query1, statement, DatabaseModelFake.Instance);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(3);

			var queryBlocks = semanticModel.QueryBlocks.ToArray();
			queryBlocks[0].Alias.ShouldBe("\"CTE2\"");
			queryBlocks[0].TableReferences.Count.ShouldBe(1);
			queryBlocks[0].Columns.Count.ShouldBe(3);
			queryBlocks[1].Alias.ShouldBe("\"CTE1\"");
			queryBlocks[1].TableReferences.Count.ShouldBe(1);
			queryBlocks[1].Columns.Count.ShouldBe(3);
			queryBlocks[2].Alias.ShouldBe(null);
			queryBlocks[2].TableReferences.Count.ShouldBe(2);
			queryBlocks[2].Columns.Count.ShouldBe(7);
			var mainQueryBlockColumns = queryBlocks[2].Columns.ToArray();
			mainQueryBlockColumns[0].IsAsterisk.ShouldBe(true);
		}

		[Test(Description = @"")]
		public void Test2()
		{
			const string query1 = @"SELECT SELECTION.* FROM SELECTION JOIN HUSQVIK.PROJECT P ON SELECTION.PROJECT_ID = P.PROJECT_ID";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, DatabaseModelFake.Instance);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var queryBlock = semanticModel.QueryBlocks.Single();
			queryBlock.Alias.ShouldBe(null);
			queryBlock.TableReferences.Count.ShouldBe(2);
			queryBlock.Columns.Count.ShouldBe(5);
		}

		[Test(Description = @"")]
		public void Test3()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var statement = (OracleStatement)_oracleSqlParser.Parse(query1).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var semanticModel = new OracleStatementSemanticModel(query1, statement, DatabaseModelFake.Instance);

			semanticModel.QueryBlocks.ShouldNotBe(null);
			semanticModel.QueryBlocks.Count.ShouldBe(1);

			var queryBlock = semanticModel.QueryBlocks.Single();
			queryBlock.Alias.ShouldBe(null);
			queryBlock.TableReferences.Count.ShouldBe(2);
			queryBlock.Columns.Count.ShouldBe(5);
		}
	}
}