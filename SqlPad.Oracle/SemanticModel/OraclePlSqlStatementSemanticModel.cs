using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SqlPad.Oracle.DatabaseConnection;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.SemanticModel
{
	public static class OracleStatementSemanticModelFactory
	{
		public static IStatementSemanticModel Build(OracleDatabaseModelBase databaseModel, OracleStatement statement, string statementText)
		{
			return null;
		}
	}

	public class OraclePlSqlStatementSemanticModel : IStatementSemanticModel
	{
		private readonly OracleDatabaseModelBase _databaseModel;
		private readonly List<OraclePlSqlProgram> _programs = new List<OraclePlSqlProgram>();
		private readonly List<OracleStatementSemanticModel> _sqlModels = new List<OracleStatementSemanticModel>();

		private CancellationToken _cancellationToken;

		public IDatabaseModel DatabaseModel => _databaseModel;

		public StatementBase Statement { get; private set; }

		public string StatementText { get; private set; }

		public bool HasDatabaseModel => DatabaseModel == null;

		public ICollection<RedundantTerminalGroup> RedundantSymbolGroups => new RedundantTerminalGroup[0];

		public IReadOnlyList<OraclePlSqlProgram> Programs => _programs.AsReadOnly();

		private OraclePlSqlStatementSemanticModel(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel)
		{
			if (statement == null)
			{
				throw new ArgumentNullException(nameof(statement));
			}

			if (!String.Equals(statement.RootNode.Id, NonTerminals.CreatePlSqlStatement) && !String.Equals(statement.RootNode.Id, NonTerminals.PlSqlBlockStatement))
			{
				throw new ArgumentException("Statement is not PL/SQL statement. ", nameof(statement));
			}

			_databaseModel = databaseModel;
			Statement = statement;
			StatementText = statementText;
		}

		public static OraclePlSqlStatementSemanticModel Build(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel)
		{
			return new OraclePlSqlStatementSemanticModel(statementText, statement, databaseModel).Build(CancellationToken.None);
		}

		public OraclePlSqlStatementSemanticModel Build(CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;

			ResolveProgramDefinitions();
			ResolveProgramBodies();

			return this;
		}

		private void ResolveProgramBodies()
		{

		}

		private void ResolveProgramDefinitions()
		{
			var anonymousPlSqlBlock = String.Equals(Statement.RootNode.Id, NonTerminals.PlSqlBlockStatement);
			var functionOrProcedure = Statement.RootNode[NonTerminals.CreatePlSqlObjectClause]?[0];
			var isSchemaFunctionOrProcedure = functionOrProcedure != null && (String.Equals(functionOrProcedure.Id, NonTerminals.CreateProcedure) || String.Equals(functionOrProcedure.Id, NonTerminals.CreateFunction));
			if (isSchemaFunctionOrProcedure || anonymousPlSqlBlock)
			{
				var plSqlProgram = new OraclePlSqlProgram { RootNode = functionOrProcedure };
				_programs.Add(plSqlProgram);

				ResolveSubProgramDefinitions(plSqlProgram);
			}
			else
			{
				throw new NotImplementedException();
				var programDefinitionNodes = Statement.RootNode.GetDescendants(NonTerminals.FunctionDefinition, NonTerminals.ProcedureDefinition);
				_programs.AddRange(programDefinitionNodes.Select(n => new OraclePlSqlProgram { RootNode = n }));
			}
		}

		private void ResolveSubProgramDefinitions(OraclePlSqlProgram program)
		{

		}
	}

	public class OraclePlSqlProgram
	{
		public StatementGrammarNode RootNode { get; set; }

		public IList<OraclePlSqlProgram> SubPrograms { get; } = new List<OraclePlSqlProgram>();
	}
}
