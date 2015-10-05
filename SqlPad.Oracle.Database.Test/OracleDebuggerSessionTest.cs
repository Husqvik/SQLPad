using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.Database.Test
{
	[TestFixture]
	public class OracleDebuggerSessionTest
	{
		private static readonly ConnectionStringSettings ConnectionString;
		private readonly OracleStatementValidator _validator = new OracleStatementValidator();

		private ManualResetEvent _resetEvent;

		static OracleDebuggerSessionTest()
		{
			var databaseConfiguration = (DatabaseConnectionConfigurationSection)ConfigurationManager.GetSection(DatabaseConnectionConfigurationSection.SectionName);
			ConnectionString = ConfigurationManager.ConnectionStrings[databaseConfiguration.Infrastructures[0].ConnectionStringName];
		}

		[SetUp]
		public void SetUpAttribute()
		{
			_resetEvent = new ManualResetEvent(false);
		}

		[TearDown]
		public void TearDownAttribute()
		{
			_resetEvent.Dispose();
			_resetEvent = null;
		}

		[Test]
		public void BasicTest()
		{
			var databaseModel = OracleDatabaseModel.GetDatabaseModel(ConnectionString, "DebuggerTestConnection");
			databaseModel.Initialize().Wait();
			var connectionAdapter = (OracleConnectionAdapter)databaseModel.CreateConnectionAdapter();

			OracleConfiguration.Configuration.StartupScript = "ALTER SESSION SET plsql_debug = true";

			const string sql =
@"DECLARE
	var1 NUMBER NOT NULL := 1;
	var2 VARCHAR2(30);
	var3 SYS.ODCIVARCHAR2LIST := SYS.ODCIVARCHAR2LIST('value 1', 'value 2');
BEGIN
	var1 := var1 + 1;
	var2 := 'Dummy test';
END;";

			var statement = OracleSqlParser.Instance.Parse(sql).Single();
			statement.ParseStatus.ShouldBe(ParseStatus.Success);
			var validationModel = _validator.BuildValidationModel(_validator.BuildSemanticModel(sql, statement, databaseModel));

			var batchModel =
				new StatementBatchExecutionModel
				{
					Statements =
						new []
						{
							new StatementExecutionModel
							{
								StatementText = sql,
								ValidationModel = validationModel,
								BindVariables = new BindVariableModel[0]
							}
						},
					EnableDebug = true
				};

			var task = connectionAdapter.ExecuteStatementAsync(batchModel, CancellationToken.None);
			task.Wait();

			connectionAdapter.DebuggerSession.ShouldNotBe(null);

			connectionAdapter.DebuggerSession.Attached += DebuggerSessionAttachedHandler;
			connectionAdapter.DebuggerSession.Start(CancellationToken.None);

			_resetEvent.WaitOne();

			connectionAdapter.DebuggerSession.ShouldBe(null);
		}

		private void DebuggerSessionAttachedHandler(object sender, EventArgs eventArgs)
		{
			try
			{
				var debuggerSession = (OracleDebuggerSession)sender;

				debuggerSession.ActiveLine.ShouldBe(1);

				const int breakPointLine = 6;
				var breakpointTask = debuggerSession.SetBreakpoint(OracleObjectIdentifier.Empty, breakPointLine, CancellationToken.None);
				breakpointTask.Wait();
				breakpointTask.Result.IsSuccessful.ShouldBe(true);
				var breakpointIdentifier = breakpointTask.Result.BreakpointIdentifier;

				debuggerSession.Continue(CancellationToken.None).Wait();

				debuggerSession.ActiveLine.ShouldBe(breakPointLine);

				var watchItemVar1 = new WatchItem { Name = "var1" };
				debuggerSession.GetValue(watchItemVar1, CancellationToken.None).Wait();

				watchItemVar1.Value.ShouldBe("1");

				debuggerSession.StepInto(CancellationToken.None).Wait();

				debuggerSession.ActiveLine.ShouldBe(breakPointLine + 1);

				debuggerSession.GetValue(watchItemVar1, CancellationToken.None).Wait();

				watchItemVar1.Value.ShouldBe("2");

				watchItemVar1.Value = 999;
				debuggerSession.SetValue(watchItemVar1, CancellationToken.None).Wait();
				debuggerSession.GetValue(watchItemVar1, CancellationToken.None).Wait();
				watchItemVar1.Value.ShouldBe("999");

				var watchItemVar3 = new WatchItem { Name = "var3" };
				debuggerSession.GetValue(watchItemVar3, CancellationToken.None).Wait();
				watchItemVar3.ChildItems.ShouldNotBe(null);
				watchItemVar3.ChildItems.Count.ShouldBe(2);
				/*watchItemVar3.ChildItems[0].Name.ShouldBe("var3(1)");
				watchItemVar3.ChildItems[0].Value.ShouldBe("value 1");
				watchItemVar3.ChildItems[1].Name.ShouldBe("var3(2)");
				watchItemVar3.ChildItems[1].Value.ShouldBe("value 2");*/

				breakpointTask = debuggerSession.DisableBreakpoint(breakpointIdentifier, CancellationToken.None);
				breakpointTask.Wait();
				breakpointTask.Result.IsSuccessful.ShouldBe(true);

				breakpointTask = debuggerSession.DeleteBreakpoint(breakpointIdentifier, CancellationToken.None);
				breakpointTask.Wait();
				breakpointTask.Result.IsSuccessful.ShouldBe(true);

				debuggerSession.Continue(CancellationToken.None).Wait();
			}
			finally
			{
				_resetEvent.Set();
			}
		}
	}
}