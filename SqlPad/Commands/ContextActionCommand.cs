using System;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad.Commands
{
	internal class ContextActionCommand : ICommand
	{
		public IContextAction ContextAction { get; private set; }


		public ContextActionCommand(IContextAction contextAction)
		{
			if (contextAction == null)
				throw new ArgumentNullException("contextAction");
			
			ContextAction = contextAction;
		}

		public bool CanExecute(object parameter)
		{
			return ContextAction.ExecutionHandler.CanExecuteHandler(ContextAction.ExecutionContext);
		}

		public void Execute(object parameter)
		{
			try
			{
				ContextAction.ExecutionHandler.ExecutionHandler(ContextAction.ExecutionContext);
				GenericCommandHandler.UpdateDocument((TextEditor)parameter, ContextAction.ExecutionContext);
			}
			catch (Exception exception)
			{
				Messages.ShowError("Action failed: " + Environment.NewLine + exception.Message);
			}
		}

		public event EventHandler CanExecuteChanged = delegate {};
	}
}