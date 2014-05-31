using System.Collections.Generic;
using SqlPad.Commands;

namespace SqlPad
{
	public interface IContextActionProvider
	{
		ICollection<IContextAction> GetContextActions(IDatabaseModel databaseModel, SqlDocument sqlDocument, int cursorPosition, int selectionLength);
	}

	public interface IContextAction
	{
		string Name { get; }

		//ICommand Command { get; }
		CommandExecutionHandler ExecutionHandler { get; }
		
		CommandExecutionContext ExecutionContext { get; }
	}
}