using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
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

		internal static ConcurrentBag<string> Messages = new ConcurrentBag<string>();

		public static WindowTraceLog Instance { get; private set; }

		public static void Initialize()
		{
			if (Instance != null)
			{
				throw new InvalidOperationException("Trace log window has been already initialized. ");
			}

			var contents = String.Join(Environment.NewLine, Messages.OrderBy(m => m));
			Instance = new WindowTraceLog { TraceLog = contents };
			Messages = new ConcurrentBag<string>();
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
				Messages.Add(message);
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
