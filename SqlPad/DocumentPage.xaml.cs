using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using Microsoft.Win32;
using SqlPad.Commands;
using SqlPad.FindReplace;

using Timer = System.Timers.Timer;

namespace SqlPad
{
	public partial class DocumentPage : IDisposable
	{
		private const int RowBatchSize = 100;
		private const string InitialDocumentHeader = "New*";
		public const string FileMaskDefault = "SQL Files (*.sql)|*.sql|All (*.*)|*";

		private SqlDocumentRepository _sqlDocumentRepository;
		private IInfrastructureFactory _infrastructureFactory;
		private ICodeCompletionProvider _codeCompletionProvider;
		private ICodeSnippetProvider _codeSnippetProvider;
		private IContextActionProvider _contextActionProvider;
		private IStatementFormatter _statementFormatter;
		private IToolTipProvider _toolTipProvider;
		private INavigationService _navigationService;

		private MultiNodeEditor _multiNodeEditor;
		private CancellationTokenSource _cancellationTokenSource;
		private readonly SqlDocumentColorizingTransformer _colorizingTransformer = new SqlDocumentColorizingTransformer();

		private static readonly CellValueConverter CellValueConverter = new CellValueConverter();
		private static readonly Style CellStyleRightAlign = new Style(typeof(DataGridCell));
		private static readonly Style CellTextBoxStyleReadOnly = new Style(typeof(TextBox));
		private static readonly Style HeaderStyleRightAlign = new Style(typeof(DataGridColumnHeader));

		private bool _isParsing;
		private bool _isInitializing = true;
		private bool _enableCodeComplete;
		private bool _isToolTipOpenByShortCut;
		
		private readonly ToolTip _toolTip = new ToolTip();
		private readonly PageModel _pageModel;
		private readonly Timer _timerReParse = new Timer(100);
		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly List<CommandBinding> _specificCommandBindings = new List<CommandBinding>();

		private CompletionWindow _completionWindow;
		private HashSet<string> _currentBindVariables = new HashSet<string>();
		
		public EventHandler ParseFinished = delegate { };

		internal TabItem TabItem { get; private set; }
		
		internal ContextMenu TabItemContextMenu { get { return ((ContentControl)TabItem.Header).ContextMenu; } }

		internal static bool IsParsingSynchronous { get; set; }

		internal bool IsSelectedPage
		{
			get
			{
				var mainWindow = (MainWindow)Window.GetWindow(this);
				return Equals(((TabItem)mainWindow.DocumentTabControl.SelectedItem).Content);
			}
		}

		public TextEditorAdapter EditorAdapter { get; private set; }

		public WorkingDocument WorkingDocument { get; private set; }

		public string DocumentHeader { get { return WorkingDocument.File == null ? InitialDocumentHeader : WorkingDocument.File.Name + (IsDirty ? "*" : null); } }

		public bool IsDirty { get { return Editor.IsModified; } }

		public IDatabaseModel DatabaseModel { get; private set; }

		static DocumentPage()
		{
			CellStyleRightAlign.Setters.Add(new Setter(Block.TextAlignmentProperty, TextAlignment.Right));
			HeaderStyleRightAlign.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
			CellTextBoxStyleReadOnly.Setters.Add(new Setter(TextBoxBase.IsReadOnlyProperty, true));
		}

		public DocumentPage(WorkingDocument workingDocument = null)
		{
			InitializeComponent();

			ComboBoxConnection.IsEnabled = ConfigurationProvider.ConnectionStrings.Count > 1;
			ComboBoxConnection.ItemsSource = ConfigurationProvider.ConnectionStrings;
			ComboBoxConnection.SelectedIndex = 0;

			InitializeGenericCommandBindings();

			_timerReParse.Elapsed += (sender, args) => Dispatcher.Invoke(ReParse);
			_timerExecutionMonitor.Elapsed += (sender, args) => Dispatcher.Invoke(() => TextExecutionTime.Text = FormatElapsedMilliseconds(_stopWatch.Elapsed));

			_pageModel = new PageModel(this);

			ConfigureEditor();

			var usedConnection = ConfigurationProvider.ConnectionStrings[0];

			if (workingDocument == null)
			{
				WorkingDocument = new WorkingDocument
				{
					ConnectionName = ((ConnectionStringSettings)ComboBoxConnection.SelectedItem).Name
				};

				WorkingDocumentCollection.AddDocument(WorkingDocument);
				_pageModel.CurrentConnection = usedConnection;

				WorkingDocument.SchemaName = _pageModel.CurrentSchema;
			}
			else
			{
				WorkingDocument = workingDocument;

				if (WorkingDocument.Text == null && WorkingDocument.File.Exists)
				{
					WorkingDocument.Text = File.ReadAllText(WorkingDocument.File.FullName);
				}

				if (!String.IsNullOrEmpty(WorkingDocument.ConnectionName))
				{
					var connectionString = ConfigurationProvider.ConnectionStrings
						.Cast<ConnectionStringSettings>()
						.FirstOrDefault(cs => cs.Name == WorkingDocument.ConnectionName);

					if (connectionString != null)
					{
						usedConnection = connectionString;
					}
				}

				_pageModel.CurrentConnection = usedConnection;

				_pageModel.CurrentSchema = WorkingDocument.SchemaName;

				Editor.Text = WorkingDocument.Text;

				if (Editor.Document.TextLength >= WorkingDocument.SelectionStart)
				{
					Editor.SelectionStart = WorkingDocument.SelectionStart;

					var storedSelectionEndIndex = WorkingDocument.SelectionStart + WorkingDocument.SelectionLength;
					var validSelectionEndIndex = storedSelectionEndIndex > Editor.Document.TextLength
						? Editor.Document.TextLength
						: storedSelectionEndIndex;

					Editor.SelectionLength = validSelectionEndIndex - WorkingDocument.SelectionStart;
				}

				Editor.CaretOffset = Editor.Document.TextLength >= WorkingDocument.CursorPosition
					? WorkingDocument.CursorPosition
					: Editor.Document.TextLength;

				Editor.IsModified = WorkingDocument.IsModified;
			}

			_pageModel.DocumentHeader = DocumentHeader;

			DataContext = _pageModel;

			InitializeTabItem();
		}

