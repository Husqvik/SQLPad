using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;
using Microsoft.Win32;
using SqlPad.Bookmarks;
using SqlPad.Commands;
using SqlPad.FindReplace;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;

namespace SqlPad
{
	public partial class DocumentPage : IDisposable
	{
		private const string InitialDocumentHeader = "New";
		private const int MaximumToolTipLines = 32;
		public const string FileMaskDefault = "SQL files (*.sql)|*.sql|SQL Pad files (*.sqlx)|*.sqlx|Text files(*.txt)|*.txt|All files (*.*)|*";

		private SqlDocumentRepository _sqlDocumentRepository;
		private ICodeCompletionProvider _codeCompletionProvider;
		private ICodeSnippetProvider _codeSnippetProvider;
		private IContextActionProvider _contextActionProvider;
		private IStatementFormatter _statementFormatter;
		private IToolTipProvider _toolTipProvider;
		private INavigationService _navigationService;
		private IHelpProvider _createHelpProvider;

		private MultiNodeEditor _multiNodeEditor;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;
		private CancellationTokenSource _parsingCancellationTokenSource;
		private FileSystemWatcher _documentFileWatcher;
		private DateTime _lastDocumentFileChange;
		private readonly SqlDocumentColorizingTransformer _colorizingTransformer = new SqlDocumentColorizingTransformer();
		private readonly ContextMenu _contextActionMenu = new ContextMenu { Placement = PlacementMode.Relative };

		private static readonly ColorCodeToBrushConverter TabHeaderBackgroundBrushConverter = new ColorCodeToBrushConverter();

		private bool _isParsing;
		private bool _isInitializing = true;
		private bool _isInitialParsing = true;
		private bool _enableCodeComplete;
		private bool _isToolTipOpenByShortCut;
		private bool _isToolTipOpenByCaretChange;
		private bool _gatherExecutionStatistics;
		private string _originalWorkDocumentContent = String.Empty;

		private readonly Popup _popup = new Popup();
		private readonly PageModel _pageModel;
		private readonly Timer _timerReParse = new Timer(100);
		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly List<CommandBinding> _specificCommandBindings = new List<CommandBinding>();

		private readonly SqlFoldingStrategy _foldingStrategy;
		private readonly IconMargin _iconMargin;

		private CompletionWindow _completionWindow;
		private ConnectionStringSettings _connectionString;
		private Dictionary<string, BindVariableConfiguration> _currentBindVariables = new Dictionary<string, BindVariableConfiguration>();
		
		internal IInfrastructureFactory InfrastructureFactory { get; private set; }

		internal TabItem TabItem { get; private set; }
		
		internal ContextMenu TabItemContextMenu { get { return ((ContentControl)TabItem.Header).ContextMenu; } }

		internal static bool IsParsingSynchronous { get; set; }

		public bool IsBusy
		{
			get { return _pageModel.IsRunning; }
			private set
			{
				_pageModel.IsRunning = value;
				MainWindow.NotifyTaskStatus();
			}
		}

		private static MainWindow MainWindow
		{
			get { return (MainWindow)Application.Current.MainWindow; }
		}

		internal bool IsSelectedPage
		{
			get { return Equals(((TabItem)MainWindow.DocumentTabControl.SelectedItem).Content); }
		}

		public TextEditorAdapter EditorAdapter { get; private set; }

		public WorkDocument WorkDocument { get; private set; }

		public bool IsDirty { get { return !String.Equals(Editor.Text, _originalWorkDocumentContent); } }

		public IDatabaseModel DatabaseModel { get; private set; }

		public DocumentPage(WorkDocument workDocument = null)
		{
			InitializeComponent();

			_iconMargin = new IconMargin(Editor);
			//Editor.TextArea.LeftMargins.Add(_iconMargin);
			//Editor.TextArea.LeftMargins.Add(new ModificationNotificationMargin(Editor));
			_foldingStrategy = new SqlFoldingStrategy(FoldingManager.Install(Editor.TextArea), Editor);
			_foldingStrategy.FoldingMargin.ContextMenu = (ContextMenu)Resources["FoldingActionMenu"];

			_popup.PlacementTarget = Editor.TextArea;

			_contextActionMenu.PlacementTarget = Editor;
			
			ComboBoxConnection.IsEnabled = ConfigurationProvider.ConnectionStrings.Count > 1;
			ComboBoxConnection.ItemsSource = ConfigurationProvider.ConnectionStrings;
			ComboBoxConnection.SelectedIndex = 0;

			InitializeGenericCommandBindings();

			_timerReParse.Elapsed += (sender, args) => Dispatcher.Invoke(Parse);
			_timerExecutionMonitor.Elapsed += delegate { UpdateTimerMessage(); };

			_pageModel = new PageModel(this) { DateTimeFormat = ConfigurationProvider.Configuration.ResultGrid.DateFormat };

			ConfigurationProvider.ConfigurationChanged += ConfigurationChangedHandler;

			ConfigureEditor();

			var usedConnection = ConfigurationProvider.ConnectionStrings[0];

			if (workDocument == null)
			{
				WorkDocument = new WorkDocument
				{
					ConnectionName = usedConnection.Name,
					HeaderBackgroundColorCode = Colors.White.ToString()
				};

				WorkDocumentCollection.AddDocument(WorkDocument);
				_pageModel.CurrentConnection = usedConnection;

				WorkDocument.SchemaName = _pageModel.CurrentSchema;
			}
			else
			{
				WorkDocument = workDocument;

				if (!WorkDocument.IsModified && WorkDocument.File != null && WorkDocument.File.Exists)
				{
					WorkDocument.Text = _originalWorkDocumentContent =
						WorkDocument.IsSqlx
							? WorkDocumentCollection.LoadDocumentFromFile(WorkDocument.File.FullName).Text
							: File.ReadAllText(WorkDocument.File.FullName);
					
					InitializeFileWatcher();
				}

				if (!String.IsNullOrEmpty(WorkDocument.ConnectionName))
				{
					var connectionString = ConfigurationProvider.ConnectionStrings
						.Cast<ConnectionStringSettings>()
						.FirstOrDefault(cs => cs.Name == WorkDocument.ConnectionName);

					if (connectionString != null)
					{
						usedConnection = connectionString;
					}
				}

				_pageModel.CurrentConnection = usedConnection;
				_pageModel.CurrentSchema = WorkDocument.SchemaName;
				_pageModel.EnableDatabaseOutput = WorkDocument.EnableDatabaseOutput;
				_pageModel.KeepDatabaseOutputHistory = WorkDocument.KeepDatabaseOutputHistory;
				_pageModel.HeaderBackgroundColorCode = WorkDocument.HeaderBackgroundColorCode;

				Editor.Text = WorkDocument.Text;

				if (Editor.Document.TextLength >= WorkDocument.SelectionStart)
				{
					Editor.SelectionStart = WorkDocument.SelectionStart;

					var storedSelectionEndIndex = WorkDocument.SelectionStart + WorkDocument.SelectionLength;
					var validSelectionEndIndex = storedSelectionEndIndex > Editor.Document.TextLength
						? Editor.Document.TextLength
						: storedSelectionEndIndex;

					Editor.SelectionLength = validSelectionEndIndex - WorkDocument.SelectionStart;
				}

				Editor.CaretOffset = Editor.Document.TextLength >= WorkDocument.CursorPosition
					? WorkDocument.CursorPosition
					: Editor.Document.TextLength;
			}

			Editor.FontSize = WorkDocument.FontSize;

			UpdateDocumentHeaderToolTip();

			if (String.IsNullOrEmpty(WorkDocument.DocumentTitle))
			{
				_pageModel.DocumentHeader = WorkDocument.File == null ? InitialDocumentHeader : WorkDocument.File.Name;
			}

			_pageModel.IsModified = WorkDocument.IsModified;

			DataContext = _pageModel;
			OutputViewer.DataModel = _pageModel;

			InitializeTabItem();
		}

