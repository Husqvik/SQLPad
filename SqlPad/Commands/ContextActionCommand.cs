using System;
using System.Threading;
using System.Windows;
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

		public event EventHandler CanExecuteChanged = delegate { };

		public bool CanExecute(object parameter)
		{
			return ContextAction.ExecutionHandler.CanExecuteHandler(ContextAction.ExecutionContext);
		}

		public void Execute(object parameter)
		{
			var textEditor = (TextEditor)parameter;

			if (ContextAction.IsLongOperation)
			{
				ExecuteLongOperation(textEditor);
			}
			else
			{
				try
				{
					ContextAction.ExecutionHandler.ExecutionHandler(ContextAction.ExecutionContext);
					GenericCommandHandler.UpdateDocument(textEditor, ContextAction.ExecutionContext);
				}
				catch (Exception exception)
				{
					ShowErrorMessage(exception);
				}
			}
		}

		private async void ExecuteLongOperation(TextEditor textEditor)
		{
			using (var cancellationTokenSource = new CancellationTokenSource())
			{
				var operationMonitor = new WindowOperationMonitor(cancellationTokenSource) { Owner = Application.Current.MainWindow };
				operationMonitor.Show();

				try
				{
					await ContextAction.ExecutionHandler.ExecutionHandlerAsync(ContextAction.ExecutionContext, cancellationTokenSource.Token);
				}
				catch (Exception exception)
				{
					ShowErrorMessage(exception);
				}

				operationMonitor.Close();
			}

			GenericCommandHandler.UpdateDocument(textEditor, ContextAction.ExecutionContext);
		}

		private static void ShowErrorMessage(Exception exception)
		{
			Messages.ShowError("Action failed: " + Environment.NewLine + exception.Message);
		}
	}
}
