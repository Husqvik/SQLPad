using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	public class OracleCommandFactory : ICommandFactory
	{
		#region Implementation of ICommandFactory
		public IToggleQuotedIdentifierCommand CreateToggleQuotedIdentifierCommand()
		{
			return new ToggleQuotedIdentifierCommand();
		}

		public IWrapAsCommonTableExpressionCommand CreateWrapAsCommonTableExpressionCommand()
		{
			return new WrapAsCommonTableExpressionCommandOld();
		}

		public IAddMissingAliasesCommand CreateAddMissingAliasesCommand()
		{
			return new AddMissingAliasesCommand();
		}
		#endregion
	}
}