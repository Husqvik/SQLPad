using System;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	internal class OracleCommandExecutionContext : CommandExecutionContext
	{
		private OracleCommandExecutionContext(string statementText, int line, int column, int caretOffset, StatementCollection statements, IDatabaseModel databaseModel)
			: base(statementText, line, column, caretOffset, statements, databaseModel)
		{
		}

		public OracleStatementSemanticModel SemanticModel { get; private set; }
		
		public StatementDescriptionNode CurrentNode { get; private set; }

		internal static OracleCommandExecutionContext Create(CommandExecutionContext executionContext, OracleStatementSemanticModel semanticModel)
		{
			return Create(executionContext.StatementText, executionContext.Line, executionContext.Column, executionContext.CaretOffset, semanticModel);
		}

		public static OracleCommandExecutionContext Create(string statementText, int line, int column, int currentPosition, OracleStatementSemanticModel semanticModel)
		{
			if (semanticModel.IsSimpleModel)
			{
				throw new ArgumentException("Full semantic model is required. ");
			}

			return new OracleCommandExecutionContext(statementText, line, column, currentPosition, new StatementCollection(new []{ semanticModel.Statement }), semanticModel.DatabaseModel)
			       {
				       SemanticModel = semanticModel,
				       CurrentNode = semanticModel.Statement.GetNodeAtPosition(currentPosition)
			       };
		}

		public OracleCommandExecutionContext Clone()
		{
			return Create(StatementText, Line, Column, CaretOffset, SemanticModel);
		}
	}
}
