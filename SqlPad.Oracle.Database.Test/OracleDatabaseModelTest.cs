using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ModelDataProviders;
using SqlPad.Oracle.ToolTips;
using SqlPad.Test;

namespace SqlPad.Oracle.Database.Test
{
	[TestFixture]
	public class OracleDatabaseModelTest : TemporaryDirectoryTestFixture
	{
		private const string LoopbackDatabaseLinkName = "HQ_PDB@LOOPBACK";
		private const string ExplainPlanTableName = "EXPLAIN_PLAN";
		private static readonly ConnectionStringSettings ConnectionString;

		private const string ExplainPlanTestQuery =
@"SELECT /*+ gather_plan_statistics */
    *
FROM
    (SELECT ROWNUM VAL FROM DUAL) T1,
    (SELECT ROWNUM VAL FROM DUAL) T2,
    (SELECT ROWNUM VAL FROM DUAL) T3
WHERE
    T1.VAL = T2.VAL AND
    T2.VAL = T3.VAL";

		static OracleDatabaseModelTest()
		{
			OracleConfiguration.Configuration.ExecutionPlan.TargetTable.Name = ExplainPlanTableName;
			ConfigurationProvider.Configuration.DataModel.DataModelRefreshPeriod = 1440;
			var databaseConfiguration = (DatabaseConnectionConfigurationSection)ConfigurationManager.GetSection(DatabaseConnectionConfigurationSection.SectionName);
			ConnectionString = ConfigurationManager.ConnectionStrings[databaseConfiguration.Infrastructures[0].ConnectionStringName];
		}

		[Test]
		public async Task TestModelInitialization()
		{
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(ConnectionString, "Original"))
			{
				databaseModel.Schemas.Count.ShouldBe(0);
				databaseModel.AllObjects.Count.ShouldBe(0);
				databaseModel.DatabaseLinks.Count.ShouldBe(0);
				databaseModel.CharacterSets.Count.ShouldBe(0);
				databaseModel.StatisticsKeys.Count.ShouldBe(0);
				databaseModel.SystemParameters.Count.ShouldBe(0);

				databaseModel.IsInitialized.ShouldBe(false);
				var refreshFinishedResetEvent = new ManualResetEvent(false);
				databaseModel.RefreshCompleted += delegate { refreshFinishedResetEvent.Set(); };
				await databaseModel.Initialize();

				databaseModel.IsInitialized.ShouldBe(true);
				databaseModel.Schemas.Count.ShouldBeGreaterThan(0);

				if (!databaseModel.IsFresh)
				{
					refreshFinishedResetEvent.WaitOne();
				}

				Trace.WriteLine("Assert original database model");
				await AssertDatabaseModel(databaseModel);

				using (var modelClone = OracleDatabaseModel.GetDatabaseModel(ConnectionString, "Clone"))
				{
					await modelClone.Refresh();

					Trace.WriteLine("Assert cloned database model");
					await AssertDatabaseModel(modelClone);
				}
			}
		}

		private async Task AssertDatabaseModel(OracleDatabaseModelBase databaseModel)
		{
			databaseModel.Version.Major.ShouldBeGreaterThanOrEqualTo(11);

			databaseModel.AllObjects.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine($"All object dictionary has {databaseModel.AllObjects.Count} members. ");
			
			databaseModel.DatabaseLinks.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine($"Database link dictionary has {databaseModel.DatabaseLinks.Count} members. ");

			databaseModel.CharacterSets.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine($"Character set collection has {databaseModel.CharacterSets.Count} members. ");

			databaseModel.StatisticsKeys.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine($"Statistics key dictionary has {databaseModel.StatisticsKeys.Count} members. ");

			databaseModel.SystemParameters.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine($"System parameters dictionary has {databaseModel.SystemParameters.Count} members. ");

			var objectForScriptCreation = databaseModel.GetFirstSchemaObject<OracleSchemaObject>(databaseModel.GetPotentialSchemaObjectIdentifiers("SYS", "OBJ$"));
			objectForScriptCreation.ShouldNotBe(null);
			var objectScript = await databaseModel.GetObjectScriptAsync(objectForScriptCreation, CancellationToken.None);

			Trace.WriteLine("Object script output: " + Environment.NewLine + objectScript + Environment.NewLine);

			objectScript.ShouldNotBe(null);
			objectScript.Length.ShouldBeGreaterThan(100);

			var executionModel =
				new StatementExecutionModel
				{
					StatementText = "SELECT /*+ gather_plan_statistics */ * FROM DUAL WHERE DUMMY = :1",
					BindVariables = new[] { new BindVariableModel(new BindVariableConfiguration { Name = "1", Value = "X", DataType = "VARCHAR2", DataTypes = OracleBindVariable.DataTypes }) }
				};

			var connectionAdapter = (OracleConnectionAdapter)databaseModel.CreateConnectionAdapter();
			connectionAdapter.EnableDatabaseOutput = true;
			var batchExecutionModel = new StatementBatchExecutionModel { Statements = new [] { executionModel }, GatherExecutionStatistics = true };
			var statementExecutionBatchResult = await connectionAdapter.ExecuteStatementAsync(batchExecutionModel, CancellationToken.None);
			statementExecutionBatchResult.StatementResults.Count.ShouldBe(1);
			statementExecutionBatchResult.ExecutionModel.ShouldBe(batchExecutionModel);
			var result = statementExecutionBatchResult.StatementResults[0];
			result.StatementModel.ShouldBe(executionModel);
			result.ExecutedSuccessfully.ShouldBe(true);
			result.AffectedRowCount.ShouldBe(-1);
			result.ExecutedAt.ShouldNotBe(null);
			result.Duration.ShouldNotBe(null);
            result.ResultInfoColumnHeaders.Count.ShouldBe(1);
			var resultInfo = result.ResultInfoColumnHeaders.Keys.First();

			connectionAdapter.CanFetch(resultInfo).ShouldBe(true);

			var columnHeaders = result.ResultInfoColumnHeaders[resultInfo];
			columnHeaders.Count.ShouldBe(1);
			columnHeaders[0].DataType.ShouldBe(typeof(string));
			columnHeaders[0].DatabaseDataType.ShouldBe("Varchar2");
			columnHeaders[0].Name.ShouldBe("DUMMY");

			var rows = await connectionAdapter.FetchRecordsAsync(resultInfo, Int32.MaxValue, CancellationToken.None);

			connectionAdapter.CanFetch(resultInfo).ShouldBe(false);

			rows.Count.ShouldBe(1);
			rows[0].Length.ShouldBe(1);
			rows[0][0].ToString().ShouldBe("X");

			rows = await connectionAdapter.FetchRecordsAsync(resultInfo, 1, CancellationToken.None);
			rows.Any().ShouldBe(false);

			var planItemCollection = await connectionAdapter.GetCursorExecutionStatisticsAsync(CancellationToken.None);
			planItemCollection.PlanText.ShouldNotBe(null);
			planItemCollection.PlanText.ShouldNotBe(String.Empty);

			Trace.WriteLine($"Display cursor output: {Environment.NewLine}{planItemCollection.PlanText}{Environment.NewLine}");

			planItemCollection.PlanText.ShouldContain(executionModel.StatementText);

			var executionStatistics = await connectionAdapter.GetExecutionStatisticsAsync(CancellationToken.None);

			var statisticsRecords = executionStatistics.Where(r => r.Value != 0).ToArray();
			statisticsRecords.Length.ShouldBeGreaterThan(0);

			var statistics = String.Join(Environment.NewLine, statisticsRecords.Select(r => $"{r.Name.PadRight(40)}: {r.Value}"));
			Trace.WriteLine($"Execution statistics output: {Environment.NewLine}{statistics}{Environment.NewLine}");

			connectionAdapter.TraceFileName.ShouldNotBe(null);
			connectionAdapter.TraceFileName.ShouldNotBe(String.Empty);
			connectionAdapter.SessionId.ShouldNotBe(null);
		}

