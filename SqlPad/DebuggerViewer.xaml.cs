using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlPad
{
	public partial class DebuggerViewer
	{
		private readonly Dictionary<string, DebuggerTabItem> _viewers = new Dictionary<string, DebuggerTabItem>();

		private IInfrastructureFactory _infrastructureFactory;
		private IDatabaseModel _databaseModel;
		private IDebuggerSession _debuggerSession;

		public IList<StackTraceItem> StackTrace { get; } = new ObservableCollection<StackTraceItem>();

		public IList<WatchItem> WatchItems { get; } = new ObservableCollection<WatchItem>();

		public DebuggerViewer()
		{
			InitializeComponent();
		}

		public void Initialize(IInfrastructureFactory infrastructureFactory, IDatabaseModel databaseModel, IDebuggerSession debuggerSession)
		{
			_databaseModel = databaseModel;
			_infrastructureFactory = infrastructureFactory;
			_debuggerSession = debuggerSession;

			TabSourceViewer.Items.Clear();
			_viewers.Clear();

			_debuggerSession.Attached += delegate { Dispatcher.Invoke(DebuggerAttachedHandler); };
		}

		private void DebuggerAttachedHandler()
		{
			DisplaySourceAsync(CancellationToken.None);
			EnsureWatchItem();
		}

		private void EnsureWatchItem()
		{
			if (WatchItems.All(i => !String.IsNullOrWhiteSpace(i.Name)))
			{
				WatchItems.Add(new WatchItem());
			}
		}

		public async void DisplaySourceAsync(CancellationToken cancellationToken)
		{
			StackTrace.Clear();
			StackTrace.AddRange(_debuggerSession.StackTrace);

			var currentStackItem = _debuggerSession.StackTrace.Last();
			DebuggerTabItem debuggerView;
			if (!_viewers.TryGetValue(currentStackItem.Header, out debuggerView))
			{
				debuggerView = new DebuggerTabItem(currentStackItem.Header);
				debuggerView.CodeViewer.Initialize(_infrastructureFactory, _databaseModel);

				await debuggerView.CodeViewer.LoadAsync(currentStackItem.ProgramText, cancellationToken);

				_viewers.Add(currentStackItem.Header, debuggerView);
				TabSourceViewer.Items.Add(debuggerView);
			}

			TabSourceViewer.SelectedItem = debuggerView;

			HighlightCurrentLine(currentStackItem);
		}

		private void HighlightCurrentLine(StackTraceItem currentItem)
		{
			foreach (var viewer in _viewers.Values)
			{
				//viewer.InactiveLine = isActive ? (int?)null : currentItem.Line;
				viewer.ActiveLine = Equals(viewer, TabSourceViewer.SelectedItem) ? currentItem.Line : (int?)null;
			}
		}

		private void MouseButtonDownHandler(object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount != 2)
			{
				return;
			}

			var currentItem = (StackTraceItem)StackTraceItems.SelectedItem;
			TabSourceViewer.SelectedItem = _viewers[currentItem.Header];
			HighlightCurrentLine(currentItem);
		}

		private void CellEditEndingHandler(object sender, DataGridCellEditEndingEventArgs e)
		{

		}
	}

	public class WatchItem : ModelBase
	{
		private string _value;
		public string Name { get; set; }

		public string Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}
	}
}
