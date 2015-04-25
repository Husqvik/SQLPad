using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IContextActionProvider
	{
		ICollection<ContextAction> GetContextActions(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext);

		ICollection<ContextAction> GetAvailableRefactorings(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext);
	}

	[DebuggerDisplay("ContextAction (Name={Name})")]
	public class ContextAction
	{
		public ContextAction(string name, CommandExecutionHandler executionHandler, CommandExecutionContext executionContext, bool isLongOperation = false)
		{
			Name = name;
			ExecutionHandler = executionHandler;
			ExecutionContext = executionContext;
			IsLongOperation = isLongOperation;
		}

		public string Name { get; private set; }

		public bool IsLongOperation { get; private set; }

		public CommandExecutionHandler ExecutionHandler { get; private set; }

		public CommandExecutionContext ExecutionContext { get; private set; }
	}
}