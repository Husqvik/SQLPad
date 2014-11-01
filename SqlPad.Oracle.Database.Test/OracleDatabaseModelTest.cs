using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.ToolTips;
using SqlPad.Test;

namespace SqlPad.Oracle.Database.Test
{
	[TestFixture]
	public class OracleDatabaseModelTest : TemporaryDirectoryTestFixture
	{
		private readonly ConnectionStringSettings _connectionString = new ConnectionStringSettings("TestConnection", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK");

		[Test]
		public void TestModelInitialization()
		{
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				databaseModel.AllObjects.Count.ShouldBe(0);
				databaseModel.DatabaseLinks.Count.ShouldBe(0);
				databaseModel.CharacterSets.Count.ShouldBe(0);
				databaseModel.StatisticsKeys.Count.ShouldBe(0);
				databaseModel.SystemParameters.Count.ShouldBe(0);

				using (var modelClone = OracleDatabaseModel.GetDatabaseModel(_connectionString))
				{
					var refreshTask = databaseModel.Refresh(true);
					var cloneRefreshTask = modelClone.Refresh();
					
					refreshTask.Wait();
					cloneRefreshTask.Wait();

					Trace.WriteLine("Assert original database model");
					AssertDatabaseModel(databaseModel);

					Trace.WriteLine("Assert cloned database model");
					AssertDatabaseModel(modelClone);
				}
			}
		}

		private void AssertDatabaseModel(OracleDatabaseModelBase databaseModel)
		{
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
			
			var result = databaseModel.ExecuteStatement(executionModel);
			result.ExecutedSucessfully.ShouldBe(true);
			result.AffectedRowCount.ShouldBe(-1);
			
			databaseModel.CanFetch.ShouldBe(false);

			var columnHeaders = result.ColumnHeaders.ToArray();
			columnHeaders.Length.ShouldBe(1);
			columnHeaders[0].DataType.ShouldBe(typeof(string));
			columnHeaders[0].DatabaseDataType.ShouldBe("Varchar2");
			columnHeaders[0].Name.ShouldBe("DUMMY");
			columnHeaders[0].ValueConverter.ShouldNotBe(null);

			var rows = result.InitialResultSet.ToArray();
			rows.Length.ShouldBe(1);
			rows[0].Length.ShouldBe(1);
			rows[0][0].ShouldBe("X");

			databaseModel.FetchRecords(1).Any().ShouldBe(false);

			var displayCursorTask = databaseModel.GetActualExecutionPlanAsync(CancellationToken.None);
			displayCursorTask.Wait();

			displayCursorTask.Result.ShouldNotBe(null);

			Trace.WriteLine("Display cursor output: " + Environment.NewLine + displayCursorTask.Result + Environment.NewLine);

			displayCursorTask.Result.Length.ShouldBeGreaterThan(100);

			var task = databaseModel.GetExecutionStatisticsAsync(CancellationToken.None);
			task.Wait();

			var statisticsRecords = task.Result.Where(r => r.Value != 0).ToArray();
			statisticsRecords.Length.ShouldBeGreaterThan(0);

			var statistics = String.Join(Environment.NewLine, statisticsRecords.Select(r => String.Format("{0}: {1}", r.Name.PadRight(40), r.Value)));
			Trace.WriteLine("Execution statistics output: " + Environment.NewLine + statistics + Environment.NewLine);
		}

		[Test]
		public void TestColumnDetailsModelUpdater()
		{
			var model = new ColumnDetailsModel();
			var columnDetailsUpdater = new ColumnDetailsModelUpdater(model, new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), "DUMMY");

			ExecuteUpdater(columnDetailsUpdater);

			model.AverageValueSize.ShouldBe(2);
			model.DistinctValueCount.ShouldBe(1);
			model.HistogramBucketCount.ShouldBe(1);
			model.HistogramType.ShouldBe("None");
			model.LastAnalyzed.ShouldBeGreaterThan(DateTime.MinValue);
			model.NullValueCount.ShouldBe(0);
			model.SampleSize.ShouldBe(1);
		}

		[Test]
		public void TestTableDetailsModelUpdater()
		{
			var model = new TableDetailsModel();
			var tableDetailsUpdater = new TableDetailsModelUpdater(model, new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""));

			ExecuteUpdater(tableDetailsUpdater);

			model.AverageRowSize.ShouldBe(2);
			model.BlockCount.ShouldBe(1);
			model.ClusterName.ShouldBe(null);
			model.Compression.ShouldBe("Disabled");
			model.IsPartitioned.ShouldBe(false);
			model.IsTemporary.ShouldBe(false);
			model.LastAnalyzed.ShouldBeGreaterThan(DateTime.MinValue);
			model.Organization.ShouldBe("Heap");
			model.ParallelDegree.ShouldBe("1");
			model.RowCount.ShouldBe(1);
		}

		[Test]
		public void TestTableSpaceAllocationModelUpdater()
		{
			var model = new TableDetailsModel();
			var tableSpaceAllocationUpdater = new TableSpaceAllocationModelUpdater(model, new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""));

			ExecuteUpdater(tableSpaceAllocationUpdater);

			model.AllocatedBytes.ShouldBe(65536);
		}

		[Test]
		public void TestDisplayCursorUpdater()
		{
			var displayCursorUpdater = (DisplayCursorUpdater)DisplayCursorUpdater.CreateDisplayLastCursorUpdater();
			ExecuteUpdater(displayCursorUpdater);

			displayCursorUpdater.PlanText.ShouldNotBe(null);
			Trace.WriteLine(displayCursorUpdater.PlanText);
		}

		private void ExecuteUpdater(IDataModelUpdater columnDetailsUpdater)
		{
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				var task = (Task)typeof(OracleDatabaseModel).GetMethod("UpdateModelAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(databaseModel, new object[] { CancellationToken.None, false, new[] { columnDetailsUpdater } });
				task.Wait();
			}
		}
	}
}
