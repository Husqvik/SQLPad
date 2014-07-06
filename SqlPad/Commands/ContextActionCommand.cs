using System;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;

namespace SqlPad.Commands
{
	internal class ContextActionCommand : ICommand
	{
		public CommandExecutionHandler ExecutionHandler { get; private set; }

		public CommandExecutionContext ExecutionContext { get; private set; }

		public ContextActionCommand(CommandExecutionHandler executionHandler, CommandExecutionContext executionContext)
		{
			if (executionHandler == null)
				throw new ArgumentNullException("executionHandler");
			
			if (executionContext == null)
				throw new ArgumentNullException("executionContext");

			ExecutionHandler = executionHandler;
			ExecutionContext = executionContext;
		}

		public bool CanExecute(object parameter)
		{
			return ExecutionHandler.CanExecuteHandler(ExecutionContext);
		}

		public void Execute(object parameter)
		{
			try
			{
				ExecutionHandler.ExecutionHandler(ExecutionContext);
			}
			catch (Exception exception)
			{
				MessageBox.Show("Action failed: "+ Environment.NewLine + exception.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			}

			GenericCommandHandler.UpdateDocument((TextEditor)parameter, ExecutionContext);
		}

		public event EventHandler CanExecuteChanged = delegate {};
	}
}