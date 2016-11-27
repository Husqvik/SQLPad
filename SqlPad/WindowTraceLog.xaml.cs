using System;
using System.ComponentModel;
using System.Windows;

namespace SqlPad
{
	public partial class WindowTraceLog
	{
		public static readonly DependencyProperty TraceLogProperty = DependencyProperty.Register(nameof(TraceLog), typeof(string), typeof(DocumentPage), new FrameworkPropertyMetadata(String.Empty));

		[Bindable(true)]
		public string TraceLog
		{
			get { return (string)GetValue(TraceLogProperty); }
			private set { SetValue(TraceLogProperty, value); }
		}

		public static readonly WindowTraceLog Instance;

		static WindowTraceLog()
		{
			Instance = new WindowTraceLog();
		}

		public WindowTraceLog()
		{
			InitializeComponent();
		}

		public void Log(string message)
		{
			TraceLog += $"{message}{Environment.NewLine}";
		}
	}
}
