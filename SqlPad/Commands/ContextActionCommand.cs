using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace SqlPad.Commands
{
	internal class ContextActionCommand : ICommand
	{
		private readonly SqlTextEditor _textEditor;
		
		public IContextAction ContextAction { get; private set; }

		public ContextActionCommand(SqlTextEditor textEditor, IContextAction contextAction)
		{
			if (textEditor == null)
				throw new ArgumentNullException("textEditor");
			
			if (contextAction == null)
				throw new ArgumentNullException("contextAction");

			_textEditor = textEditor;
			ContextAction = contextAction;
		}

		public event EventHandler CanExecuteChanged = delegate { };

		public bool CanExecute(object parameter)
		{
			return ContextAction.ExecutionHandler.CanExecuteHandler(ContextAction.ExecutionContext);
		}

		public void Execute(object parameter)
		{
			if (ContextAction.IsLongOperation)
			{
				ExecuteLongOperation();
			}
			else
			{
				try
				{
					ContextAction.ExecutionHandler.ExecutionHandler(ContextAction.ExecutionContext);
					GenericCommandHandler.UpdateDocument(_textEditor, ContextAction.ExecutionContext);
				}
				catch (Exception exception)
				{
					ShowErrorMessage(exception);
				}
			}
		}

		private async void ExecuteLongOperation()
		{
			using (var cancellationTokenSource = new CancellationTokenSource())
			{
				var operationMonitor = new WindowOperationMonitor(cancellationTokenSource) { Owner = Application.Current.MainWindow };
				operationMonitor.Show();

				Exception exception = null;

				try
				{
					_textEditor.IsEnabled = false;
					await ContextAction.ExecutionHandler.ExecutionHandlerAsync(ContextAction.ExecutionContext, cancellationTokenSource.Token);
					operationMonitor.Close();
				}
				catch (Exception e)
				{
					exception = e;
				}
				finally
				{
					operationMonitor.Close();
					_textEditor.IsEnabled = true;
				}

				if (exception != null)
				{
					ShowErrorMessage(exception);
				}

				_textEditor.Focus();
			}

			GenericCommandHandler.UpdateDocument(_textEditor, ContextAction.ExecutionContext);
		}

		private static void ShowErrorMessage(Exception exception)
		{
			Messages.ShowError(Application.Current.MainWindow, "Action failed: " + Environment.NewLine + exception.Message);
		}
	}
}
