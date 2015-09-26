using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace SqlPad
{
	public partial class WindowOperationMonitor : IProgress<int>
	{
		public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(WindowOperationMonitor), new FrameworkPropertyMetadata(true));

		[Bindable(true)]
		public bool IsIndeterminate
		{
			get { return (bool)GetValue(IsIndeterminateProperty); }
			set { SetValue(IsIndeterminateProperty, value); }
		}

		private readonly CancellationTokenSource _cancellationTokenSource;

		private int _progress;

		public WindowOperationMonitor(CancellationTokenSource cancellationTokenSource)
		{
			InitializeComponent();

			Owner = App.MainWindow;
			ProgressBar.Value = 0;

			_cancellationTokenSource = cancellationTokenSource;
		}

		private void ButtonCancelOperationClickHandler(object sender, RoutedEventArgs e)
		{
			_cancellationTokenSource.Cancel();
		}

		public void Report(int value)
		{
			if (_progress == value)
			{
				return;
			}

			_progress = value;
			Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ProgressBar.Value = value));
		}
	}
}
