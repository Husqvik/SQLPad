using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
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
using Microsoft.Win32;
using SqlPad.Commands;
using SqlPad.FindReplace;
using MessageBox = System.Windows.MessageBox;
using Timer = System.Timers.Timer;

namespace SqlPad
{
	public partial class DocumentPage : IDisposable
	{
		private const string InitialDocumentHeader = "New";
		private const string MaskWrapByQuote = "\"{0}\"";
		private const string QuoteCharacter = "\"";
		private const string DoubleQuotes = "\"\"";
		public const string FileMaskDefault = "SQL files (*.sql)|*.sql|SQL Pad files (*.sqlx)|*.sqlx|Text files(*.txt)|*.txt|All files (*.*)|*";

		private SqlDocumentRepository _sqlDocumentRepository;
		private IInfrastructureFactory _infrastructureFactory;
		private ICodeCompletionProvider _codeCompletionProvider;
		private ICodeSnippetProvider _codeSnippetProvider;
		private IContextActionProvider _contextActionProvider;
		private IStatementFormatter _statementFormatter;
		private IToolTipProvider _toolTipProvider;
		private INavigationService _navigationService;

		private MultiNodeEditor _multiNodeEditor;
		private CancellationTokenSource _statementExecutionCancellationTokenSource;
		private CancellationTokenSource _parsingCancellationTokenSource;
		private FileSystemWatcher _documentFileWatcher;
		private DateTime _lastDocumentFileChange;
		private readonly SqlDocumentColorizingTransformer _colorizingTransformer = new SqlDocumentColorizingTransformer();
		private readonly ContextMenu _contextActionMenu = new ContextMenu { Placement = PlacementMode.Relative };

		private static readonly CellValueConverter CellValueConverter = new CellValueConverter();
		private static readonly ColorCodeToBrushConverter TabHeaderBackgroundBrushConverter = new ColorCodeToBrushConverter();

		private bool _isParsing;
		private bool _isInitializing = true;
		private bool _isInitialParsing = true;
		private bool _enableCodeComplete;
		private bool _isToolTipOpenByShortCut;
		private bool _isToolTipOpenByCaretChange;
		private bool _gatherExecutionStatistics;
		
		private readonly ToolTip _toolTip = new ToolTip();
		private readonly PageModel _pageModel;
		private readonly Timer _timerReParse = new Timer(100);
		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly List<CommandBinding> _specificCommandBindings = new List<CommandBinding>();

		private CompletionWindow _completionWindow;
		private ConnectionStringSettings _connectionString;
		private Dictionary<string, BindVariableConfiguration> _currentBindVariables = new Dictionary<string, BindVariableConfiguration>();
		
		//private readonly SqlFoldingStrategy _foldingStrategy;

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

		public bool IsDirty { get { return Editor.IsModified || WorkDocument.IsModified; } }

		public IDatabaseModel DatabaseModel { get; private set; }

		public DocumentPage(WorkDocument workDocument = null)
		{
			InitializeComponent();

			//_foldingStrategy = new SqlFoldingStrategy(FoldingManager.Install(Editor.TextArea), Editor);

			_toolTip.PlacementTarget = Editor.TextArea;
			_contextActionMenu.PlacementTarget = Editor;
			
			ComboBoxConnection.IsEnabled = ConfigurationProvider.ConnectionStrings.Count > 1;
			ComboBoxConnection.ItemsSource = ConfigurationProvider.ConnectionStrings;
			ComboBoxConnection.SelectedIndex = 0;

			InitializeGenericCommandBindings();

			_timerReParse.Elapsed += (sender, args) => Dispatcher.Invoke(Parse);
			_timerExecutionMonitor.Elapsed += (sender, args) => Dispatcher.Invoke(() => TextExecutionTime.Text = FormatElapsedMilliseconds(_stopWatch.Elapsed));

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
					WorkDocument.Text = WorkDocument.IsSqlx
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

				Editor.IsModified = WorkDocument.IsModified;
			}

			_pageModel.DocumentHeaderToolTip = WorkDocument.File == null ? "Unsaved" : WorkDocument.File.FullName;

			if (String.IsNullOrEmpty(WorkDocument.DocumentTitle))
			{
				_pageModel.DocumentHeader = WorkDocument.File == null ? InitialDocumentHeader : WorkDocument.File.Name;
			}

