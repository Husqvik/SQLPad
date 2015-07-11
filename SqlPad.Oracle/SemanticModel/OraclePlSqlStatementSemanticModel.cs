using System.Collections.Generic;
using System.Linq;
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

		public IDatabaseModel DatabaseModel { get { return _databaseModel; } }
		
		public StatementBase Statement { get; private set; }
		
		public string StatementText { get; private set; }
		
		public bool HasDatabaseModel { get { return DatabaseModel == null; } }
		
		public ICollection<RedundantTerminalGroup> RedundantSymbolGroups { get { return new RedundantTerminalGroup[0]; } }

		public OraclePlSqlStatementSemanticModel(OracleDatabaseModelBase databaseModel, OracleStatement statement, string statementText)
		{
			_databaseModel = databaseModel;
			Statement = statement;
			StatementText = statementText;

			Build();
		}

		public void Build()
		{
			ResolveProgramDeclarations();
			ResolveProgramBodies();
		}

		private void ResolveProgramBodies()
		{
			
		}

		private void ResolveProgramDeclarations()
		{
			var declarationSections = Statement.RootNode.GetDescendants(NonTerminals.ProgramDeclareSection);
			var itemDeclarations = declarationSections.SelectMany(s => s.GetDescendants(NonTerminals.ItemDeclaration));
		}
	}

	internal class OraclePlSqlProgram
	{
		public StatementGrammarNode RootNode { get; set; }
	}
}
