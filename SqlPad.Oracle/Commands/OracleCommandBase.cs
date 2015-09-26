using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;

namespace SqlPad.Oracle.Commands
{
	internal abstract class OracleCommandBase
	{
		protected static readonly ICollection<CommandExecutionHandler> EmptyHandlerCollection = new CommandExecutionHandler[0];

		protected readonly ActionExecutionContext ExecutionContext;

		protected StatementGrammarNode CurrentNode { get; }
		
		protected OracleStatementSemanticModel SemanticModel { get; }

		protected OracleQueryBlock CurrentQueryBlock { get; private set; }

		protected virtual Func<StatementGrammarNode, bool> CurrentNodeFilterFunction => null;

		protected OracleCommandBase(ActionExecutionContext executionContext)
		{
			if (executionContext == null)
				throw new ArgumentNullException(nameof(executionContext));

			ExecutionContext = executionContext;

			CurrentNode = executionContext.DocumentRepository.Statements.GetNodeAtPosition(executionContext.CaretOffset, CurrentNodeFilterFunction);

			if (CurrentNode == null)
				return;

			SemanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[CurrentNode.Statement].SemanticModel;
			CurrentQueryBlock = SemanticModel.GetQueryBlock(CurrentNode);
		}

		protected static void CheckParametersNotNull(OracleStatementSemanticModel semanticModel, StatementGrammarNode currentTerminal)
		{
			if (semanticModel == null)
				throw new InvalidOperationException("semanticModel");

			if (currentTerminal == null)
				throw new InvalidOperationException("currentTerminal");
		}

		protected virtual CommandCanExecuteResult CanExecute()
		{
			return true;
		}

		protected abstract void Execute();

		protected virtual Task ExecuteAsync(CancellationToken cancellationToken)
		{
			throw new NotSupportedException($"Task '{GetType().FullName}' does not support asynchronous execution. ");
		}

		public static CommandExecutionHandler CreateStandardExecutionHandler<TCommand>(string commandName) where TCommand : OracleCommandBase
		{
			return
				new CommandExecutionHandler
				{
					Name = commandName,
					CanExecuteHandler = context => CreateCommandInstance<TCommand>(context).CanExecute(),
					ExecutionHandler = CreateExecutionHandler<TCommand>(),
					ExecutionHandlerAsync = CreateAsynchronousExecutionHandler<TCommand>()
				};
		}

		private static Action<ActionExecutionContext> CreateExecutionHandler<TCommand>() where TCommand : OracleCommandBase
		{
			return
				context =>
				{
					var commandInstance = CreateCommandInstance<TCommand>(context);
					if (commandInstance.CanExecute())
					{
						commandInstance.Execute();
					}
				};
		}

		private static Func<ActionExecutionContext, CancellationToken, Task> CreateAsynchronousExecutionHandler<TCommand>() where TCommand : OracleCommandBase
		{
			return (context, cancellationToken) =>
			{
				var commandInstance = CreateCommandInstance<TCommand>(context);

				return commandInstance.CanExecute()
					? commandInstance.ExecuteAsync(cancellationToken)
					: Task.FromResult((object)null);
			};
		}

		private static TCommand CreateCommandInstance<TCommand>(ActionExecutionContext executionContext)
		{
			var constructorInfo = typeof(TCommand).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
			return (TCommand)constructorInfo.Invoke(new object[] { executionContext });
		}
	}
}
