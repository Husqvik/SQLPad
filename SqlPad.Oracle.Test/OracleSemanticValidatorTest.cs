using System;
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

		[Test(Description = @"")]
		public void TestTableNodeValidityWithFullyQualifiedAndNormalTableNames()
		{
			const string query = "SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION";
			var statement = _oracleSqlParser.Parse(query).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(query, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.ObjectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
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
		public void TestTableNodeValidityWithQuotedNotations()
		{
			const string query = "WITH XXX1 AS (SELECT 1 FROM XXX1) SELECT * FROM XXX1, SYS.XXX1, \"XXX1\", \"xXX1\", \"PUBLIC\".DUAL";
			var statement = _oracleSqlParser.Parse(query).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(query, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.ObjectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
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
		public void TestNodeValidityForComplexQueryWithMultipleCommonTableExpressionsAtDifferentLevelAndScalarSubqueries()
		{
			const string sqlText = "WITH XXX AS (SELECT 3 COL FROM DUAL CTE_OUTER_ALIAS_1) SELECT VP1 COL1, (SELECT 1 FROM XXX SC_ALIAS_1) SCALARSUBQUERY FROM (WITH YYY AS (SELECT 1 FROM SYS.DUAL CTE_INNER_ALIAS_1), ZZZ AS (SELECT 2 FROM DUAL CTE_INNER_ALIAS_2), FFF AS (SELECT 4 FROM XXX CTE_INNER_ALIAS_3) SELECT COL + 1 VP1 FROM (SELECT TABLE_ALIAS_1.COL, TABLE_ALIAS_2.DUMMY || TABLE_ALIAS_2.DUMMY NOT_DUMMY FROM XXX TABLE_ALIAS_1, DUAL TABLE_ALIAS_2) TABLE_ALIAS_3) SUBQUERY";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToList();
			nodeValidity.Count.ShouldBe(11);
			nodeValidity.ForEach(n => n.ShouldBe(true));
		}

		[Test(Description = @"")]
		public void TestTableNodeValidityWhenUsingFullyQualifiedOrNormalNameOrCommonTableExpressionAlias()
		{
			const string sqlText = "WITH CTE AS (SELECT 1 FROM DUAL) SELECT CTE.*, SYS.DUAL.*, DUAL.*, HUSQVIK.CTE.* FROM DUAL CROSS JOIN CTE";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);
			
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
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
		public void TestTableNodeValidityInQueryWithCommonTableExpression()
		{
			const string sqlText = "WITH CTE AS (SELECT 1 COLUMN1, VAL COLUMN2, DUMMY COLUMN3 FROM DUAL) SELECT COLUMN1, 'X' || CTE.COLUMN1, CTE.VAL, CTE.COLUMN2, SYS.DUAL.COLUMN1, DUAL.VAL, DUAL.DUMMY FROM CTE, INVALID_TABLE";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
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
		public void TestColumnNodeValidityInQueryWithCommonTableExpression()
		{
			const string sqlText = "WITH CTE AS (SELECT DUMMY VAL FROM DUAL) SELECT DUAL.VAL FROM CTE, DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(false);
		}

		[Test(Description = @"")]
		public void TestSameColumnNamesInSameObjectsInDifferentSchemas()
		{
			const string sqlText = @"SELECT SYS.DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.SemanticError).ToArray();
			nodeValidity.Length.ShouldBe(1);
			nodeValidity[0].ShouldBe(SemanticError.None);
		}

		[Test(Description = @"")]
		public void TestAmbiguousColumnAndObjectNames()
		{
			const string sqlText = @"SELECT DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.SemanticError).ToArray();
			nodeValidity.Length.ShouldBe(1);
			nodeValidity[0].ShouldBe(SemanticError.AmbiguousReference);
		}

		[Test(Description = @"")]
		public void TestAmbiguousColumnReferences()
		{
			const string query1 = "SELECT T2.DUMMY FROM (SELECT DUMMY FROM DUAL) T2, DUAL";
			var statement = _oracleSqlParser.Parse(query1).Single();
			
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);
			
			var validationModel = _statementValidator.ResolveReferences(query1, statement, TestFixture.DatabaseModel);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);

			const string query2 = "SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";
			statement = _oracleSqlParser.Parse(query2).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			validationModel = _statementValidator.ResolveReferences(query2, statement, TestFixture.DatabaseModel);

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(2);
			var columnValidationData = nodeValidityDictionary.First().Value;
			columnValidationData.IsRecognized.ShouldBe(true);
			columnValidationData.SemanticError.ShouldBe(SemanticError.AmbiguousReference);
			columnValidationData.ObjectNames.Count.ShouldBe(2);
			
			var tableNames = columnValidationData.ObjectNames.OrderBy(n => n).ToArray();
			tableNames[0].ShouldBe("Dual");
			tableNames[1].ShouldBe("t2");
		}

		[Test(Description = @"")]
		public void TestColumnNodeValidityWhenExposedFromSubqueryUsingAsterisk()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (SELECT * FROM COUNTRY)";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
			nodeValidity[3].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestColumnNodeValidityWhenExposedFromSubqueryUsingAsteriskOnSpecificObject()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (SELECT COUNTRY.* FROM COUNTRY)";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
			nodeValidity[3].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestColumnNodeValidityWhenTableAsInnerTableReference()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (COUNTRY)";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(3);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
		}

		[Test(Description = @"")]
		public void TestTableNodeValidityInSimpleQuery()
		{
			const string sqlText = "SELECT SELECTION.DUMMY FROM SELECTION";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(2);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestColumnNodeValidityWhenColumnsFromNestedSubqueries()
		{
			const string sqlText = "SELECT PROJECT_ID, SELECTION_ID, RESPONDENTBUCKET_ID, DUMMY FROM (SELECT * FROM (SELECT * FROM SELECTION))";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			columnNodeValidity.Length.ShouldBe(6);
			columnNodeValidity[0].ShouldBe(true);
			columnNodeValidity[1].ShouldBe(true);
			columnNodeValidity[2].ShouldBe(true);
			columnNodeValidity[3].ShouldBe(false);
			columnNodeValidity[4].ShouldBe(true);
			columnNodeValidity[5].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestTableNodeValidityInConditions()
		{
			const string sqlText = "SELECT NULL FROM DUAL WHERE HUSQVIK.COUNTRY.ID = SELECTION.ID AND SYS.DUAL.DUMMY = DUMMY";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var objectNodeValidity = validationModel.ObjectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(6);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(false);
			objectNodeValidity[2].ShouldBe(false);
			objectNodeValidity[3].ShouldBe(false);
			objectNodeValidity[4].ShouldBe(false);
			objectNodeValidity[5].ShouldBe(false);
		}

		[Test(Description = @"")]
		public void TestTableNodeValidityInMultiFromMultiJoinQuery()
		{
			const string sqlText = @"SELECT * FROM
PROJECT P,
RESPONDENTBUCKET
JOIN TARGETGROUP TG ON RB.TARGETGROUP_ID = TG.TARGETGROUP_ID
JOIN HUSQVIK.SELECTION S ON P.PROJECT_ID = S.PROJECT_ID";
			
			var statement = (OracleStatement)_oracleSqlParser.Parse(sqlText).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var objectNodeValidity = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel)
				.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);

			var nodeValidity = objectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(9);
			nodeValidity[0].ShouldBe(true); // PROJECT
			nodeValidity[1].ShouldBe(true); // RESPONDENTBUCKET
			nodeValidity[2].ShouldBe(true); // TARGETGROUP
			nodeValidity[3].ShouldBe(false); // RB
			nodeValidity[4].ShouldBe(true); // TG
			nodeValidity[5].ShouldBe(true); // HUSQVIK
			nodeValidity[6].ShouldBe(true); // SELECTION
			nodeValidity[7].ShouldBe(false); // P
			nodeValidity[8].ShouldBe(true); // S
		}

		[TestCase("LEFT")]
		[TestCase("RIGHT")]
		[TestCase("FULL")]
		[TestCase("LEFT OUTER")]
		[TestCase("RIGHT OUTER")]
		[TestCase("FULL OUTER")]
		public void TestTableNodeValidyInLeftJoinClauseWithoutSourceTableAlias(string joinType)
		{
			var sqlText = String.Format(@"SELECT NULL FROM SELECTION {0} JOIN RESPONDENTBUCKET RB ON SELECTION.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID", joinType);
			
			var statement = (OracleStatement)_oracleSqlParser.Parse(sqlText).Single();
			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var objectNodeValidity = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel)
				.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);

			var nodeValidity = objectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true); // SELECTION
			nodeValidity[1].ShouldBe(true); // RESPONDENTBUCKET
			nodeValidity[2].ShouldBe(true); // SELECTION
			nodeValidity[3].ShouldBe(true); // RB
		}

		[Test(Description = @"")]
		public void TestAmbiguousColumnFromSubquery()
		{
			const string sqlText = "SELECT NAME FROM (SELECT S.NAME, RB.NAME FROM SELECTION S JOIN RESPONDENTBUCKET RB ON S.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID)";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.ColumnNodeValidity.OrderBy(cv => cv.Key.SourcePosition.IndexStart).Select(cv => cv.Value.SemanticError).ToArray();
			nodeValidity.Length.ShouldBe(5);
			nodeValidity[0].ShouldBe(SemanticError.AmbiguousReference);
			nodeValidity[1].ShouldBe(SemanticError.None);
			nodeValidity[2].ShouldBe(SemanticError.None);
			nodeValidity[3].ShouldBe(SemanticError.None);
			nodeValidity[4].ShouldBe(SemanticError.None);
		}

		[Test(Description = @"")]
		public void TestBasicFunctionCall()
		{
			// TODO: Update when parameterless functions are recognized
			const string sqlText = "SELECT COUNT(COUNT) OVER (), COUNT, HUSQVIK.COUNT, HUSQVIK.COUNT() FROM FTEST";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.FunctionNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();
			
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticError.ShouldBe(SemanticError.None);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticError.ShouldBe(SemanticError.InvalidParameterCount);
			nodeValidity[2].IsRecognized.ShouldBe(true);
			nodeValidity[2].SemanticError.ShouldBe(SemanticError.InvalidParameterCount);
			nodeValidity[3].IsRecognized.ShouldBe(false);
			nodeValidity[3].SemanticError.ShouldBe(SemanticError.None);
		}

		[Test(Description = @"")]
		public void TestFunctionCallWithWrongParameterCount()
		{
			const string sqlText = "SELECT COUNT(1, 2) FROM DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.FunctionNodeValidity
				.Where(f => f.Key.Type == NodeType.NonTerminal)
				.Select(cv => cv.Value.SemanticError).ToArray();
			
			nodeValidity.Length.ShouldBe(1);
			nodeValidity[0].ShouldBe(SemanticError.InvalidParameterCount);
		}

		[Test(Description = @"")]
		public void TestNestedAnalyticFuctionCall()
		{
			const string sqlText = "SELECT NULLIF(COUNT(DUMMY) OVER (), 1) FROM DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.FunctionNodeValidity
				.Where(f => f.Key.Type == NodeType.NonTerminal)
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(cv => cv.Value.SemanticError).ToArray();
			
			nodeValidity.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void TestColumnNodeValidityUsingNestedQueryAndCountAsteriskFunction()
		{
			const string sqlText = "SELECT DUMMY FROM (SELECT DUMMY, COUNT(*) OVER () ROW_COUNT FROM (SELECT DUMMY FROM DUAL))";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var firstNodeValidity = validationModel.ColumnNodeValidity.OrderBy(cv => cv.Key.SourcePosition.IndexStart).Select(cv => cv.Value.SemanticError).First();
			firstNodeValidity.ShouldBe(SemanticError.None);
		}

		[Test(Description = @"")]
		public void TestParameterlessFuctionWithParenthesisRequirement()
		{
			const string sqlText = "SELECT SYS_GUID(), SYS_GUID(123), SYS_GUID, SYSGUID() FROM DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.FunctionNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();

			nodeValidity.Length.ShouldBe(5);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticError.ShouldBe(SemanticError.None);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticError.ShouldBe(SemanticError.None);
			nodeValidity[2].IsRecognized.ShouldBe(true);
			nodeValidity[2].SemanticError.ShouldBe(SemanticError.InvalidParameterCount);
			nodeValidity[3].IsRecognized.ShouldBe(true);
			nodeValidity[3].SemanticError.ShouldBe(SemanticError.MissingParenthesis);
			nodeValidity[4].IsRecognized.ShouldBe(false);
			nodeValidity[4].SemanticError.ShouldBe(SemanticError.None);
		}

		[Test(Description = @"")]
		public void TestParameterFuctionWithUnlimitedMaximumParameterCount()
		{
			const string sqlText = "SELECT COALESCE(SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID) FROM SELECTION";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			validationModel.FunctionNodeValidity.Count.ShouldBe(1);
			var nodeValidationData = validationModel.FunctionNodeValidity.Single().Value;
			nodeValidationData.IsRecognized.ShouldBe(true);
			nodeValidationData.SemanticError.ShouldBe(SemanticError.None);
		}

		[Test(Description = @"")]
		public void TestMultiParameterFunctionWithAnalyticFunctionAsOneParameter()
		{
			const string sqlText = "SELECT NVL(LAST_VALUE(DUMMY) OVER (), 'Replacement') FROM DUAL";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidity = validationModel.FunctionNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();

			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticError.ShouldBe(SemanticError.None);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticError.ShouldBe(SemanticError.None);
		}

		[Test(Description = @"")]
		public void TestTableNodeValidityWhenOneCommonTableExpressionReferencesAnother()
		{
			const string sqlText = "WITH T1 AS (SELECT 1 A FROM DUAL), T2 AS (SELECT 1 B FROM T1) SELECT B FROM T2";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(3);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(true);
		}

		[Test(Description = @"")]
		public void TestTableNodeValidityWhenOneCommonTableExpressionReferencesAnotherDefinedLater()
		{
			const string sqlText = "WITH T1 AS (SELECT 1 A FROM T2), T2 AS (SELECT 1 B FROM T1) SELECT B FROM T2";
			var statement = _oracleSqlParser.Parse(sqlText).Single();

			statement.ProcessingStatus.ShouldBe(ProcessingStatus.Success);

			var validationModel = _statementValidator.ResolveReferences(sqlText, statement, TestFixture.DatabaseModel);

			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(3);
			objectNodeValidity[0].ShouldBe(false);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(true);
		}

		//WITH CTE AS (SELECT 1 A, 2 B, 3 C FROM DUAL) SELECT SELECTION.DUMMY, NQ.DUMMY, CTE.DUMMY, SYS.DUAL.DUMMY FROM SELECTION, (SELECT 1 X, 2 Y, 3 Z FROM DUAL) NQ, CTE, SYS.DUAL
	}
}