		[Test]
		public async Task TestRowIdFetch()
		{
			var executionModel =
				new StatementExecutionModel
				{
					StatementText = "SELECT CHARTOROWID('AAAACOAABAAAAR5AAC') SMALLFILE, CHARTOROWID('AAAXhyAAAAACAGDAAD') BIGFILE FROM DUAL",
					BindVariables = new BindVariableModel[0]
				};

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var statementBatchResult = await connectionAdapter.ExecuteStatementAsync(new StatementBatchExecutionModel { Statements = new[] { executionModel } }, CancellationToken.None);
				statementBatchResult.StatementResults.Count.ShouldBe(1);
				var result = statementBatchResult.StatementResults[0];

				result.ExecutedSuccessfully.ShouldBe(true);
				var resultInfo = result.ResultInfoColumnHeaders.Keys.First();

				var resultSet = await connectionAdapter.FetchRecordsAsync(resultInfo, Int32.MaxValue, CancellationToken.None);
				resultSet.Count.ShouldBe(1);
				var firstRow = resultSet[0];
				firstRow[0].ShouldBeTypeOf<OracleRowId>();
				var smallFileRowIdValue = (OracleRowId)firstRow[0];
				smallFileRowIdValue.RawValue.ToString().ShouldBe("AAAACOAABAAAAR5AAC");
				smallFileRowIdValue.ToString().ShouldBe("AAAACOAABAAAAR5AAC (obj=142; fil=1; blk=1145; off=2)");
				var bigFileRowIdValue = (OracleRowId)firstRow[1];
				bigFileRowIdValue.RawValue.ToString().ShouldBe("AAAXhyAAAAACAGDAAD");
				bigFileRowIdValue.ToString().ShouldBe("AAAXhyAAAAACAGDAAD (obj=96370; blk=524675; off=3)");
			}
		}

		[Test]
		public void TestInvalidDatabaseStatement()
		{
			var executionModel =
				new StatementExecutionModel
				{
					StatementText = $"SELECT NULL FROM {Guid.NewGuid().ToString("n")}",
					BindVariables = new BindVariableModel[0]
				};

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var task = connectionAdapter.ExecuteStatementAsync(new StatementBatchExecutionModel { Statements = new[] { executionModel } }, CancellationToken.None);
				var taskException = Assert.Throws<AggregateException>(() => task.Wait());
				taskException.InnerExceptions.Count.ShouldBe(1);
				var innerException = taskException.InnerExceptions[0];
				innerException.ShouldBeTypeOf<StatementExecutionException>();

				var executionException = (StatementExecutionException)innerException;
				executionException.BatchResult.StatementResults.Count.ShouldBe(1);
				var result = executionException.BatchResult.StatementResults[0];

				result.ExecutedSuccessfully.ShouldBe(false);
				result.ExecutedAt.ShouldNotBe(null);
				result.Duration.ShouldNotBe(null);
				result.Exception.ShouldNotBe(null);
				result.ResultInfoColumnHeaders.ShouldBe(null);
				result.AffectedRowCount.ShouldBe(null);
				result.CompilationErrors.ShouldBe(null);
				result.StatementModel.ShouldBe(executionModel);
			}
		}