		private void UpdateDocumentHeaderToolTip()
		{
			_pageModel.DocumentHeaderToolTip = WorkDocument.File == null
				? "Unsaved"
				: String.Format("{0}{1}Last change: {2}", WorkDocument.File.FullName, Environment.NewLine, WorkDocument.File.LastWriteTime);
		}
		
		private void InitializeFileWatcher()
		{
			_documentFileWatcher =
				new FileSystemWatcher(WorkDocument.File.DirectoryName, WorkDocument.File.Name)
				{
					EnableRaisingEvents = true,
					NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
				};

			_documentFileWatcher.Changed += DocumentFileWatcherChangedHandler;
			_documentFileWatcher.Deleted += (sender, args) => Dispatcher.BeginInvoke(new Action(() => DocumentFileWatcherDeletedHandler(args.FullPath)));
		}

		private void UpdateTimerMessage()
		{
			var cancellationTokenSource = _statementExecutionCancellationTokenSource;
			_pageModel.UpdateTimerMessage(_stopWatch.Elapsed, cancellationTokenSource != null && cancellationTokenSource.IsCancellationRequested);
		}

		private void DocumentFileWatcherDeletedHandler(string fullFileName)
		{
			MainWindow.DocumentTabControl.SelectedItem = TabItem;

			var message = String.Format("File '{0}' has been deleted. Do you want to close the document? ", fullFileName);
			if (MessageBox.Show(MainWindow, message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.No)
			{
				return;
			}

			MainWindow.CloseDocument(this);
		}

		private void DocumentFileWatcherChangedHandler(object sender, FileSystemEventArgs fileSystemEventArgs)
		{
			var writeTime = File.GetLastWriteTimeUtc(fileSystemEventArgs.FullPath);
			if (writeTime == _lastDocumentFileChange)
			{
				return;
			}

			Thread.Sleep(40);

			_lastDocumentFileChange = writeTime;

			Dispatcher.Invoke(
				() =>
				{
					MainWindow.DocumentTabControl.SelectedItem = TabItem;

					var message = String.Format("File '{0}' has been changed by another application. Do you want to load new content? ", fileSystemEventArgs.FullPath);
					if (MessageBox.Show(MainWindow, message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
					{
						Editor.Load(fileSystemEventArgs.FullPath);
					}
				});
		}

		private void ConfigurationChangedHandler(object sender, EventArgs eventArgs)
		{
			_pageModel.DateTimeFormat = ConfigurationProvider.Configuration.ResultGrid.DateFormat;
		}

		private void InitializeTabItem()
		{
			var header =
				new EditableTabHeaderControl
				{
					ContextMenu = CreateTabItemHeaderContextMenu(),
					Template = (ControlTemplate)Resources["EditableTabHeaderControlTemplate"]
				};

			var contentBinding = new Binding("DocumentHeader") { Source = _pageModel, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Mode = BindingMode.TwoWay };
			header.SetBinding(ContentProperty, contentBinding);
			var isModifiedBinding = new Binding("IsModified") { Source = _pageModel, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(EditableTabHeaderControl.IsModifiedProperty, isModifiedBinding);
			var isRunningBinding = new Binding("IsRunning") { Source = _pageModel, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(EditableTabHeaderControl.IsRunningProperty, isRunningBinding);
			var toolTipBinding = new Binding("DocumentHeaderToolTip") { Source = _pageModel, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(ToolTipProperty, toolTipBinding);

			TabItem =
				new TabItem
				{
					Content = this,
					Header = header,
					Template = (ControlTemplate)Application.Current.Resources["TabItemControlTemplate"]
				};

			var backgroundBinding = new Binding("HeaderBackgroundColorCode") { Source = _pageModel, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Converter = TabHeaderBackgroundBrushConverter };
			TabItem.SetBinding(BackgroundProperty, backgroundBinding);
		}

		private ContextMenu CreateTabItemHeaderContextMenu()
		{
			var contextMenu = new ContextMenu();
			var menuItemSave = new MenuItem
			{
				Header = "Save",
				Command = GenericCommands.Save,
			};

			contextMenu.Items.Add(menuItemSave);
			contextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.Save, SaveCommandExecutedHandler));

			var menuItemSaveAs = new MenuItem
			{
				Header = "Save as...",
				Command = GenericCommands.SaveAs,
			};

			contextMenu.Items.Add(menuItemSaveAs);
			contextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.SaveAs, SaveAsCommandExecutedHandler));

			var menuItemOpenContainingFolder = new MenuItem
			{
				Header = "Open Containing Folder",
				Command = GenericCommands.OpenContainingFolder,
				CommandParameter = this
			};

			contextMenu.Items.Add(menuItemOpenContainingFolder);
			contextMenu.CommandBindings.Add(new CommandBinding(GenericCommands.OpenContainingFolder, OpenContainingFolderCommandExecutedHandler, (sender, args) => args.CanExecute = WorkDocument.File != null));

			var menuItemClose = new MenuItem
			{
				Header = "Close",
				Command = GenericCommands.CloseDocument,
				CommandParameter = this
			};

			contextMenu.Items.Add(menuItemClose);

			var menuItemCloseAllButThis = new MenuItem
			{
				Header = "Close All But This",
				Command = GenericCommands.CloseAllDocumentsButThis,
				CommandParameter = this
			};

			contextMenu.Items.Add(menuItemCloseAllButThis);
			
			contextMenu.Items.Add(new Separator());
			var colorPickerMenuItem = (MenuItem)Resources["ColorPickerMenuItem"];
			colorPickerMenuItem.DataContext = _pageModel;
			contextMenu.Items.Add(colorPickerMenuItem);
			
			return contextMenu;
		}

		private void ConfigureEditor()
		{
			//Editor.Options.ShowColumnRuler = true;

			Editor.TextArea.SelectionCornerRadius = 0;
			Editor.TextArea.TextView.LineTransformers.Add(_colorizingTransformer);

			Editor.TextArea.TextEntering += TextEnteringHandler;
			Editor.TextArea.TextEntered += TextEnteredHandler;
			Editor.Document.Changing += DocumentChangingHandler;

			Editor.TextArea.Caret.PositionChanged += CaretPositionChangedHandler;

			EditorAdapter = new TextEditorAdapter(Editor);
		}

		private void DocumentChangingHandler(object sender, DocumentChangeEventArgs args)
		{
			if (args.InsertedText.IndexOfAny(TextSegment.Separators, 0, args.InsertionLength) != -1 ||
			    args.RemovedText.IndexOfAny(TextSegment.Separators, 0, args.RemovalLength) != -1)
			{
				DisableCodeCompletion();
			}
		}

		private void InitializeSpecificCommandBindings()
		{
			foreach (var existingBinding in _specificCommandBindings)
			{
				Editor.TextArea.DefaultInputHandler.Editing.CommandBindings.Remove(existingBinding);
			}

			_specificCommandBindings.Clear();

			foreach (var handler in InfrastructureFactory.CommandFactory.CommandHandlers)
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
			_connectionString = connectionString;

			if (DatabaseModel != null)
			{
				DatabaseModel.Dispose();
			}

			_pageModel.ResetSchemas();

			var connectionConfiguration = ConfigurationProvider.GetConnectionCofiguration(_connectionString.Name);
			_pageModel.ProductionLabelVisibility = connectionConfiguration.IsProduction ? Visibility.Visible : Visibility.Collapsed;
			InfrastructureFactory = connectionConfiguration.InfrastructureFactory;
			_codeCompletionProvider = InfrastructureFactory.CreateCodeCompletionProvider();
			_codeSnippetProvider = InfrastructureFactory.CreateSnippetProvider();
			_contextActionProvider = InfrastructureFactory.CreateContextActionProvider();
			_statementFormatter = InfrastructureFactory.CreateSqlFormatter(new SqlFormatterOptions());
			_toolTipProvider = InfrastructureFactory.CreateToolTipProvider();
			_navigationService = InfrastructureFactory.CreateNavigationService();
			_createHelpProvider = InfrastructureFactory.CreateHelpProvider();

			_colorizingTransformer.SetParser(InfrastructureFactory.CreateParser());

			InitializeSpecificCommandBindings();

			DatabaseModel = InfrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings[_connectionString.Name]);
			_pageModel.SchemaLabel = InfrastructureFactory.SchemaLabel;
			_sqlDocumentRepository = new SqlDocumentRepository(InfrastructureFactory.CreateParser(), InfrastructureFactory.CreateStatementValidator(), DatabaseModel);
			_iconMargin.DocumentRepository = _sqlDocumentRepository;

			OutputViewer.Setup(this);

			DatabaseModel.Initialized += DatabaseModelInitializedHandler;
			DatabaseModel.Disconnected += DatabaseModelInitializationFailedHandler;
			DatabaseModel.InitializationFailed += DatabaseModelInitializationFailedHandler;
			DatabaseModel.RefreshStarted += DatabaseModelRefreshStartedHandler;
			DatabaseModel.RefreshCompleted += DatabaseModelRefreshCompletedHandler;

			DatabaseModel.Initialize();

			ReParse();
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
			Process.Start("explorer.exe", "/select," + WorkDocument.File.FullName);
		}

		public bool Save()
		{
			if (WorkDocument.File == null)
				return SaveAs();

			SafeActionWithUserError(SaveDocument);
			return true;
		}

		public bool SaveAs()
		{
			var dialog = new SaveFileDialog { Filter = FileMaskDefault, OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return false;
			}

			WorkDocument.DocumentFileName = dialog.FileName;
			var documentTitle = WorkDocument.DocumentTitle;
			var isModified = WorkDocument.IsModified;

			if (!SafeActionWithUserError(SaveDocument))
			{
				WorkDocument.DocumentFileName = null;
				WorkDocument.DocumentTitle = documentTitle;
				WorkDocument.IsModified = isModified;
			}
			else
			{
				_pageModel.DocumentHeaderToolTip = WorkDocument.File.FullName;
				InitializeFileWatcher();
				WorkDocumentCollection.AddRecentDocument(WorkDocument);
			}
			
			SaveWorkingDocument();
			WorkDocumentCollection.Save();
			
			return true;
		}

		public void SaveWorkingDocument()
		{
			if (_isInitializing)
			{
				return;
			}

			WorkDocument.Text = Editor.Text;
			WorkDocument.CursorPosition = Editor.CaretOffset;
			WorkDocument.FontSize = Editor.FontSize;
			WorkDocument.SelectionStart = Editor.SelectionStart;
			WorkDocument.SelectionLength = Editor.SelectionLength;
			WorkDocument.EnableDatabaseOutput = DatabaseModel.EnableDatabaseOutput;

			var textView = Editor.TextArea.TextView;
			WorkDocument.VisualLeft = textView.ScrollOffset.X;
			WorkDocument.VisualTop = textView.ScrollOffset.Y;

			_foldingStrategy.Store(WorkDocument);

			if (RowDefinitionEditor.ActualHeight > 0)
			{
				WorkDocument.EditorGridRowHeight = RowDefinitionEditor.ActualHeight;
			}

			if (_pageModel.BindVariableListVisibility == Visibility.Visible)
			{
				WorkDocument.EditorGridColumnWidth = ColumnDefinitionEditor.ActualWidth;
			}

			if (_pageModel.CurrentConnection != null)
			{
				WorkDocument.ConnectionName = _pageModel.CurrentConnection.Name;
				WorkDocument.SchemaName = _pageModel.CurrentSchema;
			}

			WorkDocument.KeepDatabaseOutputHistory = _pageModel.KeepDatabaseOutputHistory;
		}

		private void WithDisabledFileWatcher(Action action)
		{
			if (_documentFileWatcher != null)
			{
				_documentFileWatcher.EnableRaisingEvents = false;

				try
				{
					action();
				}
				finally
				{
					_documentFileWatcher.EnableRaisingEvents = true;
				}
			}
			else
			{
				action();
			}
		}

		private void SaveDocument()
		{
			if (WorkDocument.DocumentTitle == InitialDocumentHeader)
			{
				_pageModel.DocumentHeader = WorkDocument.File.Name;
			}

			WithDisabledFileWatcher(SaveDocumentInternal);
		}

		private void SaveDocumentInternal()
		{
			if (WorkDocument.IsSqlx)
			{
				SaveWorkingDocument();
				WorkDocumentCollection.SaveDocumentAsFile(WorkDocument);
			}
			else
			{
				Editor.Save(WorkDocument.File.FullName);
			}

			_pageModel.IsModified = WorkDocument.IsModified = false;
			_originalWorkDocumentContent = Editor.Text;

			UpdateDocumentHeaderToolTip();
		}

		private void DatabaseModelInitializedHandler(object sender, EventArgs args)
		{
			_pageModel.ConnectProgressBarVisibility = Visibility.Collapsed;
			Dispatcher.Invoke(
				() =>
				{
					_pageModel.SetSchemas(DatabaseModel.Schemas);
					_pageModel.CurrentSchema = DatabaseModel.CurrentSchema;
				});
		}

		private void DatabaseModelInitializationFailedHandler(object sender, DatabaseModelConnectionErrorArgs args)
		{
			_pageModel.ConnectProgressBarVisibility = Visibility.Collapsed;
			_pageModel.ConnectionErrorMessage = args.Exception.Message;
			_pageModel.ReconnectOptionVisibility = Visibility.Visible;
		}

		private void ButtonReconnectClickHandler(object sender, RoutedEventArgs e)
		{
			_pageModel.ReconnectOptionVisibility = Visibility.Collapsed;
			_pageModel.ConnectProgressBarVisibility = Visibility.Visible;

			if (!DatabaseModel.IsInitialized)
			{
				DatabaseModel.Initialize();
			}
		}

		private void DatabaseModelRefreshStartedHandler(object sender, EventArgs args)
		{
			Dispatcher.Invoke(() => ProgressBar.IsIndeterminate = true);
		}

		private void DatabaseModelRefreshCompletedHandler(object sender, EventArgs eventArgs)
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

			Editor.TextArea.CommandBindings.Add(new CommandBinding(GenericCommands.DuplicateText, GenericCommandHandler.DuplicateText));
			Editor.TextArea.CommandBindings.Add(new CommandBinding(GenericCommands.BlockComment, GenericCommandHandler.HandleBlockComments));
			Editor.TextArea.CommandBindings.Add(new CommandBinding(GenericCommands.LineComment, GenericCommandHandler.HandleLineComments));
			Editor.TextArea.CommandBindings.Add(new CommandBinding(GenericCommands.MultiNodeEdit, EditMultipleNodes));

			Editor.TextArea.CommandBindings.Add(new CommandBinding(DiagnosticCommands.ShowTokenCommand, ShowTokenCommandExecutionHandler));
		}

		private void CanExecuteShowCodeCompletionHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = _sqlDocumentRepository.StatementText == Editor.Text;
		}

		private void ShowCodeCompletionOptions(object sender, ExecutedRoutedEventArgs e)
		{
			CreateCodeCompletionWindow(true, Editor.CaretOffset);
		}

		private void CanExecuteCancelUserActionHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = _statementExecutionCancellationTokenSource != null;
		}

		private void CancelUserActionHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Trace.WriteLine("Action is about to cancel. ");
			_statementExecutionCancellationTokenSource.Cancel();
		}

		private void ShowTokenCommandExecutionHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var tokens = InfrastructureFactory.CreateTokenReader(_sqlDocumentRepository.StatementText).GetTokens(true).ToArray();
			var message = "Parsed: " + String.Join(", ", tokens.Where(t => t.CommentType == CommentType.None).Select(t => "{" + t.Value + "}"));
			message += Environment.NewLine + "Comments: " + String.Join(", ", tokens.Where(t => t.CommentType != CommentType.None).Select(t => "{" + t.Value + "}"));
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

		private void CanFetchAllRowsHandler(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.CanExecute = CanFetchNextRows();
		}

		private async void FetchAllRowsHandler(object sender, EventArgs args)
		{
			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				while (!_statementExecutionCancellationTokenSource.IsCancellationRequested && DatabaseModel.CanFetch)
				{
					await FetchNextRows();
				}

				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
		}

		private bool CanFetchNextRows()
		{
			return !IsBusy && DatabaseModel.CanFetch && !DatabaseModel.IsExecuting;
		}

		private async Task FetchNextRows()
		{
			Task<IReadOnlyList<object[]>> innerTask = null;
			var batchSize = StatementExecutionModel.DefaultRowBatchSize - _pageModel.ResultRowItems.Count % StatementExecutionModel.DefaultRowBatchSize;
			var exception = await SafeActionAsync(() => innerTask = DatabaseModel.FetchRecords(batchSize).EnumerateAsync(_statementExecutionCancellationTokenSource.Token));

			if (exception != null)
			{
				Messages.ShowError(MainWindow, exception.Message);
			}
			else
			{
				AppendRows(innerTask.Result);

				if (_gatherExecutionStatistics)
				{
					_pageModel.SessionExecutionStatistics.MergeWith(await DatabaseModel.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				}
			}
		}

		private void AppendRows(IEnumerable<object[]> rows)
		{
			_pageModel.ResultRowItems.AddRange(rows);
			
			TextMoreRowsExist.Visibility = DatabaseModel.CanFetch ? Visibility.Visible : Visibility.Collapsed;
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
			if (offset == null)
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
			if (functionOverloads.Count == 0)
			{
				return;
			}

			_popup.Child = new FunctionOverloadList { FunctionOverloads = functionOverloads, FontFamily = new FontFamily("Segoe UI") }.AsPopupChild();
			_isToolTipOpenByShortCut = true;

			var rectangle = Editor.TextArea.Caret.CalculateCaretRectangle();
			_popup.Placement = PlacementMode.Relative;
			_popup.HorizontalOffset = rectangle.Left - Editor.TextArea.TextView.HorizontalOffset;
			_popup.VerticalOffset = rectangle.Top - Editor.TextArea.TextView.VerticalOffset + Editor.TextArea.TextView.DefaultLineHeight;
			_popup.IsOpen = true;
		}

		private void CanExecuteDatabaseCommandHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			if (IsBusy || !DatabaseModel.IsInitialized || DatabaseModel.IsExecuting || _sqlDocumentRepository.StatementText != Editor.Text)
				return;

			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			args.CanExecute = statement != null && statement.RootNode != null && statement.RootNode.FirstTerminalNode != null;
		}

		private void ExecuteDatabaseCommandWithActualExecutionPlanHandler(object sender, ExecutedRoutedEventArgs args)
		{
			_gatherExecutionStatistics = true;
			ExecuteDatabaseCommandHandlerInternal();
		}

		private void ExecuteDatabaseCommandHandler(object sender, ExecutedRoutedEventArgs args)
		{
			_gatherExecutionStatistics = false;
			ExecuteDatabaseCommandHandlerInternal();
		}

		private async void ExecuteDatabaseCommandHandlerInternal()
		{
			IsBusy = true;
			
			var executionModel = BuildStatementExecutionModel();

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				await ExecuteDatabaseCommand(executionModel);
				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
		}

		private StatementExecutionModel BuildStatementExecutionModel()
		{
			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);

			var executionModel =
				new StatementExecutionModel
				{
					Statement = statement,
					GatherExecutionStatistics = _gatherExecutionStatistics
				};
			
			if (Editor.SelectionLength > 0)
			{
				executionModel.StatementText = Editor.SelectedText;
				executionModel.BindVariables = _pageModel.BindVariables.Where(c => c.BindVariable.Nodes.Any(n => n.SourcePosition.IndexStart >= Editor.SelectionStart && n.SourcePosition.IndexEnd + 1 <= Editor.SelectionStart + Editor.SelectionLength)).ToArray();
			}
			else
			{
				executionModel.StatementText = statement.RootNode.GetText(Editor.Text);
				executionModel.BindVariables = _pageModel.BindVariables;
			}

			return executionModel;
		}

		private void InitializeViewBeforeCommandExecution()
		{
			TextMoreRowsExist.Visibility = Visibility.Collapsed;

			OutputViewer.Initialize();
		}

		private async Task ExecuteDatabaseCommand(StatementExecutionModel executionModel)
		{
			InitializeViewBeforeCommandExecution();

			Task<StatementExecutionResult> innerTask = null;
			var actionResult = await SafeTimedActionAsync(() => innerTask = DatabaseModel.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

			_pageModel.TransactionControlVisibity = DatabaseModel.HasActiveTransaction ? Visibility.Visible : Visibility.Collapsed;

			if (!actionResult.IsSuccessful)
			{
				Messages.ShowError(MainWindow, actionResult.Exception.Message);
				return;
			}

			if (!innerTask.Result.ExecutedSuccessfully)
			{
				_pageModel.NotifyExecutionCanceled();
				return;
			}

			_pageModel.UpdateTimerMessage(actionResult.Elapsed, false);
			_pageModel.WriteDatabaseOutput(innerTask.Result.DatabaseOutput);

			if (_gatherExecutionStatistics)
			{
				await OutputViewer.ExecutionPlanViewer.ShowActualAsync(_statementExecutionCancellationTokenSource.Token);
				_pageModel.ExecutionPlanAvailable = Visibility.Visible;
				_pageModel.SessionExecutionStatistics.MergeWith(await DatabaseModel.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
				OutputViewer.SelectPreviousTab();
			}
			else if (OutputViewer.IsPreviousTabAlwaysVisible)
			{
				OutputViewer.SelectPreviousTab();
			}

			if (innerTask.Result.CompilationErrors.Count > 0)
			{
				var lineOffset = Editor.GetLineNumberByOffset(executionModel.Statement.SourcePosition.IndexStart);
				foreach (var error in innerTask.Result.CompilationErrors)
				{
					error.Line += lineOffset;
					_pageModel.CompilationErrors.Add(error);
				}

				OutputViewer.ShowCompilationErrors();
			}

			if (innerTask.Result.ColumnHeaders.Count == 0)
			{
				if (innerTask.Result.AffectedRowCount == -1)
				{
					_pageModel.StatementExecutedSuccessfullyStatusMessageVisibility = Visibility.Visible;
				}
				else
				{
					_pageModel.AffectedRowCount = innerTask.Result.AffectedRowCount;
				}

				return;
			}

			OutputViewer.Initialize(innerTask.Result);

			AppendRows(innerTask.Result.InitialResultSet);
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

		public void Dispose()
		{
			ConfigurationProvider.ConfigurationChanged -= ConfigurationChangedHandler;
			MainWindow.DocumentTabControl.SelectionChanged -= DocumentTabControlSelectionChangedHandler;
			Application.Current.Deactivated -= ApplicationDeactivatedHandler;

			if (_documentFileWatcher != null)
			{
				_documentFileWatcher.Dispose();
			}

			TabItemContextMenu.CommandBindings.Clear();

			_timerReParse.Stop();
			_timerReParse.Dispose();
			_timerExecutionMonitor.Stop();
			_timerExecutionMonitor.Dispose();

			if (_parsingCancellationTokenSource != null)
			{
				_parsingCancellationTokenSource.Dispose();
			}

			if (DatabaseModel != null)
			{
				DatabaseModel.Dispose();
			}
		}

		private void NavigateToNextError(object sender, ExecutedRoutedEventArgs args)
		{
			NavigateToError(nodes => nodes.Where(n => n.SourcePosition.IndexStart > Editor.CaretOffset).OrderBy(n => n.SourcePosition.IndexStart));
		}

		private void NavigateToPreviousError(object sender, ExecutedRoutedEventArgs args)
		{
			NavigateToError(nodes => nodes.Where(n => n.SourcePosition.IndexEnd < Editor.CaretOffset).OrderByDescending(n => n.SourcePosition.IndexStart));
		}

		private void NavigateToError(Func<IEnumerable<StatementGrammarNode>, IOrderedEnumerable<StatementGrammarNode>> getOrderedNodesFunction)
		{
			if (_sqlDocumentRepository.StatementText != Editor.Text)
			{
				return;
			}

			var sourceNodes = _sqlDocumentRepository.ValidationModels.Values
				.SelectMany(vm => vm.Errors)
				.Select(e => e.Node);

			var error = getOrderedNodesFunction(sourceNodes).FirstOrDefault();
			if (error != null)
			{
				Editor.CaretOffset = error.SourcePosition.IndexStart;
				Editor.ScrollToCaret();
			}
		}

		private void FindUsages(object sender, ExecutedRoutedEventArgs args)
		{
			var findUsagesCommandHandler = InfrastructureFactory.CommandFactory.FindUsagesCommandHandler;
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
				MultiNodeEditor.TryCreateMultiNodeEditor(Editor, InfrastructureFactory.CreateMultiNodeEditorDataProvider(), DatabaseModel, out _multiNodeEditor);
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
				MainWindow.DocumentTabControl.SelectionChanged += DocumentTabControlSelectionChangedHandler;
				Application.Current.Deactivated += ApplicationDeactivatedHandler;

				if (WorkDocument.EditorGridRowHeight > 0)
				{
					RowDefinitionEditor.Height = new GridLength(WorkDocument.EditorGridRowHeight);
				}

				if (WorkDocument.EditorGridColumnWidth > 0)
				{
					ColumnDefinitionEditor.Width = new GridLength(WorkDocument.EditorGridColumnWidth);
				}
				
				Editor.ScrollToVerticalOffset(WorkDocument.VisualTop);
				Editor.ScrollToHorizontalOffset(WorkDocument.VisualLeft);
				_isInitializing = false;
			}

			Editor.Focus();
			
			if (!String.IsNullOrEmpty(Editor.Text))
			{
				ReParse();
			}
		}

		private void ApplicationDeactivatedHandler(object sender, EventArgs eventArgs)
		{
			_popup.IsOpen = false;
		}

		private void DocumentTabControlSelectionChangedHandler(object sender, SelectionChangedEventArgs selectionChangedEventArgs)
		{
			_popup.IsOpen = false;
			_isToolTipOpenByCaretChange = false;
			_isToolTipOpenByShortCut = false;
		}

		private void CaretPositionChangedHandler(object sender, EventArgs eventArgs)
		{
			EditorNavigationService.RegisterDocumentCursorPosition(WorkDocument, Editor.CaretOffset);

			_isToolTipOpenByCaretChange = false;

			CloseToolTipWhenNotOpenByShortCutOrCaretChange();

			var parenthesisNodes = new List<StatementGrammarNode>();

			if (!_isParsing)
			{
				ShowHideBindVariableList();

				var parenthesisTerminal = _sqlDocumentRepository.Statements == null
					? null
					: _sqlDocumentRepository.ExecuteStatementAction(s => s.GetTerminalAtPosition(Editor.CaretOffset, n => n.Token.Value.In("(", ")", "[", "]", "{", "}")));

				if (parenthesisTerminal != null)
				{
					var childNodes = parenthesisTerminal.ParentNode.ChildNodes;
					var index = parenthesisTerminal.ParentNode.IndexOf(parenthesisTerminal);
					var increment = parenthesisTerminal.Token.Value.In("(", "[", "{") ? 1 : -1;
					var otherParenthesis = GetOppositeParenthesisOrBracket(parenthesisTerminal.Token.Value);

					while (0 <= index && index < childNodes.Count)
					{
						index += increment;

						if (index < 0 || index >= childNodes.Count)
						{
							break;
						}

						var otherParenthesisTerminal = childNodes[index];
						if (otherParenthesisTerminal.Token != null && otherParenthesisTerminal.Token.Value == otherParenthesis)
						{
							parenthesisNodes.Add(parenthesisTerminal);
							parenthesisNodes.Add(otherParenthesisTerminal);

							var scrollOffset = Editor.TextArea.TextView.ScrollOffset;
							var position = Editor.TextArea.TextView.GetPosition(new Point(scrollOffset.X, scrollOffset.Y));
							var offset = position == null ? Editor.Document.TextLength : Editor.Document.GetOffset(position.Value.Location);
							var firstVisibleLine = Editor.Document.GetLineByOffset(offset);

							if (increment == -1 && otherParenthesisTerminal.SourcePosition.IndexStart < firstVisibleLine.Offset)
							{
								var otherParenthesisLine = Editor.Document.GetLineByOffset(otherParenthesisTerminal.SourcePosition.IndexStart);

								var toolTipBuilder = new StringBuilder();
								var previousTextLine = otherParenthesisLine.PreviousLine;
								while (previousTextLine != null)
								{
									if (previousTextLine.Length > 0)
									{
										toolTipBuilder.AppendLine(Editor.Document.GetText(previousTextLine));
										break;
									}

									previousTextLine = previousTextLine.PreviousLine;
								}

								var lastLine = Editor.Document.GetText(otherParenthesisLine.Offset, otherParenthesisTerminal.SourcePosition.IndexStart + 1 - otherParenthesisLine.Offset);
								toolTipBuilder.Append(lastLine);

								_popup.Child = new TextBox { FontFamily = Editor.FontFamily, FontSize = Editor.FontSize, Text = toolTipBuilder.ToString(), IsReadOnly = true }.AsPopupChild();
								_popup.Placement = PlacementMode.Relative;
								_popup.HorizontalOffset = Editor.TextArea.LeftMargins.Sum(m => m.DesiredSize.Width);
								_popup.VerticalOffset = -28;
								_popup.IsOpen = true;

								_isToolTipOpenByCaretChange = true;
							}

							break;
						}
					}
				}
			}

			var oldParenthesisNodes = _colorizingTransformer.HighlightParenthesis.ToArray();
			_colorizingTransformer.SetHighlightParenthesis(parenthesisNodes);

			RedrawNodes(oldParenthesisNodes.Concat(parenthesisNodes));
		}

		private string GetOppositeParenthesisOrBracket(string parenthesisOrBracket)
		{
			switch (parenthesisOrBracket)
			{
				case "(":
					return ")";
				case ")":
					return "(";
				case "[":
					return "]";
				case "]":
					return "[";
				case "{":
					return "}";
				case "}":
					return "{";
				default:
					throw new ArgumentException("invalid parenthesis symbol", "parenthesisOrBracket");
			}
		}

		private void ShowHideBindVariableList()
		{
			if (_sqlDocumentRepository.Statements == null)
				return;

			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			if (statement == null || statement.BindVariables.Count == 0)
			{
				_pageModel.BindVariables = new BindVariableModel[0];
				_currentBindVariables.Clear();
				return;
			}

			if (ApplyBindVariables(statement))
			{
				return;
			}

			_currentBindVariables = statement.BindVariables.ToDictionary(v => v.Name, v => v);
			_pageModel.BindVariables = BuildBindVariableModels(statement.BindVariables);
		}

		private bool ApplyBindVariables(StatementBase statement)
		{
			var matchedCount = 0;
			foreach (var statementVariable in statement.BindVariables)
			{
				BindVariableConfiguration currentVariable;
				if (_currentBindVariables.TryGetValue(statementVariable.Name, out currentVariable))
				{
					matchedCount++;
					statementVariable.DataType = currentVariable.DataType;
					statementVariable.Value = currentVariable.Value;
				}
			}

			return matchedCount == _currentBindVariables.Count && matchedCount == statement.BindVariables.Count;
		}

		private ICollection<BindVariableModel> BuildBindVariableModels(IEnumerable<BindVariableConfiguration> bindVariables)
		{
			var configuration = WorkDocumentCollection.GetProviderConfiguration(_connectionString.ProviderName);

			var models = new List<BindVariableModel>();
			foreach (var bindVariable in bindVariables)
			{
				var model = new BindVariableModel(bindVariable);
				model.PropertyChanged += (sender, args) => configuration.SetBindVariable(model.BindVariable);
				
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
				var startOffset = snippets[0].Snippet.SourceToReplace.IndexStart;
				CreateCompletionWindow(snippets, startOffset);

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
					pairCharacterHandled = !PreviousPairCharacterExists(text, '"', '"') && IsNextCharacterBlank() && _sqlDocumentRepository.CanAddPairCharacter(Editor.CaretOffset, '"');
					if (pairCharacterHandled)
					{
						InsertPairCharacter("\"\"");
					}
					
					break;
				case "'":
					pairCharacterHandled = !PreviousPairCharacterExists(text, '\'', '\'') && IsNextCharacterBlank() && _sqlDocumentRepository.CanAddPairCharacter(Editor.CaretOffset, '\'');
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

			if (e.Text.Length == 1 && _completionWindow != null && e.Text == "\t")
			{
				_completionWindow.CompletionList.RequestInsertion(e);
			}
		}

		private async void CreateCodeCompletionWindow(bool forcedInvokation, int caretOffset)
		{
			var items = await Task.Factory.StartNew(() => _codeCompletionProvider.ResolveItems(_sqlDocumentRepository, DatabaseModel, caretOffset, forcedInvokation)
				.Select(i => new CompletionData(i))
				.ToArray());

			if (_sqlDocumentRepository.StatementText != Editor.Text)
			{
				return;
			}

			CreateCompletionWindow(items, null);
		}

		private void CreateCompletionWindow(ICollection<CompletionData> items, int? startOffset)
		{
			if (items.Count == 0)
			{
				return;
			}

			var completionWindow =
				new CompletionWindow(Editor.TextArea)
				{
					SizeToContent = SizeToContent.WidthAndHeight,
					ResizeMode = ResizeMode.NoResize
				};

			var listItems = completionWindow.CompletionList.CompletionData;

			listItems.AddRange(items);
			
			_completionWindow = completionWindow;
			_completionWindow.Closed += delegate { _completionWindow = null; };
			_completionWindow.SizeChanged += delegate { if (_completionWindow.MinWidth < _completionWindow.Width) _completionWindow.MinWidth = _completionWindow.Width; };

			var firstItem = (CompletionData)listItems[0];
			if (firstItem.Node != null)
			{
				_completionWindow.StartOffset = firstItem.Node.SourcePosition.IndexStart;
			}
			else if (startOffset != null)
			{
				_completionWindow.StartOffset = startOffset.Value;
			}

			UpdateCompletionItemHighlight();

			if (listItems.Count == 1)
			{
				_completionWindow.CompletionList.ListBox.SelectedIndex = 0;
			}

			DisableCodeCompletion();

			_completionWindow.Show();
		}

		public void ReParse()
		{
			if (_isInitializing)
			{
				return;
			}

			DisableCodeCompletion();

			Parse();
		}

		private void EditorTextChangedHandler(object sender, EventArgs e)
		{
			if (_isInitializing)
			{
				return;
			}

			CheckDocumentModified();

			Parse();

			UpdateCompletionItemHighlight();
		}

		private void UpdateCompletionItemHighlight()
		{
			if (_completionWindow == null)
			{
				return;
			}
			
			var length = _completionWindow.EndOffset - _completionWindow.StartOffset;
			if (_completionWindow.StartOffset + length > Editor.Document.TextLength)
			{
				length = Editor.Document.TextLength - _completionWindow.StartOffset;
			}

			var textToHighlight = Editor.Document.GetText(_completionWindow.StartOffset, length);

			foreach (CompletionData item in _completionWindow.CompletionList.CompletionData)
			{
				item.Highlight(textToHighlight);
			}
		}

		private void CheckDocumentModified()
		{
			_pageModel.IsModified = WorkDocument.IsModified = IsDirty;
		}

		private void Parse()
		{
			if (_isParsing)
			{
				_parsingCancellationTokenSource.Cancel();

				if (!_timerReParse.Enabled)
				{
					_timerReParse.Start();
				}

				return;
			}

			if (_parsingCancellationTokenSource != null && _parsingCancellationTokenSource.IsCancellationRequested)
			{
				_parsingCancellationTokenSource.Dispose();
			}
			
			_parsingCancellationTokenSource = new CancellationTokenSource(); 

			_timerReParse.Stop();
			_isParsing = true;

			if (IsParsingSynchronous)
			{
				_sqlDocumentRepository.UpdateStatements(Editor.Text);
				ParseDoneHandler(true);
			}
			else
			{
				_sqlDocumentRepository.UpdateStatementsAsync(Editor.Text, _parsingCancellationTokenSource.Token)
					.ContinueWith(t => ParseDoneHandler(t.Status == TaskStatus.RanToCompletion));
			}
		}

		private void ParseDoneHandler(bool completedSuccessfully)
		{
			if (!completedSuccessfully)
			{
				_isParsing = false;
				return;
			}

			_colorizingTransformer.SetDocumentRepository(_sqlDocumentRepository);

			Dispatcher.Invoke(ParseDoneUiHandler);
		}

		private void ParseDoneUiHandler()
		{
			var hasTextChanged = String.CompareOrdinal(_sqlDocumentRepository.StatementText, Editor.Text) != 0;
			if (!hasTextChanged)
			{
				_foldingStrategy.UpdateFoldings(_sqlDocumentRepository.Statements);
			}

			if (_isInitialParsing)
			{
				_foldingStrategy.Restore(WorkDocument);
				_isInitialParsing = false;
			}

			_isParsing = false;

			if (hasTextChanged)
			{
				return;
			}

			Editor.TextArea.TextView.Redraw();

			ShowHideBindVariableList();

			if (_enableCodeComplete && _completionWindow == null && IsSelectedPage)
			{
				CreateCodeCompletionWindow(false, Editor.CaretOffset);
			}
		}

		void MouseHoverHandler(object sender, MouseEventArgs e)
		{
			if (_isToolTipOpenByShortCut || _isToolTipOpenByCaretChange || _popup.IsOpen)
			{
				return;
			}

			var visualPosition = e.GetPosition(Editor);
			var position = Editor.GetPositionFromPoint(visualPosition);
			if (!position.HasValue || _sqlDocumentRepository.Statements == null)
			{
				return;
			}

			var visualLine = Editor.TextArea.TextView.GetVisualLine(position.Value.Line);
			var textLine = visualLine.GetTextLine(position.Value.VisualColumn, position.Value.IsAtEndOfLine);
			var textVisualPosition = e.GetPosition(Editor.TextArea.TextView);
			if (textVisualPosition.X > textLine.Width)
			{
				return;
			}

			var offset = Editor.Document.GetOffset(position.Value.Line, position.Value.Column);
			var foldingSection = _foldingStrategy.FoldingManager.GetFoldingsContaining(offset).FirstOrDefault(s => s.IsFolded);
			if (foldingSection != null)
			{
				var toolTipText = FormatFoldingSectionToolTip(foldingSection);
				_popup.Child = new TextBox { Text = toolTipText, FontFamily = Editor.FontFamily, IsReadOnly = true }.AsPopupChild();
			}
			else
			{
				var toolTip = _toolTipProvider.GetToolTip(_sqlDocumentRepository, offset);
				if (toolTip == null)
				{
					return;
				}

				toolTip.Pin += ToolTipPinHandler;
				toolTip.Control.FontFamily = new FontFamily("Segoe UI");
				_popup.Child = toolTip.Control.AsPopupChild();
			}
			
			_popup.Placement = PlacementMode.Mouse;
			_popup.HorizontalOffset = 0;
			_popup.VerticalOffset = 0;
			_popup.IsOpen = true;
			e.Handled = true;
		}

		private void ToolTipPinHandler(object sender, EventArgs eventArgs)
		{
			_popup.IsOpen = false;
			
			var toolTip = (IToolTip)sender;
			var parent = (Decorator)VisualTreeHelper.GetParent(toolTip.Control);
			parent.Child = null;

			var window =
				new Window
				{
					Title = "Information",
					WindowStyle = WindowStyle.ToolWindow,
					SizeToContent = SizeToContent.WidthAndHeight,
					ShowActivated = true,
					ShowInTaskbar = false,
					MaxHeight = SystemParameters.WorkArea.Height,
					Content =
						new ScrollViewer
						{
							Content = toolTip.InnerContent,
							HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
							VerticalScrollBarVisibility = ScrollBarVisibility.Auto
						},
					Background = toolTip.Control.Background,
					Owner = MainWindow
				};
			
			window.Show();
		}

		private string FormatFoldingSectionToolTip(ICSharpCode.AvalonEdit.Document.TextSegment foldingSection)
		{
			var startLine = Editor.Document.GetLineByOffset(foldingSection.StartOffset);
			var endLine = Editor.Document.GetLineByOffset(foldingSection.EndOffset);
			var textLines = endLine.LineNumber - startLine.LineNumber + 1;
			var isTooLong = textLines > MaximumToolTipLines;
			if (isTooLong)
			{
				endLine = Editor.Document.GetLineByNumber(startLine.LineNumber + MaximumToolTipLines - 1);
			}

			var indent = foldingSection.StartOffset > startLine.Offset
				? Editor.Document.GetText(startLine.Offset, foldingSection.StartOffset - startLine.Offset)
				: String.Empty;
			
			var allowTrim = indent.Length > 0 && indent.All(c => c == ' ' || c == '\t');

			var line = startLine.NextLine;
			while (allowTrim && line.LineNumber <= endLine.LineNumber)
			{
				allowTrim &= line.Length == 0 || Editor.Document.GetText(line.Offset, line.Length).StartsWith(indent);
				line = line.NextLine;
			}

			var builder = new StringBuilder();
			line = startLine;
			while (line != null && line.LineNumber <= endLine.LineNumber)
			{
				var endOffset = Math.Min(line.EndOffset, foldingSection.EndOffset);

				string lineText;
				if (line.LineNumber > startLine.LineNumber)
				{
					builder.AppendLine();
					
					lineText = allowTrim && line.Length > 0
						? Editor.Document.GetText(line.Offset + indent.Length, endOffset - line.Offset - indent.Length)
						: Editor.Document.GetText(line.Offset, endOffset - line.Offset);
				}
				else
				{
					var startOffset = Math.Max(line.Offset, foldingSection.StartOffset);
					lineText = Editor.Document.GetText(startOffset, endOffset - startOffset);
				}

				builder.Append(lineText);
				line = line.NextLine;
			}

			if (isTooLong)
			{
				builder.AppendLine();
				builder.Append("...");
			}

			return builder.ToString();
		}

		private void MouseMoveHandler(object sender, MouseEventArgs e)
		{
			if (!_popup.IsOpen)
			{
				return;
			}

			var position = e.GetPosition(_popup.Child);

			if (position.Y < -Editor.FontSize || position.Y > _popup.Child.RenderSize.Height || position.X < 0 || position.X > _popup.Child.RenderSize.Width)
			{
				CloseToolTipWhenNotOpenByShortCutOrCaretChange();
			}
		}

		private void CloseToolTipWhenNotOpenByShortCutOrCaretChange()
		{
			if (!_isToolTipOpenByShortCut && !_isToolTipOpenByCaretChange)
			{
				_popup.IsOpen = false;
			}
		}

		private void ContextMenuOpeningHandler(object sender, ContextMenuEventArgs args)
		{
			if (!PopulateContextActionMenu())
			{
				args.Handled = true;
			}
		}

		private void DisableCodeCompletion()
		{
			_enableCodeComplete = false;
		}

		private MenuItem BuildContextMenuItem(ContextAction action)
		{
			var menuItem =
				new MenuItem
				{
					Header = action.Name.Replace("_", "__"),
					Command = new ContextActionCommand(Editor, action),
				};

			return menuItem;
		}

		private void ListContextActions(object sender, ExecutedRoutedEventArgs args)
		{
			var isAnyCommandAvailable = PopulateContextActionMenu();
			if (isAnyCommandAvailable)
			{
				DisableCodeCompletion();
			}

			_contextActionMenu.IsOpen = isAnyCommandAvailable;
		}

		private bool PopulateContextActionMenu()
		{
			return PopulateContextMenuItems(
				c => _contextActionProvider.GetContextActions(_sqlDocumentRepository, c)
					.Select(BuildContextMenuItem));
		}

		private void ListGenerateCodeItems(object sender, ExecutedRoutedEventArgs args)
		{
			if (PopulateGenerateCodeItemsMenu())
			{
				_contextActionMenu.IsOpen = true;
			}
		}

		private bool PopulateGenerateCodeItemsMenu()
		{
			return PopulateContextMenuItems(
				c => _codeSnippetProvider.GetCodeGenerationItems(_sqlDocumentRepository)
					.Select(ci => CreateContextMenuItemFromCodeSnippet(ci, c)));
		}

		private MenuItem CreateContextMenuItemFromCodeSnippet(ICodeSnippet codeSnippet, CommandExecutionContext executionContext)
		{
			var textSegment =
				new TextSegment
				{
					IndextStart = Editor.CaretOffset,
					Text = CompletionData.FormatSnippetText(codeSnippet)
				};

			var executionHandler =
				new CommandExecutionHandler
				{
					ExecutionHandler = c => c.SegmentsToReplace.Add(textSegment)
				};

			return BuildContextMenuItem(new ContextAction(codeSnippet.Name, executionHandler, executionContext));
		}

		private bool PopulateContextMenuItems(Func<CommandExecutionContext, IEnumerable<MenuItem>> buildMenuItemFunction)
		{
			_contextActionMenu.Items.Clear();

			var executionContext = CommandExecutionContext.Create(Editor, _sqlDocumentRepository);
			foreach (var menuItem in buildMenuItemFunction(executionContext))
			{
				_contextActionMenu.Items.Add(menuItem);
			}

			if (_contextActionMenu.Items.Count == 1)
			{
				_contextActionMenu.Opened += (sender, args) => ((MenuItem)_contextActionMenu.Items[0]).Focus();
			}

			var position = Editor.TextArea.Caret.CalculateCaretRectangle().BottomLeft;

			_contextActionMenu.HorizontalOffset = position.X - Editor.TextArea.TextView.HorizontalOffset + 32;
			_contextActionMenu.VerticalOffset = position.Y - Editor.TextArea.TextView.VerticalOffset + 2;

			return _contextActionMenu.Items.Count > 0;
		}

		private void EditorKeyUpHandler(object sender, KeyEventArgs e)
		{
			if (Editor.Document.IsInUpdate)
			{
				Editor.Document.EndUpdate();
			}

			CheckDocumentModified();

			if (_completionWindow != null && _completionWindow.CompletionList.ListBox.Items.Count == 0)
			{
				_completionWindow.Close();
			}
		}

		private void EditorKeyDownHandler(object sender, KeyEventArgs e)
		{
			_isToolTipOpenByShortCut = false;

			if (_popup != null)
			{
				_popup.IsOpen = false;
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

			if (e.Key == Key.Back || e.Key == Key.Delete || (e.Key.In(Key.V, Key.Insert) && Keyboard.Modifiers == ModifierKeys.Control))
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

		internal static bool SafeActionWithUserError(Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception e)
			{
				Messages.ShowError(e.Message);
				return false;
			}
		}

		private async void ExecuteExplainPlanCommandHandler(object sender, ExecutedRoutedEventArgs args)
		{
			IsBusy = true;
			await ExecuteExplainPlan();
			IsBusy = false;
		}

		private async Task ExecuteExplainPlan()
		{
			OutputViewer.SelectDefaultTabIfNeeded();

			_pageModel.ExecutionPlanAvailable = Visibility.Collapsed;

			var statementModel = BuildStatementExecutionModel();

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				var actionResult = await SafeTimedActionAsync(() => OutputViewer.ExecutionPlanViewer.ExplainAsync(statementModel, _statementExecutionCancellationTokenSource.Token));

				if (_statementExecutionCancellationTokenSource.IsCancellationRequested)
				{
					_pageModel.NotifyExecutionCanceled();
				}
				else
				{
					_pageModel.UpdateTimerMessage(actionResult.Elapsed, false);

					if (actionResult.IsSuccessful)
					{
						_pageModel.ExecutionPlanAvailable = Visibility.Visible;
						OutputViewer.ShowExecutionPlan();
					}
					else
					{
						Messages.ShowError(MainWindow, actionResult.Exception.Message);
					}
				}

				_statementExecutionCancellationTokenSource = null;
			}
		}

		private void CreateNewPage(object sender, ExecutedRoutedEventArgs e)
		{
			MainWindow.CreateNewDocumentPage();
		}

		private void ButtonCommitTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			SafeActionWithUserError(() =>
			{
				DatabaseModel.CommitTransaction();
				_pageModel.TransactionControlVisibity = Visibility.Collapsed;
			});

			Editor.Focus();
		}

		private async void ButtonRollbackTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			_pageModel.IsTransactionControlEnabled = false;
			IsBusy = true;
			
			var actionResult = await SafeTimedActionAsync(() => DatabaseModel.RollbackTransaction());
			if (!actionResult.IsSuccessful)
			{
				Messages.ShowError(MainWindow, actionResult.Exception.Message);
			}
			else
			{
				_pageModel.TransactionControlVisibity = Visibility.Collapsed;
			}

			_pageModel.UpdateTimerMessage(actionResult.Elapsed, false);

			_pageModel.IsTransactionControlEnabled = true;

			IsBusy = false;

			Editor.Focus();
		}

		private async void FetchNextRowsHandler(object sender, EventArgs e)
		{
			if (!CanFetchNextRows())
			{
				return;
			}

			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				await FetchNextRows();

				_statementExecutionCancellationTokenSource = null;
			}

			IsBusy = false;
		}

		private void CompilationErrorHandler(object sender, CompilationErrorArgs e)
		{
			var failedStatementSourcePosition = e.CompilationError.Statement.SourcePosition;
			var isStatementUnchanged = _sqlDocumentRepository.Statements.Any(s => failedStatementSourcePosition == s.SourcePosition);
			if (!isStatementUnchanged)
			{
				return;
			}

			Editor.TextArea.Caret.Line = e.CompilationError.Line;
			Editor.TextArea.Caret.Column = e.CompilationError.Column;
			Editor.ScrollToCaret();
			Editor.Focus();
		}

		private void ExpandAllFoldingsExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			SetAllFoldingIsFolded(false);
		}

		private void CollapseAllFoldingsExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			SetAllFoldingIsFolded(true);
		}

		private void SetAllFoldingIsFolded(bool isFolded)
		{
			foreach (var foldingSection in _foldingStrategy.FoldingManager.AllFoldings)
			{
				foldingSection.IsFolded = isFolded;
			}
		}

		private void CollapseAllFoldingsCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = _foldingStrategy.FoldingManager.AllFoldings.Any(f => !f.IsFolded);
		}

		private void ExpandAllFoldingsCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = _foldingStrategy.FoldingManager.AllFoldings.Any(f => f.IsFolded);
		}

		private void EditorZoomInHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.ZoomIn();
		}

		private void EditorZoomOutHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.ZoomOut();
		}

		private void ShowHelpHandler(object sender, ExecutedRoutedEventArgs e)
		{
			var executionContext = CommandExecutionContext.Create(Editor, _sqlDocumentRepository);
			_createHelpProvider.ShowHelp(executionContext);
		}
	}

	internal struct ActionResult
	{
		public bool IsSuccessful { get { return Exception == null; } }
		
		public Exception Exception { get; set; }

		public TimeSpan Elapsed { get; set; }
	}
}
