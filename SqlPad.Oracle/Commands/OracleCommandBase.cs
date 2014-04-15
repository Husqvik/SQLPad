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

		public override event EventHandler CanExecuteChanged = delegate { };
	}

	public abstract class OracleConfigurableCommandBase : OracleCommandBase
	{
		protected readonly ICommandSettingsProvider SettingsProvider;

		protected OracleConfigurableCommandBase(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal, ICommandSettingsProvider settingsProvider = null)
			: base(semanticModel, currentTerminal)
		{
			if (settingsProvider != null)
			{
				SettingsProvider = settingsProvider;
			}
			else
			{
				var model = new CommandSettingsModel { Value = "Enter value", ValidationRule = new OracleIdentifierValidationRule() };
				SettingsProvider = new EditDialog(model);
			}
		}
	}
}