			_pageModel.IsModified = WorkDocument.IsModified;

			DataContext = _pageModel;

			InitializeTabItem();
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

		private void DocumentFileWatcherDeletedHandler(string fullFileName)
		{
			MainWindow.DocumentTabControl.SelectedItem = TabItem;

			var message = String.Format("File '{0}' has been deleted. Do you want to close the document? ", fullFileName);
			if (MessageBox.Show(MainWindow, message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.No)
			{
				return;
			}

			Editor.IsModified = false;
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
			Editor.TextArea.SelectionChanged += SelectionChangedHandler;

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
			_connectionString = connectionString;

			if (DatabaseModel != null)
			{
				DatabaseModel.Dispose();
			}

			_pageModel.ResetSchemas();

			var connectionConfiguration = ConfigurationProvider.GetConnectionCofiguration(_connectionString.Name);
			_pageModel.ProductionLabelVisibility = connectionConfiguration.IsProduction ? Visibility.Visible : Visibility.Collapsed;
			_infrastructureFactory = connectionConfiguration.InfrastructureFactory;
			_codeCompletionProvider = _infrastructureFactory.CreateCodeCompletionProvider();
			_codeSnippetProvider = _infrastructureFactory.CreateSnippetProvider();
			_contextActionProvider = _infrastructureFactory.CreateContextActionProvider();
			_statementFormatter = _infrastructureFactory.CreateSqlFormatter(new SqlFormatterOptions());
			_toolTipProvider = _infrastructureFactory.CreateToolTipProvider();
			_navigationService = _infrastructureFactory.CreateNavigationService();

			_colorizingTransformer.SetParser(_infrastructureFactory.CreateParser());

			InitializeSpecificCommandBindings();

			DatabaseModel = _infrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings[_connectionString.Name]);
			_sqlDocumentRepository = new SqlDocumentRepository(_infrastructureFactory.CreateParser(), _infrastructureFactory.CreateStatementValidator(), DatabaseModel);

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
			WorkDocument.SelectionStart = Editor.SelectionStart;
			WorkDocument.SelectionLength = Editor.SelectionLength;
			WorkDocument.EnableDatabaseOutput = DatabaseModel.EnableDatabaseOutput;

			var textView = Editor.TextArea.TextView;
			WorkDocument.VisualLeft = textView.ScrollOffset.X;
			WorkDocument.VisualTop = textView.ScrollOffset.Y;

			//_foldingStrategy.Store(WorkDocument);

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
			_pageModel.IsModified = WorkDocument.IsModified = false;

			if (WorkDocument.IsSqlx)
			{
				SaveWorkingDocument();
				WorkDocumentCollection.SaveDocumentAsFile(WorkDocument);
				Editor.IsModified = false;
			}
			else
			{
				Editor.Save(WorkDocument.File.FullName);
			}
		}

		private void SelectionChangedHandler(object sender, EventArgs eventArgs)
		{
			_pageModel.SelectionLength = Editor.SelectionLength == 0 ? null : (int?)Editor.SelectionLength;
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
			CreateCodeCompletionWindow(true);
		}

		private void CanExecuteCancelUserActionHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = DatabaseModel.IsExecuting || IsBusy;
		}

