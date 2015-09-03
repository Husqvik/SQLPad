using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SqlPad
{
	public partial class DebuggerViewer
	{
		private readonly Dictionary<string, DebuggerTabItem> _viewers = new Dictionary<string, DebuggerTabItem>();

		private IInfrastructureFactory _infrastructureFactory;
		private IDatabaseModel _databaseModel;
		private IDebuggerSession _debuggerSession;

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

			_debuggerSession.Attached += delegate { Dispatcher.Invoke(() => DisplaySourceAsync(CancellationToken.None)); };
		}

		public async void DisplaySourceAsync(CancellationToken cancellationToken)
		{
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

			foreach (var viewer in _viewers.Values)
			{
				viewer.ActiveLine = Equals(viewer, debuggerView) ? currentStackItem.Line : (int?)null;
			}
		}
	}
}
