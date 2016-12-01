using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace SqlPad
{
	public partial class WindowTraceLog
	{
		public static readonly DependencyProperty TraceLogProperty = DependencyProperty.Register(nameof(TraceLog), typeof(string), typeof(WindowTraceLog), new FrameworkPropertyMetadata(String.Empty));

		[Bindable(true)]
		public string TraceLog
		{
			get { return (string)GetValue(TraceLogProperty); }
			private set { SetValue(TraceLogProperty, value); }
		}

		private static ConcurrentBag<string> _messages = new ConcurrentBag<string>();

		public static WindowTraceLog Instance { get; private set; }

		public static void Initialize()
		{
			if (Instance != null)
			{
				throw new InvalidOperationException("Trace log window has been already initialized. ");
			}

			var contentBuilder = new StringBuilder();
			foreach (var message in _messages.OrderBy(m => m))
			{
				contentBuilder.AppendLine(message);
			}

			Instance = new WindowTraceLog { TraceLog = contentBuilder.ToString() };
			_messages = new ConcurrentBag<string>();
			WorkDocumentCollection.RestoreWindowProperties(Instance);
		}

		public WindowTraceLog()
		{
			InitializeComponent();
		}

		private void WriteLineInternal(string message)
		{
			Application.Current.Dispatcher.BeginInvoke(
				DispatcherPriority.Background,
				new Action(() => TraceLog += $"{message}{Environment.NewLine}"));
		}

		public static void WriteLine(string message)
		{
			if (Instance == null)
			{
				_messages.Add(message);
			}
			else
			{
				Instance.WriteLineInternal(message);
			}
		}

		private void WindowTraceLogClosingHandler(object sender, CancelEventArgs args)
		{
			WorkDocumentCollection.StoreWindowProperties(this);

			args.Cancel = true;
			Hide();
		}
	}
}
