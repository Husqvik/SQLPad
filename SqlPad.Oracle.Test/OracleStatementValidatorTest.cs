using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleStatementValidatorTest
	{
		private static readonly OracleSqlParser Parser = OracleSqlParser.Instance;

		internal static OracleValidationModel BuildValidationModel(string statementText, StatementBase statement, OracleDatabaseModelBase databaseModel = null)
		{
			var statementValidator = new OracleStatementValidator();
			var task = statementValidator.BuildSemanticModelAsync(statementText, statement, databaseModel ?? TestFixture.DatabaseModel, CancellationToken.None);
			task.Wait();

			return (OracleValidationModel)statementValidator.BuildValidationModel(task.Result);
		}

		[Test]
		public void TestTableNodeValidityWithFullyQualifiedAndNormalTableNames()
		{
			const string query = "SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION";
			var statement = Parser.Parse(query).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(query, statement);

			var objectNodeValidity = validationModel.ObjectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(9);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(true);
			objectNodeValidity[3].ShouldBe(true);
			objectNodeValidity[4].ShouldBe(true);
			objectNodeValidity[5].ShouldBe(false);
			objectNodeValidity[6].ShouldBe(false);
			objectNodeValidity[7].ShouldBe(false);
			objectNodeValidity[8].ShouldBe(true);
		}

		[Test]
		public void TestTableNodeValidityWithQuotedNotations()
		{
			const string query = "WITH XXX1 AS (SELECT 1 FROM XXX1) SELECT * FROM XXX1, SYS.XXX1, \"XXX1\", \"xXX1\", \"PUBLIC\".DUAL";
			var statement = Parser.Parse(query).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(query, statement);

			var objectNodeValidity = validationModel.ObjectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(8);
			objectNodeValidity[0].ShouldBe(false);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(true);
			objectNodeValidity[3].ShouldBe(false);
			objectNodeValidity[4].ShouldBe(true);
			objectNodeValidity[5].ShouldBe(false);
			objectNodeValidity[6].ShouldBe(true);
			objectNodeValidity[7].ShouldBe(true);
		}

		[Test]
		public void TestNodeValidityForComplexQueryWithMultipleCommonTableExpressionsAtDifferentLevelAndScalarSubqueries()
		{
			const string sqlText = "WITH XXX AS (SELECT 3 COL FROM DUAL CTE_OUTER_ALIAS_1) SELECT VP1 COL1, (SELECT 1 FROM XXX SC_ALIAS_1) SCALARSUBQUERY FROM (WITH YYY AS (SELECT 1 FROM SYS.DUAL CTE_INNER_ALIAS_1), ZZZ AS (SELECT 2 FROM DUAL CTE_INNER_ALIAS_2), FFF AS (SELECT 4 FROM XXX CTE_INNER_ALIAS_3) SELECT COL + 1 VP1 FROM (SELECT TABLE_ALIAS_1.COL, TABLE_ALIAS_2.DUMMY || TABLE_ALIAS_2.DUMMY NOT_DUMMY FROM XXX TABLE_ALIAS_1, DUAL TABLE_ALIAS_2) TABLE_ALIAS_3) SUBQUERY";
			var statement = Parser.Parse(sqlText).Single();
			
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToList();
			objectNodeValidity.Count.ShouldBe(11);
			objectNodeValidity.ForEach(n => n.ShouldBe(true));
		}

		[Test]
		public void TestTableNodeValidityWhenUsingFullyQualifiedOrNormalNameOrCommonTableExpressionAlias()
		{
			const string sqlText = "WITH CTE AS (SELECT 1 FROM DUAL) SELECT CTE.*, SYS.DUAL.*, DUAL.*, HUSQVIK.CTE.* FROM DUAL CROSS JOIN CTE";
			var statement = Parser.Parse(sqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(sqlText, statement);
			
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(9);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(false);
			objectNodeValidity[3].ShouldBe(false);
			objectNodeValidity[4].ShouldBe(true);
			objectNodeValidity[5].ShouldBe(false);
			objectNodeValidity[6].ShouldBe(false);
			objectNodeValidity[7].ShouldBe(true);
			objectNodeValidity[8].ShouldBe(true);
		}

		[Test]
		public void TestTableNodeValidityInQueryWithCommonTableExpression()
		{
			const string sqlText = "WITH CTE AS (SELECT 1 COLUMN1, VAL COLUMN2, DUMMY COLUMN3 FROM DUAL) SELECT COLUMN1, 'X' || CTE.COLUMN1, CTE.VAL, CTE.COLUMN2, SYS.DUAL.COLUMN1, DUAL.VAL, DUAL.DUMMY FROM CTE, INVALID_TABLE";
			var statement = Parser.Parse(sqlText).Single();
			
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(sqlText, statement);
			
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

		[Test]
		public void TestColumnNodeValidityInQueryWithCommonTableExpression()
		{
			const string sqlText = "WITH CTE AS (SELECT DUMMY VAL FROM DUAL) SELECT DUAL.VAL FROM CTE, DUAL";
			var statement = Parser.Parse(sqlText).Single();
			
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(sqlText, statement);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(false);
		}

		[Test]
		public void TestSameColumnNamesInSameObjectsInDifferentSchemas()
		{
			const string sqlText = @"SELECT SYS.DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";
			var statement = Parser.Parse(sqlText).Single();
			
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(sqlText, statement);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.SemanticErrorType).ToArray();
			nodeValidity.Length.ShouldBe(1);
			nodeValidity[0].ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestAmbiguousColumnAndObjectNames()
		{
			const string sqlText = @"SELECT DUAL.DUMMY FROM SYS.DUAL, ""PUBLIC"".DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.SemanticErrorType).ToArray();
			nodeValidity.Length.ShouldBe(1);
			nodeValidity[0].ShouldBe(OracleSemanticErrorType.AmbiguousReference);
		}

		[Test]
		public void TestAmbiguousColumnReferences()
		{
			const string query1 = "SELECT T2.DUMMY FROM (SELECT DUMMY FROM DUAL) T2, DUAL";
			var statement = Parser.Parse(query1).Single();
			
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			
			var validationModel = BuildValidationModel(query1, statement);
			
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);

			const string query2 = "SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";
			statement = Parser.Parse(query2).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			validationModel = BuildValidationModel(query2, statement);

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(2);
			var columnValidationData = nodeValidityDictionary.First().Value;
			columnValidationData.IsRecognized.ShouldBe(true);
			columnValidationData.SemanticErrorType.ShouldBe(OracleSemanticErrorType.AmbiguousReference);
			columnValidationData.ObjectNames.Count.ShouldBe(2);
			
			var tableNames = columnValidationData.ObjectNames.OrderBy(n => n).ToArray();
			tableNames[0].ShouldBe("Dual");
			tableNames[1].ShouldBe("t2");
		}

		[Test]
		public void TestColumnNodeValidityWhenExposedFromSubqueryUsingAsterisk()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (SELECT * FROM COUNTRY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
			nodeValidity[3].ShouldBe(true);
		}

		[Test]
		public void TestColumnNodeValidityWhenExposedFromSubqueryUsingAsteriskOnSpecificObject()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (SELECT COUNTRY.* FROM COUNTRY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
			nodeValidity[3].ShouldBe(true);
		}

		[Test]
		public void TestColumnNodeValidityWhenTableAsInnerTableReference()
		{
			const string sqlText = "SELECT ID, NAME, DUMMY FROM (COUNTRY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nodeValidity = nodeValidityDictionary.Values.Select(c => c.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(3);
			nodeValidity[0].ShouldBe(true);
			nodeValidity[1].ShouldBe(true);
			nodeValidity[2].ShouldBe(false);
		}

		[Test]
		public void TestTableNodeValidityInSimpleQuery()
		{
			const string sqlText = "SELECT SELECTION.DUMMY FROM SELECTION";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(2);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(true);
		}

		[Test]
		public void TestColumnNodeValidityWhenColumnsFromNestedSubqueries()
		{
			const string sqlText = "SELECT PROJECT_ID, SELECTION_ID, RESPONDENTBUCKET_ID, DUMMY FROM (SELECT * FROM (SELECT * FROM SELECTION))";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

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

		[Test]
		public void TestTableNodeValidityInConditions()
		{
			const string sqlText = "SELECT NULL FROM DUAL WHERE HUSQVIK.COUNTRY.ID = SELECTION.ID AND SYS.DUAL.DUMMY = DUMMY";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var objectNodeValidity = validationModel.ObjectNodeValidity.Values
				.OrderBy(v => v.Node.SourcePosition.IndexStart)
				.Select(v => v.IsRecognized)
				.ToArray();
			
			objectNodeValidity.Length.ShouldBe(6);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(false);
			objectNodeValidity[2].ShouldBe(false);
			objectNodeValidity[3].ShouldBe(false);
			objectNodeValidity[4].ShouldBe(false);
			objectNodeValidity[5].ShouldBe(false);
		}

		[Test]
		public void TestTableNodeValidityInMultiFromMultiJoinQuery()
		{
			const string sqlText = @"SELECT * FROM
PROJECT P,
RESPONDENTBUCKET
JOIN TARGETGROUP TG ON RB.TARGETGROUP_ID = TG.TARGETGROUP_ID
JOIN HUSQVIK.SELECTION S ON P.PROJECT_ID = S.PROJECT_ID";
			
			var statement = (OracleStatement)Parser.Parse(sqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var objectNodeValidity = BuildValidationModel(sqlText, statement)
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
			var sqlText = $@"SELECT NULL FROM SELECTION {joinType} JOIN RESPONDENTBUCKET RB ON SELECTION.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID";
			
			var statement = (OracleStatement)Parser.Parse(sqlText).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var objectNodeValidity = BuildValidationModel(sqlText, statement)
				.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);

			var nodeValidity = objectNodeValidity.Values.Select(v => v.IsRecognized).ToArray();
			nodeValidity.Length.ShouldBe(4);
			nodeValidity[0].ShouldBe(true); // SELECTION
			nodeValidity[1].ShouldBe(true); // RESPONDENTBUCKET
			nodeValidity[2].ShouldBe(true); // SELECTION
			nodeValidity[3].ShouldBe(true); // RB
		}

		[Test]
		public void TestAmbiguousColumnFromSubquery()
		{
			const string sqlText = "SELECT NAME FROM (SELECT S.NAME, RB.NAME FROM SELECTION S JOIN RESPONDENTBUCKET RB ON S.RESPONDENTBUCKET_ID = RB.RESPONDENTBUCKET_ID)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ColumnNodeValidity.OrderBy(cv => cv.Key.SourcePosition.IndexStart).Select(cv => cv.Value.SemanticErrorType).ToArray();
			nodeValidity.Length.ShouldBe(5);
			nodeValidity[0].ShouldBe(OracleSemanticErrorType.AmbiguousReference);
			nodeValidity[1].ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[2].ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[3].ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[4].ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestBasicFunctionCall()
		{
			const string sqlText = "SELECT COUNT(COUNT) OVER (), COUNT, HUSQVIK.COUNT, HUSQVIK.COUNT() FROM FTEST";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();
			
			nodeValidity.Length.ShouldBe(5);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidParameterCount);
			// COUNT
			nodeValidity[2].IsRecognized.ShouldBe(true);
			nodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidParameterCount);
			// HUSQVIK.COUNT
			nodeValidity[3].IsRecognized.ShouldBe(true);
			nodeValidity[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			// HUSQVIK.COUNT()
			nodeValidity[4].IsRecognized.ShouldBe(true);
			nodeValidity[4].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestToCharFunctionCallWithMissingOptionalParameters()
		{
			const string sqlText = "SELECT TO_CHAR(1) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();

			nodeValidity.Length.ShouldBe(1);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestFunctionCallWithWrongParameterCount()
		{
			const string sqlText = "SELECT COUNT(1, 2) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.Where(f => f.Key.Type == NodeType.NonTerminal)
				.Select(cv => cv.Value.SemanticErrorType).ToArray();
			
			nodeValidity.Length.ShouldBe(1);
			nodeValidity[0].ShouldBe(OracleSemanticErrorType.InvalidParameterCount);
		}

		[Test]
		public void TestAnalyticFuctionAsParameter()
		{
			const string sqlText = "SELECT NULLIF(COUNT(DUMMY) OVER (), 1) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.Where(f => f.Key.Type == NodeType.NonTerminal)
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(cv => cv.Value.SemanticErrorType).ToArray();
			
			nodeValidity.Length.ShouldBe(0);
		}

		[Test]
		public void TestAnalyticFuctionAsAnalyticFuctionParameter()
		{
			const string sqlText = "SELECT COUNT(COUNT(DUMMY) OVER ()) OVER () FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(1);
			var validationData = validationModel.InvalidNonTerminals.Values.First();
			validationData.SemanticErrorType.ShouldBe(OracleSemanticErrorType.WindowFunctionsNotAllowedHere);
			validationData.Node.SourcePosition.IndexStart.ShouldBe(13);
			validationData.Node.SourcePosition.IndexEnd.ShouldBe(32);
		}

		[Test]
		public void TestAnalyticFuctionAsAggregateFuctionParameter()
		{
			const string sqlText = "SELECT COUNT(COUNT(DUMMY) OVER ()) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(1);
			var validationData = validationModel.InvalidNonTerminals.Values.First();
			validationData.SemanticErrorType.ShouldBe(OracleSemanticErrorType.WindowFunctionsNotAllowedHere);
			validationData.Node.SourcePosition.IndexStart.ShouldBe(13);
			validationData.Node.SourcePosition.IndexEnd.ShouldBe(32);
		}

		[Test]
		public void TestAggregateFuctionAsAnalyticFuctionParameter()
		{
			const string sqlText = "SELECT COUNT(COUNT(DUMMY)) OVER () FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}

		[Test]
		public void TestTooDeeplyNestedAggregateFunction()
		{
			const string sqlText = "SELECT COUNT(COUNT(COUNT(DUMMY))) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(1);
			var validationData = validationModel.InvalidNonTerminals.Values.First();
			validationData.SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNestedTooDeeply);
			validationData.Node.SourcePosition.IndexStart.ShouldBe(19);
			validationData.Node.SourcePosition.IndexEnd.ShouldBe(30);
		}

		[Test]
		public void TestTooDeeplyNestedAggregateFunctionInOrderByClause()
		{
			const string sqlText = "SELECT COUNT(DUMMY) DUMMY FROM DUAL ORDER BY COUNT(DUMMY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(1);
			var validationData = validationModel.InvalidNonTerminals.Values.First();
			validationData.SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNestedTooDeeply);
			validationData.Node.SourcePosition.IndexStart.ShouldBe(45);
			validationData.Node.SourcePosition.IndexEnd.ShouldBe(56);
		}

		[Test]
		public void TestNotSingleGroupGroupbyFunctionInOrderByClause()
		{
			const string sqlText = "SELECT COUNT(DUMMY) OVER () DUMMY FROM DUAL ORDER BY COUNT(DUMMY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(1);
			var validationData = validationModel.InvalidNonTerminals.Values.First();
			validationData.SemanticErrorType.ShouldBe(OracleSemanticErrorType.NotSingleGroupGroupFunction);
			validationData.Node.SourcePosition.IndexStart.ShouldBe(53);
			validationData.Node.SourcePosition.IndexEnd.ShouldBe(64);
		}

		[Test]
		public void TestColumnNodeValidityUsingNestedQueryAndCountAsteriskFunction()
		{
			const string sqlText = "SELECT DUMMY FROM (SELECT DUMMY, COUNT(*) OVER () ROW_COUNT FROM (SELECT DUMMY FROM DUAL))";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var firstNodeValidity = validationModel.ColumnNodeValidity.OrderBy(cv => cv.Key.SourcePosition.IndexStart).Select(cv => cv.Value.SemanticErrorType).First();
			firstNodeValidity.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestParameterlessFuctionWithParenthesisRequirement()
		{
			const string sqlText = "SELECT SYS_GUID(), SYS_GUID(123), SYS_GUID, SYSGUID() FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();

			nodeValidity.Length.ShouldBe(5);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[2].IsRecognized.ShouldBe(true);
			nodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidParameterCount);
			nodeValidity[3].IsRecognized.ShouldBe(true);
			nodeValidity[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.MissingParenthesis);
			nodeValidity[4].IsRecognized.ShouldBe(false);
			nodeValidity[4].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestFuctionRequiringNoParenthesis()
		{
			const string sqlText = "SELECT SESSIONTIMEZONE() FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();

			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.NonParenthesisFunction);
		}

		[Test]
		public void TestParameterFuctionWithUnlimitedMaximumParameterCount()
		{
			const string sqlText = "SELECT COALESCE(SELECTION.RESPONDENTBUCKET_ID, SELECTION.SELECTION_ID) FROM SELECTION";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.ProgramNodeValidity.Count.ShouldBe(1);
			var nodeValidationData = validationModel.ProgramNodeValidity.Single().Value;
			nodeValidationData.IsRecognized.ShouldBe(true);
			nodeValidationData.SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestMultiParameterFunctionWithAnalyticFunctionAsOneParameter()
		{
			const string sqlText = "SELECT NVL(LAST_VALUE(DUMMY) OVER (), 'Replacement') FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(kvp => kvp.Value)
				.ToArray();

			nodeValidity.Length.ShouldBe(2);
			nodeValidity[0].IsRecognized.ShouldBe(true);
			nodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestTableNodeValidityWhenOneCommonTableExpressionReferencesAnother()
		{
			const string sqlText = "WITH T1 AS (SELECT 1 A FROM DUAL), T2 AS (SELECT 1 B FROM T1) SELECT B FROM T2";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(3);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(true);
		}

		[Test]
		public void TestTableNodeValidityWhenOneCommonTableExpressionReferencesAnotherDefinedLater()
		{
			const string sqlText = "WITH T1 AS (SELECT 1 A FROM T2), T2 AS (SELECT 1 B FROM T1) SELECT B FROM T2";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToArray();
			objectNodeValidity.Length.ShouldBe(3);
			objectNodeValidity[0].ShouldBe(false);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(true);
		}

		[Test]
		public void TestNodeValidityInOrderByClause()
		{
			const string sqlText = "SELECT * FROM HUSQVIK.SELECTION ORDER BY HUSQVIK.SELECTION.NAME, SELECTION.NAME, DUAL.DUMMY, SELECTION_ID, UNDEFINED_COLUMN, UPPER(''), UNDEFINED_FUNCTION()";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.Select(v => v.IsRecognized).ToList();
			objectNodeValidity.Count.ShouldBe(6);
			objectNodeValidity[0].ShouldBe(true);
			objectNodeValidity[1].ShouldBe(true);
			objectNodeValidity[2].ShouldBe(true);
			objectNodeValidity[3].ShouldBe(true);
			objectNodeValidity[4].ShouldBe(true);
			objectNodeValidity[5].ShouldBe(false);

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(6);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].Node.Token.Value.ShouldBe("*");
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].Node.Token.Value.ShouldBe("NAME");
			columnNodeValidity[2].IsRecognized.ShouldBe(true);
			columnNodeValidity[2].Node.Token.Value.ShouldBe("NAME");
			columnNodeValidity[3].IsRecognized.ShouldBe(false);
			columnNodeValidity[3].Node.Token.Value.ShouldBe("DUMMY");
			columnNodeValidity[4].IsRecognized.ShouldBe(true);
			columnNodeValidity[4].Node.Token.Value.ShouldBe("SELECTION_ID");
			columnNodeValidity[5].IsRecognized.ShouldBe(false);
			columnNodeValidity[5].Node.Token.Value.ShouldBe("UNDEFINED_COLUMN");

			nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var functionNodeValidity = nodeValidityDictionary.Values.ToList();
			functionNodeValidity.Count.ShouldBe(2);
			functionNodeValidity[0].IsRecognized.ShouldBe(true);
			functionNodeValidity[0].Node.Token.Value.ShouldBe("UPPER");
			functionNodeValidity[1].IsRecognized.ShouldBe(false);
			functionNodeValidity[1].Node.Token.Value.ShouldBe("UNDEFINED_FUNCTION");
		}

		[Test]
		public void TestAliasReferenceNodeValidityInOrderByClause()
		{
			const string sqlText = "SELECT DUMMY NOT_DUMMY FROM DUAL ORDER BY NOT_DUMMY";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(2);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].Node.Token.Value.ShouldBe("DUMMY");
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].Node.Token.Value.ShouldBe("NOT_DUMMY");
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestColumnReferenceNodeValidityInOrderByClause()
		{
			const string sqlText = "SELECT DUMMY NOT_DUMMY, DUMMY FROM DUAL ORDER BY DUMMY";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(3);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].Node.Token.Value.ShouldBe("DUMMY");
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].Node.Token.Value.ShouldBe("DUMMY");
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[2].IsRecognized.ShouldBe(true);
			columnNodeValidity[2].Node.Token.Value.ShouldBe("DUMMY");
			columnNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestAmbiguousAliasReferenceNodeValidityInOrderByClause()
		{
			const string sqlText = "SELECT NAME, SELECTION_ID NAME FROM SELECTION ORDER BY NAME";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(3);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].Node.Token.Value.ShouldBe("NAME");
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].Node.Token.Value.ShouldBe("SELECTION_ID");
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[2].IsRecognized.ShouldBe(true);
			columnNodeValidity[2].Node.Token.Value.ShouldBe("NAME");
			columnNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.AmbiguousReference);
		}

		[Test]
		public void TestInvalidAliasReferenceNodeValidityInOrderByClause()
		{
			const string sqlText = "SELECT NAME X FROM SELECTION UNION ALL SELECT NAME FROM SELECTION ORDER BY NAME";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(3);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].Node.Token.Value.ShouldBe("NAME");
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].Node.Token.Value.ShouldBe("NAME");
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[2].IsRecognized.ShouldBe(false);
			columnNodeValidity[2].Node.Token.Value.ShouldBe("NAME");
			columnNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestValidAliasReferenceNodeValidityInOrderByClauseUsingConcatenatedSubqueriesWithMissingAliasInLastQuery()
		{
			const string sqlText = "SELECT RESPONDENTBUCKET_ID ID FROM RESPONDENTBUCKET UNION ALL SELECT TARGETGROUP_ID ID FROM TARGETGROUP UNION ALL SELECT PROJECT_ID FROM PROJECT ORDER BY ID";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(4);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].Node.Token.Value.ShouldBe("RESPONDENTBUCKET_ID");
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].Node.Token.Value.ShouldBe("TARGETGROUP_ID");
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[2].IsRecognized.ShouldBe(true);
			columnNodeValidity[2].Node.Token.Value.ShouldBe("PROJECT_ID");
			columnNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[3].IsRecognized.ShouldBe(true);
			columnNodeValidity[3].Node.Token.Value.ShouldBe("ID");
			columnNodeValidity[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestInvalidAliasReferenceNodeValidityInOrderByClauseUsingConcatenatedSubqueriesWithMissingAliasInLastQuery()
		{
			const string sqlText = "SELECT RESPONDENTBUCKET_ID ID FROM RESPONDENTBUCKET UNION ALL SELECT TARGETGROUP_ID FROM TARGETGROUP UNION ALL SELECT PROJECT_ID FROM PROJECT ORDER BY ID";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(4);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].Node.Token.Value.ShouldBe("RESPONDENTBUCKET_ID");
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].Node.Token.Value.ShouldBe("TARGETGROUP_ID");
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[2].IsRecognized.ShouldBe(true);
			columnNodeValidity[2].Node.Token.Value.ShouldBe("PROJECT_ID");
			columnNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[3].IsRecognized.ShouldBe(false);
			columnNodeValidity[3].Node.Token.Value.ShouldBe("ID");
			columnNodeValidity[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestColumnNodeValidityWhenObjectReferenceIsAliasedCommonTableExpression()
		{
			const string sqlText = "WITH CTE AS (SELECT DUMMY FROM DUAL) SELECT	DUMMY FROM CTE T1";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(2);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
		}

		[Test]
		public void TestGreatestFuctionWithUnlimitedMaximumParameterCount()
		{
			const string sqlText = "SELECT GREATEST(1, 2, 3) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(1);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestAmbiguousColumnReferenceUsingAsterisk()
		{
			const string sqlText = "SELECT * FROM (SELECT 1 NAME, 2 NAME FROM DUAL)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(1);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.AmbiguousReference);
		}

		[Test]
		public void TestFunctionIdentifierNodeValidWithoutOwnerNodeInSameSchema()
		{
			const string sqlText = "SELECT SQLPAD_FUNCTION() WITH_PARENTHESES, SQLPAD_FUNCTION WITHOUT_PARENTHESES FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(2);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programNodeValidity[1].IsRecognized.ShouldBe(true);
			programNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestNotAmbiguousRowIdReference()
		{
			const string sqlText = "SELECT SELECTION.ROWID FROM SELECTION, PROJECT";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(1);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestSemanticErrorWhenUserNonAggregateFunctionHasAnalyticClause()
		{
			const string sqlText = "SELECT HUSQVIK.COUNT() OVER () FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var nodeValidity = validationModel.ProgramNodeValidity
				.OrderBy(cv => cv.Key.SourcePosition.IndexStart)
				.Select(cv => cv.Value).ToArray();

			nodeValidity.Length.ShouldBe(2);
			nodeValidity[1].IsRecognized.ShouldBe(true);
			nodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.AnalyticClauseNotSupported);
		}

		[Test]
		public void TestColumnNodeValidityInCorrelatedSubquery()
		{
			const string sqlText = "SELECT * FROM DUAL D WHERE EXISTS (SELECT NULL FROM DUAL WHERE DUMMY = D.DUMMY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.ToList();
			objectNodeValidity.Count.ShouldBe(3);
			objectNodeValidity.ForEach(v => v.IsRecognized.ShouldBe(true));

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(3);
			columnNodeValidity[2].IsRecognized.ShouldBe(true);
			columnNodeValidity[2].Node.Token.Value.ShouldBe("DUMMY");
			columnNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestColumnNodeValidityInCorrelatedSubqueryWithoutObjectQualifierAndInSelectList()
		{
			const string sqlText = "SELECT (SELECT ID FROM INVOICES WHERE ID = S.SELECTION_ID) FROM SELECTION S WHERE EXISTS (SELECT NULL FROM INVOICES WHERE ID = SELECTION_ID)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.ToList();
			objectNodeValidity.Count.ShouldBe(4);
			objectNodeValidity.ForEach(v => v.IsRecognized.ShouldBe(true));
			objectNodeValidity.ForEach(v => v.SemanticErrorType.ShouldBe(OracleSemanticErrorType.None));

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(5);
			columnNodeValidity.ForEach(v => v.IsRecognized.ShouldBe(true));
			columnNodeValidity.ForEach(v => v.SemanticErrorType.ShouldBe(OracleSemanticErrorType.None));
		}

		[Test]
		public void TestFunctionIdentifierNodeValidDefinedBySynonym()
		{
			const string sqlText = "SELECT DBMS_RANDOM.STRING('X', 16) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(2);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programNodeValidity[1].IsRecognized.ShouldBe(true);
			programNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestMissingFunctionInExistingPackage()
		{
			const string sqlText = "SELECT DBMS_RANDOM.UNDEFINED_FUNCTION() FROM DUAL";
			TestMissingFunctionInExistingPackageInternal(sqlText);
		}

		[Test]
		public void TestMissingFunctionInExistingPackageWithoutInvokationParentheses()
		{
			const string sqlText = "SELECT DBMS_RANDOM.UNDEFINED_FUNCTION FROM DUAL";
			TestMissingFunctionInExistingPackageInternal(sqlText);
		}

		private void TestMissingFunctionInExistingPackageInternal(string statementText)
		{
			var statement = Parser.Parse(statementText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(statementText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(2);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programNodeValidity[1].IsRecognized.ShouldBe(false);
			programNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestSchemaFunctionWithCompilationErrors()
		{
			const string sqlText = "SELECT UNCOMPILABLE_FUNCTION() FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var functionNodeValidity = nodeValidityDictionary.Values.ToList();
			functionNodeValidity.Count.ShouldBe(1);
			functionNodeValidity[0].IsRecognized.ShouldBe(true);
			functionNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ObjectStatusInvalid);
		}

		[Test]
		public void TestPackageFunctionWithCompilationErrors()
		{
			const string sqlText = "SELECT UNCOMPILABLE_PACKAGE.FUNCTION() FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(2);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ObjectStatusInvalid);
			programNodeValidity[1].IsRecognized.ShouldBe(true);
			programNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestBuildInFunctionValidityWithoutLoadedSchemaObjects()
		{
			const string sqlText = "SELECT NVL2(DUMMY, 'X', 'NOT DUMMY') FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var databaseModel = new OracleTestDatabaseModel();
			databaseModel.AllObjects.Clear();

			var validationModel = BuildValidationModel(sqlText, statement, databaseModel);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(1);
			programNodeValidity[0].IsRecognized.ShouldBe(false);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestProgramValidityNodesWithObjectTypes()
		{
			const string sqlText = "SELECT XMLTYPE('<Root/>'), SYS.XMLTYPE('<Root/>') FROM DUAL WHERE XMLTYPE('<Root/>') IS NOT NULL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(3);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programNodeValidity[1].IsRecognized.ShouldBe(true);
			programNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programNodeValidity[2].IsRecognized.ShouldBe(true);
			programNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestObjectTypeConstructorWithInvalidParameterCount()
		{
			const string sqlText = "SELECT SYS.ODCIARGDESC(1) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(1);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidParameterCount);
			programNodeValidity[0].Node.ShouldNotBe(null);
			var invalidTerminals = programNodeValidity[0].Node.Terminals.ToArray();
			invalidTerminals.Length.ShouldBe(3);
			invalidTerminals[0].Token.Value.ShouldBe("(");
			invalidTerminals[1].Token.Value.ShouldBe("1");
			invalidTerminals[2].Token.Value.ShouldBe(")");
		}

		[Test]
		public void TestCollectionTypeConstructorHasAlwaysValidParameterCount()
		{
			const string sqlText = "SELECT SYS.ODCIRAWLIST(NULL, NULL) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(1);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestCollectionTypeConstructorWithNoParameters()
		{
			const string sqlText = "SELECT SYS.ODCIARGDESCLIST() FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(1);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestSequenceCombinedWithOrderByClause()
		{
			const string sqlText = "SELECT TEST_SEQ.NEXTVAL FROM DUAL ORDER BY 1";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nonTerminalValidity = nodeValidityDictionary.Values.ToList();
			nonTerminalValidity.Count.ShouldBe(2);
			nonTerminalValidity[0].IsRecognized.ShouldBe(true);
			nonTerminalValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].Node.ShouldNotBe(null);
			nonTerminalValidity[0].Node.TerminalCount.ShouldBe(3);
			nonTerminalValidity[1].IsRecognized.ShouldBe(true);
			nonTerminalValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ClauseNotAllowed);
			nonTerminalValidity[1].ToolTipText.ShouldBe(OracleSemanticErrorType.ClauseNotAllowed);
			nonTerminalValidity[1].Node.ShouldNotBe(null);
			nonTerminalValidity[1].Node.TerminalCount.ShouldBe(3);
		}

		[Test]
		public void TestSequenceCombinedWithGroupByClause()
		{
			const string sqlText = "SELECT test_seq.nextval FROM dual GROUP BY dummy";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nonTerminalValidity = validationModel.InvalidNonTerminals.Values.OrderBy(nv => nv.Node.SourcePosition.IndexStart).ToList();
			nonTerminalValidity.Count.ShouldBe(1);
			nonTerminalValidity[0].IsRecognized.ShouldBe(true);
			nonTerminalValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].Node.ShouldNotBe(null);
			nonTerminalValidity[0].Node.TerminalCount.ShouldBe(3);
		}

		[Test]
		public void TestSequenceCombinedWithHavingClause()
		{
			const string sqlText = "SELECT test_seq.nextval FROM dual HAVING dummy IS NOT NULL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nonTerminalValidity = validationModel.InvalidNonTerminals.Values.OrderBy(nv => nv.Node.SourcePosition.IndexStart).ToList();
			nonTerminalValidity.Count.ShouldBe(1);
			nonTerminalValidity[0].IsRecognized.ShouldBe(true);
			nonTerminalValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].Node.ShouldNotBe(null);
			nonTerminalValidity[0].Node.TerminalCount.ShouldBe(3);
		}

		[Test]
		public void TestSequenceWithinSubquery()
		{
			const string sqlText = "SELECT NEXTVAL FROM (SELECT TEST_SEQ.NEXTVAL FROM DUAL)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nonTerminalValidity = nodeValidityDictionary.Values.ToList();
			nonTerminalValidity.Count.ShouldBe(1);
			nonTerminalValidity[0].IsRecognized.ShouldBe(true);
			nonTerminalValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].Node.ShouldNotBe(null);
			nonTerminalValidity[0].Node.TerminalCount.ShouldBe(3);
		}

		[Test]
		public void TestSequenceWithDistinct()
		{
			const string sqlText = "SELECT DISTINCT TEST_SEQ.NEXTVAL FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nonTerminalValidity = nodeValidityDictionary.Values.ToList();
			nonTerminalValidity.Count.ShouldBe(1);
			nonTerminalValidity[0].IsRecognized.ShouldBe(true);
			nonTerminalValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].Node.ShouldNotBe(null);
			nonTerminalValidity[0].Node.TerminalCount.ShouldBe(3);
		}

		[Test]
		public void TestColumnValidityNodesWithSequence()
		{
			const string sqlText = "SELECT TEST_SEQ.NEXTVAL, HUSQVIK.TEST_SEQ.\"NEXTVAL\", SYNONYM_TO_TEST_SEQ.CURRVAL FROM DUAL WHERE TEST_SEQ.\"CURRVAL\" < TEST_SEQ.NEXTVAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var nonTerminalValidity = nodeValidityDictionary.Values.ToList();
			nonTerminalValidity.Count.ShouldBe(2);
			nonTerminalValidity.ForEach(n => n.IsRecognized.ShouldBe(true));
			nonTerminalValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[0].Node.ShouldNotBe(null);
			nonTerminalValidity[0].Node.TerminalCount.ShouldBe(3);
			nonTerminalValidity[0].Node.SourcePosition.IndexStart.ShouldBe(97);
			nonTerminalValidity[0].Node.SourcePosition.Length.ShouldBe(18);
			nonTerminalValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[1].ToolTipText.ShouldBe(OracleSemanticErrorType.SequenceNumberNotAllowedHere);
			nonTerminalValidity[1].Node.ShouldNotBe(null);
			nonTerminalValidity[1].Node.TerminalCount.ShouldBe(3);
			nonTerminalValidity[1].Node.SourcePosition.IndexStart.ShouldBe(118);
			nonTerminalValidity[1].Node.SourcePosition.Length.ShouldBe(16);

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(5);
			columnNodeValidity.ForEach(n => n.IsRecognized.ShouldBe(true));
			columnNodeValidity.ForEach(n => n.SemanticErrorType.ShouldBe(OracleSemanticErrorType.None));
		}

		[Test]
		public void TestSequenceInvalidColumnValidity()
		{
			const string sqlText = "SELECT TEST_SEQ.UNDEFINED_COLUMN FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(1);
			columnNodeValidity[0].IsRecognized.ShouldBe(false);
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestAsteriskValidOrderByExpressionIndexValidityWithUnrecognizedObject()
		{
			const string sqlText = "SELECT * FROM DUAL, NOT_EXISTING_TABLE ORDER BY 1";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(1);
		}

		[Test]
		public void TestAsteriskInvalidOrderByExpressionIndexValidityWithUnrecognizedObject()
		{
			const string sqlText = "SELECT * FROM DUAL, NOT_EXISTING_TABLE ORDER BY 2";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(1);
		}

		[Test]
		public void TestFunctionNodeValidityOverUndefinedDatabaseLink()
		{
			const string sqlText = "SELECT SQLPAD_FUNCTION@UNDEFINED_DB_LINK FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(1);
			var databaseLinkNameTerminals = programNodeValidity[0].Node.Terminals.ToArray();
			databaseLinkNameTerminals.Length.ShouldBe(1);
			databaseLinkNameTerminals[0].Token.Value.ShouldBe("UNDEFINED_DB_LINK");
			programNodeValidity[0].IsRecognized.ShouldBe(false);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestObjectReferenceNodeValidityOverUndefinedDatabaseLink()
		{
			const string sqlText = "SELECT * FROM SELECTION@UNDEFINED_DB_LINK";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.ToList();
			objectNodeValidity.Count.ShouldBe(1);
			var databaseLinkNameTerminals = objectNodeValidity[0].Node.Terminals.ToArray();
			databaseLinkNameTerminals.Length.ShouldBe(1);
			databaseLinkNameTerminals[0].Token.Value.ShouldBe("UNDEFINED_DB_LINK");
			objectNodeValidity[0].IsRecognized.ShouldBe(false);
			objectNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestSequenceAndPseudocolumnValidityOverUndefinedDatabaseLink()
		{
			const string sqlText = "SELECT TEST_SEQ.NEXTVAL@UNDEFINED_DB_LINK FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var objectNodeValidity = nodeValidityDictionary.Values.ToList();
			objectNodeValidity.Count.ShouldBe(2);
			var databaseLinkNameTerminals = objectNodeValidity[0].Node.Terminals.ToArray();
			databaseLinkNameTerminals.Length.ShouldBe(1);
			databaseLinkNameTerminals[0].Token.Value.ShouldBe("UNDEFINED_DB_LINK");
			objectNodeValidity[0].IsRecognized.ShouldBe(false);
			objectNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			objectNodeValidity[1].Node.Token.Value.ShouldBe("DUAL");
			objectNodeValidity[1].IsRecognized.ShouldBe(true);
			objectNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);

			nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(0);

			nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(0);
		}

		[Test]
		public void TestInvalidIdentifier()
		{
			const string sqlText = "SELECT \"\" FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var identifierNodeValidity = nodeValidityDictionary.Values.ToList();
			identifierNodeValidity.Count.ShouldBe(1);
			identifierNodeValidity[0].IsRecognized.ShouldBe(true);
			identifierNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIdentifier);
			identifierNodeValidity[0].ToolTipText.ShouldBe("Identifier length must be between one and 30 characters excluding quotes. ");
		}

		[Test]
		public void TestInvalidBindVariableIdentifier()
		{
			const string sqlText = "SELECT :999999, :9 FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var identifierNodeValidity = nodeValidityDictionary.Values.ToList();
			identifierNodeValidity.Count.ShouldBe(1);
			identifierNodeValidity[0].IsRecognized.ShouldBe(true);
			identifierNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIdentifier);
			identifierNodeValidity[0].ToolTipText.ShouldBe("Numeric bind variable identifier must be between 0 and 65535. ");
		}

		[Test]
		public void TestAmbiguousColumnReferenceUsingAsteriskReferingAnotherAsterisk()
		{
			const string sqlText = "SELECT * FROM (SELECT * FROM DUAL, DUAL X)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(2);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.AmbiguousReference);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestExposedGeneratedColumnWithoutAliasValidity()
		{
			const string sqlText = "SELECT TO_CHAR FROM (SELECT TO_CHAR('') FROM DUAL)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(0);

			nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(2);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidParameterCount);
			programNodeValidity[1].IsRecognized.ShouldBe(true);
			programNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestUpdateSubqueryColumnValidity()
		{
			const string sqlText = "UPDATE (SELECT * FROM SELECTION) SET NAME = 'Dummy selection' WHERE SELECTION_ID = 0";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var columnNodeValidity = nodeValidityDictionary.Values.ToList();
			columnNodeValidity.Count.ShouldBe(3);
			columnNodeValidity.ForEach(c => c.IsRecognized.ShouldBe(true));
			columnNodeValidity.ForEach(c => c.SemanticErrorType.ShouldBe(null));
		}

		[Test]
		public void TestValidInsertColumnCount()
		{
			const string sqlText = "INSERT INTO SELECTION (RESPONDENTBUCKET_ID, NAME) SELECT RESPONDENTBUCKET_ID, NAME FROM SELECTION";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Values.ToList().ForEach(v => v.SemanticErrorType.ShouldBe(null));
		}

		[Test]
		public void TestValidInsertColumnCountIntoTableWithInvisibleColumns()
		{
			const string sqlText = "INSERT INTO \"CaseSensitiveTable\" VALUES (NULL, NULL)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}

		[Test]
		public void TestInvalidInsertColumnCountWithBothListsDefined()
		{
			const string sqlText = "INSERT INTO SELECTION (RESPONDENTBUCKET_ID, NAME) SELECT RESPONDENTBUCKET_ID, NAME, PROJECT_ID FROM SELECTION";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var semanticErrorNodes = nodeValidityDictionary.Values.Where(v => v.SemanticErrorType != null).ToList();
			semanticErrorNodes.Count.ShouldBe(2);
			semanticErrorNodes[0].Node.GetText(sqlText).ShouldBe("(RESPONDENTBUCKET_ID, NAME)");
			semanticErrorNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			semanticErrorNodes[1].Node.GetText(sqlText).ShouldBe("RESPONDENTBUCKET_ID, NAME, PROJECT_ID");
			semanticErrorNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
		}

		[Test]
		public void TestInvalidInsertColumnCountWithInsertListOnly()
		{
			const string sqlText = "INSERT INTO SELECTION (RESPONDENTBUCKET_ID, NAME) SELECT * FROM SELECTION";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var semanticErrorNodes = nodeValidityDictionary.Values.Where(v => v.SemanticErrorType != null).ToList();
			semanticErrorNodes.Count.ShouldBe(2);
			semanticErrorNodes[0].Node.GetText(sqlText).ShouldBe("(RESPONDENTBUCKET_ID, NAME)");
			semanticErrorNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			semanticErrorNodes[1].Node.GetText(sqlText).ShouldBe("*");
			semanticErrorNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
		}

		[Test]
		public void TestInvalidInsertColumnCountWithSelectListOnly()
		{
			const string sqlText = "INSERT INTO SELECTION SELECT RESPONDENTBUCKET_ID, NAME, PROJECT_ID FROM SELECTION";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var semanticErrorNodes = nodeValidityDictionary.Values.Where(v => v.SemanticErrorType != null).ToList();
			semanticErrorNodes.Count.ShouldBe(1);
			semanticErrorNodes[0].Node.GetText(sqlText).ShouldBe("RESPONDENTBUCKET_ID, NAME, PROJECT_ID");
			semanticErrorNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
		}

		[Test]
		public void TestInsertColumnNodeValidityUsingValuesCaluseWithFunctionTypeAndSequence()
		{
			const string sqlText = "INSERT INTO SELECTION (SELECTION_ID, SELECTIONNAME, RESPONDENTBUCKET_ID) VALUES (SQLPAD_FUNCTION, XMLTYPE(), TEST_SEQ.NEXTVAL)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			var semanticErrorNodes = nodeValidityDictionary.Values.Where(v => v.SemanticErrorType != null).ToList();
			semanticErrorNodes.Count.ShouldBe(0);

			nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(2);
			var programNodeValidity = nodeValidityDictionary.Values.ToList();
			programNodeValidity.Count.ShouldBe(2);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[1].IsRecognized.ShouldBe(true);

			nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(2);
			var columnNodeValidity = nodeValidityDictionary.Values.ToArray();
			columnNodeValidity[1].SemanticErrorType.ShouldBe(null);
		}

		[Test]
		public void TestUnfinishedInsertValidationModelBuild()
		{
			const string sqlText = "INSERT INTO SELECTION";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.SequenceNotFound);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(1);
			var columnNodeValidity = nodeValidityDictionary.Values.ToArray();
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
		}

		[Test]
		public void TestConcatenatedQueryBlocksWithDifferentColumnCount()
		{
			const string sqlText = "SELECT 1, 2 FROM DUAL UNION ALL SELECT 1 FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(2);
			var invalidNonTerminalValidityDictionary = nodeValidityDictionary.Values.ToList();
			invalidNonTerminalValidityDictionary.ForEach(nv => nv.SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount));
		}

		[Test]
		public void TestMultipleConcatenatedQueryBlocksWithDifferentColumnCountWithTerminatorSymbol()
		{
			const string sqlText = "SELECT 1 FROM DUAL UNION ALL SELECT 2 FROM DUAL UNION ALL SELECT 3, 4 FROM DUAL;";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(3);
			var invalidNonTerminalValidityDictionary = nodeValidityDictionary.Values.ToList();
			invalidNonTerminalValidityDictionary.ForEach(nv => nv.SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount));
		}

		[Test]
		public void TestAggregateFunctionWithinAnalyticFunctionWithScalarFunction()
		{
			const string sqlText = "SELECT ROUND(RATIO_TO_REPORT(COUNT(*)) OVER (PARTITION BY TRUNC(NULL, 'HH')) * 100, 2) PERCENT FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(4);
			var nodeValidity = nodeValidityDictionary.Values.ToList();
			nodeValidity.ForEach(nv => nv.SemanticErrorType.ShouldBe(null));
		}

		[Test]
		public void TestOrderByReferencesToAliasWhenSelectListContainsColumnWithSameName()
		{
			const string sqlText = "SELECT VAL + 1 VAL, VAL ALIAS FROM (SELECT 1 VAL FROM DUAL) ORDER BY VAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(3);
			var nodeValidity = nodeValidityDictionary.Values.ToList();
			nodeValidity.ForEach(nv => nv.SemanticErrorType.ShouldBe(null));
		}

		[Test]
		public void TestOrderByAmbiguousColumnReference()
		{
			const string sqlText = "SELECT VAL + 1 VAL, VAL FROM (SELECT 1 VAL FROM DUAL) ORDER BY VAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(3);
			var nodeValidity = nodeValidityDictionary.Values.ToList();
			nodeValidity[0].SemanticErrorType.ShouldBe(null);
			nodeValidity[1].SemanticErrorType.ShouldBe(null);
			nodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.AmbiguousReference);
		}

		[Test]
		public void TestModelInitializatinWhileTypingConcatenatedSubquery()
		{
			const string sqlText = "SELECT NULL FROM DUAL UNION SELECT";
			var statement = Parser.Parse(sqlText).Single();

			BuildValidationModel(sqlText, statement);
		}

		[Test]
		public void TestLongXmlAliases()
		{
			const string sqlText = "SELECT XMLELEMENT(NAME \"VeryLongXmlAliasVeryLongXmlAlias\", NULL) VAL1, XMLELEMENT(NAME VeryLongXmlAliasVeryLongXmlAlias, NULL) VAL2 FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidityDictionary = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).ToDictionary(nv => nv.Key, nv => nv.Value);
			nodeValidityDictionary.Count.ShouldBe(0);
		}

		[Test]
		public void TestEmptyXmlAlias()
		{
			const string sqlText = "SELECT XMLELEMENT(NAME \"\", NULL) VAL FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValiditities = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(nv => nv.Value).ToArray();
			nodeValiditities.Length.ShouldBe(1);
			nodeValiditities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIdentifier);
		}

		[Test]
		public void TestUndefinedPackageFunctionCall()
		{
			const string sqlText = @"SELECT UNDEFINEDPACKAGE.FUNCTION() FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodesWithSemanticError = validationModel.SemanticErrors.ToArray();
			nodesWithSemanticError.Length.ShouldBe(0);
		}

		[Test]
		public void TestFullyQualifiedTableOverDatabaseLink()
		{
			const string sqlText = @"SELECT * FROM HUSQVIK.SELECTION@HQ_PDB_LOOPBACK";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var objectNodes = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			objectNodes.Length.ShouldBe(1);
			objectNodes[0].IsRecognized.ShouldBe(true);
			objectNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			objectNodes[0].Node.ShouldNotBe(null);
			objectNodes[0].Node.Token.Value.ShouldBe("HQ_PDB_LOOPBACK");
		}

		[Test]
		public void TestColumnSuggestionOverDatabaseLink()
		{
			const string sqlText = @"SELECT NAME FROM SELECTION@HQ_PDB_LOOPBACK, DUAL@HQ_PDB_LOOPBACK";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnNodes = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			columnNodes.Length.ShouldBe(1);
			columnNodes[0].IsRecognized.ShouldBe(true);
			columnNodes[0].SuggestionType.ShouldBe(OracleSuggestionType.PotentialDatabaseLink);
			columnNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestColumnSuggestionOverDatabaseLinkWhenSameColumnAvailableFromLocalReference()
		{
			const string sqlText = @"SELECT DUMMY FROM DUAL, DUAL@HQ_PDB_LOOPBACK";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnNodes = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			columnNodes.Length.ShouldBe(1);
			columnNodes[0].IsRecognized.ShouldBe(true);
			columnNodes[0].SuggestionType.ShouldBe(OracleSuggestionType.None);
			columnNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestNoColumnSuggestionOverDatabaseLinkWhenOnlySingleObjectReferenced()
		{
			const string sqlText = @"SELECT DUMMY FROM DUAL@HQ_PDB_LOOPBACK";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnNodes = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			columnNodes.Length.ShouldBe(0);
		}

		[Test]
		public void TestAsteriskColumnSuggestionOverDatabaseLink()
		{
			const string sqlText = @"SELECT * FROM SELECTION@HQ_PDB_LOOPBACK";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnValidationData = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			columnValidationData.Length.ShouldBe(1);
			columnValidationData[0].IsRecognized.ShouldBe(true);
			columnValidationData[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestObjectQualifiedColumnSuggestionOverDatabaseLink()
		{
			const string sqlText = @"SELECT SELECTION.NAME FROM SELECTION@HQ_PDB_LOOPBACK";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnNodes = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			columnNodes.Length.ShouldBe(0);
		}

		[Test]
		public void TestDateAndTimeStampLiteralInvalidFormat()
		{
			const string sqlText = @"SELECT DATE'2014-12-06 17:50:42', TIMESTAMP'2014-12-06', DATE'-2014-12-06', TIMESTAMP'+2014-12-06 17:50:42 CET' FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNodes = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			invalidNodes.Length.ShouldBe(2);
			invalidNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidDateLiteral);
			invalidNodes[0].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidDateLiteral);
			invalidNodes[0].Node.Token.Value.ShouldBe("'2014-12-06 17:50:42'");
			invalidNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidTimestampLiteral);
			invalidNodes[1].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidTimestampLiteral);
			invalidNodes[1].Node.Token.Value.ShouldBe("'2014-12-06'");
		}

		[Test]
		public void TestInvalidDateAndTimeStampLiteralStartingWithSpace()
		{
			const string sqlText = @"SELECT DATE' 2014-12-06', TIMESTAMP' 2014-12-06 17:50:42' FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNodes = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			invalidNodes.Length.ShouldBe(2);
			invalidNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidDateLiteral);
			invalidNodes[0].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidDateLiteral);
			invalidNodes[0].Node.Token.Value.ShouldBe("' 2014-12-06'");
			invalidNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidTimestampLiteral);
			invalidNodes[1].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidTimestampLiteral);
			invalidNodes[1].Node.Token.Value.ShouldBe("' 2014-12-06 17:50:42'");
		}

		[Test]
		public void TestIntervalYearToMonthLiteral()
		{
			const string sqlText =
@"SELECT
	INTERVAL ' 123 - 2 ' YEAR(3) TO MONTH,
	INTERVAL ' - 999999999 ' MONTH,
	INTERVAL '123-12' YEAR(3) TO MONTH,
	INTERVAL '9999999999' MONTH,
	INTERVAL '123-11' MONTH(3) TO YEAR,
	INTERVAL '1234-11' MONTH(2),
	INTERVAL '0' MONTH(10)
FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNodes = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			invalidNodes.Length.ShouldBe(5);
			invalidNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[0].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidIntervalYearToMonthLiteral);
			invalidNodes[0].Node.Token.Value.ShouldBe("'123-12'");
			invalidNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[1].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidIntervalYearToMonthLiteral);
			invalidNodes[1].Node.Token.Value.ShouldBe("'9999999999'");
			invalidNodes[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[2].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[2].Node.Token.Value.ShouldBe("YEAR");
			invalidNodes[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[3].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidIntervalYearToMonthLiteral);
			invalidNodes[3].Node.Token.Value.ShouldBe("'1234-11'");
			invalidNodes[4].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[4].ToolTipText.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[4].Node.Token.Value.ShouldBe("10");
		}

		[Test]
		public void TestIntervalDayToSecondLiteral()
		{
			const string sqlText =
@"SELECT
	INTERVAL ' + 000000000 03 : 35 : 21 . 135802468  ' DAY(9) TO SECOND(9),
	INTERVAL ' - 999999999 . 999999999 ' SECOND,
	INTERVAL ' - 999999999 : 59 . 999999999 ' MINUTE(9) TO SECOND,
	INTERVAL ' - 999999999 : 59 : 59 . 999999999 ' HOUR(9) TO SECOND,
	INTERVAL ' 999999999 : 60 . 999999999 ' MINUTE(9) TO HOUR,
	INTERVAL ' - 999999999 : 59 . 015 ' MINUTE(5) TO SECOND(2),
	INTERVAL '0' DAY(10) TO SECOND(11),
	INTERVAL ' - 123456789 . 987654321 ' SECOND(10, 99),
	INTERVAL '0' SECOND(9, -0)
FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNodes = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			invalidNodes.Length.ShouldBe(8);

			invalidNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[0].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidIntervalDayToSecondLiteral);
			invalidNodes[0].Node.Token.Value.ShouldBe("' 999999999 : 60 . 999999999 '");
			invalidNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[1].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidIntervalLiteral);
			invalidNodes[1].Node.Token.Value.ShouldBe("HOUR");
			invalidNodes[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.LeadingPrecisionOfTheIntervalIsTooSmall);
			invalidNodes[2].ToolTipText.ShouldBe(OracleSemanticErrorType.LeadingPrecisionOfTheIntervalIsTooSmall);
			invalidNodes[2].Node.Token.Value.ShouldBe("5");
			invalidNodes[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[3].ToolTipText.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[3].Node.Token.Value.ShouldBe("10");
			invalidNodes[4].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[4].ToolTipText.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[4].Node.Token.Value.ShouldBe("11");
			invalidNodes[5].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[5].ToolTipText.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[5].Node.Token.Value.ShouldBe("10");
			invalidNodes[6].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[6].ToolTipText.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[6].Node.Id.ShouldBe(NonTerminals.NegativeInteger);
			invalidNodes[7].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[7].ToolTipText.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
			invalidNodes[7].Node.Id.ShouldBe(NonTerminals.NegativeInteger);
		}

		[Test]
		public void TestInvalidDateAndTimeStampLiteralUsingMultiByteStrings()
		{
			const string sqlText = @"SELECT DATE N'2014-12-06', TIMESTAMP n'2014-12-06 17:50:42' FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNodes = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			invalidNodes.Length.ShouldBe(2);
			invalidNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidDateLiteral);
			invalidNodes[0].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidDateLiteral);
			invalidNodes[0].Node.Token.Value.ShouldBe("N'2014-12-06'");
			invalidNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidTimestampLiteral);
			invalidNodes[1].ToolTipText.ShouldBe(OracleSemanticErrorTooltipText.InvalidTimestampLiteral);
			invalidNodes[1].Node.Token.Value.ShouldBe("n'2014-12-06 17:50:42'");
		}

		[Test]
		public void TestLevelFunctionWithoutConnectByClause()
		{
			const string sqlText = @"SELECT LEVEL FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var programNodeValidity = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			programNodeValidity.Length.ShouldBe(1);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			programNodeValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			programNodeValidity[0].Node.Token.Value.ShouldBe("LEVEL");
		}

		[Test]
		public void TestLevelFunctionWithoutConnectByClauseAsFunctionParameter()
		{
			const string sqlText = @"SELECT DBMS_RANDOM.VALUE(1, LEVEL) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var programNodeValidity = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			programNodeValidity.Length.ShouldBe(3);
			programNodeValidity[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			programNodeValidity[2].Node.FirstTerminalNode.Token.Value.ShouldBe("LEVEL");
		}

		[Test]
		public void TestLevelFunctionOutsideQueryBlock()
		{
			const string sqlText = @"UPDATE DUAL SET DUMMY = LEVEL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var programNodeValidity = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			programNodeValidity.Length.ShouldBe(1);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			programNodeValidity[0].ToolTipText.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			programNodeValidity[0].Node.Token.Value.ShouldBe("LEVEL");
		}

		[Test]
		public void TestPriorOperator()
		{
			const string sqlText = @"UPDATE DUAL SET DUMMY = (SELECT PRIOR DUMMY FROM DUAL) WHERE CONNECT_BY_ROOT DUMMY IS NULL OR PRIOR DUMMY IS NULL OR SYS_CONNECT_BY_PATH(DUMMY, '-') IS NOT NULL OR EXISTS (SELECT PRIOR DUMMY, CONNECT_BY_ROOT DUMMY, SYS_CONNECT_BY_PATH(DUMMY, '-') FROM DUAL CONNECT BY LEVEL <= 3)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var nodeValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			nodeValidities.Length.ShouldBe(3);
			nodeValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[0].ToolTipText.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[0].Node.Token.Value.ShouldBe("PRIOR");
			nodeValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[1].ToolTipText.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[1].Node.Token.Value.ShouldBe("CONNECT_BY_ROOT");
			nodeValidities[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[2].ToolTipText.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[2].Node.Token.Value.ShouldBe("PRIOR");

			nodeValidities = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			nodeValidities.Length.ShouldBe(4);
			nodeValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[0].ToolTipText.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			nodeValidities[0].Node.FirstTerminalNode.Token.Value.ShouldBe("SYS_CONNECT_BY_PATH");
			nodeValidities[1].IsRecognized.ShouldBe(true);
			nodeValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidities[1].Node.Token.Value.ShouldBe("SYS_CONNECT_BY_PATH");
			nodeValidities[2].IsRecognized.ShouldBe(true);
			nodeValidities[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidities[2].Node.Token.Value.ShouldBe("SYS_CONNECT_BY_PATH");
			nodeValidities[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			nodeValidities[3].Node.Token.Value.ShouldBe("LEVEL");
		}

		[Test]
		public void TestColumnResolutionFromTableCollectionExpressionUsingCollectionType()
		{
			const string sqlText = @"SELECT PLAN_TABLE_OUTPUT, COLUMN_VALUE FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST ADVANCED')) T1, TABLE(SYS.ODCIRAWLIST(HEXTORAW('ABCDEF'), HEXTORAW('A12345'), HEXTORAW('F98765'))) T2";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnNodeValidity = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToArray();
			columnNodeValidity.Length.ShouldBe(2);
			columnNodeValidity[0].IsRecognized.ShouldBe(true);
			columnNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnNodeValidity[1].IsRecognized.ShouldBe(true);
			columnNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);

			var programNodeValidity = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programNodeValidity.ForEach(v =>
			{
				v.IsRecognized.ShouldBe(true);
				v.SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			});
		}

		[Test]
		public void TestPipelinedFunctionReturningCollection()
		{
			const string sqlText = @"SELECT COLUMN_VALUE FROM TABLE(SQLPAD.PIPELINED_FUNCTION(SYSDATE, SYSDATE))";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var programNodeValidity = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programNodeValidity.Count.ShouldBe(2);
			programNodeValidity[0].IsRecognized.ShouldBe(true);
			programNodeValidity[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programNodeValidity[1].IsRecognized.ShouldBe(true);
			programNodeValidity[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestColumnResolutionFromXmlTable()
		{
			const string sqlText = @"SELECT SEQ#, TITLE, DESCRIPTION FROM XMLTABLE('for $i in $RSS_DATA/rss/channel/item return $i' PASSING HTTPURITYPE('http://servis.idnes.cz/rss.asp?c=zpravodaj').GETXML() AS RSS_DATA COLUMNS SEQ# FOR ORDINALITY, TITLE VARCHAR2(4000) PATH 'title', DESCRIPTION CLOB PATH 'description') T";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnNodeValidity = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnNodeValidity.Count.ShouldBe(3);
			columnNodeValidity.ForEach(v =>
			{
				v.IsRecognized.ShouldBe(true);
				v.SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			});
		}

		[Test]
		public void TestOrderByClauseWithinScalarSubquery()
		{
			const string sqlText = @"SELECT (SELECT 1 FROM (SELECT 1 FROM DUAL ORDER BY DUMMY) ORDER BY DUMMY) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ClauseNotAllowed);
			invalidNonTerminals[0].ToolTipText.ShouldBe(OracleSemanticErrorType.ClauseNotAllowed);
			invalidNonTerminals[0].Node.ShouldNotBe(null);
		}

		[Test]
		public void TestOrderByClauseWithinInClause()
		{
			const string sqlText = @"SELECT * FROM DUAL WHERE DUMMY IN (SELECT DUMMY FROM DUAL ORDER BY DUMMY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ClauseNotAllowed);
			invalidNonTerminals[0].ToolTipText.ShouldBe(OracleSemanticErrorType.ClauseNotAllowed);
			invalidNonTerminals[0].Node.ShouldNotBe(null);
		}

		[Test]
		public void TestDatabaseLinkPropagatedColumnUsingAsterisk()
		{
			const string sqlText = @"SELECT DUMMY FROM (SELECT * FROM DUAL@HQ_PDB_LOOPBACK)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnValidities = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnValidities.Count.ShouldBe(2);
			columnValidities[0].IsRecognized.ShouldBe(true);
			columnValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnValidities[1].IsRecognized.ShouldBe(true);
			columnValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnValidities[1].SuggestionType.ShouldBe(OracleSuggestionType.UseExplicitColumnList);
			columnValidities[1].ToolTipText.ShouldBe(OracleSuggestionType.UseExplicitColumnList);
		}

		[Test]
		public void TestDatabaseLinkPropagatedColumnUsingObjectQualifiedAsterisk()
		{
			const string sqlText = @"SELECT DUMMY FROM (SELECT DUAL.* FROM DUAL@HQ_PDB_LOOPBACK)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnValidities = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnValidities.Count.ShouldBe(2);
			columnValidities[0].IsRecognized.ShouldBe(true);
			columnValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnValidities[1].IsRecognized.ShouldBe(true);
			columnValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			columnValidities[1].SuggestionType.ShouldBe(OracleSuggestionType.UseExplicitColumnList);
			columnValidities[1].ToolTipText.ShouldBe(OracleSuggestionType.UseExplicitColumnList);
		}

		[Test]
		public void TestDicrepancyBetweenColumnsAndCommonTableExpressionExplicitColumnList()
		{
			const string sqlText = @"WITH CTE(C1) AS (SELECT 1, 2 FROM DUAL) SELECT C1 FROM CTE";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var cteQueryBlock = validationModel.SemanticModel.QueryBlocks.Single(qb => qb.Type == QueryBlockType.CommonTableExpression);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(2);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].Node.ShouldBe(cteQueryBlock.ExplicitColumnNameList);
			invalidNonTerminals[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[1].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[1].Node.ShouldBe(cteQueryBlock.SelectList);
		}

		[Test]
		public void TestDicrepancyBetweenColumnsAndCommonTableExpressionExplicitColumnListWithRecursiveQuery()
		{
			const string sqlText =
@"WITH sampleData(c1, c2) AS (
	SELECT 0 FROM DUAL
	UNION ALL
	SELECT c1 + 1 FROM sampleData WHERE c1 <= 3
)
SELECT NULL FROM sampleData";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var cteQueryBlocks = validationModel.SemanticModel.QueryBlocks.Where(qb => qb.Type == QueryBlockType.CommonTableExpression).ToArray();
			cteQueryBlocks.Length.ShouldBe(2);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(3);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].Node.ShouldBe(cteQueryBlocks[0].ExplicitColumnNameList);
			invalidNonTerminals[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[1].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[1].Node.ShouldBe(cteQueryBlocks[0].SelectList);
			invalidNonTerminals[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[2].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[2].Node.ShouldBe(cteQueryBlocks[1].SelectList);
		}

		[Test]
		public void TestDicrepancyBetweenColumnsAndCommonTableExpressionExplicitColumnListWhenWithinConcatenatedQueryBlock()
		{
			const string sqlText =
@"WITH CTE(C1, C2) AS (
	SELECT 1, 2 FROM DUAL UNION ALL
	SELECT 2, 2 FROM DUAL UNION ALL
	SELECT 3, 2 FROM DUAL UNION ALL
	SELECT 4, 2 FROM DUAL UNION ALL
	SELECT 5 FROM DUAL
)
SELECT * FROM CTE";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var cteQueryBlocks = validationModel.SemanticModel.QueryBlocks
				.Where(qb => qb.Type == QueryBlockType.CommonTableExpression)
				.OrderBy(qb => qb.RootNode.SourcePosition.IndexStart)
				.ToArray();

			cteQueryBlocks.Length.ShouldBe(5);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(2);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].Node.ShouldBe(cteQueryBlocks[0].ExplicitColumnNameList);
			invalidNonTerminals[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[1].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[1].Node.ShouldBe(cteQueryBlocks[4].SelectList);
			invalidNonTerminals[1].Node.TerminalCount.ShouldBe(1);
			invalidNonTerminals[1].Node.FirstTerminalNode.Token.Value.ShouldBe("5");
		}

		[Test]
		public void TestScalarSubqueryWithMultipleColumns()
		{
			const string sqlText = @"SELECT (SELECT 1, 2 FROM DUAL) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var cteQueryBlock = validationModel.SemanticModel.QueryBlocks.Single(qb => qb.Type == QueryBlockType.ScalarSubquery);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].ToolTipText.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminals[0].Node.ShouldBe(cteQueryBlock.SelectList);
		}

		[Test]
		public void TestTableCollectionExpressionWithIncompatibleFunction()
		{
			const string sqlText = @"SELECT * FROM TABLE(NVL(1, 1))";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var programValidityItems = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programValidityItems.Count.ShouldBe(1);
			programValidityItems[0].IsRecognized.ShouldBe(true);
			programValidityItems[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.FunctionReturningRowSetRequired);
			programValidityItems[0].ToolTipText.ShouldBe(OracleSemanticErrorType.FunctionReturningRowSetRequired);
			programValidityItems[0].Node.Token.Value.ShouldBe("NVL");
		}

		[Test]
		public void TestFunctionParameterErrors()
		{
			const string sqlText = @"SELECT TO_CHAR(left => 2), DBMS_XPLAN.DISPLAY_CURSOR(sql_idx => NULL, dummy + 1) FROM DUAL";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var identifierValidityItems = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			identifierValidityItems.Count.ShouldBe(2);
			identifierValidityItems[0].IsRecognized.ShouldBe(true);
			identifierValidityItems[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.NamedParameterNotAllowed);
			identifierValidityItems[0].ToolTipText.ShouldBe(OracleSemanticErrorType.NamedParameterNotAllowed);
			identifierValidityItems[0].Node.Token.Value.ShouldBe("left");

			identifierValidityItems[1].IsRecognized.ShouldBe(false);
			identifierValidityItems[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			identifierValidityItems[1].Node.Token.Value.ShouldBe("sql_idx");

			var invalidNonTerminalItems = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalItems.Count.ShouldBe(1);

			invalidNonTerminalItems[0].IsRecognized.ShouldBe(true);
			invalidNonTerminalItems[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.PositionalParameterNotAllowed);
			invalidNonTerminalItems[0].ToolTipText.ShouldBe(OracleSemanticErrorType.PositionalParameterNotAllowed);
			invalidNonTerminalItems[0].Node.TerminalCount.ShouldBe(3);
			invalidNonTerminalItems[0].Node.FirstTerminalNode.Token.Value.ShouldBe("dummy");
			invalidNonTerminalItems[0].Node.LastTerminalNode.Token.Value.ShouldBe("1");
		}

		[Test]
		public void TestInvalidOrderByColumnIndex()
		{
			const string sqlText = @"SELECT T.*, '[' || NAME || ']' FROM (SELECT NAME FROM SELECTION) T ORDER BY 3, 2, 1, 4";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var validationData = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			validationData.Count.ShouldBe(5);
			validationData[3].IsRecognized.ShouldBe(true);
			validationData[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnIndex);
			validationData[3].Node.Token.Value.ShouldBe("3");
			validationData[4].IsRecognized.ShouldBe(true);
			validationData[4].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnIndex);
			validationData[4].Node.Token.Value.ShouldBe("4");
		}

		[Test]
		public void TestAggregateFunctionsInDifferentQueryBlockClauses()
		{
			const string sqlText = @"SELECT COUNT(T1.DUMMY) FROM DUAL T1 JOIN DUAL T2 ON COUNT(T1.DUMMY) = COUNT(T2.DUMMY) WHERE COUNT(T1.DUMMY) = 1 GROUP BY COUNT(T1.DUMMY) HAVING COUNT(T1.DUMMY) = 1 ORDER BY COUNT(T1.DUMMY)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.SemanticModel.MainQueryBlock.HavingClause.ShouldNotBe(null);

			var programValidityNodes = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programValidityNodes.Count.ShouldBe(7);
			programValidityNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programValidityNodes[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNotAllowed);
			programValidityNodes[1].Node.SourcePosition.IndexStart.ShouldBe(52);
			programValidityNodes[1].Node.SourcePosition.IndexEnd.ShouldBe(56);
			programValidityNodes[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNotAllowed);
			programValidityNodes[2].Node.SourcePosition.IndexStart.ShouldBe(70);
			programValidityNodes[2].Node.SourcePosition.IndexEnd.ShouldBe(74);
			programValidityNodes[3].SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNotAllowed);
			programValidityNodes[3].Node.SourcePosition.IndexStart.ShouldBe(92);
			programValidityNodes[3].Node.SourcePosition.IndexEnd.ShouldBe(96);
			programValidityNodes[4].SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNotAllowed);
			programValidityNodes[4].Node.SourcePosition.IndexStart.ShouldBe(121);
			programValidityNodes[4].Node.SourcePosition.IndexEnd.ShouldBe(125);
			programValidityNodes[5].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
			programValidityNodes[6].SemanticErrorType.ShouldBe(OracleSemanticErrorType.None);
		}

		[Test]
		public void TestAggregateFunctionsInUpdateSetClause()
		{
			const string sqlText = @"UPDATE DUAL SET DUMMY = COUNT(*) OVER (ORDER BY ROWNUM)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var programValidityNodes = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programValidityNodes.Count.ShouldBe(1);
			programValidityNodes[0].IsRecognized.ShouldBe(true);
			programValidityNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNotAllowed);
		}

		[Test]
		public void TestAggregateFunctionsInMultiInsert()
		{
			const string sqlText =
@"INSERT
    WHEN COUNT(NULL) = 1 THEN INTO DUAL
SELECT NULL FROM DUAL;";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var programValidityNodes = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programValidityNodes.Count.ShouldBe(1);
			programValidityNodes[0].IsRecognized.ShouldBe(true);
			programValidityNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNotAllowed);
		}

		[Test]
		public void TestAggregateFunctionsInInsertValues()
		{
			const string sqlText = @"INSERT INTO DUAL (DUMMY) VALUES (COUNT(*))";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var programValidityNodes = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programValidityNodes.Count.ShouldBe(1);
			programValidityNodes[0].IsRecognized.ShouldBe(true);
			programValidityNodes[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.GroupFunctionNotAllowed);
		}

		[Test]
		public void TestRecursiveQueryWithRecursiveClauses()
		{
			const string sqlText =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 5
)
SEARCH DEPTH FIRST BY VAL SET SEQ#
CYCLE DUMMY SET CYCLE# TO 'X' DEFAULT 0.0E+000
SELECT * FROM CTE";
			
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}

		[Test]
		public void TestQueryWithInvalidRecursiveClausesAndReferences()
		{
			const string sqlText =
@"WITH CTE AS (
	SELECT DUMMY DUMMY1, 2 DUMMY2 FROM DUAL
)
SEARCH DEPTH FIRST BY DUMMY1, DUMMY2, DUMMY3 SET DUMMY3
CYCLE DUMMY2, DUMMY3 SET DUMMY4 TO 'String length <> 1' DEFAULT ''
SELECT DUMMY1, DUMMY2, DUMMY3, DUMMY4 FROM CTE";
			
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(2);
			var invalidNonterminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonterminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.MissingWithClauseColumnAliasList);
			invalidNonterminals[0].Node.Id.ShouldBe(NonTerminals.SubqueryFactoringSearchClause);
			invalidNonterminals[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.MissingWithClauseColumnAliasList);
			invalidNonterminals[1].Node.Id.ShouldBe(NonTerminals.SubqueryFactoringCycleClause);

			validationModel.ColumnNodeValidity.Count.ShouldBe(10);
			var columnValidityNodes = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnValidityNodes[0].Node.Token.Value.ShouldBe("DUMMY");
			columnValidityNodes[0].IsRecognized.ShouldBe(true);
			columnValidityNodes[1].Node.Token.Value.ShouldBe("DUMMY1");
			columnValidityNodes[1].IsRecognized.ShouldBe(true);
			columnValidityNodes[2].Node.Token.Value.ShouldBe("DUMMY2");
			columnValidityNodes[2].IsRecognized.ShouldBe(true);
			columnValidityNodes[3].Node.Token.Value.ShouldBe("DUMMY3");
			columnValidityNodes[3].IsRecognized.ShouldBe(false);
			columnValidityNodes[4].Node.Token.Value.ShouldBe("DUMMY2");
			columnValidityNodes[4].IsRecognized.ShouldBe(true);
			columnValidityNodes[5].Node.Token.Value.ShouldBe("DUMMY3");
			columnValidityNodes[5].IsRecognized.ShouldBe(false);
			columnValidityNodes[6].Node.Token.Value.ShouldBe("DUMMY1");
			columnValidityNodes[6].IsRecognized.ShouldBe(true);
			columnValidityNodes[7].Node.Token.Value.ShouldBe("DUMMY2");
			columnValidityNodes[7].IsRecognized.ShouldBe(true);
			columnValidityNodes[8].Node.Token.Value.ShouldBe("DUMMY3");
			columnValidityNodes[8].IsRecognized.ShouldBe(false);
			columnValidityNodes[9].Node.Token.Value.ShouldBe("DUMMY4");
			columnValidityNodes[9].IsRecognized.ShouldBe(false);

			validationModel.IdentifierNodeValidity.Count.ShouldBe(2);
			var identifierNodes = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			identifierNodes.ForEach(
				i =>
				{
					i.IsRecognized.ShouldBe(true);
					i.SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidCycleMarkValue);
				});
		}

		[Test]
		public void TestValidationModelBuildWithOrderByPostfixedNumber()
		{
			const string sqlText = @"SELECT * FROM DUAL ORDER BY 1d";
			
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}

		[Test]
		public void TestPartitionAndSubpartitionReferences()
		{
			const string sqlText = @"SELECT * FROM INVOICES PARTITION (P2015), INVOICES PARTITION (P2016), INVOICES SUBPARTITION (P2015_PRIVATE), INVOICES SUBPARTITION (P2016_ENTERPRISE), INVOICES PARTITION (P2015_PRIVATE), INVOICES SUBPARTITION (P2015)";
			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var objectValidityItems = validationModel.ObjectNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			objectValidityItems.Count.ShouldBe(10);
			objectValidityItems[0].IsRecognized.ShouldBe(true);
			objectValidityItems[0].Node.Token.Value.ShouldBe("INVOICES");
			objectValidityItems[1].IsRecognized.ShouldBe(true);
			objectValidityItems[1].Node.Token.Value.ShouldBe("INVOICES");
			objectValidityItems[2].IsRecognized.ShouldBe(false);
			objectValidityItems[2].Node.Token.Value.ShouldBe("P2016");
			objectValidityItems[3].IsRecognized.ShouldBe(true);
			objectValidityItems[3].Node.Token.Value.ShouldBe("INVOICES");
			objectValidityItems[4].IsRecognized.ShouldBe(true);
			objectValidityItems[4].Node.Token.Value.ShouldBe("INVOICES");
			objectValidityItems[5].IsRecognized.ShouldBe(false);
			objectValidityItems[5].Node.Token.Value.ShouldBe("P2016_ENTERPRISE");
			objectValidityItems[6].IsRecognized.ShouldBe(true);
			objectValidityItems[6].Node.Token.Value.ShouldBe("INVOICES");
			objectValidityItems[7].IsRecognized.ShouldBe(false);
			objectValidityItems[7].Node.Token.Value.ShouldBe("P2015_PRIVATE");
			objectValidityItems[8].IsRecognized.ShouldBe(true);
			objectValidityItems[8].Node.Token.Value.ShouldBe("INVOICES");
			objectValidityItems[9].IsRecognized.ShouldBe(false);
			objectValidityItems[9].Node.Token.Value.ShouldBe("P2015");
		}

		[Test]
		public void TestInvalidLnNvlCondition()
		{
			const string sqlText = @"SELECT NULL FROM DUAL WHERE LNNVL(1 <> 1) AND LNNVL((1 <> 1)) AND LNNVL(1 <> 1 AND 0 <> 0) AND LNNVL(0 BETWEEN 1 AND 2) AND LNNVL(DUMMY IN ('X')) AND LNNVL(DUMMY IN ('X', 'Y')) AND LNNVL(DUMMY IN (SELECT * FROM DUAL))";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(4);
		}

		[Test]
		public void TestInvalidPivotAggregateExpression()
		{
			const string sqlText =
@"SELECT
	*
FROM (
	SELECT 1 VAL FROM DUAL
	)
	PIVOT (
		(COUNT(VAL)) AS COUNT1,
		(COUNT(VAL) + 1) AS COUNT2,
		MAX(VAL + 1) AS MAX,
		MAX(VAL) + COUNT(VAL) AS SUM
		FOR (VAL)
			IN (1)
	) PT";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(2);
		}

		[Test]
		public void TestUnpivotInValueDataTypeMismatch()
		{
			const string sqlText =
@"SELECT
	*
FROM (
	SELECT 0 VAL1, 0 VAL2 FROM DUAL)
	UNPIVOT INCLUDE NULLS (
		VAL 
	FOR PLACEHOLDER IN (
		VAL1 AS 0,
		VAL2 AS '0')
	)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(2);
			var nodeValidities = validationModel.InvalidNonTerminals.Values.ToArray();
			nodeValidities[0].Node.FirstTerminalNode.Id.ShouldBe(Terminals.NumberLiteral);
			nodeValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ExpressionMustHaveSameDatatypeAsCorrespondingExpression);
			nodeValidities[1].Node.FirstTerminalNode.Id.ShouldBe(Terminals.StringLiteral);
			nodeValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ExpressionMustHaveSameDatatypeAsCorrespondingExpression);
		}

		[Test]
		public void TestUnpivotInValueDataTypeMismatchWithNullValue()
		{
			const string sqlText =
@"SELECT
	*
FROM (
	SELECT 0 VAL1, 0 VAL2 FROM DUAL)
	UNPIVOT (
		VAL 
	FOR PLACEHOLDER IN (
		VAL1 AS NULL,
		VAL2 AS 0)
	)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}

		[Test]
		public void TestModelBuildWithNonexistingUnpivotColumnReference()
		{
			const string sqlText =
@"SELECT
	*
FROM (
	SELECT 0 VAL1, 0 VAL2 FROM DUAL)
	UNPIVOT INCLUDE NULLS (
		VAL 
	FOR PLACEHOLDER IN (
		UNDEFINED AS 0,
		VAL2 AS 0)
	)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			Should.NotThrow(() => BuildValidationModel(sqlText, statement));
		}

		[Test]
		public void TestModelBuildWithVectorConditionWithUnfinishedQueryBlock()
		{
			const string sqlText = @"SELECT NULL FROM DUAL WHERE (DUMMY, DUMMY) IN (SELECT )";

			var statement = Parser.Parse(sqlText).Single();

			Should.NotThrow(() => BuildValidationModel(sqlText, statement));
		}

		[Test]
		public void TestModelBuildWithUnfinishedCastDataType()
		{
			const string sqlText = @"SELECT CAST(NULL AS VARCHAR2) FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			Should.NotThrow(() => BuildValidationModel(sqlText, statement));
		}

		[Test]
		public void TestUnpivotInColumnDataTypeMismatch()
		{
			const string sqlText =
@"SELECT
	*
FROM (
	SELECT 0 VAL1, '0' VAL2 FROM DUAL)
	UNPIVOT INCLUDE NULLS (
		VAL 
	FOR PLACEHOLDER IN (
		VAL1 AS 0,
		VAL2 AS 1)
	)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(2);
			var nodeValidities = validationModel.InvalidNonTerminals.Values.ToArray();
			nodeValidities[0].Node.FirstTerminalNode.Token.Value.ShouldBe("VAL1");
			nodeValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ExpressionMustHaveSameDatatypeAsCorrespondingExpression);
			nodeValidities[1].Node.FirstTerminalNode.Token.Value.ShouldBe("VAL2");
			nodeValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ExpressionMustHaveSameDatatypeAsCorrespondingExpression);
        }

		[Test]
		public void TestCursorParameter()
		{
			const string sqlText = @"SELECT * FROM TABLE(SQLPAD.CURSOR_FUNCTION(CURSOR(SELECT * FROM SELECTION WHERE ROWNUM <= 5)))";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}
		
		[Test]
		public void TestNonNestedTableColumnInTableCollectionExpression()
		{
			const string sqlText = @"SELECT (SELECT * FROM TABLE(DUMMY)) FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var columnValidityItems = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnValidityItems.Count.ShouldBe(2);
			columnValidityItems[1].IsRecognized.ShouldBe(true);
			columnValidityItems[1].Node.Token.Value.ShouldBe("DUMMY");
			columnValidityItems[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.CannotAccessRowsFromNonNestedTableItem);
		}

		[Test]
		public void TestDynamicTypeInTableCollectionExpression()
		{
			const string sqlText = @"SELECT (SELECT COUNT(*) FROM TABLE(DYNAMIC_TABLE)) FROM (SELECT COLLECT(DUMMY) DYNAMIC_TABLE FROM DUAL)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var columnValidityItems = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnValidityItems.Count.ShouldBe(2);
			columnValidityItems[0].IsRecognized.ShouldBe(true);
			columnValidityItems[0].Node.Token.Value.ShouldBe("DYNAMIC_TABLE");
			columnValidityItems[0].SemanticErrorType.ShouldBe(null);
			columnValidityItems[1].IsRecognized.ShouldBe(true);
			columnValidityItems[1].Node.Token.Value.ShouldBe("DUMMY");
			columnValidityItems[1].SemanticErrorType.ShouldBe(null);
		}

		[Test]
		public void TestDataTypeValidation()
		{
			const string sqlText = @"SELECT CAST(NULL AS SYS.ODCIRAWLIST), CAST(NULL AS ODCIRAWLIST), CAST(NULL AS HUSQVIK.ODCIRAWLIST), CAST(NULL AS NONEXISTING_SCHEMA.ODCIRAWLIST), CAST(NULL AS INVALID_OBJECT_TYPE) FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var identifierValidityItems = validationModel.IdentifierNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			identifierValidityItems.Count.ShouldBe(4);
			identifierValidityItems[0].IsRecognized.ShouldBe(false);
			identifierValidityItems[0].Node.Token.Value.ShouldBe("ODCIRAWLIST");
			identifierValidityItems[0].SemanticErrorType.ShouldBe(null);
			identifierValidityItems[1].IsRecognized.ShouldBe(false);
			identifierValidityItems[1].Node.Token.Value.ShouldBe("ODCIRAWLIST");
			identifierValidityItems[1].SemanticErrorType.ShouldBe(null);
			identifierValidityItems[2].IsRecognized.ShouldBe(false);
			identifierValidityItems[2].Node.Token.Value.ShouldBe("NONEXISTING_SCHEMA");
			identifierValidityItems[2].SemanticErrorType.ShouldBe(null);
			identifierValidityItems[3].IsRecognized.ShouldBe(false);
			identifierValidityItems[3].Node.Token.Value.ShouldBe("ODCIRAWLIST");
			identifierValidityItems[3].SemanticErrorType.ShouldBe(null);
		}

		[Test]
		public void TestExtractFunctionInvalidParameter()
		{
			const string sqlText = @"SELECT EXTRACT(SYSDATE) FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].IsRecognized.ShouldBe(true);
			invalidNonTerminals[0].Node.Id.ShouldBe(NonTerminals.OptionalParameterExpression);
			invalidNonTerminals[0].Node.FirstTerminalNode.Token.Value.ShouldBe("SYSDATE");
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.NotEnoughArgumentsForFunction);
		}

		[Test]
		public void TestInvalidDataTypesWithinCastFunction()
		{
			const string sqlText =
@"SELECT
	CAST(NULL AS NUMBER(39)),
	CAST(NULL AS NUMBER(0)),
	CAST(NULL AS NUMBER(38, -85)),
	CAST(NULL AS NUMBER(38, 128)),
	CAST(NULL AS RAW(2001)),
	CAST(NULL AS RAW(0)),
	CAST(NULL AS VARCHAR2(32768)),
	CAST(NULL AS NVARCHAR2(2001)),
	CAST(NULL AS FLOAT(127)),
	CAST(NULL AS TIMESTAMP(10)),
	CAST(NULL AS INTERVAL YEAR(10) TO MONTH),
	CAST(NULL AS INTERVAL DAY(10) TO SECOND(10)),
	CAST(NULL AS UROWID(4001)),
	CAST(NULL AS CHAR(2001)),
	CAST(NULL AS NCHAR(1001))
FROM
	DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(16);
			invalidNonTerminals[0].IsRecognized.ShouldBe(true);
			invalidNonTerminals[0].Node.Id.ShouldBe(Terminals.IntegerLiteral);
			invalidNonTerminals[0].Node.Token.Value.ShouldBe("39");
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.NumericPrecisionSpecifierOutOfRange);
			invalidNonTerminals[2].IsRecognized.ShouldBe(true);
			invalidNonTerminals[2].Node.Id.ShouldBe(NonTerminals.NegativeInteger);
			invalidNonTerminals[2].Node.TerminalCount.ShouldBe(2);
			invalidNonTerminals[2].Node.FirstTerminalNode.Id.ShouldBe(Terminals.MathMinus);
			invalidNonTerminals[2].Node.LastTerminalNode.Token.Value.ShouldBe("85");
			invalidNonTerminals[2].SemanticErrorType.ShouldBe(OracleSemanticErrorType.NumericScaleSpecifierOutOfRange);
			invalidNonTerminals[4].IsRecognized.ShouldBe(true);
			invalidNonTerminals[4].Node.Id.ShouldBe(Terminals.IntegerLiteral);
			invalidNonTerminals[4].Node.Token.Value.ShouldBe("2001");
			invalidNonTerminals[4].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SpecifiedLengthTooLongForDatatype);
			invalidNonTerminals[5].IsRecognized.ShouldBe(true);
			invalidNonTerminals[5].Node.Id.ShouldBe(Terminals.IntegerLiteral);
			invalidNonTerminals[5].Node.Token.Value.ShouldBe("0");
			invalidNonTerminals[5].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ZeroLengthColumnsNotAllowed);
			invalidNonTerminals[8].IsRecognized.ShouldBe(true);
			invalidNonTerminals[8].Node.Id.ShouldBe(Terminals.IntegerLiteral);
			invalidNonTerminals[8].Node.Token.Value.ShouldBe("127");
			invalidNonTerminals[8].SemanticErrorType.ShouldBe(OracleSemanticErrorType.FloatingPointPrecisionOutOfRange);
			invalidNonTerminals[9].IsRecognized.ShouldBe(true);
			invalidNonTerminals[9].Node.Id.ShouldBe(Terminals.IntegerLiteral);
			invalidNonTerminals[9].Node.Token.Value.ShouldBe("10");
			invalidNonTerminals[9].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DatetimeOrIntervalPrecisionIsOutOfRange);
		}

		[Test]
		public void TestJsonTableInUnsupportedOracleVersion()
		{
			const string sqlText =
@"SELECT
	*
FROM
	JSON_TABLE(
			'[""Value 1"", ""Value 2"", ""Value 3""]', '$[*]'
			DEFAULT 'invalid data' ON ERROR
			COLUMNS (
				SEQ$ FOR ORDINALITY,
				VALUE VARCHAR2 PATH '$'
			)
		)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement, new OracleTestDatabaseModel { TestDatabaseVersion = new Version(12, 1, 0, 1) } );

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].IsRecognized.ShouldBe(true);
			invalidNonTerminals[0].Node.Id.ShouldBe(NonTerminals.TableReference);
			invalidNonTerminals[0].Node.FirstTerminalNode.Id.ShouldBe(Terminals.JsonTable);
			invalidNonTerminals[0].Node.LastTerminalNode.Id.ShouldBe(Terminals.RightParenthesis);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.UnsupportedInConnectedDatabaseVersion);
		}

		[Test]
		public void TestOrderByNotAllowed()
		{
			const string sqlText = @"SELECT RATIO_TO_REPORT(NULL) OVER (ORDER BY NULL) FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var programNodeValidities = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programNodeValidities.Count.ShouldBe(2);
			programNodeValidities[1].IsRecognized.ShouldBe(true);
			programNodeValidities[1].Node.Id.ShouldBe(NonTerminals.OrderByClause);
			programNodeValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.OrderByNotAllowedHere);
		}

		[Test]
		public void TestColumnCountValidationInInsertWithCommonTableExpression()
		{
			const string sqlText =
@"INSERT INTO DUAL (DUMMY)
WITH CTE AS (SELECT 1 C1, 2 C2 FROM DUAL)
SELECT C1 FROM CTE";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(0);
		}

		[Test]
		public void TestColumnCountValidationInMultisetInClause()
		{
			const string sqlText = @"SELECT * FROM DUAL WHERE (DUMMY, DUMMY) IN (SELECT DUMMY, DUMMY, DUMMY FROM DUAL)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(2);
			invalidNonTerminalValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminalValidities[0].Node.Id.ShouldBe(NonTerminals.ExpressionList);
			invalidNonTerminalValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminalValidities[1].Node.Id.ShouldBe(NonTerminals.SelectList);
		}

		[Test]
		public void TestInvalidColumnCountValidationInMultisetInClauseWithParenthesisWrappedSubquery()
		{
			const string sqlText = @"SELECT NULL FROM DUAL WHERE (DUMMY, DUMMY) IN ((SELECT DUMMY FROM DUAL))";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(2);
			invalidNonTerminalValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminalValidities[0].Node.Id.ShouldBe(NonTerminals.ExpressionList);
			invalidNonTerminalValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminalValidities[1].Node.Id.ShouldBe(NonTerminals.SelectList);
		}

		[Test]
		public void TestValidColumnCountValidationInMultisetInClauseWithParenthesisWrappedSubquery()
		{
			const string sqlText = @"SELECT NULL FROM DUAL WHERE (DUMMY, DUMMY) IN ((SELECT DUMMY, DUMMY FROM DUAL))";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}

		[Test]
		public void TestValidationModelBuildWhileTypingCastWithinTableExpression()
		{
			const string sqlText = @"SELECT * FROM TABLE(CAST())";

			var statement = Parser.Parse(sqlText).Single();

			Should.NotThrow(() => BuildValidationModel(sqlText, statement));
		}

		[Test]
		public void TestValidationModelBuildWhileTypingAggregateFunctionInFrontOfOtherAggregateFunction()
		{
			const string sqlText = @"SELECT MAX MAX(flag) FROM dual";

			var statement = Parser.Parse(sqlText).Single();

			Should.NotThrow(() => BuildValidationModel(sqlText, statement));
		}

		[Test]
		public void TestColumnCountValidationInSimpleInClause()
		{
			const string sqlText = @"SELECT * FROM DUAL WHERE DUMMY IN (SELECT DUMMY, DUMMY FROM DUAL)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(1);
			invalidNonTerminalValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InvalidColumnCount);
			invalidNonTerminalValidities[0].Node.Id.ShouldBe(NonTerminals.SelectList);
		}

		[Test]
		public void TestConditionNotAlwaysTrueFalseWhenAnotherTableInnerJoined()
		{
			const string sqlText =
@"SELECT
	CASE WHEN TMP.DUMMY IS NULL THEN 1 END
FROM
    DUAL
    JOIN DUAL TMP2 ON DUAL.DUMMY = TMP2.DUMMY
    LEFT JOIN (
        SELECT 1 DUMMY FROM DUAL
    ) TMP
    ON DUAL.DUMMY = TMP.DUMMY";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(0);
		}

		[Test]
		public void TestRedundantIsNotNullCondition()
		{
			const string sqlText = @"SELECT * FROM SELECTION WHERE SELECTION_ID + 1 IS NOT NULL AND SELECTION_ID IS NOT NULL AND PROJECT_ID IS NOT NULL OR PROJECT_ID IS NULL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(3);
			invalidNonTerminalValidities[0].SuggestionType.ShouldBe(OracleSuggestionType.ExpressionIsAlwaysTrue);
			invalidNonTerminalValidities[0].Node.TerminalCount.ShouldBe(4);
			invalidNonTerminalValidities[0].Node.FirstTerminalNode.Token.Value.ShouldBe("SELECTION_ID");
			invalidNonTerminalValidities[0].Node.LastTerminalNode.Id.ShouldBe(Terminals.Null);
			invalidNonTerminalValidities[1].SuggestionType.ShouldBe(OracleSuggestionType.ExpressionIsAlwaysTrue);
			invalidNonTerminalValidities[1].Node.TerminalCount.ShouldBe(4);
			invalidNonTerminalValidities[1].Node.FirstTerminalNode.Token.Value.ShouldBe("PROJECT_ID");
			invalidNonTerminalValidities[1].Node.LastTerminalNode.Id.ShouldBe(Terminals.Null);
			invalidNonTerminalValidities[2].SuggestionType.ShouldBe(OracleSuggestionType.ExpressionIsAlwaysFalse);
			invalidNonTerminalValidities[2].Node.TerminalCount.ShouldBe(3);
			invalidNonTerminalValidities[2].Node.FirstTerminalNode.Token.Value.ShouldBe("PROJECT_ID");
			invalidNonTerminalValidities[2].Node.LastTerminalNode.Id.ShouldBe(Terminals.Null);
		}

		[Test]
		public void TestNotNullConditionInOuterJoin()
		{
			const string sqlText =
@"SELECT
	CASE WHEN C1.SELECTION_ID IS NULL THEN 1 END,
	CASE WHEN C2.SELECTION_ID IS NULL THEN 1 END
FROM
	SELECTION
	LEFT JOIN SELECTION C1 ON SELECTION.SELECTION_ID = C1.SELECTION_ID,
	SELECTION C2
WHERE
	SELECTION.SELECTION_ID = C2.SELECTION_ID(+)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(0);
		}

		[Test]
		public void TestPartitionClauseOnBothOuterJoinEnds()
		{
			const string sqlText = @"SELECT NULL FROM DUAL T1 PARTITION BY (T1.DUMMY, DBMS_RANDOM.VALUE) LEFT JOIN DUAL T2 PARTITION BY (T2.DUMMY, DBMS_RANDOM.VALUE) ON NULL IS NULL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(2);
			invalidNonTerminalValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.PartitionedTableOnBothSidesOfPartitionedOuterJoinNotSupported);
			invalidNonTerminalValidities[0].Node.Id.ShouldBe(NonTerminals.OuterJoinPartitionClause);
			invalidNonTerminalValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.PartitionedTableOnBothSidesOfPartitionedOuterJoinNotSupported);
			invalidNonTerminalValidities[1].Node.Id.ShouldBe(NonTerminals.OuterJoinPartitionClause);
		}

		[Test]
		public void TestInvalidPseudocolumnUsage()
		{
			const string sqlText = "SELECT SYS.STANDARD.\"LEVEL\"(), SYS.STANDARD.\"ROWNUM\" FROM DUAL CONNECT BY LEVEL <= 2";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminalValidities = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminalValidities.Count.ShouldBe(2);
			invalidNonTerminalValidities[0].SemanticErrorType.ShouldBe(OracleSemanticErrorTooltipText.FunctionOrPseudocolumnMayBeUsedInsideSqlStatementOnly);
			invalidNonTerminalValidities[0].Node.Id.ShouldBe(NonTerminals.Expression);
			invalidNonTerminalValidities[1].SemanticErrorType.ShouldBe(OracleSemanticErrorTooltipText.FunctionOrPseudocolumnMayBeUsedInsideSqlStatementOnly);
			invalidNonTerminalValidities[1].Node.Id.ShouldBe(NonTerminals.PrefixedColumnReference);
		}

		[Test]
		public void TestQuotedPseudocolumnUsage()
		{
			const string sqlText = "SELECT \"ROWNUM\", \"LEVEL\" FROM DUAL CONNECT BY LEVEL <= 2";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var columnValidities = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnValidities.Count.ShouldBe(2);
			columnValidities[0].IsRecognized.ShouldBe(false);
			columnValidities[1].IsRecognized.ShouldBe(false);

			var programValidities = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programValidities.Count.ShouldBe(1);
			programValidities[0].IsRecognized.ShouldBe(true);
		}

		[Test]
		public void TestColumnReferenceInMultiSetTableCollectionExpression()
		{
			const string sqlText = "SELECT NULL FROM DUAL T, TABLE(CAST(MULTISET(SELECT SYSDATE - LEVEL FROM DUAL WHERE T.DUMMY = 'X' CONNECT BY LEVEL <= 5) AS SYS.ODCIDATELIST))";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var columnValidities = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			columnValidities.Count.ShouldBe(1);
			columnValidities[0].IsRecognized.ShouldBe(true);
			columnValidities[0].SemanticErrorType.ShouldBe(null);

			var programValidities = validationModel.ProgramNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			programValidities.Count.ShouldBe(3);
			programValidities[0].IsRecognized.ShouldBe(true);
			programValidities[0].Node.FirstTerminalNode.Token.Value.ShouldBe("CAST");
			programValidities[0].SemanticErrorType.ShouldBe(null);
		}

		[Test]
		public void TestSelectIntoClauseOutsidePlSql()
		{
			const string sqlText = "SELECT DUMMY INTO x FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SelectIntoClauseAllowedOnlyInMainQueryBlockWithinPlSqlScope);
		}

		[Test]
		public void TestConditionalCompilationSymbolOutsidePlSql()
		{
			const string sqlText = "SELECT $$compilation_symbol FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.PlSqlCompilationParameterAllowedOnlyWithinPlSqlScope);
		}

		[Test]
		public void TestWhereCurrentOfCursorConditionOutsidePlSql()
		{
			const string sqlText = "UPDATE dual SET dummy = NULL WHERE CURRENT OF test_cursor";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.CurrentOfConditionAllowedOnlyWithinPlSqlScope);
			invalidNonTerminals[0].Node.Id.ShouldBe(NonTerminals.CurrentOfCursorIdentifier);
			invalidNonTerminals[0].Node.TerminalCount.ShouldBe(3);
		}

		[Test]
		public void TestConditionalCompilationSymbolInsidePlSql()
		{
			const string sqlText = "DECLARE x VARCHAR2(255); BEGIN SELECT $$compilation_symbol INTO x FROM DUAL; END;";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			validationModel.InvalidNonTerminals.Count.ShouldBe(0);
		}

		[Test]
		public void TestSelectIntoClauseWithinPlSql()
		{
			const string sqlText =
@"BEGIN
	SELECT DUMMY INTO :x FROM (SELECT DUMMY INTO :y FROM DUAL);
END;";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.SelectIntoClauseAllowedOnlyInMainQueryBlockWithinPlSqlScope);
			invalidNonTerminals[0].Node.LastTerminalNode.Token.Value.ShouldBe("y");
		}

		[Test]
		public void TestFunctionWithCaseAsParameter()
		{
			const string sqlText = @"SELECT NVL(CASE WHEN 1 IN (1) THEN NULL END, ', ') FROM DUAL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);
			validationModel.ProgramNodeValidity.Count.ShouldBe(1);
		}

		[Test]
		public void TestInsertionIntoVirtualColumn()
		{
			const string sqlText = @"INSERT INTO ""CaseSensitiveTable"" (HIDDEN_COLUMN, VIRTUAL_COLUMN) VALUES (NULL, NULL)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(1);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.InsertOperationDisallowedOnVirtualColumns);
			invalidNonTerminals[0].Node.FirstTerminalNode.Token.Value.ShouldBe("VIRTUAL_COLUMN");
		}

		[Test]
		public void TestSameExplicitColumnNamesInCommonTableExpressionDefinition()
		{
			const string sqlText = @"WITH sample (value1, value2, value1) AS (SELECT 1, 1, 1 FROM dual) SELECT 1 FROM sample";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var invalidNonTerminals = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			invalidNonTerminals.Count.ShouldBe(2);
			invalidNonTerminals[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DuplicateNameFoundInColumnAliasListForWithClause);
			invalidNonTerminals[0].Node.Token.Value.ShouldBe("value1");
			invalidNonTerminals[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.DuplicateNameFoundInColumnAliasListForWithClause);
			invalidNonTerminals[1].Node.Token.Value.ShouldBe("value1");
		}

		[Test]
		public void TestOuterCorrelatedSubqueryNonQualifiedColumn()
		{
			const string sqlText = @"SELECT NULL FROM SELECTION WHERE EXISTS (SELECT NULL FROM DUAL WHERE SELECTION_ID = 1)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var validationData = validationModel.ColumnNodeValidity.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			validationData.Count.ShouldBe(1);
			validationData[0].SuggestionType.ShouldBe(OracleSuggestionType.CorrelatedSubqueryColumnNotQualified);
			validationData[0].Node.Token.Value.ShouldBe("SELECTION_ID");
		}

		[Test]
		public void TestConnectByPseudocolumnConflictingWithRowSourceColumns()
		{
			const string sqlText = @"SELECT connect_by_isleaf, connect_by_iscycle, ""CONNECT_BY_ISLEAF"", ""CONNECT_BY_ISCYCLE"" FROM (SELECT 'x' connect_by_isleaf, 'y' connect_by_iscycle FROM dual)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var validationData = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			validationData.Count.ShouldBe(2);
			validationData[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			validationData[0].Node.FirstTerminalNode.Token.Value.ShouldBe("connect_by_isleaf");
			validationData[1].SemanticErrorType.ShouldBe(OracleSemanticErrorType.ConnectByClauseRequired);
			validationData[1].Node.FirstTerminalNode.Token.Value.ShouldBe("connect_by_iscycle");
		}

		[Test]
		public void TestConnectByIsCyclePseudocolumnWithoutNoCycleKeyword()
		{
			const string sqlText = @"SELECT connect_by_isleaf, connect_by_iscycle FROM dual CONNECT BY LEVEL < 3";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var validationData = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			validationData.Count.ShouldBe(1);
			validationData[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.NoCycleKeywordRequiredWithConnectByIsCyclePseudocolumn);
			validationData[0].Node.FirstTerminalNode.Token.Value.ShouldBe("connect_by_iscycle");
		}

		[Test]
		public void TestOldStyleOuterJoinCannotBeUsedWithAnsiJoins()
		{
			const string sqlText = @"SELECT NULL FROM dual t1 JOIN dual t2 ON t1.dummy = t2.dummy WHERE t2.dummy(+) = 'X'";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			var validationData = validationModel.InvalidNonTerminals.OrderBy(nv => nv.Key.SourcePosition.IndexStart).Select(kvp => kvp.Value).ToList();
			validationData.Count.ShouldBe(1);
			validationData[0].SemanticErrorType.ShouldBe(OracleSemanticErrorType.OldStyleOuterJoinCannotBeUsedWithAnsiJoins);
			var text = String.Concat(validationData[0].Node.Terminals.Select(t => t.Token.Value));

			text.ShouldBe("(+)");
		}

		[Test]
		public void TestInsertIntoColumnClauseWithPrefixedColumnReferringNonExistingTable()
		{
			const string sqlText = @"INSERT INTO tmp (tmp.val) VALUES (1)";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.ColumnNodeValidity.Values.Count(c => !c.IsRecognized).ShouldBe(1);
		}

		[Test]
		public void TestNullabilityOfLeftJoinedNotNullColumn()
		{
			const string sqlText = @"SELECT c1, c2 FROM (SELECT t1.selection_id c1, t2.selection_id c2 FROM selection t1 LEFT JOIN selection t2 ON 1 = 0) WHERE c2 IS NULL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.Suggestions.Count().ShouldBe(0);
		}

		[Test]
		public void TestNullabilityOfLeftJoinedNotNullPhysicalColumnExposedUsingAsterisk()
		{
			const string sqlText = @"SELECT NULL FROM (SELECT t2.* FROM selection t1 LEFT JOIN selection t2 ON 1 = 0) WHERE selection_id IS NULL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.Suggestions.Count().ShouldBe(0);
		}

		[Test]
		public void TestNullabilityOfLeftJoinedNotNullInlineViewColumnExposedUsingAsterisk()
		{
			const string sqlText = @"SELECT NULL FROM (SELECT t2.* FROM dual t1 LEFT JOIN (SELECT 1 val FROM DUAL) t2 ON 1 = 0) WHERE val IS NOT NULL";

			var statement = Parser.Parse(sqlText).Single();

			statement.ParseStatus.ShouldBe(ParseStatus.Success);

			var validationModel = BuildValidationModel(sqlText, statement);

			validationModel.Suggestions.Count().ShouldBe(0);
		}
	}
}
