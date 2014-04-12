using System;
using System.Windows.Input;

namespace SqlPad.Oracle.Commands
{
	public class AddAliasCommand : ICommand
	{
		private readonly OracleStatementSemanticModel _semanticModel;

		public AddAliasCommand(OracleStatementSemanticModel semanticModel)
		{
			if (semanticModel == null)
				throw new ArgumentNullException("semanticModel");
			
			_semanticModel = semanticModel;
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
