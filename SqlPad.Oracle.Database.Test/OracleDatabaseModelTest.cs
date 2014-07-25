using System.Configuration;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Database.Test
{
	[TestFixture]
	public class OracleDatabaseModelTest
	{
		[Test]
		public void ModelInitializationTest()
		{
			var connectionString = new ConnectionStringSettings("TestConnection", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;PERSIST SECURITY INFO=True;USER ID=HUSQVIK");
			using (var databaseModel = OracleDatabaseModel.GetDatabaseModel(connectionString))
			{
				databaseModel.AllObjects.Count.ShouldBe(0);

				var refreshTask = databaseModel.Refresh(true);
				refreshTask.Wait();

				AssertDatabaseModel(databaseModel);
			}
		}

		private void AssertDatabaseModel(OracleDatabaseModelBase databaseModel)
		{
			databaseModel.AllObjects.Count.ShouldBeGreaterThan(0);

			var objectForScriptCreation = databaseModel.GetFirstSchemaObject<OracleSchemaObject>(databaseModel.GetPotentialSchemaObjectIdentifiers("SYS", "OBJ$"));
			objectForScriptCreation.ShouldNotBe(null);
			var scriptTask = databaseModel.GetObjectScriptAsync(objectForScriptCreation, CancellationToken.None);
			scriptTask.Wait();
			scriptTask.Result.ShouldNotBe(null);
			scriptTask.Result.Length.ShouldBeGreaterThan(100);

			databaseModel.ExecuteStatement("SELECT * FROM DUAL", true).ShouldBe(0);
			databaseModel.CanFetch.ShouldBe(true);

			var columnHeaders = databaseModel.GetColumnHeaders().ToArray();
			columnHeaders.Length.ShouldBe(1);
			columnHeaders[0].DataType.ShouldBe(typeof(string));
			columnHeaders[0].DatabaseDataType.ShouldBe("Varchar2");
			columnHeaders[0].Name.ShouldBe("DUMMY");
			columnHeaders[0].ValueConverterFunction.ShouldNotBe(null);

			var rows = databaseModel.FetchRecords(2).ToArray();
			rows.Length.ShouldBe(1);
			rows[0].Length.ShouldBe(1);
			rows[0][0].ShouldBe("X");

			databaseModel.CanFetch.ShouldBe(false);
		}
	}
}