#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
		[Test]
		public async Task TestDataTypesFetch()
		{
			var clobParameter = String.Join(" ", Enumerable.Repeat("CLOB DATA", 200));
			var executionModel =
				new StatementExecutionModel
				{
					StatementText = "SELECT TO_BLOB(RAWTOHEX('BLOB')), TO_CLOB('" + clobParameter + "'), TO_NCLOB('NCLOB DATA'), DATA_DEFAULT, TIMESTAMP'2014-11-01 14:16:32.123456789 CET' AT TIME ZONE '02:00', TIMESTAMP'2014-11-01 14:16:32.123456789', 0.1234567890123456789012345678901234567891, XMLTYPE('<root/>'), 1.23456789012345678901234567890123456789E-125, DATE'-4712-01-01' FROM ALL_TAB_COLS WHERE OWNER = 'SYS' AND TABLE_NAME = 'DUAL'",
					BindVariables = new BindVariableModel[0]
				};

			CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var statementBatchResult = await connectionAdapter.ExecuteStatementAsync(new StatementBatchExecutionModel { Statements = new[] { executionModel }, GatherExecutionStatistics = true }, CancellationToken.None);
				statementBatchResult.StatementResults.Count.ShouldBe(1);
				var result = statementBatchResult.StatementResults[0];

				result.ExecutedSuccessfully.ShouldBe(true);
				var resultInfo = result.ResultInfoColumnHeaders.Keys.First();

				var columnHeaders = result.ResultInfoColumnHeaders[resultInfo];
				columnHeaders.Count.ShouldBe(10);
				columnHeaders[0].DatabaseDataType.ShouldBe("Blob");
				columnHeaders[1].DatabaseDataType.ShouldBe("Clob");
				columnHeaders[2].DatabaseDataType.ShouldBe("NClob");
				columnHeaders[3].DatabaseDataType.ShouldBe("Long");
				columnHeaders[4].DatabaseDataType.ShouldBe("TimeStampTZ");
				columnHeaders[5].DatabaseDataType.ShouldBe("TimeStamp");
				columnHeaders[6].DatabaseDataType.ShouldBe("Decimal");
				columnHeaders[7].DatabaseDataType.ShouldBe("XmlType");
				columnHeaders[8].DatabaseDataType.ShouldBe("Decimal");
				columnHeaders[9].DatabaseDataType.ShouldBe("Date");

				var resultSet = await connectionAdapter.FetchRecordsAsync(resultInfo, Int32.MaxValue, CancellationToken.None);

				resultSet.Count.ShouldBe(1);
				var firstRow = resultSet[0];
				firstRow[0].ShouldBeTypeOf<OracleBlobValue>();
				var blobValue = (OracleBlobValue)firstRow[0];
				blobValue.Length.ShouldBe(4);
				blobValue.GetChunk(2).ShouldBe(new byte[] { 66, 76 });
				blobValue.Value.Length.ShouldBe(4);
				blobValue.ToString().ShouldBe("(BLOB[4 B])");
				blobValue.ToSqlLiteral().ShouldBe("TO_BLOB('424C4F42')");
				blobValue.ToXml().ShouldBe("<![CDATA[QkxPQg==]]>");
				blobValue.ToJson().ShouldBe("\"QkxPQg==\"");
				firstRow[1].ShouldBeTypeOf<OracleClobValue>();
				var expectedPreview = clobParameter.Substring(0, 1023) + OracleLargeTextValue.Ellipsis;
				var clobValue = (OracleClobValue)firstRow[1];
				clobValue.ToString().ShouldBe(expectedPreview);
				clobValue.DataTypeName.ShouldBe("CLOB");
				var expectedLiteral = $"TO_CLOB('{clobParameter}')";
				clobValue.ToSqlLiteral().ShouldBe(expectedLiteral);
				clobValue.ToXml().ShouldBe($"<![CDATA[{clobParameter}]]>");
				clobValue.ToJson().ShouldBe($"\"{clobParameter}\"");
				firstRow[2].ShouldBeTypeOf<OracleClobValue>();
				var nClobValue = (OracleClobValue)firstRow[2];
				nClobValue.DataTypeName.ShouldBe("NCLOB");
				nClobValue.Length.ShouldBe(20);
				nClobValue.Value.ShouldBe("NCLOB DATA");
				nClobValue.ToSqlLiteral().ShouldBe("TO_NCLOB('NCLOB DATA')");
				firstRow[3].ShouldBeTypeOf<OracleSimpleValue>();
				((OracleSimpleValue)firstRow[3]).Value.ShouldBe(String.Empty);
				firstRow[4].ShouldBeTypeOf<OracleTimestampWithTimeZone>();
				var timestampWithTimezoneValue = (OracleTimestampWithTimeZone)firstRow[4];
				timestampWithTimezoneValue.ToString().ShouldBe("11/01/2014 15:16:32.123456789 +02:00");
				timestampWithTimezoneValue.ToSqlLiteral().ShouldBe("TIMESTAMP'2014-11-1 15:16:32.123456789 +02:00'");
				timestampWithTimezoneValue.ToXml().ShouldBe("2014-11-01T15:16:32.123");
				timestampWithTimezoneValue.ToJson().ShouldBe("\"2014-11-01T15:16:32.123+02:00\"");
				firstRow[5].ShouldBeTypeOf<OracleTimestamp>();
				var timestampValue = (OracleTimestamp)firstRow[5];
				timestampValue.ToString().ShouldBe("11/01/2014 14:16:32.123456789");
				timestampValue.ToSqlLiteral().ShouldBe("TIMESTAMP'2014-11-1 14:16:32.123456789'");
				timestampValue.ToXml().ShouldBe("2014-11-01T14:16:32.123");
				timestampValue.ToJson().ShouldBe("\"2014-11-01T14:16:32.123\"");
				firstRow[6].ShouldBeTypeOf<OracleNumber>();
				var numberValue = (OracleNumber)firstRow[6];
				var expectedValue = "0.1234567890123456789012345678901234567891";
				numberValue.ToString().ShouldBe(expectedValue);
				numberValue.ToSqlLiteral().ShouldBe("0.1234567890123456789012345678901234567891");
				firstRow[7].ShouldBeTypeOf<OracleXmlValue>();
				var xmlValue = (OracleXmlValue)firstRow[7];
				xmlValue.DataTypeName.ShouldBe("XMLTYPE");
				xmlValue.Length.ShouldBe(8);
				xmlValue.Preview.ShouldBe("<root/>\u2026");
				xmlValue.ToSqlLiteral().ShouldBe("XMLTYPE('<root/>\n')");
				firstRow[8].ShouldBeTypeOf<OracleNumber>();
				numberValue = (OracleNumber)firstRow[8];
				expectedValue = "1.23456789012345678901234567890123456789E-125";
				numberValue.ToString().ShouldBe(expectedValue);
				numberValue.ToSqlLiteral().ShouldBe("1.23456789012345678901234567890123456789E-125");
				firstRow[9].ShouldBeTypeOf<OracleDateTime>();
				var dateValue = (OracleDateTime)firstRow[9];
				dateValue.ToString().ShouldBe("BC 01/01/4712 00:00:00");
				dateValue.ToSqlLiteral().ShouldBe("TO_DATE('-4712-01-01 00:00:00', 'SYYYY-MM-DD HH24:MI:SS')");
				dateValue.ToXml().ShouldBe("4712-01-01T00:00:00");
				dateValue.ToJson().ShouldBe("\"4712-01-01T00:00:00\"");
			}
		}

		[Test]
		public async Task TestNullDataTypesFetch()
		{
			var executionModel =
				new StatementExecutionModel
				{
					StatementText = "SELECT EMPTY_BLOB(), EMPTY_CLOB(), CAST(NULL AS TIMESTAMP WITH TIME ZONE), CAST(NULL AS TIMESTAMP), CAST(NULL AS DATE), CAST(NULL AS DECIMAL), NVL2(XMLTYPE('<root/>'), null, XMLTYPE('<root/>')) FROM DUAL",
					BindVariables = new BindVariableModel[0]
				};

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var statementBatchResult = await connectionAdapter.ExecuteStatementAsync(new StatementBatchExecutionModel { Statements = new[] { executionModel }, GatherExecutionStatistics = true }, CancellationToken.None);
				statementBatchResult.StatementResults.Count.ShouldBe(1);
				var result = statementBatchResult.StatementResults[0];

				result.ExecutedSuccessfully.ShouldBe(true);
				var resultInfo = result.ResultInfoColumnHeaders.Keys.First();

				var columnHeaders = result.ResultInfoColumnHeaders[resultInfo];
				columnHeaders.Count.ShouldBe(7);
				columnHeaders[0].DatabaseDataType.ShouldBe("Blob");
				columnHeaders[1].DatabaseDataType.ShouldBe("Clob");
				columnHeaders[2].DatabaseDataType.ShouldBe("TimeStampTZ");
				columnHeaders[3].DatabaseDataType.ShouldBe("TimeStamp");
				columnHeaders[4].DatabaseDataType.ShouldBe("Date");
				columnHeaders[5].DatabaseDataType.ShouldBe("Decimal");
				columnHeaders[6].DatabaseDataType.ShouldBe("XmlType");

				var resultSet = await connectionAdapter.FetchRecordsAsync(resultInfo, Int32.MaxValue, CancellationToken.None);
				resultSet.Count.ShouldBe(1);
				var firstRow = resultSet[0];
				firstRow[0].ShouldBeTypeOf<OracleBlobValue>();
				var blobValue = (OracleBlobValue)firstRow[0];
				blobValue.Length.ShouldBe(0);
				blobValue.ToString().ShouldBe(String.Empty);
				blobValue.ToSqlLiteral().ShouldBe("NULL");
				firstRow[1].ShouldBeTypeOf<OracleClobValue>();
				firstRow[1].ToString().ShouldBe(String.Empty);
				((IValue)firstRow[1]).ToSqlLiteral().ShouldBe("NULL");
				((OracleClobValue)firstRow[1]).DataTypeName.ShouldBe("CLOB");
				firstRow[2].ShouldBeTypeOf<OracleTimestampWithTimeZone>();
				firstRow[2].ToString().ShouldBe(String.Empty);
				((IValue)firstRow[2]).ToSqlLiteral().ShouldBe("NULL");
				firstRow[3].ShouldBeTypeOf<OracleTimestamp>();
				firstRow[3].ToString().ShouldBe(String.Empty);
				((IValue)firstRow[3]).ToSqlLiteral().ShouldBe("NULL");
				firstRow[4].ShouldBeTypeOf<OracleDateTime>();
				firstRow[4].ToString().ShouldBe(String.Empty);
				((IValue)firstRow[4]).ToSqlLiteral().ShouldBe("NULL");
				firstRow[5].ShouldBeTypeOf<OracleNumber>();
				firstRow[5].ToString().ShouldBe(String.Empty);
				((IValue)firstRow[5]).ToSqlLiteral().ShouldBe("NULL");
				firstRow[6].ShouldBeTypeOf<OracleXmlValue>();
				firstRow[6].ToString().ShouldBe(String.Empty);
				((IValue)firstRow[6]).ToSqlLiteral().ShouldBe("NULL");

			}
		}
