using System.Collections.Generic;

namespace SqlPad
{
	public interface IContextActionProvider
	{
		ICollection<IContextAction> GetContextActions(string statementText, int cursorPosition);
	}

	public interface IContextAction
	{
		string Name { get; }
		
		void Execute();
	}
}