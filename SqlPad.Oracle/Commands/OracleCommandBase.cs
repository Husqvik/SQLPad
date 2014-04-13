using System;

namespace SqlPad.Oracle.Commands
{
	public abstract class OracleCommandBase : CommandBase
	{
		protected OracleStatementSemanticModel SemanticModel { get; private set; }

		protected StatementDescriptionNode CurrentTerminal { get; private set; }

		protected OracleCommandBase(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
		{
			CheckParameters(semanticModel, currentTerminal);

			SemanticModel = semanticModel;
			CurrentTerminal = currentTerminal;
		}

		protected static void CheckParameters(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
		{
			if (semanticModel == null)
				throw new ArgumentNullException("semanticModel");

			if (currentTerminal == null)
				throw new ArgumentNullException("currentTerminal");
		}
	}
}