#endif

		[Test]
		public async Task TestDmlExecution()
		{
			var executionModel =
				new StatementExecutionModel
				{
					StatementText = $"DELETE {ExplainPlanTableName}",
					BindVariables = new BindVariableModel[0],
				};

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var statementBatchResult = await connectionAdapter.ExecuteStatementAsync(new StatementBatchExecutionModel { Statements = new[] { executionModel } }, CancellationToken.None);
				statementBatchResult.StatementResults.Count.ShouldBe(1);
				var result = statementBatchResult.StatementResults[0];

				result.ExecutedSuccessfully.ShouldBe(true);
				result.ResultInfoColumnHeaders.Count.ShouldBe(0);
				result.AffectedRowCount.ShouldBe(0);
				result.CompilationErrors.Count.ShouldBe(0);
			}
		}

		[Test]
		public async Task TestPlSqlExecution()
		{
			var executionModel =
				new StatementExecutionModel
				{
					StatementText =
@"DECLARE
	C SYS_REFCURSOR;
BEGIN
	OPEN C FOR SELECT * FROM SYS.DUAL;
	DBMS_SQL.RETURN_RESULT(C);
END;",
					BindVariables = new BindVariableModel[0],
				};

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var statementBatchResult = await connectionAdapter.ExecuteStatementAsync(new StatementBatchExecutionModel { Statements = new[] { executionModel } }, CancellationToken.None);
				statementBatchResult.StatementResults.Count.ShouldBe(1);
				var result = statementBatchResult.StatementResults[0];

				result.ExecutedSuccessfully.ShouldBe(true);
				var resultInfo = result.ResultInfoColumnHeaders.Keys.First();

				var columnHeaders = result.ResultInfoColumnHeaders[resultInfo];
				columnHeaders.Count.ShouldBe(1);

				var resultSet = await connectionAdapter.FetchRecordsAsync(resultInfo, Int32.MaxValue, CancellationToken.None);
				resultSet.Count.ShouldBe(1);
				var rowData = resultSet[0];
				rowData[0].ToString().ShouldBe("X");
			}
		}

		[Test]
		public async Task TestColumnDetailDataProvider()
		{
			var model = new ColumnDetailsModel();
			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await databaseModel.UpdateColumnDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), "\"DUMMY\"", model, CancellationToken.None);
			}

			model.AverageValueSize.ShouldBe(2);
			model.DistinctValueCount.ShouldBe(1);
			model.HistogramBucketCount.ShouldBe(1);
			model.HistogramType.ShouldBe("None");
			model.LastAnalyzed.ShouldBeGreaterThan(DateTime.MinValue);
			model.NullValueCount.ShouldBe(0);
			model.SampleSize.ShouldBe(1);
		}

		[Test]
		public async Task TestSpecialFunctionSemanticValidity()
		{
			const string testQuery =
@"SELECT
	CUME_DIST(1, 1) WITHIN GROUP (ORDER BY NULL),
	RANK(1) WITHIN GROUP (ORDER BY NULL),
	DENSE_RANK(1) WITHIN GROUP (ORDER BY NULL),
	PERCENTILE_CONT(0) WITHIN GROUP (ORDER BY NULL),
	PERCENTILE_DISC(0) WITHIN GROUP (ORDER BY NULL),
	COALESCE(NULL, NULL, 0),
	SYSTIMESTAMP(9),
	SYSTIMESTAMP,
	LOCALTIMESTAMP(9),
	LOCALTIMESTAMP,
	CURRENT_TIMESTAMP(9),
	CURRENT_TIMESTAMP,
	CASE WHEN LNNVL(1 <> 1) THEN 1 END,
	EXTRACT(DAY FROM SYSDATE)
FROM
	TABLE(DBMS_XPLAN.DISPLAY_CURSOR(NULL, NULL, 'ALLSTATS LAST ADVANCED')) T1, TABLE(SYS.ODCIRAWLIST(HEXTORAW('ABCDEF'), HEXTORAW('A12345'), HEXTORAW('F98765'))) T2
WHERE
	LNNVL(1 <> 1)";

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var statement = OracleSqlParser.Instance.Parse(testQuery).Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var validator = new OracleStatementValidator();
				var sematicModel = await validator.BuildSemanticModelAsync(testQuery, statement, databaseModel, CancellationToken.None);
				var validationModel = validator.BuildValidationModel(sematicModel);
				validationModel.SemanticErrors.Count().ShouldBe(0);
			}
		}

		[Test]
		public async Task TestColumnIndexAndConstraintDetails()
		{
			var model = new ColumnDetailsModel();
			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await databaseModel.UpdateColumnDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"COL$\""), "\"OBJ#\"", model, CancellationToken.None);
			}

			model.IndexDetails.Count.ShouldBe(3);
			var indexes = model.IndexDetails.ToArray();
			indexes[0].IndexColumns.ShouldBe("OBJ#, NAME");
			indexes[1].IndexColumns.ShouldBe("OBJ#, COL#");
			indexes[2].IndexColumns.ShouldBe("OBJ#, INTCOL#");
			
			model.ConstraintDetails.Count.ShouldBe(1);
		}

		[Test]
		public async Task TestTableDetailDataProvider()
		{
			var model = new TableDetailsModel();

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await databaseModel.UpdateTableDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), model, CancellationToken.None);
			}

			model.AverageRowSize.ShouldBe(2);
			model.BlockCount.ShouldBe(1);
			model.ClusterName.ShouldBe(String.Empty);
			model.Compression.ShouldBe("Disabled");
			model.IsTemporary.ShouldBe(false);
			model.LastAnalyzed.ShouldBeGreaterThan(DateTime.MinValue);
			model.Organization.ShouldBe("Heap");
			model.ParallelDegree.ShouldBe("1");
			model.RowCount.ShouldBe(1);
		}

		[Test]
		public async Task TestIndexDetailDataProvider()
		{
			var model = new TableDetailsModel();

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await databaseModel.UpdateTableDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"COL$\""), model, CancellationToken.None);
			}

			model.IndexDetails.Count.ShouldBe(3);
			var indexDetails = model.IndexDetails.ToList();
			indexDetails.ForEach(i =>
			{
				i.Blocks.ShouldBeGreaterThan(0);
				i.Bytes.ShouldBeGreaterThan(0);
				i.ClusteringFactor.ShouldBeGreaterThan(0);
				i.LastAnalyzed.ShouldBeGreaterThan(DateTime.MinValue);
				i.Owner.ShouldNotBe(null);
				i.Name.ShouldNotBe(null);
				i.Rows.ShouldBeGreaterThan(0);
				i.SampleRows.ShouldBeGreaterThan(0);
				i.Status.ShouldBe("Valid");
				i.Compression.ShouldBe("Disabled");
				i.Type.ShouldBe("Normal");
			});

			indexDetails[0].IndexColumns.ShouldBe("OBJ#, NAME");
			indexDetails[1].IndexColumns.ShouldBe("OBJ#, COL#");
			indexDetails[2].IndexColumns.ShouldBe("OBJ#, INTCOL#");
		}

		[Test]
		public async Task TestConstraintDetailDataProvider()
		{
			var model = new ColumnDetailsModel();

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await databaseModel.UpdateColumnDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"XS$OBJ\""), "TENANT", model, CancellationToken.None);
			}

			model.ConstraintDetails.Count.ShouldBe(2);
			var constraintDetails = model.ConstraintDetails.ToList();
			
			constraintDetails[0].DeleteRule.ShouldBe("No Action");
			constraintDetails[0].IsDeferrable.ShouldBe(false);
			constraintDetails[0].IsDeferred.ShouldBe(false);
			constraintDetails[0].IsEnabled.ShouldBe(true);
			constraintDetails[0].Owner.ShouldBe("SYS");
			constraintDetails[0].Name.ShouldNotBe(null);
			constraintDetails[0].LastChange.ShouldBeGreaterThan(DateTime.MinValue);
			constraintDetails[0].SearchCondition.ShouldBe(String.Empty);
			constraintDetails[0].Type.ShouldBe("Referential integrity");

			constraintDetails[1].DeleteRule.ShouldBe(String.Empty);
			constraintDetails[1].IsDeferrable.ShouldBe(false);
			constraintDetails[1].IsDeferred.ShouldBe(false);
			constraintDetails[1].IsEnabled.ShouldBe(true);
			constraintDetails[1].Owner.ShouldBe("SYS");
			constraintDetails[1].Name.ShouldNotBe(null);
			constraintDetails[1].LastChange.ShouldBeGreaterThan(DateTime.MinValue);
			constraintDetails[1].SearchCondition.ShouldBe(String.Empty);
			constraintDetails[1].Type.ShouldBe("Unique key");
		}

		[Test]
		public async Task TestRemoteTableColumnDataProvider()
		{
			IReadOnlyList<string> remoteTableColumns;

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				remoteTableColumns = await databaseModel.GetRemoteTableColumnsAsync(LoopbackDatabaseLinkName, new OracleObjectIdentifier(null, "\"USER_TABLES\""), CancellationToken.None);
			}

			remoteTableColumns.Count.ShouldBe(64);
			remoteTableColumns[0].ShouldBe("\"TABLE_NAME\"");
			remoteTableColumns[63].ShouldBe("\"INMEMORY_DUPLICATE\"");
		}

		[Test]
		public async Task TestTableSpaceAllocationDataProvider()
		{
			var model = new TableDetailsModel();
			var tableSpaceAllocationDataProvider = new TableSpaceAllocationDataProvider(model, new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), String.Empty);

			await ExecuteDataProvider(tableSpaceAllocationDataProvider);

			model.AllocatedBytes.ShouldBe(65536);
			model.LargeObjectBytes.ShouldBe(null);
		}

		[Test]
		public async Task TestDisplayCursorDataProvider()
		{
			var displayCursorDataProvider = DisplayCursorDataProvider.CreateDisplayLastCursorDataProvider(new Version(12, 1));
			await ExecuteDataProvider(displayCursorDataProvider);

			displayCursorDataProvider.PlanText.ShouldNotBe(null);
			Trace.WriteLine(displayCursorDataProvider.PlanText);
		}

		[Test]
		public async Task TestViewDetailDataProvider()
		{
			var model = new ViewDetailsModel();

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await databaseModel.UpdateViewDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DBA_DV_STATUS\""), model, CancellationToken.None);
			}

			model.ConstraintDetails.Count.ShouldBe(1);
			var constraint = model.ConstraintDetails.Single();
			constraint.Owner.ShouldBe("SYS");
			constraint.Name.ShouldNotBeEmpty();
			constraint.Type.ShouldBe("With read only");
			constraint.DeleteRule.ShouldBeEmpty();
			constraint.SearchCondition.ShouldBeEmpty();
			constraint.IsDeferrable.ShouldBe(false);
			constraint.IsDeferred.ShouldBe(false);
			constraint.IsValidated.ShouldBe(false);
			constraint.IsEnabled.ShouldBe(true);
			constraint.LastChange.ShouldBeGreaterThan(DateTime.MinValue);
			model.Comment.ShouldBe(null);
		}

		[Test]
		public async Task TestViewCommentDataProvider()
		{
			var model = new ViewDetailsModel();

			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await databaseModel.UpdateViewDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"ALL_COL_PRIVS\""), model, CancellationToken.None);
			}

			model.ConstraintDetails.Count.ShouldBe(0);
			model.Comment.ShouldBe("Grants on columns for which the user is the grantor, grantee, owner,\n or an enabled role or PUBLIC is the grantee");
		}

		[Test]
		public async Task TestExplainPlanDataProvider()
		{
			ExecutionPlanItemCollection planItemCollection;
			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var executionModel =
					new StatementExecutionModel
					{
						BindVariables = new BindVariableModel[0],
						StatementText = ExplainPlanTestQuery
					};

				planItemCollection = await databaseModel.ExplainPlanAsync(executionModel, CancellationToken.None);
			}

			var rootItem = planItemCollection.RootItem;
			planItemCollection.AllItems.Count.ShouldBe(12);
			rootItem.ShouldNotBe(null);
			rootItem.Operation.ShouldBe("SELECT STATEMENT");
			rootItem.ExecutionOrder.ShouldBe(12);
			rootItem.ChildItems.Count.ShouldBe(1);
			rootItem.ChildItems[0].ExecutionOrder.ShouldBe(11);
			rootItem.ChildItems[0].Operation.ShouldBe("HASH JOIN");
			rootItem.ChildItems[0].ChildItems.Count.ShouldBe(2);
			rootItem.ChildItems[0].ChildItems[0].ExecutionOrder.ShouldBe(7);
			rootItem.ChildItems[0].ChildItems[0].Operation.ShouldBe("MERGE JOIN");
			rootItem.ChildItems[0].ChildItems[0].ChildItems.Count.ShouldBe(2);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ExecutionOrder.ShouldBe(3);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].Operation.ShouldBe("VIEW");
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ChildItems.Count.ShouldBe(1);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].ExecutionOrder.ShouldBe(2);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].Operation.ShouldBe("COUNT");
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].ChildItems.Count.ShouldBe(1);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].ExecutionOrder.ShouldBe(1);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].Operation.ShouldBe("FAST DUAL");
			rootItem.ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].ChildItems[0].ChildItems.Count.ShouldBe(0);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ExecutionOrder.ShouldBe(6);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].Operation.ShouldBe("VIEW");
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ChildItems.Count.ShouldBe(1);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ChildItems[0].ExecutionOrder.ShouldBe(5);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ChildItems[0].Operation.ShouldBe("COUNT");
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ChildItems[0].ChildItems.Count.ShouldBe(1);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ChildItems[0].ChildItems[0].ExecutionOrder.ShouldBe(4);
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ChildItems[0].ChildItems[0].Operation.ShouldBe("FAST DUAL");
			rootItem.ChildItems[0].ChildItems[0].ChildItems[1].ChildItems[0].ChildItems[0].ChildItems.Count.ShouldBe(0);
			rootItem.ChildItems[0].ChildItems[1].ExecutionOrder.ShouldBe(10);
			rootItem.ChildItems[0].ChildItems[1].Operation.ShouldBe("VIEW");
			rootItem.ChildItems[0].ChildItems[1].ChildItems.Count.ShouldBe(1);
			rootItem.ChildItems[0].ChildItems[1].ChildItems[0].ExecutionOrder.ShouldBe(9);
			rootItem.ChildItems[0].ChildItems[1].ChildItems[0].Operation.ShouldBe("COUNT");
			rootItem.ChildItems[0].ChildItems[1].ChildItems[0].ChildItems.Count.ShouldBe(1);
			rootItem.ChildItems[0].ChildItems[1].ChildItems[0].ChildItems[0].ExecutionOrder.ShouldBe(8);
			rootItem.ChildItems[0].ChildItems[1].ChildItems[0].ChildItems[0].Operation.ShouldBe("FAST DUAL");
			rootItem.ChildItems[0].ChildItems[1].ChildItems[0].ChildItems[0].ChildItems.Count.ShouldBe(0);
		}

		[Test]
		public async Task TestCursorExecutionStatisticsDataProvider()
		{
			ExecutionStatisticsPlanItemCollection taskStatistics;
			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var executionModel =
					new StatementExecutionModel
					{
						BindVariables = new BindVariableModel[0],
						StatementText = ExplainPlanTestQuery
					};

				var connectionAdapter = (OracleConnectionAdapter)databaseModel.CreateConnectionAdapter();
				var task = connectionAdapter.ExecuteStatementAsync(new StatementBatchExecutionModel { Statements = new[] { executionModel } }, CancellationToken.None);
				task.Wait();

				taskStatistics = await connectionAdapter.GetCursorExecutionStatisticsAsync(CancellationToken.None);
			}

			var allItems = taskStatistics.AllItems.Values.ToArray();
			allItems.Length.ShouldBe(12);

			var hashJoin = allItems[1];
			hashJoin.Operation.ShouldBe("HASH JOIN");
			hashJoin.Executions.ShouldBeGreaterThan(0);
			hashJoin.LastStarts.ShouldNotBe(null);
			hashJoin.TotalStarts.ShouldNotBe(null);
			hashJoin.LastOutputRows.ShouldNotBe(null);
			hashJoin.TotalOutputRows.ShouldNotBe(null);
			hashJoin.LastConsistentReadBufferGets.ShouldNotBe(null);
			hashJoin.TotalConsistentReadBufferGets.ShouldNotBe(null);
			hashJoin.LastCurrentReadBufferGets.ShouldNotBe(null);
			hashJoin.TotalCurrentReadBufferGets.ShouldNotBe(null);
			hashJoin.LastDiskReads.ShouldNotBe(null);
			hashJoin.TotalDiskReads.ShouldNotBe(null);
			hashJoin.LastDiskWrites.ShouldNotBe(null);
			hashJoin.TotalDiskWrites.ShouldNotBe(null);
			hashJoin.LastElapsedTime.ShouldNotBe(null);
			hashJoin.TotalElapsedTime.ShouldNotBe(null);
			hashJoin.WorkAreaSizingPolicy.ShouldNotBe(null);
			hashJoin.EstimatedOptimalSizeBytes.ShouldNotBe(null);
			hashJoin.EstimatedOnePassSizeBytes.ShouldNotBe(null);
			hashJoin.LastMemoryUsedBytes.ShouldNotBe(null);
			hashJoin.LastExecutionMethod.ShouldNotBe(null);
			hashJoin.LastParallelDegree.ShouldNotBe(null);
			hashJoin.TotalWorkAreaExecutions.ShouldNotBe(null);
			hashJoin.OptimalWorkAreaExecutions.ShouldNotBe(null);
			hashJoin.OnePassWorkAreaExecutions.ShouldNotBe(null);
			hashJoin.MultiPassWorkAreaExecutions.ShouldNotBe(null);
			hashJoin.ActiveWorkAreaTime.ShouldNotBe(null);
		}

		[Test]
		public async Task TestComplextExplainExecutionOrder()
		{
			const string testQuery =
@"WITH PLAN_SOURCE AS (
	SELECT
		OPERATION, OPTIONS, ID, PARENT_ID, DEPTH, CASE WHEN PARENT_ID IS NULL THEN 1 ELSE POSITION END POSITION
	FROM V$SQL_PLAN
	WHERE SQL_ID = :SQL_ID AND CHILD_NUMBER = :CHILD_NUMBER)
