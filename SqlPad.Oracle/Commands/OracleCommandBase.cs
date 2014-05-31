using System;

namespace SqlPad.Oracle.Commands
{
	internal abstract class OracleCommandBase
	{
		protected readonly OracleCommandExecutionContext ExecutionContext;

		protected OracleStatementSemanticModel SemanticModel { get { return ExecutionContext.SemanticModel; } }

		protected StatementDescriptionNode CurrentNode { get { return ExecutionContext.CurrentNode; } }

		protected OracleQueryBlock CurrentQueryBlock { get; private set; }

		protected OracleCommandBase(OracleCommandExecutionContext executionContext)
		{
			if (executionContext == null)
				throw new ArgumentNullException("executionContext");

			ExecutionContext = executionContext;

			if (SemanticModel != null && CurrentNode != null)
			{
				CurrentQueryBlock = SemanticModel.GetQueryBlock(CurrentNode);
			}
		}
	}
}