		private void CancelUserActionHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Trace.WriteLine("Action is about to cancel. ");
			_statementExecutionCancellationTokenSource.Cancel();
		}

		private void ShowTokenCommandExecutionHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var tokens = _infrastructureFactory.CreateTokenReader(_sqlDocumentRepository.StatementText).GetTokens(true).ToArray();
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

		private void CanFetchAllRows(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.CanExecute = CanFetchNextRows();
			canExecuteRoutedEventArgs.ContinueRouting = canExecuteRoutedEventArgs.CanExecute;
		}

		private async void FetchAllRows(object sender, ExecutedRoutedEventArgs args)
		{
			IsBusy = true;

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				while (DatabaseModel.CanFetch)
				{
					if (_statementExecutionCancellationTokenSource.Token.IsCancellationRequested)
					{
						break;
					}

					await FetchNextRows();
				}
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
					_pageModel.SessionExecutionStatistics.MergeWith(await DatabaseModel.GetExecutionStatisticsAsync(CancellationToken.None));
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
			if (functionOverloads.Count == 0)
			{
				return;
			}

			_toolTip.Content = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			_isToolTipOpenByShortCut = true;

			var rectangle = Editor.TextArea.Caret.CalculateCaretRectangle();
			_toolTip.Placement = PlacementMode.Relative;
			_toolTip.HorizontalOffset = rectangle.Left - Editor.TextArea.TextView.HorizontalOffset;
			_toolTip.VerticalOffset = rectangle.Top - Editor.TextArea.TextView.VerticalOffset + Editor.TextArea.TextView.DefaultLineHeight;
			_toolTip.IsOpen = true;
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
			await ExecuteDatabaseCommand(executionModel);

			IsBusy = false;
		}

		private StatementExecutionModel BuildStatementExecutionModel()
		{
			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);

			var executionModel = new StatementExecutionModel { GatherExecutionStatistics = _gatherExecutionStatistics };
			if (Editor.SelectionLength > 0)
			{
				executionModel.StatementText = Editor.SelectedText;
				executionModel.BindVariables = _pageModel.BindVariables.Where(c => c.BindVariable.Nodes.Any(n => n.SourcePosition.IndexStart >= Editor.SelectionStart && n.SourcePosition.IndexEnd + 1 <= Editor.SelectionStart + Editor.SelectionLength)).ToArray();
			}
			else
			{
				executionModel.StatementText = statement.RootNode.GetStatementSubstring(Editor.Text);
				executionModel.BindVariables = _pageModel.BindVariables;
			}

			return executionModel;
		}

		private void InitializeViewBeforeCommandExecution()
		{
			_pageModel.ResultRowItems.Clear();
			_pageModel.GridRowInfoVisibility = Visibility.Collapsed;
			_pageModel.StatementExecutedSuccessfullyStatusMessageVisibility = Visibility.Collapsed;
			_pageModel.TextExecutionPlan = null;
			_pageModel.SessionExecutionStatistics.Clear();
			_pageModel.WriteDatabaseOutput(String.Empty);

			TextMoreRowsExist.Visibility = Visibility.Collapsed;

			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.None;

			_pageModel.AffectedRowCount = -1;

			if (!IsTabAlwaysVisible(TabControlResult.SelectedItem))
			{
				TabControlResult.SelectedIndex = 0;
			}
		}

		private bool IsTabAlwaysVisible(object tabItem)
		{
			return TabControlResult.Items.IndexOf(tabItem).In(0, 3);
		}

		private async Task ExecuteDatabaseCommand(StatementExecutionModel executionModel)
		{
			var previousSelectedTab = TabControlResult.SelectedItem;

			InitializeViewBeforeCommandExecution();

			Task<StatementExecutionResult> innerTask = null;
			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				var actionResult = await SafeTimedActionAsync(() => innerTask = DatabaseModel.ExecuteStatementAsync(executionModel, _statementExecutionCancellationTokenSource.Token));

				_pageModel.TransactionControlVisibity = DatabaseModel.HasActiveTransaction ? Visibility.Visible : Visibility.Collapsed;

				if (!actionResult.IsSuccessful)
				{
					Messages.ShowError(MainWindow, actionResult.Exception.Message);
					return;
				}

				if (!innerTask.Result.ExecutedSuccessfully)
				{
					return;
				}

				UpdateStatusBarElapsedExecutionTime(actionResult.Elapsed);

				_pageModel.WriteDatabaseOutput(innerTask.Result.DatabaseOutput);

				if (_gatherExecutionStatistics)
				{
					_pageModel.TextExecutionPlan = await DatabaseModel.GetActualExecutionPlanAsync(_statementExecutionCancellationTokenSource.Token);
					_pageModel.SessionExecutionStatistics.MergeWith(await DatabaseModel.GetExecutionStatisticsAsync(_statementExecutionCancellationTokenSource.Token));
					TabControlResult.SelectedItem = previousSelectedTab;
				}
				else if (IsTabAlwaysVisible(previousSelectedTab))
				{
					TabControlResult.SelectedItem = previousSelectedTab;
				}

				if (innerTask.Result.ColumnHeaders.Count == 0)
				{
					if (innerTask.Result.AffectedRowCount == - 1)
					{
						_pageModel.StatementExecutedSuccessfullyStatusMessageVisibility = Visibility.Visible;
					}
					else
					{
						_pageModel.AffectedRowCount = innerTask.Result.AffectedRowCount;
					}

					return;
				}

				InitializeResultGrid(innerTask.Result.ColumnHeaders);

				AppendRows(innerTask.Result.InitialResultSet);
			}
		}

		private void UpdateStatusBarElapsedExecutionTime(TimeSpan timeSpan)
		{
			TextExecutionTime.Text = FormatElapsedMilliseconds(timeSpan);
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
			ResultGrid.Columns.Clear();

			foreach (var columnHeader in columnHeaders)
			{
				var columnTemplate = CreateDataGridTextColumnTemplate(columnHeader);
				ResultGrid.Columns.Add(columnTemplate);
			}

			_pageModel.GridRowInfoVisibility = Visibility.Visible;
			ResultGrid.HeadersVisibility = DataGridHeadersVisibility.Column;

			_pageModel.ResultRowItems.Clear();
		}

		internal static DataGridTextColumn CreateDataGridTextColumnTemplate(ColumnHeader columnHeader)
		{
			var columnTemplate =
				new DataGridTextColumn
				{
					Header = columnHeader.Name.Replace("_", "__"),
					Binding = new Binding(String.Format("[{0}]", columnHeader.ColumnIndex)) { Converter = CellValueConverter, ConverterParameter = columnHeader },
					EditingElementStyle = (Style)Application.Current.Resources["CellTextBoxStyleReadOnly"]
				};

			if (columnHeader.DataType.In(typeof(Decimal), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Byte)))
			{
				columnTemplate.HeaderStyle = (Style)Application.Current.Resources["HeaderStyleRightAlign"];
				columnTemplate.CellStyle = (Style)Application.Current.Resources["CellStyleRightAlign"];
			}

			return columnTemplate;
		}

		public void Dispose()
		{
			ConfigurationProvider.ConfigurationChanged -= ConfigurationChangedHandler;
			MainWindow.DocumentTabControl.SelectionChanged -= DocumentTabControlSelectionChangedHandler;

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
				MainWindow.DocumentTabControl.SelectionChanged += DocumentTabControlSelectionChangedHandler;

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

		private void DocumentTabControlSelectionChangedHandler(object sender, SelectionChangedEventArgs selectionChangedEventArgs)
		{
			_toolTip.IsOpen = false;
			_isToolTipOpenByCaretChange = false;
			_isToolTipOpenByShortCut = false;
		}

		private void CaretPositionChangedHandler(object sender, EventArgs eventArgs)
		{
			EditorNavigationService.RegisterDocumentCursorPosition(WorkDocument, Editor.CaretOffset);

			_isToolTipOpenByCaretChange = false;

			CloseToolTipWhenNotOpenByShortCut();

			var parenthesisNodes = new List<StatementGrammarNode>();

			var location = Editor.Document.GetLocation(Editor.CaretOffset);
			_pageModel.CurrentLine = location.Line;
			_pageModel.CurrentColumn = location.Column;

			if (!_isParsing)
			{
				ShowHideBindVariableList();

				var parenthesisTerminal = _sqlDocumentRepository.Statements == null
					? null
					: _sqlDocumentRepository.ExecuteStatementAction(s => s.GetTerminalAtPosition(Editor.CaretOffset, n => n.Token.Value.In("(", ")", "[", "]", "{", "}")));

				if (parenthesisTerminal != null)
				{
					var childNodes = parenthesisTerminal.ParentNode.ChildNodes;
					var index = childNodes.IndexOf(parenthesisTerminal);
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

								_toolTip.Content = new TextBlock { FontFamily = Editor.FontFamily, FontSize = Editor.FontSize, Text = toolTipBuilder.ToString() };
								_toolTip.Placement = PlacementMode.Relative;
								_toolTip.HorizontalOffset = Editor.TextArea.LeftMargins.Sum(m => m.DesiredSize.Width);
								_toolTip.VerticalOffset = -28;
								_toolTip.IsOpen = true;

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

			if (e.Text.Length == 1 && _completionWindow != null && e.Text == "\t")
			{
				_completionWindow.CompletionList.RequestInsertion(e);
			}
		}

		private async void CreateCodeCompletionWindow(bool forcedInvokation)
		{
			var items = await _codeCompletionProvider.ResolveItems(_sqlDocumentRepository, DatabaseModel, Editor.Text, Editor.CaretOffset, forcedInvokation)
				.Select(i => new CompletionData(i))
				.EnumerateAsync(CancellationToken.None);

			if (_sqlDocumentRepository.StatementText != Editor.Text)
			{
				return;
			}

			CreateCompletionWindow(items);
		}

		private void CreateSnippetCompletionWindow(IEnumerable<CompletionData> items)
		{
			CreateCompletionWindow(items);
		}

		private void CreateCompletionWindow(IEnumerable<CompletionData> items)
		{
			var completionWindow =
				new CompletionWindow(Editor.TextArea)
				{
					SizeToContent = SizeToContent.WidthAndHeight,
					ResizeMode = ResizeMode.NoResize
				};
			
			var listItems = completionWindow.CompletionList.CompletionData;

			listItems.AddRange(items);

			if (listItems.Count == 0)
			{
				return;
			}
			
			_completionWindow = completionWindow;
			_completionWindow.Closed += delegate { _completionWindow = null; };
			_completionWindow.SizeChanged += delegate { if (_completionWindow.MinWidth < _completionWindow.Width) _completionWindow.MinWidth = _completionWindow.Width; };

			var firstItem = (CompletionData)listItems[0];
			if (firstItem.Node != null)
			{
				_completionWindow.StartOffset = firstItem.Node.SourcePosition.IndexStart;
			}

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

			_pageModel.IsModified = WorkDocument.IsModified = IsDirty;

			Parse();
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
				ParseDoneHandler();
			}
			else
			{
				_sqlDocumentRepository.UpdateStatementsAsync(Editor.Text, _parsingCancellationTokenSource.Token)
					.ContinueWith(t => ParseDoneHandler());
			}
		}

		private void ParseDoneHandler()
		{
			_colorizingTransformer.SetDocumentRepository(_sqlDocumentRepository);

			Dispatcher.Invoke(ParseDoneUiHandler);
		}

		private void ParseDoneUiHandler()
		{
			//_foldingStrategy.UpdateFoldings(_sqlDocumentRepository.Statements);

			if (_isInitialParsing)
			{
				//_foldingStrategy.Restore(WorkDocument);
				_isInitialParsing = false;
			}

			Editor.TextArea.TextView.Redraw();
			_isParsing = false;

			ShowHideBindVariableList();

			if (_enableCodeComplete && _completionWindow == null && IsSelectedPage && _sqlDocumentRepository.StatementText == Editor.Text)
			{
				CreateCodeCompletionWindow(false);
			}
		}

		void MouseHoverHandler(object sender, MouseEventArgs e)
		{
			if (_isToolTipOpenByShortCut || _isToolTipOpenByCaretChange)
				return;

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

			var toolTip = _toolTipProvider.GetToolTip(_sqlDocumentRepository, offset);
			if (toolTip == null)
				return;

			_toolTip.Placement = PlacementMode.Mouse;
			_toolTip.HorizontalOffset = 0;
			_toolTip.VerticalOffset = 0;
			_toolTip.Content = toolTip;
			_toolTip.IsOpen = true;
			e.Handled = true;
		}

		private void MouseHoverStoppedHandler(object sender, MouseEventArgs e)
		{
			CloseToolTipWhenNotOpenByShortCut();
		}

		private void CloseToolTipWhenNotOpenByShortCut()
		{
			if (!_isToolTipOpenByShortCut && !_isToolTipOpenByCaretChange)
			{
				_toolTip.IsOpen = false;
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

		private MenuItem BuildContextMenuItem(IContextAction action)
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
			_contextActionMenu.Items.Clear();

			var executionContext = CommandExecutionContext.Create(Editor, _sqlDocumentRepository);
			var contextActions = _contextActionProvider.GetContextActions(_sqlDocumentRepository, executionContext);
			foreach (var contextAction in contextActions)
			{
				_contextActionMenu.Items.Add(BuildContextMenuItem(contextAction));
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

			_pageModel.IsModified = WorkDocument.IsModified = IsDirty;

			if (_completionWindow != null && _completionWindow.CompletionList.ListBox.Items.Count == 0)
			{
				_completionWindow.Close();
			}
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

		private void ResultGridMouseDoubleClickHandler(object sender, MouseButtonEventArgs e)
		{
			ShowLargeValueEditor(ResultGrid);
		}

		internal static void ShowLargeValueEditor(DataGrid dataGrid)
		{
			var currentRow = (object[])dataGrid.CurrentItem;
			if (currentRow == null || dataGrid.CurrentColumn == null)
				return;

			var cellValue = currentRow[dataGrid.CurrentColumn.DisplayIndex];
			var largeValue = cellValue as ILargeValue;
			if (largeValue != null)
			{
				new LargeValueEditor(dataGrid.CurrentColumn.Header.ToString(), largeValue) { Owner = Window.GetWindow(dataGrid) }.ShowDialog();
			}
		}

		private void CanExportToCsv(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ResultGrid.Items.Count > 0;
		}

		private void ExportToCsv(object sender, ExecutedRoutedEventArgs args)
		{
			var dialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*", OverwritePrompt = true };
			if (dialog.ShowDialog() != true)
			{
				return;
			}

			SafeActionWithUserError(() =>
			{
				using (var file = File.CreateText(dialog.FileName))
				{
					ExportToCsv(file);
				}
			});
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

		private void ExportToCsv(TextWriter writer)
		{
			var orderedColumns = ResultGrid.Columns
				.OrderBy(c => c.DisplayIndex)
				.ToArray();

			var columnHeaders = orderedColumns
				.Select(c => String.Format(MaskWrapByQuote, c.Header.ToString().Replace("__", "_").Replace(QuoteCharacter, DoubleQuotes)));

			const string separator = ";";
			var headerLine = String.Join(separator, columnHeaders);
			writer.WriteLine(headerLine);

			var converterParameters = orderedColumns
				.Select(c => ((Binding)((DataGridTextColumn)c).Binding).ConverterParameter)
				.ToArray();

			foreach (object[] rowValues in ResultGrid.Items)
			{
				var contentLine = String.Join(separator, rowValues.Select((t, i) => FormatCsvValue(t, converterParameters[i])));
				writer.WriteLine(contentLine);
			}
		}

		private static string FormatCsvValue(object value, object converterParameter)
		{
			if (value == DBNull.Value)
				return null;

			var stringValue = CellValueConverter.Convert(value, typeof(String), converterParameter, CultureInfo.CurrentUICulture).ToString();
			return String.Format(MaskWrapByQuote, stringValue.Replace(QuoteCharacter, DoubleQuotes));
		}

		private async void ExecuteExplainPlanCommandHandler(object sender, ExecutedRoutedEventArgs args)
		{
			IsBusy = true;
			await ExecuteExplainPlan();
			IsBusy = false;
		}

		private async Task ExecuteExplainPlan()
		{
			_gatherExecutionStatistics = false;

			InitializeViewBeforeCommandExecution();

			var statementText = BuildStatementExecutionModel().StatementText;

			Task<ExplainPlanResult> innerTask = null;
			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				var actionResult = await SafeTimedActionAsync(() => innerTask = DatabaseModel.ExplainPlanAsync(statementText, _statementExecutionCancellationTokenSource.Token));
				
				UpdateStatusBarElapsedExecutionTime(actionResult.Elapsed);
				
				if (!actionResult.IsSuccessful)
				{
					Messages.ShowError(MainWindow, actionResult.Exception.Message);
					return;
				}
			}

			InitializeResultGrid(innerTask.Result.ColumnHeaders);
			_pageModel.ResultRowItems.AddRange(innerTask.Result.ResultSet);
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

		private void ButtonRollbackTransactionClickHandler(object sender, RoutedEventArgs e)
		{
			SafeActionWithUserError(() =>
			{
				DatabaseModel.RollbackTransaction();
				_pageModel.TransactionControlVisibity = Visibility.Collapsed;
			});

			Editor.Focus();
		}

		private void TabControlResultGiveFeedbackHandler(object sender, GiveFeedbackEventArgs e)
		{
			e.Handled = true;
		}


		private async void ResultGridScrollChangedHandler(object sender, ScrollChangedEventArgs e)
		{
			if (!CanFetchNextRows() || e.VerticalOffset + e.ViewportHeight != e.ExtentHeight)
			{
				return;
			}

			using (_statementExecutionCancellationTokenSource = new CancellationTokenSource())
			{
				IsBusy = true;
				await FetchNextRows();
				IsBusy = false;
			}
		}

		private void ResultGridKeyDownHandler(object sender, KeyEventArgs e)
		{
			e.Handled = true;
		}
	}

	internal struct ActionResult
	{
		public bool IsSuccessful { get { return Exception == null; } }
		
		public Exception Exception { get; set; }

		public TimeSpan Elapsed { get; set; }
	}
}
