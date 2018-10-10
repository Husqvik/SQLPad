using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SqlPad.Bookmarks;

namespace SqlPad
{
	public partial class DebuggerViewer
	{
		private static readonly int[] EmptyInt32Array = new int[0];

		private readonly Dictionary<string, DebuggerTabItem> _viewers = new Dictionary<string, DebuggerTabItem>();
		private readonly Dictionary<BreakpointData, object> _breakpointIdentifiers = new Dictionary<BreakpointData, object>();

		private OutputViewer _outputViewer;
		private IDebuggerSession _debuggerSession;
		private bool _isInitialized;

		public IList<DebugProgramItem> StackTrace { get; } = new ObservableCollection<DebugProgramItem>();

		public IList<WatchItem> WatchItems { get; } = new ObservableCollection<WatchItem>();

		public DebuggerViewer()
		{
			InitializeComponent();
		}

		public void Initialize(OutputViewer outputViewer, IDebuggerSession debuggerSession)
		{
			_outputViewer = outputViewer;
			_debuggerSession = debuggerSession;

			foreach (var tabItem in TabSourceViewer.Items.Cast<DebuggerTabItem>().ToArray())
			{
				TabSourceViewer.RemoveTabItemWithoutBindingError(tabItem);
			}

			_viewers.Clear();

			WatchItems.Clear();
			foreach (var watchItem in _outputViewer.DocumentPage.WorkDocument.WatchItems)
			{
				WatchItems.Add(new WatchItem { Name = watchItem });
			}

			if (!_isInitialized)
			{
				TabDebuggerOptions.SelectedIndex = _outputViewer.DocumentPage.WorkDocument.DebuggerViewDefaultTabIndex;
				_isInitialized = true;
			}

			_debuggerSession.Attached += delegate { Dispatcher.Invoke(DebuggerAttachedHandler); };
		}

		private async void DebuggerAttachedHandler()
		{
			await RestoreBreakPointConfiguration();
			await Refresh(CancellationToken.None);
			EnsureWatchItem();
		}

		private async Task RestoreBreakPointConfiguration()
		{
			foreach (var breakpoint in _outputViewer.DocumentPage.WorkDocument.Breakpoints)
			{
				if (_breakpointIdentifiers.TryGetValue(breakpoint, out var breakpointIdentifier))
				{
					continue;
				}

				var result = await _debuggerSession.SetBreakpoint(breakpoint.ProgramIdentifier, breakpoint.LineNumber, CancellationToken.None);
				if (result.IsSuccessful)
				{
					_breakpointIdentifiers.Add(breakpoint, result.BreakpointIdentifier);
				}

				if (!breakpoint.IsEnabled)
				{
					await _debuggerSession.DisableBreakpoint(result.BreakpointIdentifier, CancellationToken.None);
				}
			}
		}

		private void EnsureWatchItem()
		{
			if (WatchItems.All(i => !String.IsNullOrWhiteSpace(i.Name)))
			{
				WatchItems.Add(new WatchItem());
			}
		}

		public async Task Refresh(CancellationToken cancellationToken)
		{
			_debuggerSession.BreakOnExceptions = _outputViewer.BreakOnExceptions;

			StackTrace.Clear();
			StackTrace.AddRange(_debuggerSession.StackTrace);

			DebuggerTabItem debuggerView = null;
			foreach (var stackItem in _debuggerSession.StackTrace)
			{
				if (_viewers.TryGetValue(stackItem.Header, out debuggerView))
				{
					continue;
				}

				debuggerView = new DebuggerTabItem(stackItem, _outputViewer);
				debuggerView.BreakpointChanging += CodeViewerBreakpointChangingHandler;
				debuggerView.BreakpointChanged += CodeViewerBreakpointChangedHandler;

				if (_viewers.Count == 0)
				{
					debuggerView.Loaded += DebuggerViewLoadedHandler;
				}

				await debuggerView.CodeViewer.LoadAsync(stackItem.ProgramText, cancellationToken);

				_viewers.Add(stackItem.Header, debuggerView);
				TabSourceViewer.Items.Add(debuggerView);

				foreach (var breakpointIdentifier in _breakpointIdentifiers.Where(kvp => kvp.Key.ProgramIdentifier.Equals(stackItem.ProgramIdentifier)).ToArray())
				{
					debuggerView.AddBreakpoint(breakpointIdentifier.Key, breakpointIdentifier.Value);
				}
			}

			debuggerView.IsSelected = true;

			HighlightStackTraceLines();

			if (_debuggerSession.CurrentException != null)
			{
				Messages.ShowError($"An exception has been raised: {Environment.NewLine}{_debuggerSession.CurrentException.ErrorMessage}", "Database Exception");
			}

			await RefreshWatchItemsAsync(cancellationToken);
		}

		private void DebuggerViewLoadedHandler(object sender, RoutedEventArgs routedEventArgs)
		{
			var debuggerTabItem = (DebuggerTabItem)sender;
			debuggerTabItem.Loaded -= DebuggerViewLoadedHandler;
			debuggerTabItem.CodeViewer.Editor.Focus();
		}

