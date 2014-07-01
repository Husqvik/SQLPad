using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using NUnit.Framework;

namespace SqlPad.Test
{
	[TestFixture]
	public class VisualComponentTest
	{
		private MainWindow _mainWindow;
		private DocumentPage _page;
		private ICollection<CommandBinding> _commandBindings;

		private void InitializeApplicationWindow()
		{
			_mainWindow = new MainWindow();
			_mainWindow.Show();
			_page = (DocumentPage)((TabItem)_mainWindow.DocumentTabControl.SelectedItem).Content;
			_page.IsParsingSynchronous = true;
			_mainWindow.Show();
			_commandBindings = _page.Editor.TextArea.DefaultInputHandler.Editing.CommandBindings;
		}

		private RoutedCommand GetCommand(string commandName)
		{
			return _commandBindings.Select(b => (RoutedCommand)b.Command).Single(c => c.Name == commandName);
		}

		private static void Wait(double seconds)
		{
			var frame = new DispatcherFrame();
			Task.Factory.StartNew(
				() =>
				{
					Thread.Sleep(TimeSpan.FromSeconds(seconds));
					frame.Continue = false;
				});
			
			Dispatcher.PushFrame(frame);
		}

		[Test(Description = @""), STAThread, RequiresThread]
		public void TestBasicApplicationBehavior()
		{
			InitializeApplicationWindow();

			_page.Editor.Document.Insert(0, "SELECT UNDEFINED, DUMMY AMBIGUOUS, SQLPAD_FUNCTION('Invalid parameter 1', 'Invalid parameter 2') FROM DUAL, DUAL D;\nSELECT *");
			_page.Editor.CaretOffset = 50;

			Wait(0.1);

			_page.Editor.CaretOffset = 108;

			var findUsagesCommandName = ConfigurationProvider.InfrastructureFactory.CommandFactory.FindUsagesCommandHandler.Name;
			GetCommand(findUsagesCommandName).Execute(null, _page.Editor.TextArea);
			GetCommand(DocumentPage.ExecuteDatabaseCommandName).Execute(null, _page.Editor.TextArea);

			Wait(0.2);

			_mainWindow.Close();
			Dispatcher.CurrentDispatcher.InvokeShutdown();
		}
	}
}
