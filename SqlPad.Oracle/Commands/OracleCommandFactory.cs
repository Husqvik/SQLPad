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
		public static readonly CommandExecutionHandler AddAlias = OracleCommandBase.CreateStandardExecutionHandler<AddAliasCommand>("AddAlias");
		public static readonly CommandExecutionHandler AddCreateTableAs = OracleCommandBase.CreateStandardExecutionHandler<AddCreateTableAsCommand>("AddCreateTableAs");
		public static readonly CommandExecutionHandler AddInsertIntoColumnList = OracleCommandBase.CreateStandardExecutionHandler<AddInsertIntoColumnListCommand>("AddInsertIntoColumnList");
		public static readonly CommandExecutionHandler AddMissingColumn = OracleCommandBase.CreateStandardExecutionHandler<AddMissingColumnCommand>("AddMissingColumn");
		public static readonly CommandExecutionHandler AddToGroupByClause = OracleCommandBase.CreateStandardExecutionHandler<AddToGroupByCommand>("AddToGroupByClause");
		public static readonly CommandExecutionHandler CleanRedundantSymbol = OracleCommandBase.CreateStandardExecutionHandler<CleanRedundantSymbolCommand>("CleanRedundantSymbol");
		public static readonly CommandExecutionHandler ConvertOrderByNumberColumnReferences = OracleCommandBase.CreateStandardExecutionHandler<ConvertOrderByNumberColumnReferencesCommand>("ConvertOrderByNumberColumnReferences");
		public static readonly CommandExecutionHandler CreateScript = OracleCommandBase.CreateStandardExecutionHandler<CreateScriptCommand>("CreateScript");
		public static readonly CommandExecutionHandler ExpandAsterisk = OracleCommandBase.CreateStandardExecutionHandler<ExpandAsteriskCommand>("ExpandAsterisk");
		public static readonly CommandExecutionHandler GenerateCustomTypeCSharpWrapperClass = OracleCommandBase.CreateStandardExecutionHandler<GenerateCustomTypeCSharpWrapperClassCommand>("GenerateCustomTypeCSharpWrapperClass");
		public static readonly CommandExecutionHandler PropagateColumn = OracleCommandBase.CreateStandardExecutionHandler<PropagateColumnCommand>("PropagateColumn");
		public static readonly CommandExecutionHandler SplitString = OracleCommandBase.CreateStandardExecutionHandler<SplitStringCommand>("SplitString");
		public static readonly CommandExecutionHandler ToggleFullyQualifiedReferences = OracleCommandBase.CreateStandardExecutionHandler<ToggleFullyQualifiedReferencesCommand>("ToggleFullyQualifiedReferences");
		public static readonly CommandExecutionHandler ToggleQuotedNotation = OracleCommandBase.CreateStandardExecutionHandler<ToggleQuotedNotationCommand>("ToggleQuotedNotation");
		public static readonly CommandExecutionHandler UnnestInlineView = OracleCommandBase.CreateStandardExecutionHandler<UnnestInlineViewCommand>(UnnestInlineViewCommand.Title);
		public static readonly CommandExecutionHandler Unquote = OracleCommandBase.CreateStandardExecutionHandler<UnquoteCommand>(UnquoteCommand.Title);
		public static readonly CommandExecutionHandler WrapAsCommonTableExpression = OracleCommandBase.CreateStandardExecutionHandler<WrapAsCommonTableExpressionCommand>("WrapAsCommonTableExpression");
		public static readonly CommandExecutionHandler WrapAsInlineView = OracleCommandBase.CreateStandardExecutionHandler<WrapAsInlineViewCommand>("WrapAsInlineView");
	}
}
