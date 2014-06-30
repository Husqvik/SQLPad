using System;
using System.Windows.Controls;
using System.Windows.Threading;
using NUnit.Framework;

namespace SqlPad.Test
{
	[TestFixture]
	public class VisualComponentTest
	{
		[Test(Description = @""), STAThread, RequiresThread]
		public void TestMainWindowInitialization()
		{
			var mainWindow = new MainWindow();
			mainWindow.Show();
			var page = (DocumentPage)((TabItem)mainWindow.DocumentTabControl.SelectedItem).Content;
			page.IsParsingSynchronous = true;
			mainWindow.Show();
			page.Editor.Text = "SELECT * FROM DUAL";
			mainWindow.Close();
			Dispatcher.CurrentDispatcher.InvokeShutdown();
		}
	}
}
