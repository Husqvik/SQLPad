using System;
using System.Reflection;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	internal abstract class OracleCommandBase
	{
		protected readonly CommandExecutionContext ExecutionContext;

		protected StatementDescriptionNode CurrentNode { get; private set; }
		
		protected OracleStatementSemanticModel SemanticModel { get; private set; }

		protected OracleQueryBlock CurrentQueryBlock { get; private set; }

		protected OracleCommandBase(CommandExecutionContext executionContext)
		{
			if (executionContext == null)
				throw new ArgumentNullException("executionContext");

			ExecutionContext = executionContext;

			CurrentNode = executionContext.DocumentRepository.Statements.GetNodeAtPosition(executionContext.CaretOffset);

			if (CurrentNode == null)
				return;

			SemanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[CurrentNode.Statement].SemanticModel;
			CurrentQueryBlock = SemanticModel.GetQueryBlock(CurrentNode);
		}

		protected virtual bool CanExecute()
		{
			return true;
		}

		protected abstract void Execute();

		public static CommandExecutionHandler CreateStandardExecutionHandler<TCommand>(string commandName) where TCommand : OracleCommandBase
		{
			return new CommandExecutionHandler
			{
				Name = commandName,
				ExecutionHandler = CreateExecutionHandler<TCommand>(),
				CanExecuteHandler = context => CreateCommandInstance<TCommand>(context).CanExecute()
			};			
		}

		private static Action<CommandExecutionContext> CreateExecutionHandler<TCommand>() where TCommand : OracleCommandBase
		{
			return context =>
			       {
					   var commandInstance = CreateCommandInstance<TCommand>(context);
				       if (commandInstance.CanExecute())
				       {
					       commandInstance.Execute();
				       }
			       };
		}

		private static TCommand CreateCommandInstance<TCommand>(CommandExecutionContext executionContext)
		{
			var constructorInfo = typeof(TCommand).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
			return (TCommand)constructorInfo.Invoke(new object[] { executionContext });
		}
	}
}