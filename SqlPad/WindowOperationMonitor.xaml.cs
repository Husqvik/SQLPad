using System.Threading;
using System.Windows;

namespace SqlPad
{
	public partial class WindowOperationMonitor
	{
		private readonly CancellationTokenSource _cancellationTokenSource;

		public WindowOperationMonitor(CancellationTokenSource cancellationTokenSource)
		{
			InitializeComponent();
			
			_cancellationTokenSource = cancellationTokenSource;
		}

		private void ButtonCancelOperationClickHandler(object sender, RoutedEventArgs e)
		{
			_cancellationTokenSource.Cancel();
			Close();
		}
	}
}
