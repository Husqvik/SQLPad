using System.Collections.Generic;
using System.Windows.Input;

namespace SqlPad
{
	public interface IContextActionProvider
	{
		ICollection<IContextAction> GetContextActions(IDatabaseModel databaseModel, SqlDocument sqlDocument, int cursorPosition, int selectionLength);
	}

	public interface IContextAction
	{
		string Name { get; }

		ICommand Command { get; }
	}
}