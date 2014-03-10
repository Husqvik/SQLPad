namespace SqlPad.Commands
{
	public interface ICommandFactory
	{
		IAddMissingAliasesCommand CreateAddMissingAliasesCommand();
		IWrapAsCommonTableExpressionCommand CreateWrapAsCommonTableExpressionCommand();
		IToggleQuotedIdentifierCommand CreateToggleQuotedIdentifierCommand();
	}
}