		private void InitializeTabItem()
		{
			var header = new ContentControl { ContextMenu = CreateTabItemHeaderContextMenu() };
			TabItem = new TabItem { Content = this, Header = header };

			var binding = new Binding("DocumentHeader") { Source = _pageModel, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(ContentProperty, binding);
		}

		private ContextMenu CreateTabItemHeaderContextMenu()
		{
			var contextMenu = new ContextMenu();
			var menuItemSave = new MenuItem
			{
				Header = "Save",
				Command = GenericCommands.SaveCommand,
			};

			contextMenu.Items.Add(menuItemSave);
			contextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.SaveCommand, SaveCommandExecutedHandler));

			var menuItemSaveAs = new MenuItem
			{
				Header = "Save as...",
				Command = GenericCommands.SaveAsCommand,
			};

			contextMenu.Items.Add(menuItemSaveAs);
			contextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.SaveAsCommand, SaveAsCommandExecutedHandler));

			var menuItemOpenContainingFolder = new MenuItem
			{
				Header = "Open Containing Folder",
				Command = GenericCommands.OpenContainingFolderCommand,
				CommandParameter = this
			};

			contextMenu.Items.Add(menuItemOpenContainingFolder);
			contextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.OpenContainingFolderCommand, OpenContainingFolderCommandExecutedHandler, (sender, args) => args.CanExecute = WorkingDocument.File != null));

			var menuItemClose = new MenuItem
			{
				Header = "Close",
				Command = GenericCommands.CloseDocumentCommand,
				CommandParameter = this
			};

			contextMenu.Items.Add(menuItemClose);

			var menuItemCloseAllButThis = new MenuItem
			{
				Header = "Close All But This",
				Command = GenericCommands.CloseAllDocumentsButThisCommand,
				CommandParameter = this
			};

			contextMenu.Items.Add(menuItemCloseAllButThis);
			return contextMenu;
		}

		private void ConfigureEditor()
		{
			//Editor.Options.ShowColumnRuler = true;

			Editor.TextArea.SelectionCornerRadius = 0;
			Editor.TextArea.TextView.LineTransformers.Add(_colorizingTransformer);

			Editor.TextArea.TextEntering += TextEnteringHandler;
			Editor.TextArea.TextEntered += TextEnteredHandler;

			Editor.TextArea.Caret.PositionChanged += CaretOnPositionChanged;
			Editor.TextArea.SelectionChanged += SelectionChangedHandler;

			EditorAdapter = new TextEditorAdapter(Editor);
		}

		private void InitializeSpecificCommandBindings()
		{
			foreach (var existingBinding in _specificCommandBindings)
			{
				Editor.TextArea.DefaultInputHandler.Editing.CommandBindings.Remove(existingBinding);
			}

			_specificCommandBindings.Clear();

			foreach (var handler in _infrastructureFactory.CommandFactory.CommandHandlers)
			{
				var command = new RoutedCommand(handler.Name, typeof(TextEditor), handler.DefaultGestures);
				var routedHandlerMethod = GenericCommandHandler.CreateRoutedEditCommandHandler(handler, () => _sqlDocumentRepository);
				var commandBinding = new CommandBinding(command, routedHandlerMethod);
				_specificCommandBindings.Add(commandBinding);
				Editor.TextArea.DefaultInputHandler.Editing.CommandBindings.Add(commandBinding);
			}
		}

		internal void InitializeInfrastructureComponents(ConnectionStringSettings connectionString)
		{
			if (DatabaseModel != null)
			{
				DatabaseModel.Dispose();
			}

			var connectionConfiguration = ConfigurationProvider.GetConnectionCofiguration(connectionString.Name);
			_pageModel.ProductionLabelVisibility = connectionConfiguration.IsProduction ? Visibility.Visible : Visibility.Collapsed;
			_infrastructureFactory = connectionConfiguration.InfrastructureFactory;
			_codeCompletionProvider = _infrastructureFactory.CreateCodeCompletionProvider();
			_codeSnippetProvider = _infrastructureFactory.CreateSnippetProvider();
			_contextActionProvider = _infrastructureFactory.CreateContextActionProvider();
			_statementFormatter = _infrastructureFactory.CreateSqlFormatter(new SqlFormatterOptions());
			_toolTipProvider = _infrastructureFactory.CreateToolTipProvider();
			_navigationService = _infrastructureFactory.CreateNavigationService();
			DatabaseModel = _infrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings[connectionString.Name]);
			_sqlDocumentRepository = new SqlDocumentRepository(_infrastructureFactory.CreateParser(), _infrastructureFactory.CreateStatementValidator(), DatabaseModel);

			_colorizingTransformer.SetParser(_infrastructureFactory.CreateParser());

			InitializeSpecificCommandBindings();

			DatabaseModel.RefreshStarted += DatabaseModelRefreshStartedHandler;
			DatabaseModel.RefreshFinished += DatabaseModelRefreshFinishedHandler;

			if (DatabaseModel.IsModelFresh)
			{
				ReParse();
			}
			else
			{
				DatabaseModel.RefreshIfNeeded();
			}
		}

		private ScrollViewer GetResultGridScrollViewer()
		{
			var border = VisualTreeHelper.GetChild(ResultGrid, 0) as Decorator;
			if (border == null)
				return null;

			return border.Child as ScrollViewer;
		}

		private void SaveCommandExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Save();
		}

		private void SaveAsCommandExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			SaveAs();
		}

		private void OpenContainingFolderCommandExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Process.Start("explorer.exe", "/select," + WorkingDocument.File.FullName);
		}

		public bool Save()
		{
			if (WorkingDocument.File == null)
				return SaveAs();

			if (!IsDirty)
				return true;

			SaveDocument();
			return true;
		}

		public bool SaveAs()
		{
			var dialog = new SaveFileDialog { Filter = FileMaskDefault };
			if (dialog.ShowDialog() != true)
			{
				return false;
			}

			WorkingDocument.DocumentFileName = dialog.FileName;

			SaveDocument();
			SaveWorkingDocument();
			return true;
		}

		public void SaveWorkingDocument()
		{
			WorkingDocument.Text = Editor.Text;
			WorkingDocument.CursorPosition = Editor.CaretOffset;
			WorkingDocument.SelectionStart = Editor.SelectionStart;
			WorkingDocument.SelectionLength = Editor.SelectionLength;

			var textView = Editor.TextArea.TextView;
			WorkingDocument.VisualLeft = textView.ScrollOffset.X;
			WorkingDocument.VisualTop = textView.ScrollOffset.Y;
			WorkingDocument.EditorGridRowHeight = RowDefinitionEditor.ActualHeight;
			WorkingDocument.EditorGridColumnWidth = ColumnDefinitionEditor.ActualWidth;

			if (_pageModel.CurrentConnection != null)
			{
				WorkingDocument.ConnectionName = _pageModel.CurrentConnection.Name;
				WorkingDocument.SchemaName = _pageModel.CurrentSchema;
			}
			
			WorkingDocumentCollection.Save();
		}

		private void SaveDocument()
		{
			Editor.Save(WorkingDocument.File.FullName);
			WorkingDocument.IsModified = false;
			_pageModel.DocumentHeader = WorkingDocument.File.Name;
		}

		private void SelectionChangedHandler(object sender, EventArgs eventArgs)
		{
			_pageModel.SelectionLength = Editor.SelectionLength == 0 ? null : (int?)Editor.SelectionLength;
		}

		private void DatabaseModelRefreshStartedHandler(object sender, EventArgs args)
		{
			Dispatcher.Invoke(() => ProgressBar.IsIndeterminate = true);
		}

		private void DatabaseModelRefreshFinishedHandler(object sender, EventArgs eventArgs)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  ProgressBar.IsIndeterminate = false;
								  ReParse();
			                  });
		}

		private void InitializeGenericCommandBindings()
		{
			ChangeDeleteLineCommandInputGesture();

			var commandBindings = Editor.TextArea.DefaultInputHandler.Editing.CommandBindings;
			commandBindings.Add(new CommandBinding(GenericCommands.ShowCodeCompletionOptionCommand, ShowCodeCompletionOptions, (sender, args) => args.CanExecute = _sqlDocumentRepository.StatementText == Editor.Text));
			commandBindings.Add(new CommandBinding(GenericCommands.ShowFunctionOverloadCommand, ShowFunctionOverloads));
			commandBindings.Add(new CommandBinding(GenericCommands.DuplicateTextCommand, GenericCommandHandler.DuplicateText));
			commandBindings.Add(new CommandBinding(GenericCommands.BlockCommentCommand, GenericCommandHandler.HandleBlockComments));
			commandBindings.Add(new CommandBinding(GenericCommands.LineCommentCommand, GenericCommandHandler.HandleLineComments));
			commandBindings.Add(new CommandBinding(GenericCommands.ListContextActionCommand, ListContextActions));
			commandBindings.Add(new CommandBinding(GenericCommands.MultiNodeEditCommand, EditMultipleNodes));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToPreviousUsageCommand, NavigateToPreviousHighlightedUsage));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToNextUsageCommand, NavigateToNextHighlightedUsage));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToQueryBlockRootCommand, NavigateToQueryBlockRoot));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToDefinitionRootCommand, NavigateToDefinition));
			commandBindings.Add(new CommandBinding(GenericCommands.ExecuteDatabaseCommandCommand, ExecuteDatabaseCommandHandler, CanExecuteDatabaseCommandHandler));
			commandBindings.Add(new CommandBinding(GenericCommands.SaveCommand, SaveCommandExecutedHandler));
			commandBindings.Add(new CommandBinding(GenericCommands.FormatStatementCommand, FormatStatement));
			commandBindings.Add(new CommandBinding(GenericCommands.FormatStatementAsSingleLineCommand, FormatStatementAsSingleLine));
			commandBindings.Add(new CommandBinding(GenericCommands.FindUsagesCommand, FindUsages));
			commandBindings.Add(new CommandBinding(GenericCommands.RefreshDatabaseModelCommand, RefreshDatabaseModel));
			commandBindings.Add(new CommandBinding(GenericCommands.CancelStatementCommand, CancelStatementHandler, (sender, args) => args.CanExecute = DatabaseModel.IsExecuting));

			commandBindings.Add(new CommandBinding(DiagnosticCommands.ShowTokenCommand, ShowTokenCommandExecutionHandler));

			ResultGrid.CommandBindings.Add(new CommandBinding(GenericCommands.FetchNextRowsCommand, FetchNextRows, CanFetchNextRows));
		}

		private void ShowCodeCompletionOptions(object sender, ExecutedRoutedEventArgs e)
		{
			CreateCodeCompletionWindow(true);
		}

		private void CancelStatementHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Trace.WriteLine("Command is about to cancel. ");
			_cancellationTokenSource.Cancel();
		}

		private void ShowTokenCommandExecutionHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var tokens = _infrastructureFactory.CreateTokenReader(_sqlDocumentRepository.StatementText).GetTokens(true).ToArray();
			var message = "Parsed: " + String.Join(", ", tokens.Where(t => !t.IsComment).Select(t => "{" + t.Value + "}"));
			message += Environment.NewLine + "Comments: " + String.Join(", ", tokens.Where(t => t.IsComment).Select(t => "{" + t.Value + "}"));
			MessageBox.Show(message, "Tokens", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void FormatStatement(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			GenericCommandHandler.ExecuteEditCommand(_sqlDocumentRepository, Editor, _statementFormatter.ExecutionHandler.ExecutionHandler);
		}

		private void FormatStatementAsSingleLine(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			GenericCommandHandler.ExecuteEditCommand(_sqlDocumentRepository, Editor, _statementFormatter.SingleLineExecutionHandler.ExecutionHandler);
		}

		private void CanFetchNextRows(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.ContinueRouting = ResultGrid.SelectedIndex < ResultGrid.Items.Count - 1 || !DatabaseModel.CanFetch;
			canExecuteRoutedEventArgs.CanExecute = !canExecuteRoutedEventArgs.ContinueRouting;
		}

		private async void FetchNextRows(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var nextRowBatch = DatabaseModel.FetchRecords(RowBatchSize);
			var exception = await SafeActionAsync(() => Task.Factory.StartNew(() => Dispatcher.Invoke(() => _pageModel.ResultRowItems.AddRange(nextRowBatch))));

			TextMoreRowsExist.Visibility = DatabaseModel.CanFetch ? Visibility.Visible : Visibility.Collapsed;

			if (exception != null)
			{
				Messages.ShowError(exception.Message);
			}
		}

		private void NavigateToQueryBlockRoot(object sender, ExecutedRoutedEventArgs args)
		{
			var queryBlockRootIndex = _navigationService.NavigateToQueryBlockRoot(_sqlDocumentRepository, Editor.CaretOffset);
			NavigateToOffset(queryBlockRootIndex);
		}
		
		private void NavigateToDefinition(object sender, ExecutedRoutedEventArgs args)
		{
			var queryBlockRootIndex = _navigationService.NavigateToDefinition(_sqlDocumentRepository, Editor.CaretOffset);
			NavigateToOffset(queryBlockRootIndex);
		}

		private void NavigateToOffset(int? offset)
		{
			if (!offset.HasValue)
				return;
			
			Editor.CaretOffset = offset.Value;
			Editor.ScrollToCaret();
		}

		private void RefreshDatabaseModel(object sender, ExecutedRoutedEventArgs args)
		{
			DatabaseModel.Refresh(true);
		}

		private void ShowFunctionOverloads(object sender, ExecutedRoutedEventArgs args)
		{
			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_sqlDocumentRepository, Editor.CaretOffset);
			if (functionOverloads.Count <= 0)
				return;

			_toolTip.Content = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			_isToolTipOpenByShortCut = true;

			var rectangle = Editor.TextArea.Caret.CalculateCaretRectangle();
			_toolTip.PlacementTarget = Editor.TextArea;
			_toolTip.Placement = PlacementMode.Relative;
			_toolTip.HorizontalOffset = rectangle.Left;
			_toolTip.VerticalOffset = rectangle.Top + Editor.TextArea.TextView.DefaultLineHeight;

			_toolTip.IsOpen = true;
		}

		private void CanExecuteDatabaseCommandHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			if (DatabaseModel.IsExecuting || _sqlDocumentRepository.StatementText != Editor.Text)
				return;

			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			args.CanExecute = statement != null && statement.RootNode != null && statement.RootNode.FirstTerminalNode != null;
		}

		private async void ExecuteDatabaseCommandHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);

			SqlPadConfiguration.StoreConfiguration();

			_pageModel.ResultRowItems.Clear();
			_pageModel.GridRowInfoVisibility = Visibility.Collapsed;
			TextMoreRowsExist.Visibility = Visibility.Collapsed;

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.None;
			ResultGrid.Columns.Clear();

			ActionResult actionResult;
			_pageModel.AffectedRowCount = -1;
			Task<int> innerTask = null;
			var statementText = Editor.SelectionLength > 0 ? Editor.SelectedText : statement.RootNode.GetStatementSubstring(Editor.Text);
			using (_cancellationTokenSource = new CancellationTokenSource())
			{
				var executionModel =
					new StatementExecutionModel
					{
						StatementText = statementText,
						BindVariables = _pageModel.BindVariables
					};

				actionResult = await SafeTimedActionAsync(() => innerTask = DatabaseModel.ExecuteStatementAsync(executionModel, _cancellationTokenSource.Token));
			}

			if (!actionResult.IsSuccessful)
			{
				Messages.ShowError(actionResult.Exception.Message);
				return;
			}

			_pageModel.AffectedRowCount = innerTask.Result;
			
			TextExecutionTime.Text = FormatElapsedMilliseconds(actionResult.Elapsed);

			var columnHeaders = DatabaseModel.GetColumnHeaders();

			if (columnHeaders.Count == 0)
			{
				return;
			}

			InitializeResultGrid(columnHeaders);
				
			FetchNextRows(null, null);

			if (ResultGrid.Items.Count > 0)
			{
				ResultGrid.SelectedIndex = 0;
			}
		}

		private async Task<ActionResult> SafeTimedActionAsync(Func<Task> action)
		{
			var actionResult = new ActionResult();

			_stopWatch.Restart();
			_timerExecutionMonitor.Start();

			actionResult.Exception = await SafeActionAsync(action);
			actionResult.Elapsed = _stopWatch.Elapsed;
			
			_timerExecutionMonitor.Stop();
			_stopWatch.Stop();
			
			return actionResult;
		}

		private async Task<Exception> SafeActionAsync(Func<Task> action)
		{
			try
			{
				await action();

				return null;
			}
			catch (Exception exception)
			{
				return exception;
			}
		}

		private static string FormatElapsedMilliseconds(TimeSpan timeSpan)
		{
			string formattedValue;
			if (timeSpan.TotalMilliseconds < 1000)
			{
				formattedValue = String.Format("{0} {1}", (int)timeSpan.TotalMilliseconds, "ms");
			}
			else if (timeSpan.TotalMilliseconds < 60000)
			{
				formattedValue = String.Format("{0} {1}", Math.Round(timeSpan.TotalMilliseconds / 1000, 2), "s");
			}
			else
			{
				formattedValue = String.Format("{0:00}:{1:00}", (int)timeSpan.TotalMinutes, timeSpan.Seconds);
			}

			return formattedValue;
		}

		private void InitializeResultGrid(IEnumerable<ColumnHeader> columnHeaders)
		{
			_pageModel.GridRowInfoVisibility = Visibility.Visible;

			foreach (var columnHeader in columnHeaders)
			{
				var columnTemplate =
					new DataGridTextColumn
					{
						Header = columnHeader.Name.Replace("_", "__"),
						Binding = new Binding(String.Format("[{0}]", columnHeader.ColumnIndex)) { Converter = CellValueConverter, ConverterParameter = columnHeader },
						EditingElementStyle = CellTextBoxStyleReadOnly
					};

				if (columnHeader.DataType.In(typeof(Decimal), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Byte)))
				{
					columnTemplate.HeaderStyle = HeaderStyleRightAlign;
					columnTemplate.CellStyle = CellStyleRightAlign;
				}

				ResultGrid.Columns.Add(columnTemplate);
			}

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
		}

		public void Dispose()
		{
			TabItemContextMenu.CommandBindings.Clear();
			TabItem.Header = null;
			DataContext = null;
			_timerReParse.Stop();
			_timerReParse.Dispose();
			_timerExecutionMonitor.Stop();
			_timerExecutionMonitor.Dispose();

			if (DatabaseModel != null)
			{
				DatabaseModel.Dispose();
			}
		}

		private void FindUsages(object sender, ExecutedRoutedEventArgs args)
		{
			var findUsagesCommandHandler = _infrastructureFactory.CommandFactory.FindUsagesCommandHandler;
			var executionContext = CommandExecutionContext.Create(Editor, _sqlDocumentRepository);
			findUsagesCommandHandler.ExecutionHandler(executionContext);
			_colorizingTransformer.SetHighlightSegments(executionContext.SegmentsToReplace);
			Editor.TextArea.TextView.Redraw();
		}
		private void NavigateToPreviousHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _colorizingTransformer.HighlightSegments
						.Where(s => s.IndextStart < Editor.CaretOffset)
						.OrderByDescending(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToNextHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _colorizingTransformer.HighlightSegments
						.Where(s => s.IndextStart > Editor.CaretOffset)
						.OrderBy(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToUsage(IEnumerable<TextSegment> nextSegments)
		{
			if (!_colorizingTransformer.HighlightSegments.Any())
				return;

			var nextSegment = nextSegments.FirstOrDefault();
			if (!nextSegment.Equals(TextSegment.Empty))
			{
				Editor.CaretOffset = nextSegment.IndextStart;
				Editor.ScrollToCaret();
			}
		}

		private void EditMultipleNodes(object sender, ExecutedRoutedEventArgs args)
		{
			if (_multiNodeEditor == null)
			{
				MultiNodeEditor.TryCreateMultiNodeEditor(Editor, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), DatabaseModel, out _multiNodeEditor);
			}
		}

		private void ChangeDeleteLineCommandInputGesture()
		{
			var deleteLineCommand = (RoutedCommand)Editor.TextArea.DefaultInputHandler.Editing.CommandBindings
				.Single(b => b.Command == AvalonEditCommands.DeleteLine)
				.Command;

			deleteLineCommand.InputGestures[0] = new KeyGesture(Key.L, ModifierKeys.Control);
		}

		private void PageLoadedHandler(object sender, RoutedEventArgs e)
		{
			if (_isInitializing)
			{
				if (WorkingDocument.EditorGridRowHeight > 0)
				{
					RowDefinitionEditor.Height = new GridLength(WorkingDocument.EditorGridRowHeight);
				}

				if (WorkingDocument.EditorGridColumnWidth > 0)
				{
					ColumnDefinitionEditor.Width = new GridLength(WorkingDocument.EditorGridColumnWidth);
				}
				
				Editor.ScrollToVerticalOffset(WorkingDocument.VisualTop);
				Editor.ScrollToHorizontalOffset(WorkingDocument.VisualLeft);
				_isInitializing = false;
			}

			Editor.Focus();
			
			if (!String.IsNullOrEmpty(Editor.Text))
			{
				ReParse();
			}
		}

		private void CaretOnPositionChanged(object sender, EventArgs eventArgs)
		{
			var parenthesisNodes = new List<StatementGrammarNode>();

			var location = Editor.Document.GetLocation(Editor.CaretOffset);
			_pageModel.CurrentLine = location.Line;
			_pageModel.CurrentColumn = location.Column;

			if (!_isParsing)
			{
				ShowHideBindVariableList();

				var parenthesisTerminal = _sqlDocumentRepository.Statements == null
					? null
					: _sqlDocumentRepository.ExecuteStatementAction(s => s.GetTerminalAtPosition(Editor.CaretOffset, n => n.Token.Value.In("(", ")")));

				if (parenthesisTerminal != null)
				{
					var childNodes = parenthesisTerminal.ParentNode.ChildNodes.ToList();
					var index = childNodes.IndexOf(parenthesisTerminal);
					var increment = parenthesisTerminal.Token.Value == "(" ? 1 : -1;
					var otherParenthesis = parenthesisTerminal.Token.Value == "(" ? ")" : "(";

					while (0 <= index && index < childNodes.Count)
					{
						index += increment;

						if (index < 0 || index >= childNodes.Count)
							break;

						var otherParenthesisTerminal = childNodes[index];
						if (otherParenthesisTerminal.Token != null && otherParenthesisTerminal.Token.Value == otherParenthesis)
						{
							parenthesisNodes.Add(parenthesisTerminal);
							parenthesisNodes.Add(otherParenthesisTerminal);
							break;
						}
					}
				}
			}

			var oldNodes = _colorizingTransformer.HighlightParenthesis.ToArray();
			_colorizingTransformer.SetHighlightParenthesis(parenthesisNodes);

			RedrawNodes(oldNodes.Concat(parenthesisNodes));
		}

		private void ShowHideBindVariableList()
		{
			if (_sqlDocumentRepository.Statements == null)
				return;

			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			if (statement == null || statement.BindVariables.Count == 0)
			{
				_pageModel.BindVariables = null;
				_currentBindVariables.Clear();
				return;
			}

			var bindVariables = new HashSet<string>(statement.BindVariables.Select(v => v.Name));
			if (bindVariables.SetEquals(_currentBindVariables))
			{
				return;
			}

			_currentBindVariables = bindVariables;
			_pageModel.BindVariables = BuildBindVariableModels(statement.BindVariables);
		}

		private ICollection<BindVariableModel> BuildBindVariableModels(IEnumerable<BindVariableConfiguration> bindVariables)
		{
			var configuration = SqlPadConfiguration.GetConfiguration(DatabaseModel.ConnectionString.ProviderName);

			var models = new List<BindVariableModel>();
			foreach (var bindVariable in bindVariables)
			{
				var model = new BindVariableModel(bindVariable);
				model.PropertyChanged += (sender, args) => configuration.SetBindVariable(bindVariable);
				
				var storedVariable = configuration.GetBindVariable(bindVariable.Name);
				if (storedVariable != null)
				{
					model.DataType = storedVariable.DataType;
					model.Value = storedVariable.Value;
				}

				models.Add(model);
			}

			return models;
		}

		private void RedrawNodes(IEnumerable<StatementGrammarNode> nodes)
		{
			foreach (var node in nodes)
			{
				Editor.TextArea.TextView.Redraw(node.SourcePosition.IndexStart, node.SourcePosition.Length);
			}
		}

		private void TextEnteredHandler(object sender, TextCompositionEventArgs e)
		{
			if (Editor.Document.IsInUpdate)
			{
				Editor.Document.EndUpdate();
			}

			var snippets = _codeSnippetProvider.GetSnippets(_sqlDocumentRepository, Editor.Text, Editor.CaretOffset).Select(i => new CompletionData(i)).ToArray();
			if (_completionWindow == null && snippets.Length > 0)
			{
				CreateSnippetCompletionWindow(snippets);
				return;
			}

			if (e.Text != "." && e.Text != " " && e.Text != "\n" && e.Text != "\r")
			{
				if (_completionWindow != null && _completionWindow.CompletionList.ListBox.Items.Count == 0)
				{
					_completionWindow.Close();
				}
			}

			_enableCodeComplete = _completionWindow == null && e.Text.Length == 1;
		}

		private void InsertPairCharacter(string pairCharacter)
		{
			Editor.Document.Insert(Editor.CaretOffset, pairCharacter);
			Editor.CaretOffset--;
		}

		private bool PreviousPairCharacterExists(string text, char matchCharacter, char pairCharacter)
		{
			return text.Length == 1 && text[0] == matchCharacter && Editor.CaretOffset > 0 && Editor.Text[Editor.CaretOffset - 1] == pairCharacter;
		}

		private bool NextPairCharacterExists(string text, char matchCharacter, char pairCharacter)
		{
			return text.Length == 1 && text[0] == matchCharacter && Editor.Document.TextLength > Editor.CaretOffset && Editor.Text[Editor.CaretOffset] == pairCharacter;
		}

		private bool IsNextCharacterBlank()
		{
			var nextCharacter = Editor.Document.TextLength == Editor.CaretOffset ? null : (char?)Editor.Text[Editor.CaretOffset];
			return !nextCharacter.HasValue || nextCharacter == ' ' || nextCharacter == '\r' || nextCharacter == '\n' || nextCharacter == '\t';
		}

		private bool HandlePairCharacterInsertion(string text)
		{
			var pairCharacterHandled = false;

			switch (text)
			{
				case "(":
					pairCharacterHandled = IsNextCharacterBlank();
					if (pairCharacterHandled)
					{
						InsertPairCharacter("()");
					}

					break;
				case "\"":
					pairCharacterHandled = !PreviousPairCharacterExists(text, '"', '"') && IsNextCharacterBlank();
					if (pairCharacterHandled)
					{
						InsertPairCharacter("\"\"");
					}
					
					break;
				case "'":
					pairCharacterHandled = !PreviousPairCharacterExists(text, '\'', '\'') && IsNextCharacterBlank();
					if (pairCharacterHandled)
					{
						InsertPairCharacter("''");
					}

					break;
			}

			return pairCharacterHandled;
		}

		private void TextEnteringHandler(object sender, TextCompositionEventArgs e)
		{
			if (NextPairCharacterExists(e.Text, ')', ')') || NextPairCharacterExists(e.Text, '\'', '\'') || NextPairCharacterExists(e.Text, '"', '"'))
			{
				Editor.CaretOffset++;
				e.Handled = true;
				return;
			}

			if (HandlePairCharacterInsertion(e.Text))
			{
				e.Handled = true;
				return;
			}

			if (_multiNodeEditor != null)
			{
				Editor.Document.BeginUpdate();

				if (!_multiNodeEditor.Replace(e.Text))
					_multiNodeEditor = null;
			}

			if (e.Text.Length == 1 && _completionWindow != null)
			{
				if (e.Text == " " || e.Text == "\t")
				{
					// Whenever a non-letter is typed while the completion window is open,
					// insert the currently selected element.
					_completionWindow.CompletionList.RequestInsertion(e);
				}
			}
		}

		private void CreateCodeCompletionWindow(bool forcedInvokation)
		{
			CreateCompletionWindow(
				() => _codeCompletionProvider.ResolveItems(_sqlDocumentRepository, DatabaseModel, Editor.Text, Editor.CaretOffset, forcedInvokation)
					.Select(i => new CompletionData(i)));
		}

		private void CreateSnippetCompletionWindow(IEnumerable<CompletionData> items)
		{
			CreateCompletionWindow(() => items);
		}

		private void CreateCompletionWindow(Func<IEnumerable<CompletionData>> getCompletionDataFunc)
		{
			var completionWindow = new CompletionWindow(Editor.TextArea) { SizeToContent = SizeToContent.WidthAndHeight };
			var data = completionWindow.CompletionList.CompletionData;

			foreach (var item in getCompletionDataFunc())
			{
				data.Add(item);
			}

			if (data.Count == 0)
				return;
			
			_completionWindow = completionWindow;
			_completionWindow.Closed += delegate { _completionWindow = null; };

			var firstItem = (CompletionData)data.First();
			if (firstItem.Node != null)
			{
				_completionWindow.StartOffset = firstItem.Node.SourcePosition.IndexStart;
			}

			if (data.Count == 1)
			{
				_completionWindow.CompletionList.ListBox.SelectedIndex = 0;
			}

			DisableCodeCompletion();

			_completionWindow.Show();
		}

		public void ReParse()
		{
			DisableCodeCompletion();

			EditorTextChangedHandler(this, EventArgs.Empty);
		}

		private void EditorTextChangedHandler(object sender, EventArgs e)
		{
			WorkingDocument.IsModified = IsDirty;
			_pageModel.DocumentHeader = DocumentHeader;

			if (_isParsing)
			{
				if (!_timerReParse.Enabled)
				{
					_timerReParse.Start();
				}

				return;
			}

			_isParsing = true;

			if (IsParsingSynchronous)
			{
				_sqlDocumentRepository.UpdateStatements(Editor.Text);
				ParseDoneHandler();
			}
			else
			{
				var asynchronousAction =
					new Action(async () =>
					                 {
						                 await _sqlDocumentRepository.UpdateStatementsAsync(Editor.Text);
										 ParseDoneHandler();
					                 });

				asynchronousAction.Invoke();
			}
		}

		private void ParseDoneHandler()
		{
			_colorizingTransformer.SetDocumentRepository(_sqlDocumentRepository);

			Dispatcher.Invoke(ParseDoneHandlerInternal);

			_timerReParse.Stop();
		}

		private void ParseDoneHandlerInternal()
		{
			Editor.TextArea.TextView.Redraw();
			_isParsing = false;
			ParseFinished(this, EventArgs.Empty);

			ShowHideBindVariableList();

			if (_enableCodeComplete && _completionWindow == null && IsSelectedPage)
			{
				CreateCodeCompletionWindow(false);
			}
		}

		void MouseHoverHandler(object sender, MouseEventArgs e)
		{
			if (_isToolTipOpenByShortCut)
				return;

			var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
			if (!position.HasValue || _sqlDocumentRepository.Statements == null)
				return;

			var offset = Editor.Document.GetOffset(position.Value.Line, position.Value.Column);
			//var lineByOffset = Editor.Document.GetLineByOffset(offset);

			var toolTip = _toolTipProvider.GetToolTip(_sqlDocumentRepository, offset);
			if (toolTip == null)
				return;

			_toolTip.Placement = PlacementMode.Mouse;
			_toolTip.PlacementTarget = this; // required for property inheritance
			_toolTip.Content = toolTip;
			_toolTip.IsOpen = true;
			e.Handled = true;
		}

		void MouseHoverStoppedHandler(object sender, MouseEventArgs e)
		{
			if (!_isToolTipOpenByShortCut)
				_toolTip.IsOpen = false;
		}

		private void ContextMenuOpeningHandler(object sender, ContextMenuEventArgs args)
		{
			if (!PopulateContextActionMenu())
				args.Handled = true;
		}

		private void DisableCodeCompletion()
		{
			_enableCodeComplete = false;
		}

		private void BuildContextMenuItem(IContextAction action)
		{
			var menuItem =
				new MenuItem
				{
					Header = action.Name,
					Command = new ContextActionCommand(action),
					CommandParameter = Editor
				};

			Editor.ContextMenu.Items.Add(menuItem);
		}

		private void ListContextActions(object sender, ExecutedRoutedEventArgs args)
		{
			var isAnyCommandAvailable = PopulateContextActionMenu();
			if (isAnyCommandAvailable)
			{
				DisableCodeCompletion();
			}

			Editor.ContextMenu.IsOpen = isAnyCommandAvailable;
		}

		private bool PopulateContextActionMenu()
		{
			var executionContext = CommandExecutionContext.Create(Editor, _sqlDocumentRepository);
			_contextActionProvider.GetContextActions(_sqlDocumentRepository, executionContext)
				.ToList()
				.ForEach(BuildContextMenuItem);

			if (Editor.ContextMenu.Items.Count == 1)
			{
				Editor.ContextMenu.Opened += (sender, args) => ((MenuItem)Editor.ContextMenu.Items[0]).Focus();
			}

			Editor.ContextMenu.Closed += (sender, args) => Editor.ContextMenu.Items.Clear();

			Editor.ContextMenu.PlacementTarget = Editor;

			var position = Editor.TextArea.Caret.CalculateCaretRectangle().TopRight;
			Editor.ContextMenu.HorizontalOffset = position.X - 24;
			Editor.ContextMenu.VerticalOffset = position.Y - 32;

			return Editor.ContextMenu.Items.Count > 0;
		}

		private void EditorKeyUpHandler(object sender, KeyEventArgs e)
		{
			if (Editor.Document.IsInUpdate)
			{
				Editor.Document.EndUpdate();
			}

			WorkingDocument.IsModified = IsDirty;
			_pageModel.DocumentHeader = DocumentHeader;
		}

		private void EditorKeyDownHandler(object sender, KeyEventArgs e)
		{
			_isToolTipOpenByShortCut = false;

			if (_toolTip != null)
			{
				_toolTip.IsOpen = false;
			}

			if (e.Key == Key.Return || e.Key == Key.Escape)
			{
				DisableCodeCompletion();

				_multiNodeEditor = null;

				if (e.Key == Key.Escape)
				{
					_colorizingTransformer.SetHighlightSegments(null);
					Editor.TextArea.TextView.Redraw();
				}
			}

			if (e.Key == Key.Back || e.Key == Key.Delete)
			{
				DisableCodeCompletion();
			}

			if ((e.Key == Key.Back || e.Key == Key.Delete) && _multiNodeEditor != null)
			{
				Editor.Document.BeginUpdate();
				if (!_multiNodeEditor.RemoveCharacter(e.Key == Key.Back))
					_multiNodeEditor = null;
			}
			else if (e.Key == Key.Back && Editor.Document.TextLength > Editor.CaretOffset)
			{
				if (AreConsencutive('(', ')') ||
				    AreConsencutive('"', '"') ||
				    AreConsencutive('\'', '\''))
				{
					Editor.Document.Remove(Editor.CaretOffset, 1);
				}
			}
		}

		private bool AreConsencutive(char previousCharacter, char currentCharacter)
		{
			return Editor.Text[Editor.CaretOffset] == currentCharacter && Editor.Text[Editor.CaretOffset - 1] == previousCharacter;
		}

		private void ResultGridPreviewMouseWheelHandler(object sender, MouseWheelEventArgs e)
		{
			var scrollViewer = GetResultGridScrollViewer();
			if (e.Delta >= 0 || scrollViewer == null)
				return;

			if (scrollViewer.ScrollableHeight == scrollViewer.VerticalOffset && DatabaseModel.CanFetch)
			{
				FetchNextRows(sender, null);
			}
		}

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			var currentRow = (object[])ResultGrid.CurrentItem;
			if (currentRow == null || ResultGrid.CurrentColumn == null)
				return;

			var cellValue = currentRow[ResultGrid.CurrentColumn.DisplayIndex];
			var largeValue = cellValue as ILargeValue;
			if (largeValue != null)
			{
				new LargeValueEditor(ResultGrid.CurrentColumn.Header.ToString(), largeValue) { Owner = Window.GetWindow(this) }.ShowDialog();
			}
		}
	}

	internal struct ActionResult
	{
		public bool IsSuccessful { get { return Exception == null; } }
		
		public Exception Exception { get; set; }

		public TimeSpan Elapsed { get; set; }
	}
}