SELECT
	OPERATION, OPTIONS,
	ID,
	DEPTH, POSITION, CHILDREN_COUNT, TMP, TREEPATH, TREEPATH_SUM
FROM (
	SELECT
		OPERATION,
		OPTIONS,
		ID,
		DEPTH,
		POSITION,
		CHILDREN_COUNT,
		(SELECT NVL(SUM(CHILDREN_COUNT), 0) FROM PLAN_SOURCE CHILDREN WHERE PARENT_ID = PLAN_DATA.PARENT_ID AND POSITION < PLAN_DATA.POSITION) TMP,
		TREEPATH,
		(SELECT
			SUM(
				SUBSTR(
			        TREEPATH,
			        INSTR(TREEPATH, '|', 1, LEVEL) + 1,
			        INSTR(TREEPATH, '|', 1, LEVEL + 1) - INSTR(TREEPATH, '|', 1, LEVEL) - 1
			    )
		    ) AS TOKEN
		FROM
			DUAL
		CONNECT BY
			LEVEL <= LENGTH(TREEPATH) - LENGTH(REPLACE(TREEPATH, '|', NULL))
		) TREEPATH_SUM
	FROM (
		SELECT
			LPAD('-', 2 * DEPTH, '-') || OPERATION OPERATION,
			SYS_CONNECT_BY_PATH(POSITION, '|') || '|' TREEPATH,
			OPTIONS,
			ID, PARENT_ID, DEPTH, POSITION,
			(SELECT COUNT(*) FROM PLAN_SOURCE CHILDREN START WITH ID = PLAN_SOURCE.ID CONNECT BY PRIOR ID = PARENT_ID) CHILDREN_COUNT
		FROM
			PLAN_SOURCE
		START WITH
			PARENT_ID IS NULL
		CONNECT BY
			PARENT_ID = PRIOR ID		
	) PLAN_DATA
) RICH_PLAN_DATA
ORDER BY
	ID";

			ExecutionPlanItemCollection planItemCollection;
			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				var executionModel =
					new StatementExecutionModel
					{
						BindVariables = new BindVariableModel[0],
						StatementText = testQuery
					};

				planItemCollection = await databaseModel.ExplainPlanAsync(executionModel, CancellationToken.None);
			}

			var executionOrder = planItemCollection.AllItems.Values.Select(i => i.ExecutionOrder).ToArray();
			var expectedExecutionOrder = new [] { 19, 4, 3, 2, 1, 7, 6, 5, 10, 9, 8, 18, 12, 11, 17, 16, 15, 14, 13 };

			executionOrder.ShouldBe(expectedExecutionOrder);
		}

		private async Task ExecuteDataProvider(params IModelDataProvider[] updaters)
		{
			using (var databaseModel = DataModelInitializer.GetInitializedDataModel(ConnectionString))
			{
				await (Task)typeof (OracleDatabaseModel).GetMethod("UpdateModelAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(databaseModel, new object[] { CancellationToken.None, false, updaters });
			}
		}
	}

	internal static class DataModelInitializer
	{
		public static OracleDatabaseModel GetInitializedDataModel(ConnectionStringSettings connectionString, string identifier = null)
		{
			var databaseModel = OracleDatabaseModel.GetDatabaseModel(connectionString, identifier);

			using (var refreshFinishedResetEvent = new ManualResetEvent(false))
			{
				databaseModel.RefreshCompleted += delegate { refreshFinishedResetEvent.Set(); };

				databaseModel.Initialize().Wait();

				if (!databaseModel.IsFresh)
				{
					refreshFinishedResetEvent.WaitOne();
				}
			}

			return databaseModel;
		}
	}
}
