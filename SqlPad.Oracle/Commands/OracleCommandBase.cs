using System;
using System.Windows.Input;

namespace SqlPad.Oracle.Commands
{
	public abstract class OracleCommandBase : ICommand
	{
		protected OracleStatementSemanticModel SemanticModel { get; private set; }

		protected StatementDescriptionNode CurrentTerminal { get; private set; }

		protected OracleCommandBase(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
		{
			CheckParameters(semanticModel, currentTerminal);

			SemanticModel = semanticModel;
			CurrentTerminal = currentTerminal;
		}

		public abstract bool CanExecute(object parameter);
		public abstract void Execute(object parameter);

		public abstract event EventHandler CanExecuteChanged;

		protected static void CheckParameters(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
		{
			if (semanticModel == null)
				throw new ArgumentNullException("semanticModel");

			if (currentTerminal == null)
				throw new ArgumentNullException("currentTerminal");
		}
	}
}