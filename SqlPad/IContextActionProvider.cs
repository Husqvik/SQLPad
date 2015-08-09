using System.Collections.Generic;
using System.Diagnostics;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IContextActionProvider
	{
		ICollection<ContextAction> GetContextActions(SqlDocumentRepository sqlDocumentRepository, ActionExecutionContext executionContext);

		ICollection<ContextAction> GetAvailableRefactorings(SqlDocumentRepository sqlDocumentRepository, ActionExecutionContext executionContext);
	}

	[DebuggerDisplay("ContextAction (Name={Name})")]
	public class ContextAction
	{
		public ContextAction(string name, CommandExecutionHandler executionHandler, ActionExecutionContext executionContext, bool isLongOperation = false)
		{
			Name = name;
			ExecutionHandler = executionHandler;
			ExecutionContext = executionContext;
			IsLongOperation = isLongOperation;
		}

		public string Name { get; }

		public bool IsLongOperation { get; private set; }

		public CommandExecutionHandler ExecutionHandler { get; private set; }

		public ActionExecutionContext ExecutionContext { get; private set; }
	}
}