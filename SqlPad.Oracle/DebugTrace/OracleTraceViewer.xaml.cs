using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

namespace SqlPad.Oracle.DebugTrace
{
	public partial class OracleTraceViewer : ITraceViewer
	{
		public static readonly DependencyProperty IsTracingProperty = DependencyProperty.Register("IsTracing", typeof(bool), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty TraceFileNameProperty = DependencyProperty.Register("TraceFileName", typeof(string), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(String.Empty));

		[Bindable(true)]
		public bool IsTracing
		{
			get { return (bool)GetValue(IsTracingProperty); }
			private set { SetValue(IsTracingProperty, value); }
		}

		[Bindable(true)]
		public string TraceFileName
		{
			get { return (string)GetValue(TraceFileNameProperty); }
			private set { SetValue(TraceFileNameProperty, value); }
		}

		private readonly OracleConnectionAdapterBase _connectionAdapter;

		private readonly OracleTraceEventModel[] _traceEvents = OracleTraceEvent.AllTraceEvents.Select(e => new OracleTraceEventModel { TraceEvent = e }).ToArray();

		public Control Control { get { return this; } }

		public IReadOnlyCollection<OracleTraceEventModel> TraceEvents { get { return _traceEvents; } }

		public OracleTraceViewer(OracleConnectionAdapterBase connectionAdapter)
		{
			InitializeComponent();

			_connectionAdapter = connectionAdapter;
		}

		private void SelectItemCommandExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			bool? newValue = null;
			foreach (OracleTraceEventModel traceEvent in ((ListView)sender).SelectedItems)
			{
				if (!newValue.HasValue)
				{
					newValue = !traceEvent.IsEnabled;
				}

				traceEvent.IsEnabled = newValue.Value;
			}
		}

		private async void TraceBottonClickHandler(object sender, RoutedEventArgs args)
		{
			try
			{
				if (IsTracing)
				{
					await _connectionAdapter.StopTraceEvents(CancellationToken.None);
				}
				else
				{
					await _connectionAdapter.ActivateTraceEvents(_traceEvents.Where(e => e.IsEnabled).Select(e => e.TraceEvent), CancellationToken.None);
				}

				IsTracing = !IsTracing;
			}
			catch (Exception e)
			{
				Messages.ShowError(e.Message);
			}

			TraceFileName = _connectionAdapter.TraceFileName;
		}

		private void TraceFileNameHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			Process.Start("explorer.exe", "/select," + TraceFileName);
		}
	}

	public class OracleTraceEventModel
	{
		public bool IsEnabled { get; set; }
		public OracleTraceEvent TraceEvent { get; set; }
	}
}
