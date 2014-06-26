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

		public ICollection<CommandExecutionHandler> CommandHandlers
		{
			get { return CommandHandlerCollection; }
		}

		public CommandExecutionHandler FindUsagesCommandHandler
		{
			get { return FindUsagesCommand.FindUsages; }
		}
	}

	public static class OracleCommands
	{
		public static readonly CommandExecutionHandler AddAlias = OracleCommandBase.CreateStandardExecutionHandler<AddAliasCommand>("AddAlias");
		public static readonly CommandExecutionHandler AddToGroupByClause = OracleCommandBase.CreateStandardExecutionHandler<AddToGroupByCommand>("AddToGroupByClause");
		public static readonly CommandExecutionHandler ExpandAsterisk = OracleCommandBase.CreateStandardExecutionHandler<ExpandAsteriskCommand>("ExpandAsterisk");
		public static readonly CommandExecutionHandler GenerateMissingColumns = OracleCommandBase.CreateStandardExecutionHandler<GenerateMissingColumnsCommand>("GenerateMissingColumns");
		public static readonly CommandExecutionHandler ToggleFullyQualifiedReferences = OracleCommandBase.CreateStandardExecutionHandler<ToggleFullyQualifiedReferencesCommand>("ToggleFullyQualifiedReferences");
		public static readonly CommandExecutionHandler ToggleQuotedNotation = OracleCommandBase.CreateStandardExecutionHandler<ToggleQuotedNotationCommand>("ToggleQuotedNotation");
		public static readonly CommandExecutionHandler UnnestInlineView = OracleCommandBase.CreateStandardExecutionHandler<UnnestInlineViewCommand>(UnnestInlineViewCommand.Title);
		public static readonly CommandExecutionHandler WrapAsCommonTableExpression = OracleCommandBase.CreateStandardExecutionHandler<WrapAsCommonTableExpressionCommand>("WrapAsCommonTableExpression");
		public static readonly CommandExecutionHandler WrapAsInlineView = OracleCommandBase.CreateStandardExecutionHandler<WrapAsInlineViewCommand>("WrapAsInlineView");
	}
}
