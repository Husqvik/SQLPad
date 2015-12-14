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
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle.DebugTrace
{
	public partial class OracleTraceViewer : ITraceViewer
	{
		public static readonly DependencyProperty IsTracingProperty = DependencyProperty.Register(nameof(IsTracing), typeof(bool), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(false));
		public static readonly DependencyProperty TraceFileNameProperty = DependencyProperty.Register(nameof(TraceFileName), typeof(string), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty TraceIdentifierProperty = DependencyProperty.Register(nameof(TraceIdentifier), typeof(string), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(String.Empty));
		public static readonly DependencyProperty SessionIdProperty = DependencyProperty.Register(nameof(SessionId), typeof(int?), typeof(OracleTraceViewer), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty TKProfEnabledProperty = DependencyProperty.Register(nameof(TKProfEnabled), typeof(bool), typeof(OracleTraceViewer), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty TKProfFileNameProperty = DependencyProperty.Register(nameof(TKProfFileName), typeof(string), typeof(OracleTraceViewer), new FrameworkPropertyMetadata(String.Empty));

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

		[Bindable(true)]
		public int? SessionId
		{
			get { return (int)GetValue(SessionIdProperty); }
			set { SetValue(SessionIdProperty, value); }
		}

		[Bindable(true)]
		public bool TKProfEnabled
		{
			get { return (bool)GetValue(TKProfEnabledProperty); }
			private set { SetValue(TKProfEnabledProperty, value); }
		}

		[Bindable(true)]
		public string TKProfFileName
		{
			get { return (string)GetValue(TKProfFileNameProperty); }
			private set { SetValue(TKProfFileNameProperty, value); }
		}

		private readonly OracleConnectionAdapterBase _connectionAdapter;

		private readonly ObservableCollection<OracleTraceEventModel> _traceEvents = new ObservableCollection<OracleTraceEventModel>();

		public Control Control => this;

		public IReadOnlyCollection<OracleTraceEventModel> TraceEvents => _traceEvents;

		private FileInfo TraceFileInfo => new FileInfo(_connectionAdapter.TraceFileName);

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
			UpdateSessionIdAndTraceFileName();
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
						Messages.ShowInformation("Select an event first. ");
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

			UpdateSessionIdAndTraceFileName();
		}

		private void UpdateSessionIdAndTraceFileName()
		{
			SessionId = _connectionAdapter.SessionId;

			if (String.IsNullOrEmpty(_connectionAdapter.TraceFileName))
			{
				return;
			}

			var traceDirectory = OracleConfiguration.Configuration.GetRemoteTraceDirectory(_connectionAdapter.DatabaseModel.ConnectionString.Name);
			TraceFileName = String.IsNullOrWhiteSpace(traceDirectory)
				? _connectionAdapter.TraceFileName
				: Path.Combine(traceDirectory, TraceFileInfo.Name);

			TKProfEnabled = !String.IsNullOrEmpty(OracleConfiguration.Configuration.TKProfPath);
		}

		private void TraceFileNameHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			NavigateToTraceFile(TraceFileName);
		}

		internal static void NavigateToTraceFile(string traceFileName)
		{
			var fileInfo = new FileInfo(traceFileName);
			var directoryName = fileInfo.DirectoryName;
			if (!Directory.Exists(directoryName))
			{
				var directoryType = new Uri(traceFileName).IsUnc
					? "Remote"
					: "Local";

				Messages.ShowError($"{directoryType} trace directory '{directoryName}' does not exist or is not accessible. ");
				return;
			}

			var arguments = File.Exists(traceFileName)
				? $"/select,{traceFileName}"
				: $"/root,{directoryName}";

			Process.Start("explorer.exe", arguments);
		}

		private async void TKProfClickHandler(object sender, RoutedEventArgs e)
		{
			if (!File.Exists(TraceFileName))
			{
				Messages.ShowError($"File '{TraceFileName}' does not exist. ");
				return;
			}

			var tkProfFileName = Path.Combine(Path.GetTempPath(), $"{TraceFileInfo.Name}.tkprof.txt");
			var connectionStringBuilder = new OracleConnectionStringBuilder(_connectionAdapter.DatabaseModel.ConnectionString.ConnectionString);
			var tkProfParameters = $"{TraceFileName} {tkProfFileName} explain={connectionStringBuilder.UserID}/{connectionStringBuilder.Password}@{connectionStringBuilder.DataSource} waits=yes sort=(exeela, fchela)";

			await App.ExecuteExternalProcess(
				OracleConfiguration.Configuration.TKProfPath,
				tkProfParameters,
				TimeSpan.FromMinutes(1),
				() => TKProfFileName = tkProfFileName,
				() => Messages.ShowError($"Command '{OracleConfiguration.Configuration.TKProfPath} {tkProfParameters}' timed out. "),
				(exitCode, errorMessage) => Messages.ShowError(String.IsNullOrEmpty(errorMessage) ? $"Command '{OracleConfiguration.Configuration.TKProfPath} {tkProfParameters}' terminated with exit code {exitCode}. " : errorMessage),
				CancellationToken.None);
		}

		private void TKProfFileNameHyperlinkClickHandler(object sender, RoutedEventArgs e)
		{
			App.SafeActionWithUserError(() => Process.Start(TKProfFileName));
		}
	}

	public class OracleTraceEventModel
	{
		public bool IsSelected { get; set; }

		public bool IsEnabled { get; set; }
		
		public OracleTraceEvent TraceEvent { get; set; }
	}
}
