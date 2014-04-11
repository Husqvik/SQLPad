using System;
using System.Windows.Input;

namespace SqlPad.Oracle.Commands
{
	public class AddAliasCommand : ICommand
	{
		public AddAliasCommand()
		{
			
		}

		public bool CanExecute(object parameter)
		{
			return true;
		}

		public void Execute(object parameter)
		{
			
		}

		public event EventHandler CanExecuteChanged = delegate { };
	}
}
