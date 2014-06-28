using System.Collections.Generic;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IContextActionProvider
	{
		ICollection<IContextAction> GetContextActions(SqlDocument sqlDocument, CommandExecutionContext executionContext);
	}

	public interface IContextAction
	{
		string Name { get; }

		//ICommand Command { get; }
		CommandExecutionHandler ExecutionHandler { get; }
		
		CommandExecutionContext ExecutionContext { get; }
	}
}