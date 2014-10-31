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

					AssertDatabaseModel(databaseModel);

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
					BindVariables = new[] {new BindVariableModel(new BindVariableConfiguration {Name = "1", Value = "X"})}
				};
			
			var result = databaseModel.ExecuteStatement(executionModel);
			result.ExecutedSucessfully.ShouldBe(true);
			result.AffectedRowCount.ShouldBe(-1);
			
			databaseModel.CanFetch.ShouldBe(true);

			var columnHeaders = result.ColumnHeaders.ToArray();
			columnHeaders.Length.ShouldBe(1);
			columnHeaders[0].DataType.ShouldBe(typeof(string));
			columnHeaders[0].DatabaseDataType.ShouldBe("Varchar2");
			columnHeaders[0].Name.ShouldBe("DUMMY");
			columnHeaders[0].ValueConverter.ShouldNotBe(null);

			var rows = databaseModel.FetchRecords(2).ToArray();
			rows.Length.ShouldBe(1);
			rows[0].Length.ShouldBe(1);
			rows[0][0].ShouldBe("X");

			var displayCursorTask = databaseModel.GetActualExecutionPlanAsync(CancellationToken.None);
			displayCursorTask.Wait();

			Trace.WriteLine("Display cursor output: " + Environment.NewLine + displayCursorTask.Result + Environment.NewLine);

			displayCursorTask.Result.ShouldNotBe(null);
			displayCursorTask.Result.Length.ShouldBeGreaterThan(100);

			databaseModel.CanFetch.ShouldBe(false);
		}

		[Test]
		public void TestColumnDetailsModelUpdater()
		{
			var model = new ColumnDetailsModel();
			var columnDetailsUpdater = new ColumnDetailsModelUpdater(model, new OracleObjectIdentifier(OracleDatabaseModelBase.SchemaSys, "\"DUAL\""), "DUMMY");

			UpdateModel(columnDetailsUpdater);

			model.AverageValueSize.ShouldBe(2);
			model.DistinctValueCount.ShouldBe(1);
			model.HistogramBucketCount.ShouldBe(1);
			model.HistogramType.ShouldBe("None");
			model.LastAnalyzed.ShouldBeGreaterThan(DateTime.MinValue);
			model.NullValueCount.ShouldBe(0);
			model.SampleSize.ShouldBe(1);
		}

		private void UpdateModel(IDataModelUpdater columnDetailsUpdater)
		{
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(_connectionString))
			{
				var task = (Task)typeof(OracleDatabaseModel).GetMethod("UpdateModelAsync", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(databaseModel, new object[] { CancellationToken.None, false, new[] { columnDetailsUpdater } });
				task.Wait();
			}
		}
	}
}
