using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle.DebugTrace
{
	public partial class OracleTraceViewer : ITraceViewer
	{
		public static readonly DependencyProperty IsTracingProperty = DependencyProperty.Register(nameof(IsTracing), typeof(bool), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty TraceFileNameProperty = DependencyProperty.Register(nameof(TraceFileName), typeof(string), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty TraceIdentifierProperty = DependencyProperty.Register(nameof(TraceIdentifier), typeof(string), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(String.Empty));

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

		[Bindable(true)]
		public string TraceIdentifier
		{
			get { return (string)GetValue(TraceIdentifierProperty); }
			set { SetValue(TraceIdentifierProperty, value); }
		}

		private readonly OracleConnectionAdapterBase _connectionAdapter;

		private readonly ObservableCollection<OracleTraceEventModel> _traceEvents = new ObservableCollection<OracleTraceEventModel>();

		public Control Control => this;

	    public IReadOnlyCollection<OracleTraceEventModel> TraceEvents => _traceEvents;

	    public OracleTraceViewer(OracleConnectionAdapterBase connectionAdapter)
		{
			InitializeComponent();

			_connectionAdapter = connectionAdapter;

			var eventSource = OracleTraceEvent.AllTraceEvents;
			var hasDbaPrivilege = ((OracleDatabaseModelBase)connectionAdapter.DatabaseModel).HasDbaPrivilege;

			_traceEvents.AddRange(eventSource.Select(e => new OracleTraceEventModel { TraceEvent = e, IsEnabled = hasDbaPrivilege || !e.RequiresDbaPrivilege }));
		}

		private void ComponentLoadedHandler(object sender, RoutedEventArgs e)
		{
			UpdateTraceFileName();
		}

		private void SelectItemCommandExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			bool? newValue = null;
			foreach (OracleTraceEventModel traceEvent in ((ListView)sender).SelectedItems)
			{
				if (!newValue.HasValue)
				{
					newValue = !traceEvent.IsSelected;
				}

				traceEvent.IsSelected = newValue.Value;
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
					var selectedEvents = _traceEvents.Where(e => e.IsSelected).Select(e => e.TraceEvent).ToArray();
					if (selectedEvents.Length == 0)
					{
						Messages.ShowError("Select an event first. ");
						return;
					}
					
					await _connectionAdapter.ActivateTraceEvents(selectedEvents, TraceIdentifier, CancellationToken.None);
				}

				IsTracing = !IsTracing;
			}
			catch (Exception e)
			{
				Messages.ShowError(e.Message);
			}

			UpdateTraceFileName();
		}

		private void UpdateTraceFileName()
		{
			TraceFileName = String.IsNullOrWhiteSpace(OracleConfiguration.Configuration.RemoteTraceDirectory)
				? _connectionAdapter.TraceFileName
				: Path.Combine(OracleConfiguration.Configuration.RemoteTraceDirectory, new FileInfo(_connectionAdapter.TraceFileName).Name);
		}

		private void TraceFileNameHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			var directoryName = new FileInfo(TraceFileName).DirectoryName;
			if (!Directory.Exists(directoryName))
			{
				var directoryType = String.IsNullOrWhiteSpace(OracleConfiguration.Configuration.RemoteTraceDirectory)
					? "Local"
					: "Remote";

				Messages.ShowError($"{directoryType} trace directory '{directoryName}' does not exist or is not accessible. ");
				return;
			}

			var arguments = File.Exists(TraceFileName)
				? $"/select,{TraceFileName}"
			    : $"/root,{directoryName}";

			Process.Start("explorer.exe", arguments);
		}
	}

	public class OracleTraceEventModel
	{
		public bool IsSelected { get; set; }

		public bool IsEnabled { get; set; }
		
		public OracleTraceEvent TraceEvent { get; set; }
	}
}