		private void CodeViewerBreakpointChangingHandler(object sender, BreakpointChangingEventArgs args)
		{
			var debuggerView = (DebuggerTabItem)sender;
			var breakpoint = args.Breakpoint;

			switch (args.State)
			{
				case BreakpointState.Added:
					args.SetBreakpointTask = _debuggerSession.SetBreakpoint(debuggerView.ProgramItem.ProgramIdentifier, breakpoint.Anchor.Line, CancellationToken.None);
					break;
				case BreakpointState.Enabled:
					args.SetBreakpointTask = _debuggerSession.EnableBreakpoint(breakpoint.Identifier, CancellationToken.None);
					break;
				case BreakpointState.Disabled:
					args.SetBreakpointTask = _debuggerSession.DisableBreakpoint(breakpoint.Identifier, CancellationToken.None);
					break;
				case BreakpointState.Removed:
					args.SetBreakpointTask = _debuggerSession.DeleteBreakpoint(breakpoint.Identifier, CancellationToken.None);
					break;
			}
		}

		private void CodeViewerBreakpointChangedHandler(object sender, BreakpointChangedEventArgs args)
		{
			if (args.State == BreakpointState.Added)
			{
				_breakpointIdentifiers[args.BreakpointData] = args.Breakpoint.Identifier;
			}
			else
			{
				_breakpointIdentifiers.Remove(args.BreakpointData);
			}
		}

		private void HighlightStackTraceLines()
		{
			var inactiveItems = _debuggerSession.StackTrace
				.Take(_debuggerSession.StackTrace.Count - 1)
				.ToLookup(i => i.Header, i => i.Line);

			var activeStackItem = _debuggerSession.StackTrace.Last();

			var activeItemHighlighted = false;
			foreach (var group in inactiveItems)
			{
				var debuggerView = _viewers[group.Key];
				var activeLineNumber = String.Equals(activeStackItem.Header, group.Key) ? activeStackItem.Line : (int?)null;
				activeItemHighlighted |= activeLineNumber.HasValue;
				debuggerView.HighlightStackTraceLines(activeLineNumber, group);
			}

			foreach (var kvp in _viewers)
			{
				if (!inactiveItems.Contains(kvp.Key))
				{
					kvp.Value.HighlightStackTraceLines(null, EmptyInt32Array);
				}
			}

			if (!activeItemHighlighted)
			{
				_viewers[activeStackItem.Header].HighlightStackTraceLines(activeStackItem.Line, EmptyInt32Array);
			}
		}

		private void MouseButtonDownHandler(object sender, MouseButtonEventArgs args)
		{
			if (args.ClickCount != 2)
			{
				return;
			}

			var currentItem = (DebugProgramItem)StackTraceItems.SelectedItem;
			var debuggerViewer = _viewers[currentItem.Header];
			TabSourceViewer.SelectedItem = debuggerViewer;
			debuggerViewer.CodeViewer.Editor.ScrollToLine(currentItem.Line);
		}

		private async void CellEditEndingHandler(object sender, DataGridCellEditEndingEventArgs args)
		{
			if (args.EditAction == DataGridEditAction.Cancel)
			{
				return;
			}

			try
			{
				var watchItem = (WatchItem)args.Row.DataContext;
				if (Equals(args.Column, ColumnDebugExpressionName))
				{
					_outputViewer.DocumentPage.WorkDocument.WatchItems = WatchItems.Select(i => i.Name).ToArray();

					if (String.IsNullOrWhiteSpace(watchItem.Name))
					{
						watchItem.Value = null;
						return;
					}
				}
				else
				{
					await _debuggerSession.SetValue(watchItem, CancellationToken.None);
				}

				await _debuggerSession.GetValue(watchItem, CancellationToken.None);
			}
			catch (Exception exception)
			{
				Messages.ShowError(exception.Message);
			}
		}

		private async Task RefreshWatchItemsAsync(CancellationToken token)
		{
			try
			{
				foreach (var watchItem in WatchItems.Where(i => !String.IsNullOrWhiteSpace(i.Name)))
				{
					await _debuggerSession.GetValue(watchItem, token);
				}
			}
			catch (Exception exception)
			{
				Messages.ShowError(exception.Message);
			}
		}

		private void DebuggerOptionsSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
		{
			if (_outputViewer != null)
			{
				_outputViewer.DocumentPage.WorkDocument.DebuggerViewDefaultTabIndex = ((TabControl)sender).SelectedIndex;
			}
		}

		private void WatchItemGridPreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (WatchItemGrid.CurrentItem != null && e.Key == Key.Delete && Keyboard.Modifiers != ModifierKeys.Alt && Keyboard.Modifiers != ModifierKeys.Control && Keyboard.Modifiers != ModifierKeys.Windows)
			{
				WatchItems.Remove((WatchItem)WatchItemGrid.CurrentItem);
			}
		}

		private void WatchItemExpandMouseLeftButtonDownHandler(object sender, MouseButtonEventArgs e)
		{
			var textBlock = (TextBlock)sender;
			//var dataGrid = textBlock.FindParentVisual<DataGrid>();

			var watchItem = (WatchItem)textBlock.DataContext;
			var itemLabels = watchItem.ChildItems.Select(i => $"{i.Name} = {i.Value}");
			Messages.ShowInformation(String.Join(Environment.NewLine, itemLabels));
		}
	}

	[DebuggerDisplay("WatchItem (Name={Name}; Value={_value})")]
	public class WatchItem : ModelBase
	{
		private object _value;
		private ObservableCollection<WatchItem> _childItems;

		public string Name { get; set; }

		public ObservableCollection<WatchItem> ChildItems
		{
			get { return _childItems; }
			set { UpdateValueAndRaisePropertyChanged(ref _childItems, value); }
		}

		public object Value
		{
			get { return _value; }
			set { UpdateValueAndRaisePropertyChanged(ref _value, value); }
		}
	}
}
