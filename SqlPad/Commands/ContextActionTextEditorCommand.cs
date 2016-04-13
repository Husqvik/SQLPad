using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace SqlPad.Commands
{
	internal class ContextActionCommand : ICommand
	{
		public ContextAction ContextAction { get; }

		public ContextActionCommand(ContextAction contextAction)
		{
			if (contextAction == null)
			{
				throw new ArgumentNullException(nameof(contextAction));
			}

			ContextAction = contextAction;
		}

		public event EventHandler CanExecuteChanged = delegate { };

		public bool CanExecute(object parameter)
		{
			return ContextAction.ExecutionHandler.CanExecuteHandler == null || ContextAction.ExecutionHandler.CanExecuteHandler(ContextAction.ExecutionContext);
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
					OnBeforeExecute();
					ContextAction.ExecutionHandler.ExecutionHandler(ContextAction.ExecutionContext);
					OnAfterExecute();
				}
				catch (Exception exception)
				{
					OnAfterFailure();
					ShowErrorMessage(exception);
				}
			}
		}

		protected virtual void OnBeforeExecute() { }

		protected virtual void OnAfterExecute() { }

		protected virtual void OnAfterFailure() { }

		private async void ExecuteLongOperation()
		{
			using (var cancellationTokenSource = new CancellationTokenSource())
			{
				var operationMonitor = new WindowOperationMonitor(cancellationTokenSource) { Owner = Application.Current.MainWindow };
				operationMonitor.Show();

				Exception exception = null;

				try
				{
					OnBeforeExecute();
					await ContextAction.ExecutionHandler.ExecutionHandlerAsync(ContextAction.ExecutionContext, cancellationTokenSource.Token);
				}
				catch (Exception e)
				{
					exception = e;
				}
				finally
				{
					OnAfterExecute();
					operationMonitor.Close();
				}

				if (exception != null)
				{
					ShowErrorMessage(exception);
				}
			}
		}

		private static void ShowErrorMessage(Exception exception)
		{
			Messages.ShowError("Action failed: " + Environment.NewLine + exception.Message);
		}
	}

	internal class ContextActionTextEditorCommand : ContextActionCommand
	{
		private readonly SqlTextEditor _textEditor;

		public ContextActionTextEditorCommand(SqlTextEditor textEditor, ContextAction contextAction)
			: base(contextAction)
		{
			if (textEditor == null)
			{
				throw new ArgumentNullException(nameof(textEditor));
			}

			_textEditor = textEditor;
		}

		protected override void OnBeforeExecute()
		{
			_textEditor.IsEnabled = false;
		}

		protected override void OnAfterExecute()
		{
			EnableAndFocusEditor();
			GenericCommandHandler.UpdateDocument(_textEditor, ContextAction.ExecutionContext);
		}

		protected override void OnAfterFailure()
		{
			EnableAndFocusEditor();
		}

		private void EnableAndFocusEditor()
		{
			_textEditor.IsEnabled = true;
			_textEditor.Focus();
		}
	}
}
