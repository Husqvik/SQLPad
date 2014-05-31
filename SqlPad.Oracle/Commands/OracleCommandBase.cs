using System;
using System.Reflection;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	internal abstract class OracleCommandBase
	{
		protected readonly OracleCommandExecutionContext ExecutionContext;

		protected OracleStatementSemanticModel SemanticModel { get { return ExecutionContext.SemanticModel; } }

		protected StatementDescriptionNode CurrentNode { get { return ExecutionContext.CurrentNode; } }

		protected OracleQueryBlock CurrentQueryBlock { get; private set; }

		protected OracleCommandBase(OracleCommandExecutionContext executionContext)
		{
			if (executionContext == null)
				throw new ArgumentNullException("executionContext");

			ExecutionContext = executionContext;

			if (SemanticModel != null && CurrentNode != null)
			{
				CurrentQueryBlock = SemanticModel.GetQueryBlock(CurrentNode);
			}
		}

		protected virtual bool CanExecute()
		{
			return true;
		}

		protected abstract void Execute();

		protected static CommandExecutionHandler CreateStandardExecutionHandler<TCommand>(string commandName) where TCommand : OracleCommandBase
		{
			return new CommandExecutionHandler
			{
				Name = commandName,
				ExecutionHandler = CreatExecutionHandler<TCommand>(),
				CanExecuteHandler = context => CreateCommandInstance<TCommand>((OracleCommandExecutionContext)context).CanExecute()
			};			
		}

		private static Action<CommandExecutionContext> CreatExecutionHandler<TCommand>() where TCommand : OracleCommandBase
		{
			return context =>
			       {
				       var executionContext = context as OracleCommandExecutionContext;
				       if (executionContext == null)
				       {
						   throw new ArgumentException(String.Format("Execution context of type '{0}' is incompatible with type '{1}' ", context.GetType().FullName, typeof(OracleCommandExecutionContext).FullName), "context");
				       }

					   var commandInstance = CreateCommandInstance<TCommand>(executionContext);
				       if (commandInstance.CanExecute())
				       {
					       commandInstance.Execute();
				       }
			       };
		}

		private static TCommand CreateCommandInstance<TCommand>(OracleCommandExecutionContext executionContext)
		{
			var constructorInfo = typeof(TCommand).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
			return (TCommand)constructorInfo.Invoke(new object[] { executionContext });
		}
	}
}