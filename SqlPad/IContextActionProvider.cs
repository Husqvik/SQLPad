using System.Collections.Generic;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IContextActionProvider
	{
		ICollection<IContextAction> GetContextActions(SqlDocumentRepository sqlDocumentRepository, CommandExecutionContext executionContext);
	}

	public interface IContextAction
	{
		string Name { get; }
		
		bool IsLongOperation { get; }

		CommandExecutionHandler ExecutionHandler { get; }
		
		CommandExecutionContext ExecutionContext { get; }
	}
}