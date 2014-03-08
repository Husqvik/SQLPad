using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	[TestFixture]
	public class OracleSemanticValidatorTest
	{
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();
		private readonly OracleSemanticValidator _semanticValidator = new OracleSemanticValidator();
		private readonly DatabaseModelFake _databaseModel = new DatabaseModelFake();

		[Test(Description = @"")]
		public void Test1()
		{
			var statement = _oracleSqlParser.Parse("SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION").Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			var validationModel = _semanticValidator.Validate(statement, _databaseModel);

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
			var statement = _oracleSqlParser.Parse("WITH XXX1 AS (SELECT 1 FROM XXX1) SELECT * FROM XXX1").Single();
			var validationModel = _semanticValidator.Validate(statement, _databaseModel);

			var nodeValidity = validationModel.NodeValidity.Values.ToArray();
			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].ShouldBe(false);
			nodeValidity[1].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void Test3()
		{
			//const string sqlText = "WITH XXX AS (SELECT 3 COL FROM DUAL) SELECT VP1 COL1, (SELECT 1 FROM XXX) SCALARSUBQUERY FROM (WITH YYY AS (SELECT 1 FROM DUAL), ZZZ AS (SELECT 2 FROM DUAL), FFF AS (SELECT 4 FROM XXX) SELECT COL + 1 VP1 FROM (SELECT COL FROM XXX)) SUBQUERY";
			const string sqlText = "WITH XXX AS (SELECT 3 COL FROM DUAL CTE_OUTER_ALIAS_1) SELECT VP1 COL1, (SELECT 1 FROM XXX SC_ALIAS_1) SCALARSUBQUERY FROM (WITH YYY AS (SELECT 1 FROM SYS.DUAL CTE_INNER_ALIAS_1), ZZZ AS (SELECT 2 FROM DUAL CTE_INNER_ALIAS_2), FFF AS (SELECT 4 FROM XXX CTE_INNER_ALIAS_3) SELECT COL + 1 VP1 FROM (SELECT TABLE_ALIAS_1.COL, TABLE_ALIAS_2.DUMMY || TABLE_ALIAS_2.DUMMY NOT_DUMMY FROM XXX TABLE_ALIAS_1, DUAL TABLE_ALIAS_2) TABLE_ALIAS_3) SUBQUERY";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			_semanticValidator.ResolveReferences(sqlText, statement, _databaseModel);
		}
	}
}