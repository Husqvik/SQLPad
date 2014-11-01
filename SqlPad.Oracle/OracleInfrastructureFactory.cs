using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using Microsoft.Win32;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;

namespace SqlPad.Oracle
{
	public class OracleInfrastructureFactory : IInfrastructureFactory
	{
		private const string OracleDataAccessRegistryPath = @"Software\Oracle\ODP.NET";

		private readonly OracleCommandFactory _commandFactory = new OracleCommandFactory();

		static OracleInfrastructureFactory()
		{
			string odacVersion = null;
			var odacRegistryKey = Registry.LocalMachine.OpenSubKey(OracleDataAccessRegistryPath);
			if (odacRegistryKey != null)
			{
				odacVersion = odacRegistryKey.GetSubKeyNames().OrderByDescending(n => n).FirstOrDefault();
			}

			var traceMessage = odacVersion == null
				? "Oracle Data Access Client registry entry was not found. "
				: String.Format("Oracle Data Access Client version {0} found. ", odacVersion);

			Trace.WriteLine(traceMessage);
		}

		#region Implementation of IInfrastructureFactory
		public ICommandFactory CommandFactory { get { return _commandFactory; } }
		
		public ITokenReader CreateTokenReader(string sqlText)
		{
			return OracleTokenReader.Create(sqlText);
		}

		public ISqlParser CreateParser()
		{
			return new OracleSqlParser();
		}

		public IStatementValidator CreateStatementValidator()
		{
			return new OracleStatementValidator();
		}

		public IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString)
		{
			return OracleDatabaseModel.GetDatabaseModel(connectionString);
		}

		public ICodeCompletionProvider CreateCodeCompletionProvider()
		{
			return new OracleCodeCompletionProvider();
		}

		public ICodeSnippetProvider CreateSnippetProvider()
		{
			return new OracleSnippetProvider();
		}

		public IContextActionProvider CreateContextActionProvider()
		{
			return new OracleContextActionProvider();

		}

		public IMultiNodeEditorDataProvider CreateMultiNodeEditorDataProvider()
		{
			return new OracleMultiNodeEditorDataProvider();
		}

		public IStatementFormatter CreateSqlFormatter(SqlFormatterOptions options)
		{
			return new OracleStatementFormatter(options);
		}

		public IToolTipProvider CreateToolTipProvider()
		{
			return new OracleToolTipProvider();
		}

		public INavigationService CreateNavigationService()
		{
			return new OracleNavigationService();
		}
		#endregion
	}
}
