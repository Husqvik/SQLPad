using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleSemanticValidatorTest
	{
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();
		private readonly OracleStatementValidator _statementValidator = new OracleStatementValidator();
		private readonly DatabaseModelFake _databaseModel = DatabaseModelFake.Instance;

		[Test(Description = @"")]
		public void Test1()
		{
			const string query = "SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION";
			var statement = _oracleSqlParser.Parse(query).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(query, statement, _databaseModel);

			var nodeValidity = validationModel.TableNodeValidity.Values.ToArray();
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

			var nodeValidity = validationModel.TableNodeValidity.Values.ToArray();
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

		[Test(Description = @"")]
		public void Test4()
		{
			const string sqlText = "WITH CTE AS (SELECT 1 FROM DUAL) SELECT CTE.*, SYS.DUAL.*, DUAL.*, HUSQVIK.CTE.* FROM DUAL CROSS JOIN CTE";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, _databaseModel);
			
			var nodeValidityDictionary = validationModel.TableNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.ToArray();
			nodeValidity.Length.ShouldBe(9);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
			nodeValidity[3].ShouldBe(false);
			nodeValidity[4].ShouldBe(true);
			nodeValidity[5].ShouldBe(false);
			nodeValidity[6].ShouldBe(false);
			nodeValidity[7].ShouldBe(true);
			nodeValidity[8].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void Test5()
		{
			const string sqlText = "WITH CTE AS (SELECT 1 COLUMN1, VAL COLUMN2, DUMMY COLUMN3 FROM DUAL) SELECT COLUMN1, 'X' || CTE.COLUMN1, CTE.VAL, CTE.COLUMN2, SYS.DUAL.COLUMN1, DUAL.VAL, DUAL.DUMMY FROM CTE, INVALID_TABLE";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, _databaseModel);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsValid).ToArray();
			nodeValidity.Length.ShouldBe(9);
			nodeValidity[0].ShouldBe(false);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(true);
			nodeValidity[3].ShouldBe(true);
			nodeValidity[4].ShouldBe(false);
			nodeValidity[5].ShouldBe(true);
			nodeValidity[6].ShouldBe(false);
			nodeValidity[7].ShouldBe(false);
			nodeValidity[8].ShouldBe(false);
		}

		[Test(Description = @"")]
		public void Test6()
		{
			const string sqlText = "WITH CTE AS (SELECT DUMMY VAL FROM DUAL) SELECT DUAL.VAL FROM CTE, DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, _databaseModel);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsValid).ToArray();
			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(false);
		}

		[Test(Description = @"")]
		public void Test7()
		{
			const string query1 = "SELECT T2.DUMMY FROM (SELECT DUMMY FROM DUAL) T2, DUAL";
			var statement = _oracleSqlParser.Parse(query1).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(query1, statement, _databaseModel);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsValid).ToArray();
			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);

			const string query2 = "SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";
			statement = _oracleSqlParser.Parse(query2).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			validationModel = _statementValidator.ResolveReferences(query2, statement, _databaseModel);

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(2);
			var columnValidationData = nodeValidityDictionary.First().Value;
			columnValidationData.IsValid.ShouldBe(false);
			columnValidationData.TableNames.Count.ShouldBe(2);
			
			var tableNames = columnValidationData.TableNames.OrderBy(n => n).ToArray();
			tableNames[0].ShouldBe("Dual");
			tableNames[1].ShouldBe("t2");
		}

		[Test(Description = @"")]
		public void Test8()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (SELECT * FROM COUNTRY)";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, _databaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsValid).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
			nodeValidity[3].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void Test9()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (SELECT COUNTRY.* FROM COUNTRY)";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, _databaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsValid).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
			nodeValidity[3].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void Test10()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (COUNTRY)";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, _databaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsValid).ToArray();
			nodeValidity.Length.ShouldBe(3);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
		}
	}
}