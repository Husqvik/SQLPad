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
		private const string LoopbackDatabaseLinkName = "HQ_PDB";
		private const string ExplainPlanTableName = "TOAD_PLAN_TABLE";
		private readonly ConnectionStringSettings _connectionString = new ConnectionStringSettings("TestConnection", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK");

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
		}

		[Test]
		public void TestModelInitialization()
		{
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Schemas.Count.ShouldBe(0);
				databaseModel.AllObjects.Count.ShouldBe(0);
				databaseModel.DatabaseLinks.Count.ShouldBe(0);
				databaseModel.CharacterSets.Count.ShouldBe(0);
				databaseModel.StatisticsKeys.Count.ShouldBe(0);
				databaseModel.SystemParameters.Count.ShouldBe(0);

				databaseModel.IsInitialized.ShouldBe(false);
				var refreshStartedResetEvent = new ManualResetEvent(false);
				var refreshFinishedResetEvent = new ManualResetEvent(false);
				databaseModel.RefreshStarted += (sender, args) => refreshStartedResetEvent.Set();
				databaseModel.RefreshCompleted += (sender, args) => refreshFinishedResetEvent.Set();
				databaseModel.Initialize();
				refreshStartedResetEvent.WaitOne();

				using (var modelClone = OracleDatabaseModel.GetDatabaseModel(_connectionString))
				{
					var cloneRefreshTask = modelClone.Refresh();
					refreshFinishedResetEvent.WaitOne();
					cloneRefreshTask.Wait();

					databaseModel.IsInitialized.ShouldBe(true);
					databaseModel.Schemas.Count.ShouldBeGreaterThan(0);

					Trace.WriteLine("Assert original database model");
					AssertDatabaseModel(databaseModel);

					Trace.WriteLine("Assert cloned database model");
					AssertDatabaseModel(modelClone);
				}
			}
		}

		private void AssertDatabaseModel(OracleDatabaseModelBase databaseModel)
		{
			databaseModel.Version.Major.ShouldBeGreaterThanOrEqualTo(11);

			databaseModel.AllObjects.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine(String.Format("All object dictionary has {0} members. ", databaseModel.AllObjects.Count));
			
			databaseModel.DatabaseLinks.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine(String.Format("Database link dictionary has {0} members. ", databaseModel.DatabaseLinks.Count));

			databaseModel.CharacterSets.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine(String.Format("Character set collection has {0} members. ", databaseModel.CharacterSets.Count));

			databaseModel.StatisticsKeys.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine(String.Format("Statistics key dictionary has {0} members. ", databaseModel.StatisticsKeys.Count));

			databaseModel.SystemParameters.Count.ShouldBeGreaterThan(0);
			Trace.WriteLine(String.Format("System parameters dictionary has {0} members. ", databaseModel.SystemParameters.Count));

			var objectForScriptCreation = databaseModel.GetFirstSchemaObject<OracleSchemaObject>(databaseModel.GetPotentialSchemaObjectIdentifiers("SYS", "OBJ$"));
			objectForScriptCreation.ShouldNotBe(null);
			var scriptTask = databaseModel.GetObjectScriptAsync(objectForScriptCreation, CancellationToken.None);
			scriptTask.Wait();

			Trace.WriteLine("Object script output: " + Environment.NewLine + scriptTask.Result + Environment.NewLine);

			scriptTask.Result.ShouldNotBe(null);
			scriptTask.Result.Length.ShouldBeGreaterThan(100);

			var executionModel =
				new StatementExecutionModel
				{
					StatementText = "SELECT /*+ gather_plan_statistics */ * FROM DUAL WHERE DUMMY = :1",
					BindVariables = new[] { new BindVariableModel(new BindVariableConfiguration { Name = "1", Value = "X" }) },
					GatherExecutionStatistics = true
				};

			var connectionAdapter = (OracleConnectionAdapter)databaseModel.CreateConnectionAdapter();
			var taskStatement = connectionAdapter.ExecuteStatementAsync(executionModel, CancellationToken.None);
			taskStatement.Wait();
			var result = taskStatement.Result;
			result.ExecutedSuccessfully.ShouldBe(true);
			result.AffectedRowCount.ShouldBe(-1);

			connectionAdapter.CanFetch.ShouldBe(false);

			var columnHeaders = result.ColumnHeaders.ToArray();
			columnHeaders.Length.ShouldBe(1);
			columnHeaders[0].DataType.ShouldBe(typeof(string));
			columnHeaders[0].DatabaseDataType.ShouldBe("Varchar2");
			columnHeaders[0].Name.ShouldBe("DUMMY");

			var rows = result.InitialResultSet.ToArray();
			rows.Length.ShouldBe(1);
			rows[0].Length.ShouldBe(1);
			rows[0][0].ToString().ShouldBe("X");

			var task = connectionAdapter.FetchRecordsAsync(1, CancellationToken.None);
			task.Wait();
			task.Result.Any().ShouldBe(false);

			var displayCursorTask = connectionAdapter.GetCursorExecutionStatisticsAsync(CancellationToken.None);
			displayCursorTask.Wait();

			var planItemCollection = displayCursorTask.Result;
			planItemCollection.PlanText.ShouldNotBe(null);

			Trace.WriteLine("Display cursor output: " + Environment.NewLine + planItemCollection.PlanText + Environment.NewLine);

			planItemCollection.PlanText.Length.ShouldBeGreaterThan(100);

			var taskStatistics = connectionAdapter.GetExecutionStatisticsAsync(CancellationToken.None);
			taskStatistics.Wait();

			var statisticsRecords = taskStatistics.Result.Where(r => r.Value != 0).ToArray();
			statisticsRecords.Length.ShouldBeGreaterThan(0);

			var statistics = String.Join(Environment.NewLine, statisticsRecords.Select(r => String.Format("{0}: {1}", r.Name.PadRight(40), r.Value)));
			Trace.WriteLine("Execution statistics output: " + Environment.NewLine + statistics + Environment.NewLine);
		}

		#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
		[Test]
		public void TestDataTypesFetch()
		{
			var clobParameter = String.Join(" ", Enumerable.Repeat("CLOB DATA", 200));
			var executionModel =
				new StatementExecutionModel
				{
					StatementText = "SELECT TO_BLOB(RAWTOHEX('BLOB')), TO_CLOB('" + clobParameter + "'), TO_NCLOB('NCLOB DATA'), DATA_DEFAULT, TIMESTAMP'2014-11-01 14:16:32.123456789 CET' AT TIME ZONE '02:00', TIMESTAMP'2014-11-01 14:16:32.123456789', 0.1234567890123456789012345678901234567891, XMLTYPE('<root/>'), 1.23456789012345678901234567890123456789E-125, DATE'-4712-01-01' FROM ALL_TAB_COLS WHERE OWNER = 'SYS' AND TABLE_NAME = 'DUAL'",
					BindVariables = new BindVariableModel[0],
					GatherExecutionStatistics = true
				};

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();

				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var task = connectionAdapter.ExecuteStatementAsync(executionModel, CancellationToken.None);
				task.Wait();
				var result = task.Result;
				
				result.ExecutedSuccessfully.ShouldBe(true);

				result.ColumnHeaders.Count.ShouldBe(10);
				result.ColumnHeaders[0].DatabaseDataType.ShouldBe("Blob");
				result.ColumnHeaders[1].DatabaseDataType.ShouldBe("Clob");
				result.ColumnHeaders[2].DatabaseDataType.ShouldBe("NClob");
				result.ColumnHeaders[3].DatabaseDataType.ShouldBe("Long");
				result.ColumnHeaders[4].DatabaseDataType.ShouldBe("TimeStampTZ");
				result.ColumnHeaders[5].DatabaseDataType.ShouldBe("TimeStamp");
				result.ColumnHeaders[6].DatabaseDataType.ShouldBe("Decimal");
				result.ColumnHeaders[7].DatabaseDataType.ShouldBe("XmlType");
				result.ColumnHeaders[8].DatabaseDataType.ShouldBe("Decimal");
				result.ColumnHeaders[9].DatabaseDataType.ShouldBe("Date");

				result.InitialResultSet.Count.ShouldBe(1);
				var firstRow = result.InitialResultSet[0];
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
				var expectedLiteral = String.Format("TO_CLOB('{0}')", clobParameter);
				clobValue.ToSqlLiteral().ShouldBe(expectedLiteral);
				clobValue.ToXml().ShouldBe(String.Format("<![CDATA[{0}]]>", clobParameter));
				clobValue.ToJson().ShouldBe(String.Format("\"{0}\"", clobParameter));
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
				timestampWithTimezoneValue.ToString().ShouldBe("11/1/2014 3:16:32 PM.123456789 +02:00");
				timestampWithTimezoneValue.ToSqlLiteral().ShouldBe("TIMESTAMP'2014-11-1 15:16:32.123456789 +02:00'");
				timestampWithTimezoneValue.ToXml().ShouldBe("2014-11-01T15:16:32.123");
				timestampWithTimezoneValue.ToJson().ShouldBe("\"2014-11-01T15:16:32.123+02:00\"");
				firstRow[5].ShouldBeTypeOf<OracleTimestamp>();
				var timestampValue = (OracleTimestamp)firstRow[5];
				timestampValue.ToString().ShouldBe("11/1/2014 2:16:32 PM.123456789");
				timestampValue.ToSqlLiteral().ShouldBe("TIMESTAMP'2014-11-1 14:16:32.123456789'");
				timestampValue.ToXml().ShouldBe("2014-11-01T14:16:32.123");
				timestampValue.ToJson().ShouldBe("\"2014-11-01T14:16:32.123\"");
				firstRow[6].ShouldBeTypeOf<OracleNumber>();
				var numberValue = (OracleNumber)firstRow[6];
				var expectedValue = String.Format("0{0}1234567890123456789012345678901234567891", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
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
				expectedValue = String.Format("1{0}23456789012345678901234567890123456789E-125", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
				numberValue.ToString().ShouldBe(expectedValue);
				numberValue.ToSqlLiteral().ShouldBe("1.23456789012345678901234567890123456789E-125");
				firstRow[9].ShouldBeTypeOf<OracleDateTime>();
				var dateValue = (OracleDateTime)firstRow[9];
				dateValue.ToString().ShouldBe("BC 1/1/4712 12:00:00 AM");
				dateValue.ToSqlLiteral().ShouldBe("TO_DATE('-4712-01-01 00:00:00', 'SYYYY-MM-DD HH24:MI:SS')");
				dateValue.ToXml().ShouldBe("4712-01-01T00:00:00");
				dateValue.ToJson().ShouldBe("\"4712-01-01T00:00:00\"");
			}
		}

		[Test]
		public void TestNullDataTypesFetch()
		{
			var executionModel =
					new StatementExecutionModel
					{
						StatementText = "SELECT EMPTY_BLOB(), EMPTY_CLOB(), CAST(NULL AS TIMESTAMP WITH TIME ZONE), CAST(NULL AS TIMESTAMP), CAST(NULL AS DATE), CAST(NULL AS DECIMAL), NVL2(XMLTYPE('<root/>'), null, XMLTYPE('<root/>')) FROM DUAL",
						BindVariables = new BindVariableModel[0],
						GatherExecutionStatistics = true
					};

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();

				var connectionAdapter = databaseModel.CreateConnectionAdapter();
				var task = connectionAdapter.ExecuteStatementAsync(executionModel, CancellationToken.None);
				task.Wait();
				var result = task.Result;

				result.ExecutedSuccessfully.ShouldBe(true);

				result.ColumnHeaders.Count.ShouldBe(7);
				result.ColumnHeaders[0].DatabaseDataType.ShouldBe("Blob");
				result.ColumnHeaders[1].DatabaseDataType.ShouldBe("Clob");
				result.ColumnHeaders[2].DatabaseDataType.ShouldBe("TimeStampTZ");
				result.ColumnHeaders[3].DatabaseDataType.ShouldBe("TimeStamp");
				result.ColumnHeaders[4].DatabaseDataType.ShouldBe("Date");
				result.ColumnHeaders[5].DatabaseDataType.ShouldBe("Decimal");
				result.ColumnHeaders[6].DatabaseDataType.ShouldBe("XmlType");

				result.InitialResultSet.Count.ShouldBe(1);
				var firstRow = result.InitialResultSet[0];
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
		public void TestColumnDetailDataProvider()
		{
			var model = new ColumnDetailsModel();
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				databaseModel.UpdateColumnDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), "\"DUMMY\"", model, CancellationToken.None).Wait();
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
		public void TestSpecialFunctionSemanticValidity()
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

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				if (!databaseModel.IsFresh)
				{
					var refreshFinishedResetEvent = new ManualResetEvent(false);
					databaseModel.RefreshCompleted += delegate { refreshFinishedResetEvent.Set(); };
					refreshFinishedResetEvent.WaitOne(TimeSpan.FromSeconds(30));
				}

				var statement = new OracleSqlParser().Parse(testQuery).Single();
				statement.ParseStatus.ShouldBe(ParseStatus.Success);

				var validator = new OracleStatementValidator();
				var validationModel = validator.BuildValidationModel(validator.BuildSemanticModel(testQuery, statement, databaseModel));
				validationModel.SemanticErrors.Count().ShouldBe(0);
			}
		}

		[Test]
		public void TestColumnIndexAndConstraintDetails()
		{
			var model = new ColumnDetailsModel();
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				databaseModel.UpdateColumnDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"COL$\""), "\"OBJ#\"", model, CancellationToken.None).Wait();
			}

			model.IndexDetails.Count.ShouldBe(3);
			var indexes = model.IndexDetails.ToArray();
			indexes[0].IndexColumns.ShouldBe("OBJ#, NAME");
			indexes[1].IndexColumns.ShouldBe("OBJ#, COL#");
			indexes[2].IndexColumns.ShouldBe("OBJ#, INTCOL#");
			
			model.ConstraintDetails.Count.ShouldBe(1);
		}

		[Test]
		public void TestTableDetailDataProvider()
		{
			var model = new TableDetailsModel();

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				databaseModel.UpdateTableDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), model, CancellationToken.None).Wait();
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
		public void TestIndexDetailDataProvider()
		{
			var model = new TableDetailsModel();

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				databaseModel.UpdateTableDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"COL$\""), model, CancellationToken.None).Wait();
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
		public void TestConstraintDetailDataProvider()
		{
			var model = new ColumnDetailsModel();

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				databaseModel.UpdateColumnDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"XS$OBJ\""), "TENANT", model, CancellationToken.None).Wait();
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
		public void TestRemoteTableColumnDataProvider()
		{
			IReadOnlyList<string> remoteTableColumns;

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				var task = databaseModel.GetRemoteTableColumnsAsync(LoopbackDatabaseLinkName, new OracleObjectIdentifier(null, "\"USER_TABLES\""), CancellationToken.None);
				task.Wait();

				remoteTableColumns = task.Result;
			}

			remoteTableColumns.Count.ShouldBe(64);
			remoteTableColumns[0].ShouldBe("\"TABLE_NAME\"");
			remoteTableColumns[63].ShouldBe("\"INMEMORY_DUPLICATE\"");
		}

		[Test]
		public void TestTableSpaceAllocationDataProvider()
		{
			var model = new TableDetailsModel();
			var tableSpaceAllocationDataProvider = new TableSpaceAllocationDataProvider(model, new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), String.Empty);

			ExecuteDataProvider(tableSpaceAllocationDataProvider);

			model.AllocatedBytes.ShouldBe(65536);
			model.LargeObjectBytes.ShouldBe(null);
		}

		[Test]
		public void TestDisplayCursorDataProvider()
		{
			var displayCursorDataProvider = DisplayCursorDataProvider.CreateDisplayLastCursorDataProvider();
			ExecuteDataProvider(displayCursorDataProvider);

			displayCursorDataProvider.PlanText.ShouldNotBe(null);
			Trace.WriteLine(displayCursorDataProvider.PlanText);
		}

		[Test]
		public void TestViewDetailDataProvider()
		{
			var model = new ViewDetailsModel();

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				databaseModel.UpdateViewDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DBA_DV_STATUS\""), model, CancellationToken.None).Wait();
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
		public void TestViewCommentDataProvider()
		{
			var model = new ViewDetailsModel();

			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				databaseModel.UpdateViewDetailsAsync(new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"ALL_COL_PRIVS\""), model, CancellationToken.None).Wait();
			}

			model.ConstraintDetails.Count.ShouldBe(0);
			model.Comment.ShouldBe("Grants on columns for which the user is the grantor, grantee, owner,\n or an enabled role or PUBLIC is the grantee");
		}

		[Test]
		public void TestExplainPlanDataProvider()
		{
			Task<ExecutionPlanItemCollection> task;
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();
				
				var executionModel =
					new StatementExecutionModel
					{
						BindVariables = new BindVariableModel[0],
						StatementText = ExplainPlanTestQuery
					};

				task = databaseModel.ExplainPlanAsync(executionModel, CancellationToken.None);
				task.Wait();
			}

			var rootItem = task.Result.RootItem;
			task.Result.AllItems.Count.ShouldBe(12);
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
		public void TestCursorExecutionStatisticsDataProvider()
		{
			Task<ExecutionStatisticsPlanItemCollection> taskStatistics;
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();

				var executionModel =
					new StatementExecutionModel
					{
						BindVariables = new BindVariableModel[0],
						StatementText = ExplainPlanTestQuery
					};

				var connectionAdapter = (OracleConnectionAdapter)databaseModel.CreateConnectionAdapter();
				var task = connectionAdapter.ExecuteStatementAsync(executionModel, CancellationToken.None);
				task.Wait();

				taskStatistics = connectionAdapter.GetCursorExecutionStatisticsAsync(CancellationToken.None);
				taskStatistics.Wait();
			}

			var allItems = taskStatistics.Result.AllItems.Values.ToArray();
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
		public void TestComplextExplainExecutionOrder()
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
			
			Task<ExecutionPlanItemCollection> task;
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();

				var executionModel =
					new StatementExecutionModel
					{
						BindVariables = new BindVariableModel[0],
						StatementText = testQuery
					};

				task = databaseModel.ExplainPlanAsync(executionModel, CancellationToken.None);
				task.Wait();
			}

			var executionOrder = task.Result.AllItems.Values.Select(i => i.ExecutionOrder).ToArray();
			var expectedExecutionOrder = new [] { 19, 4, 3, 2, 1, 7, 6, 5, 10, 9, 8, 18, 12, 11, 17, 16, 15, 14, 13 };

			executionOrder.ShouldBe(expectedExecutionOrder);
		}

		private void ExecuteDataProvider(params IModelDataProvider[] updaters)
		{
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.Initialize().Wait();

				var task = (Task)typeof (OracleDatabaseModel).GetMethod("UpdateModelAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(databaseModel, new object[] { CancellationToken.None, false, updaters });
				task.Wait();
			}
		}
	}
}
