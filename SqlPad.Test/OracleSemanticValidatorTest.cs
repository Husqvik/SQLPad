using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	[TestFixture]
	public class OracleSemanticValidatorTest
	{
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();
		private readonly OracleStatementValidator _statementValidator = new OracleStatementValidator();
		private readonly DatabaseModelFake _databaseModel = new DatabaseModelFake();

		[Test(Description = @"")]
		public void Test1()
		{
			const string query = "SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION";
			var statement = _oracleSqlParser.Parse(query).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			var validationModel = _statementValidator.ResolveReferences(query, statement, _databaseModel);

			var nodeValidity = validationModel.NodeValidity.Values.ToArray();
			nodeValidity.Length.ShouldBe(9);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(true);
			nodeValidity[3].ShouldBe(true);
			nodeValidity[4].ShouldBe(true);
			nodeValidity[5].ShouldBe(false);
			nodeValidity[6].ShouldBe(false);
			nodeValidity[7].ShouldBe(false);
			nodeValidity[8].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void Test2()
		{
			const string query = "WITH XXX1 AS (SELECT 1 FROM XXX1) SELECT * FROM XXX1, SYS.XXX1, \"XXX1\", \"xXX1\", \"PUBLIC\".DUAL";
			var statement = _oracleSqlParser.Parse(query).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			var validationModel = _statementValidator.ResolveReferences(query, statement, _databaseModel);

			var nodeValidity = validationModel.NodeValidity.Values.ToArray();
			nodeValidity.Length.ShouldBe(8);
			nodeValidity[0].ShouldBe(false);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(true);
			nodeValidity[3].ShouldBe(false);
			nodeValidity[4].ShouldBe(true);
			nodeValidity[5].ShouldBe(false);
			nodeValidity[6].ShouldBe(true);
			nodeValidity[7].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void Test3()
		{
			//const string sqlText = "WITH XXX AS (SELECT 3 COL FROM DUAL) SELECT VP1 COL1, (SELECT 1 FROM XXX) SCALARSUBQUERY FROM (WITH YYY AS (SELECT 1 FROM DUAL), ZZZ AS (SELECT 2 FROM DUAL), FFF AS (SELECT 4 FROM XXX) SELECT COL + 1 VP1 FROM (SELECT COL FROM XXX)) SUBQUERY";
			const string sqlText = "WITH XXX AS (SELECT 3 COL FROM DUAL CTE_OUTER_ALIAS_1) SELECT VP1 COL1, (SELECT 1 FROM XXX SC_ALIAS_1) SCALARSUBQUERY FROM (WITH YYY AS (SELECT 1 FROM SYS.DUAL CTE_INNER_ALIAS_1), ZZZ AS (SELECT 2 FROM DUAL CTE_INNER_ALIAS_2), FFF AS (SELECT 4 FROM XXX CTE_INNER_ALIAS_3) SELECT COL + 1 VP1 FROM (SELECT TABLE_ALIAS_1.COL, TABLE_ALIAS_2.DUMMY || TABLE_ALIAS_2.DUMMY NOT_DUMMY FROM XXX TABLE_ALIAS_1, DUAL TABLE_ALIAS_2) TABLE_ALIAS_3) SUBQUERY";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, _databaseModel);
		}
	}
}