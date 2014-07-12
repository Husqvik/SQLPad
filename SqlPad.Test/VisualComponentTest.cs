using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using NUnit.Framework;
using SqlPad.Commands;

namespace SqlPad.Test
{
	[TestFixture]
	public class VisualComponentTest
	{
		private MainWindow _mainWindow;
		private DocumentPage _page;

		private void InitializeApplicationWindow()
		{
			_mainWindow = new MainWindow();
			_mainWindow.Show();
			_page = (DocumentPage)((TabItem)_mainWindow.DocumentTabControl.SelectedItem).Content;
			_page.IsParsingSynchronous = true;
			_mainWindow.Show();
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

			GenericCommands.FindUsagesCommand.Execute(null, _page.Editor.TextArea);
			GenericCommands.ExecuteDatabaseCommandCommand.Execute(null, _page.Editor.TextArea);

			Wait(0.2);

			_mainWindow.Close();
			Dispatcher.CurrentDispatcher.InvokeShutdown();
		}
	}
}
