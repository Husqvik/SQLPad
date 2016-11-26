using System.Collections.Generic;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	public class OracleCommandFactory : ICommandFactory
	{
		private static readonly List<CommandExecutionHandler> CommandHandlerCollection = new List<CommandExecutionHandler>();

		static OracleCommandFactory()
		{
			CommandHandlerCollection.Add(ModifyCaseCommand.MakeUpperCase);
			CommandHandlerCollection.Add(ModifyCaseCommand.MakeLowerCase);
			CommandHandlerCollection.Add(SafeDeleteCommand.SafeDelete);
			CommandHandlerCollection.Add(MoveContentCommand.MoveContentUp);
			CommandHandlerCollection.Add(MoveContentCommand.MoveContentDown);
		}

		public ICollection<CommandExecutionHandler> CommandHandlers => CommandHandlerCollection;
	}

	public static class OracleCommands
	{
		public static readonly CommandExecutionHandler AddAlias = OracleCommandBase.CreateStandardExecutionHandler<AddAliasCommand>(nameof(AddAlias));
		public static readonly CommandExecutionHandler AddCreateTableAs = OracleCommandBase.CreateStandardExecutionHandler<AddCreateTableAsCommand>(nameof(AddCreateTableAs));
		public static readonly CommandExecutionHandler AddInsertIntoColumnList = OracleCommandBase.CreateStandardExecutionHandler<AddInsertIntoColumnListCommand>(nameof(AddInsertIntoColumnList));
		public static readonly CommandExecutionHandler AddMissingColumn = OracleCommandBase.CreateStandardExecutionHandler<AddMissingColumnCommand>(nameof(AddMissingColumn));
		public static readonly CommandExecutionHandler AddToGroupByClause = OracleCommandBase.CreateStandardExecutionHandler<AddToGroupByCommand>(nameof(AddToGroupByClause));
		public static readonly CommandExecutionHandler AddToOrderByClause = OracleCommandBase.CreateStandardExecutionHandler<AddToOrderByCommand>(nameof(AddToOrderByClause));
		public static readonly CommandExecutionHandler CleanRedundantSymbol = OracleCommandBase.CreateStandardExecutionHandler<CleanRedundantSymbolCommand>(nameof(CleanRedundantSymbol));
		public static readonly CommandExecutionHandler ConfigureNamedParameters = OracleCommandBase.CreateStandardExecutionHandler<ConfigureNamedParameterCommand>(nameof(ConfigureNamedParameters));
		public static readonly CommandExecutionHandler ConvertOrderByNumberColumnReferences = OracleCommandBase.CreateStandardExecutionHandler<ConvertOrderByNumberColumnReferencesCommand>(nameof(ConvertOrderByNumberColumnReferences));
		public static readonly CommandExecutionHandler CreateScript = OracleCommandBase.CreateStandardExecutionHandler<CreateScriptCommand>(nameof(CreateScript));
		public static readonly CommandExecutionHandler ExpandAsterisk = OracleCommandBase.CreateStandardExecutionHandler<ExpandAsteriskCommand>(nameof(ExpandAsterisk));
		public static readonly CommandExecutionHandler ExpandView = OracleCommandBase.CreateStandardExecutionHandler<ExpandViewCommand>(nameof(ExpandView));
		public static readonly CommandExecutionHandler GenerateCustomTypeCSharpWrapperClass = OracleCommandBase.CreateStandardExecutionHandler<GenerateCustomTypeCSharpWrapperClassCommand>(nameof(GenerateCustomTypeCSharpWrapperClass));
		public static readonly CommandExecutionHandler ExtractPackageInterface = OracleCommandBase.CreateStandardExecutionHandler<ExtractPackageInterfaceCommand>(nameof(ExtractPackageInterface));
		public static readonly CommandExecutionHandler PropagateColumn = OracleCommandBase.CreateStandardExecutionHandler<PropagateColumnCommand>(nameof(PropagateColumn));
		public static readonly CommandExecutionHandler SplitString = OracleCommandBase.CreateStandardExecutionHandler<SplitStringCommand>(nameof(SplitString));
		public static readonly CommandExecutionHandler ToggleFullyQualifiedReferences = OracleCommandBase.CreateStandardExecutionHandler<ToggleFullyQualifiedReferencesCommand>(nameof(ToggleFullyQualifiedReferences));
		public static readonly CommandExecutionHandler ToggleQuotedNotation = OracleCommandBase.CreateStandardExecutionHandler<ToggleQuotedNotationCommand>(nameof(ToggleQuotedNotation));
		public static readonly CommandExecutionHandler UnnestInlineView = OracleCommandBase.CreateStandardExecutionHandler<UnnestInlineViewCommand>(UnnestInlineViewCommand.Title);
		public static readonly CommandExecutionHandler Unquote = OracleCommandBase.CreateStandardExecutionHandler<UnquoteCommand>(UnquoteCommand.Title);
		public static readonly CommandExecutionHandler WrapAsCommonTableExpression = OracleCommandBase.CreateStandardExecutionHandler<WrapAsCommonTableExpressionCommand>(nameof(WrapAsCommonTableExpression));
		public static readonly CommandExecutionHandler WrapAsInlineView = OracleCommandBase.CreateStandardExecutionHandler<WrapAsInlineViewCommand>(nameof(WrapAsInlineView));
	}
}
