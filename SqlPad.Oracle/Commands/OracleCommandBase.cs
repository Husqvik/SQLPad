using System;

namespace SqlPad.Oracle.Commands
{
	public abstract class OracleCommandBase : EditCommandBase
	{
		protected OracleStatementSemanticModel SemanticModel { get; private set; }

		protected StatementDescriptionNode CurrentNode { get; private set; }
		
		protected OracleQueryBlock CurrentQueryBlock { get; private set; }

		protected OracleCommandBase(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode)
		{
			//CheckParameters(semanticModel, CurrentNode);

			SemanticModel = semanticModel;
			CurrentNode = currentNode;

			if (SemanticModel != null && CurrentNode != null)
			{
				CurrentQueryBlock = SemanticModel.GetQueryBlock(CurrentNode);
			}
		}

		protected static void CheckParameters(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode)
		{
			if (semanticModel == null)
				throw new InvalidOperationException("semanticModel");

			if (currentNode == null)
				throw new InvalidOperationException("currentNode");
		}
	}

	public abstract class OracleConfigurableCommandBase : OracleCommandBase
	{
		protected readonly ICommandSettingsProvider SettingsProvider;
		protected readonly CommandSettingsModel SettingsModel;

		protected OracleConfigurableCommandBase(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode, ICommandSettingsProvider settingsProvider = null)
			: base(semanticModel, currentNode)
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

			SettingsModel = SettingsProvider.Settings;
		}
	}
}