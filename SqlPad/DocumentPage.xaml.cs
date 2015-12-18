using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.Win32;
using SqlPad.Commands;
using SqlPad.FindReplace;
using MessageBox = System.Windows.MessageBox;

namespace SqlPad
{
	public partial class DocumentPage : IDisposable
	{
		private const string InitialDocumentHeader = "New";
		private const int MaximumToolTipLines = 32;
		private const int MaximumOutputViewersPerPage = 16;
		public const string FileMaskDefault = "SQL files (*.sql)|*.sql|SQL Pad files (*.sqlx)|*.sqlx|Text files(*.txt)|*.txt|All files (*.*)|*";

		private static readonly string[] OpeningParenthesisOrBrackets = { "(", "[", "{" };
		private static readonly string[] ClosingParenthesisOrBrackets = { ")", "]", "}" };
		private static readonly ColorCodeToBrushConverter TabHeaderBackgroundBrushConverter = new ColorCodeToBrushConverter();

		private SqlDocumentRepository _documentRepository;
		private ICodeCompletionProvider _codeCompletionProvider;
		private ICodeSnippetProvider _codeSnippetProvider;
		private IContextActionProvider _contextActionProvider;
		private IStatementFormatter _statementFormatter;
		private IToolTipProvider _toolTipProvider;
		private INavigationService _navigationService;
		private IHelpProvider _createHelpProvider;

		private MultiNodeEditor _multiNodeEditor;
		private CancellationTokenSource _parsingCancellationTokenSource;
		private FileSystemWatcher _documentFileWatcher;
		private DateTime _lastDocumentFileChange;
		private DatabaseProviderConfiguration _providerConfiguration;

		private bool _undoExecuted;
		private bool _isParsing;
		private bool _isInitializing = true;
		private bool _isInitialParsing = true;
		private bool _enableCodeComplete;
		private bool _isToolTipOpenByShortCut;
		private bool _isToolTipOpenByCaretChange;

		private int _outputViewerCounter;
		
		private string _originalWorkDocumentContent = String.Empty;

		private readonly DispatcherTimer _timerReParse;
		private readonly DispatcherTimer _timerTimedNotification;
		private readonly List<CommandBinding> _specificCommandBindings = new List<CommandBinding>();
		private readonly ObservableCollection<OutputViewer> _outputViewers = new ObservableCollection<OutputViewer>();
		private readonly ObservableCollection<string> _schemas = new ObservableCollection<string>();
		private readonly SqlDocumentColorizingTransformer _colorizingTransformer = new SqlDocumentColorizingTransformer();
		private readonly SqlEditorBackgroundRenderer _backgroundRenderer;
		private readonly ContextMenu _contextActionMenu = new ContextMenu { Placement = PlacementMode.Relative };
		private readonly Dictionary<string, BindVariableConfiguration> _currentBindVariables = new Dictionary<string, BindVariableConfiguration>();

		private readonly SqlFoldingStrategy _foldingStrategy;

		private CompletionWindow _completionWindow;
		private ConnectionStringSettings _connectionString;

		internal IInfrastructureFactory InfrastructureFactory { get; private set; }

		internal TabItem TabItem { get; private set; }
		
		internal ContextMenu TabItemContextMenu => ((ContentControl)TabItem.Header).ContextMenu;

		internal static bool IsParsingSynchronous { get; set; }

		public bool IsBusy { get { return _outputViewers.Any(v => v.IsBusy); } }

		internal bool IsSelectedPage => Equals(((TabItem)App.MainWindow.DocumentTabControl.SelectedItem).Content);

		public TextEditorAdapter EditorAdapter { get; private set; }

		public WorkDocument WorkDocument { get; }

		public bool IsDirty => !String.Equals(Editor.Text, _originalWorkDocumentContent);

		public IDatabaseModel DatabaseModel { get; private set; }

		public IReadOnlyList<OutputViewer> OutputViewers => _outputViewers;

		public IReadOnlyList<string> Schemas => _schemas;

		internal OutputViewer ActiveOutputViewer => (OutputViewer)OutputViewerList.SelectedItem;

		public DocumentPage(WorkDocument workDocument = null)
		{
			InitializeComponent();

			//Editor.TextArea.LeftMargins.Add(new ModificationNotificationMargin(Editor));
			_foldingStrategy = new SqlFoldingStrategy(FoldingManager.Install(Editor.TextArea), Editor);
			_foldingStrategy.FoldingMargin.ContextMenu = (ContextMenu)Resources["FoldingActionMenu"];
			_backgroundRenderer = new SqlEditorBackgroundRenderer(Editor);

			_contextActionMenu.PlacementTarget = Editor;
			
			InitializeGenericCommandBindings();

			_timerReParse = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
			_timerReParse.Tick += delegate { Parse(); };
			_timerTimedNotification = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
			_timerTimedNotification.Tick += TimedNotificationTickHandler;

			_outputViewers.CollectionChanged += delegate { OutputViewerList.Visibility = _outputViewers.Count > 1 ? Visibility.Visible : Visibility.Collapsed; };

			DateTimeFormat = ConfigurationProvider.Configuration.ResultGrid.DateFormat;

			ConfigurationProvider.ConfigurationChanged += ConfigurationChangedHandler;

			ConfigureEditor();

			if (workDocument == null)
			{
				WorkDocument = new WorkDocument
				{
					ConnectionName = ConfigurationProvider.ConnectionStrings[0].Name,
					HeaderBackgroundColorCode = Colors.White.ToString()
				};

				WorkDocumentCollection.AddDocument(WorkDocument);
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

				DocumentHeaderBackgroundColorCode = WorkDocument.HeaderBackgroundColorCode;
			}

			Editor.FontSize = WorkDocument.FontSize;

			UpdateDocumentHeaderToolTip();

			InitializeTabItem();
		}

		private void TimedNotificationTickHandler(object sender, EventArgs e)
		{
			TimedNotificationMessage = String.Empty;
			_timerTimedNotification.Stop();
		}

		public void InsertStatement(string statementText)
		{
			var insertIndex = Editor.CaretOffset;
			var builder = new StringBuilder(statementText);

			var statement = _documentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			if (statement?.RootNode != null)
			{
				insertIndex = statement.SourcePosition.IndexEnd + 1;
				builder.Insert(0, Environment.NewLine, 2);
			}
			
			Editor.Document.Insert(insertIndex, builder.ToString());
		}

		internal void NotifyExecutionEvent()
		{
			IsRunning = IsBusy;
			App.MainWindow.NotifyTaskStatus();
		}

		private void UpdateDocumentHeaderToolTip()
		{
			DocumentHeaderToolTip = WorkDocument.File == null
				? "Unsaved"
				: $"{WorkDocument.File.FullName}{Environment.NewLine}Last change: {CellValueConverter.FormatDateTime(WorkDocument.File.LastWriteTime)}";
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
			App.MainWindow.DocumentTabControl.SelectedItem = TabItem;

			var message = $"File '{fullFileName}' has been deleted. Do you want to close the document? ";
			if (MessageBox.Show(App.MainWindow, message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.No)
			{
				return;
			}

			App.MainWindow.CloseDocument(this);
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
					App.MainWindow.DocumentTabControl.SelectedItem = TabItem;

					var message = $"File '{fileSystemEventArgs.FullPath}' has been changed by another application. Do you want to load new content? ";
					if (MessageBox.Show(App.MainWindow, message, "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes) == MessageBoxResult.Yes)
					{
						Editor.Load(fileSystemEventArgs.FullPath);
					}
				});
		}

		private void ConfigurationChangedHandler(object sender, EventArgs eventArgs)
		{
			DateTimeFormat = ConfigurationProvider.Configuration.ResultGrid.DateFormat;
		}

		private void InitializeTabItem()
		{
			var header =
				new EditableTabHeaderControl
				{
					ContextMenu = CreateTabItemHeaderContextMenu(),
					Template = (ControlTemplate)Resources["EditableTabHeaderControlTemplate"]
				};

			var contentBinding = new Binding("DocumentHeader") { Source = this, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Mode = BindingMode.TwoWay };
			header.SetBinding(ContentProperty, contentBinding);
			var isModifiedBinding = new Binding("IsModified") { Source = this, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(EditableTabHeaderControl.IsModifiedProperty, isModifiedBinding);
			var isRunningBinding = new Binding("IsRunning") { Source = this, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(EditableTabHeaderControl.IsRunningProperty, isRunningBinding);
			var toolTipBinding = new Binding("DocumentHeaderToolTip") { Source = this, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged };
			header.SetBinding(ToolTipProperty, toolTipBinding);

			TabItem =
				new TabItem
				{
					Content = this,
					Header = header,
					Template = (ControlTemplate)Application.Current.Resources["TabItemControlTemplate"]
				};

			var backgroundBinding = new Binding("DocumentHeaderBackgroundColorCode") { Source = this, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged, Converter = TabHeaderBackgroundBrushConverter };
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
			colorPickerMenuItem.DataContext = this;
			contextMenu.Items.Add(colorPickerMenuItem);
			
			return contextMenu;
		}

		private void ConfigureEditor()
		{
			Editor.TextArea.SelectionCornerRadius = 0;
			Editor.TextArea.TextView.LineTransformers.Add(_colorizingTransformer);

			Editor.TextArea.TextEntering += TextEnteringHandler;
			Editor.TextArea.TextEntered += TextEnteredHandler;
			Editor.Document.Changing += DocumentChangingHandler;

			Editor.TextArea.Caret.PositionChanged += CaretPositionChangedHandler;
			Editor.TextArea.SelectionChanged += delegate { ShowHideBindVariableList(); };

			Editor.TextArea.TextView.BackgroundRenderers.Add(_backgroundRenderer);

			EditorAdapter = new TextEditorAdapter(Editor);
		}

		private void DocumentChangingHandler(object sender, DocumentChangeEventArgs args)
		{
			if ((args.InsertionLength > 0 && args.RemovalLength > 0) ||
				args.InsertedText.IndexOfAny(TextSegment.Separators, 0, args.InsertionLength) != -1 ||
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
				var routedHandlerMethod = GenericCommandHandler.CreateRoutedEditCommandHandler(handler, () => _documentRepository);
				var commandBinding = new CommandBinding(command, routedHandlerMethod);
				_specificCommandBindings.Add(commandBinding);
				Editor.TextArea.DefaultInputHandler.Editing.CommandBindings.Add(commandBinding);
			}
		}

		private void InitializeInfrastructureComponents(ConnectionStringSettings connectionString)
		{
			_connectionString = connectionString;

			DatabaseModel?.Dispose();

			ResetSchemas();

			var connectionConfiguration = ConfigurationProvider.GetConnectionConfiguration(_connectionString.Name);
			_providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(_connectionString.ProviderName);

			IsProductionConnection = connectionConfiguration.IsProduction;
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

			DatabaseModel = InfrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings[_connectionString.Name], DocumentHeader);
			SchemaLabel = InfrastructureFactory.SchemaLabel;
			_documentRepository = new SqlDocumentRepository(InfrastructureFactory.CreateParser(), InfrastructureFactory.CreateStatementValidator(), DatabaseModel);

			DisposeOutputViewers();
			AddNewOutputViewer();

			DatabaseModel.Initialized += delegate { Dispatcher.Invoke(DatabaseModelInitializedHandler); };
			DatabaseModel.PasswordRequired += (sender, args) => Dispatcher.Invoke(() => DatabaseModelPasswordRequiredHandler(args));
			DatabaseModel.Disconnected += (sender, args) => Dispatcher.Invoke(() => DatabaseModelInitializationFailedHandler(args.Exception));
			DatabaseModel.InitializationFailed += (sender, args) => Dispatcher.Invoke(() => DatabaseModelInitializationFailedHandler(args.Exception));
			DatabaseModel.RefreshStarted += DatabaseModelRefreshStartedHandler;
			DatabaseModel.RefreshStatusChanged += DatabaseModelRefreshStatusChangedHandler;
			DatabaseModel.RefreshCompleted += DatabaseModelRefreshCompletedHandler;

			ProgressBar.IsIndeterminate = false;
			DatabaseModel.Initialize();
		}

		private void AddNewOutputViewer()
		{
			if (_outputViewers.Count >= MaximumOutputViewersPerPage)
			{
				throw new InvalidOperationException($"Maximum number of simultaneous views is {MaximumOutputViewersPerPage}. ");
			}

			var outputViewer =
				new OutputViewer(this)
				{
					Title = $"View {++_outputViewerCounter}",
					EnableDatabaseOutput = WorkDocument.EnableDatabaseOutput,
					KeepDatabaseOutputHistory = WorkDocument.KeepDatabaseOutputHistory
				};

			if (ActiveOutputViewer != null)
			{
				outputViewer.EnableChildReferenceDataSources = ActiveOutputViewer.EnableChildReferenceDataSources;
			}

			outputViewer.CompilationError += CompilationErrorHandler;
			_outputViewers.Add(outputViewer);

			OutputViewerList.SelectedItem = outputViewer;
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

			App.SafeActionWithUserError(SaveDocument);
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

			if (!App.SafeActionWithUserError(SaveDocument))
			{
				WorkDocument.DocumentFileName = null;
				WorkDocument.DocumentTitle = documentTitle;
				WorkDocument.IsModified = isModified;
			}
			else
			{
				DocumentHeaderToolTip = WorkDocument.File.FullName;
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
			WorkDocument.SelectionType = Editor.IsMultiSelectionActive ? SelectionType.Rectangle : SelectionType.Simple;

			WorkDocument.EnableDatabaseOutput = ActiveOutputViewer.EnableDatabaseOutput;
			WorkDocument.KeepDatabaseOutputHistory = ActiveOutputViewer.KeepDatabaseOutputHistory;

			var textView = Editor.TextArea.TextView;
			WorkDocument.VisualLeft = textView.ScrollOffset.X;
			WorkDocument.VisualTop = textView.ScrollOffset.Y;

			_foldingStrategy.Store(WorkDocument);

			if (RowDefinitionEditor.ActualHeight > 0)
			{
				WorkDocument.EditorGridRowHeight = RowDefinitionEditor.ActualHeight;
			}

			if (BindVariables != null && BindVariables.Count > 0)
			{
				WorkDocument.EditorGridColumnWidth = ColumnDefinitionEditor.ActualWidth;
			}

			if (CurrentConnection != null)
			{
				WorkDocument.ConnectionName = CurrentConnection.Name;
				WorkDocument.SchemaName = CurrentSchema;
			}
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
				DocumentHeader = WorkDocument.File.Name;
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

			IsModified = WorkDocument.IsModified = false;
			_originalWorkDocumentContent = Editor.Text;

			UpdateDocumentHeaderToolTip();
		}

		private void DatabaseModelInitializedHandler()
		{
			ConnectionStatus = ConnectionStatus.Connected;
			SetSchemas(DatabaseModel.Schemas);
		}

		private void ResetSchemas()
		{
			_schemas.Clear();
			CurrentSchema = null;
		}

		private void SetSchemas(IEnumerable<string> schemas)
		{
			ResetSchemas();
			_schemas.AddRange(schemas.OrderBy(s => s));
			CurrentSchema = DatabaseModel.CurrentSchema;
		}

		private void DatabaseModelPasswordRequiredHandler(DatabaseModelPasswordArgs args)
		{
			if (App.MainWindow.ActiveDocument.CurrentConnection.ConnectionString == CurrentConnection.ConnectionString)
			{
				args.Password = PasswordDialog.AskForPassword("Password: ", App.MainWindow);
			}

			args.CancelConnection = args.Password == null;
		}

		private void DatabaseModelInitializationFailedHandler(Exception exception)
		{
			ConnectionStatus = ConnectionStatus.Disconnected;
			ConnectionErrorMessage = exception.Message;
		}

		private void ButtonReconnectClickHandler(object sender, RoutedEventArgs e)
		{
			ConnectionStatus = ConnectionStatus.Connecting;

			if (!DatabaseModel.IsInitialized)
			{
				DatabaseModel.Initialize();
			}
		}

		private void DatabaseModelRefreshStartedHandler(object sender, EventArgs args)
		{
			Dispatcher.Invoke(() => ProgressBar.IsIndeterminate = true);
		}

		private void DatabaseModelRefreshStatusChangedHandler(object sender, DatabaseModelRefreshStatusChangedArgs args)
		{
			Dispatcher.Invoke(() => DatabaseModelRefreshStatus = args.Message);
		}

		private void DatabaseModelRefreshCompletedHandler(object sender, EventArgs eventArgs)
		{
			Dispatcher.Invoke(() =>
			{
				if (DatabaseModel.IsFresh)
				{
					DatabaseModelRefreshStatus = null;
				}

				ProgressBar.IsIndeterminate = false;
				SetSchemas(DatabaseModel.Schemas);
			});
		}

		private void InitializeGenericCommandBindings()
		{
			Editor.TextArea.CommandBindings.Add(new CommandBinding(GenericCommands.MultiNodeEdit, MultiNodeEditHandler));
			Editor.TextArea.CommandBindings.Add(new CommandBinding(DiagnosticCommands.ShowTokenCommand, ShowTokenCommandExecutionHandler));
		}

		private void CanExecuteShowCodeCompletionHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = _documentRepository.StatementText == Editor.Text;
		}

		private void ShowCodeCompletionOptions(object sender, ExecutedRoutedEventArgs e)
		{
			CreateCodeCompletionWindow(true, Editor.CaretOffset);
		}

		private void CanExecuteCancelUserActionHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			args.CanExecute = ActiveOutputViewer.IsBusy;
		}

		private void CancelUserActionHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Trace.WriteLine("Action is about to cancel. ");
			ActiveOutputViewer.CancelUserAction();
		}

		private void ShowTokenCommandExecutionHandler(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var tokens = InfrastructureFactory.CreateTokenReader(_documentRepository.StatementText).GetTokens(true).ToArray();
			var message = "Parsed: " + String.Join(", ", tokens.Where(t => t.CommentType == CommentType.None).Select(t => "{" + t.Value + "}"));
			message += Environment.NewLine + "Comments: " + String.Join(", ", tokens.Where(t => t.CommentType != CommentType.None).Select(t => "{" + t.Value + "}"));
			MessageBox.Show(message, "Tokens", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		private void FormatStatement(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			GenericCommandHandler.ExecuteEditCommand(_documentRepository, Editor, _statementFormatter.ExecutionHandler.ExecutionHandler);
		}

		private void FormatStatementAsSingleLine(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			GenericCommandHandler.ExecuteEditCommand(_documentRepository, Editor, _statementFormatter.SingleLineExecutionHandler.ExecutionHandler);
		}

		private void NavigateToQueryBlockRoot(object sender, ExecutedRoutedEventArgs args)
		{
			var executionContext = ActionExecutionContext.Create(Editor, _documentRepository);
			var queryBlockRootIndex = _navigationService.NavigateToQueryBlockRoot(executionContext);
			Editor.NavigateToOffset(queryBlockRootIndex);
		}
		
		private void NavigateToDefinition(object sender, ExecutedRoutedEventArgs args)
		{
			var executionContext = ActionExecutionContext.Create(Editor, _documentRepository);
			var queryBlockRootIndex = _navigationService.NavigateToDefinition(executionContext);
			Editor.NavigateToOffset(queryBlockRootIndex);
		}

		private void RefreshDatabaseModel(object sender, ExecutedRoutedEventArgs args)
		{
			DatabaseModel.Refresh(true);
		}

		private void ShowFunctionOverloads(object sender, ExecutedRoutedEventArgs args)
		{
			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, Editor.CaretOffset);
			if (functionOverloads.Count == 0)
			{
				return;
			}

			DynamicPopup.Child = new FunctionOverloadList { FunctionOverloads = functionOverloads, FontFamily = new FontFamily("Segoe UI") }.AsPopupChild();
			_isToolTipOpenByShortCut = true;

			var rectangle = Editor.TextArea.Caret.CalculateCaretRectangle();
			DynamicPopup.Placement = PlacementMode.Relative;
			DynamicPopup.HorizontalOffset = rectangle.Left - Editor.TextArea.TextView.HorizontalOffset;
			DynamicPopup.VerticalOffset = rectangle.Top - Editor.TextArea.TextView.VerticalOffset + Editor.TextArea.TextView.DefaultLineHeight;
			DynamicPopup.IsOpen = true;
		}

		private void CanExecuteDatabaseCommandHandler(object sender, CanExecuteRoutedEventArgs args)
		{
			if (!DatabaseModel.IsInitialized || ActiveOutputViewer.IsBusy || ActiveOutputViewer.IsDebuggerControlVisible || !String.Equals(_documentRepository.StatementText, Editor.Text))
			{
				return;
			}

			args.CanExecute = BuildStatementExecutionModel(false).Count > 0;
		}

		private void ExecuteDatabaseCommandWithActualExecutionPlanHandler(object sender, ExecutedRoutedEventArgs args)
		{
			ExecuteDatabaseCommandHandlerInternal(true);
		}

		private void ExecuteDatabaseCommandHandler(object sender, ExecutedRoutedEventArgs args)
		{
			ExecuteDatabaseCommandHandlerInternal(false);
		}

		private async void ExecuteDatabaseCommandHandlerInternal(bool gatherExecutionStatistics)
		{
			if (ActiveOutputViewer.IsPinned)
			{
				try
				{
					AddNewOutputViewer();
				}
				catch (InvalidOperationException e)
				{
					Messages.ShowInformation(e.Message);
					return;
				}
			}

			var executionModels = BuildStatementExecutionModel(true);
			var executionModel =
				new StatementBatchExecutionModel
				{
					Statements = executionModels,
					GatherExecutionStatistics = gatherExecutionStatistics,
					EnableDebug = EnableDebug
				};

			if (EnableDebug && executionModels.Count > 1)
			{
				Messages.ShowInformation("Debugging of multiple statements is not supported. ");
				return;
			}

			await ActiveOutputViewer.ExecuteDatabaseCommandAsync(executionModel);
		}

		private IReadOnlyList<StatementExecutionModel> BuildStatementExecutionModel(bool includeStatementText)
		{
			var executionModels = new List<StatementExecutionModel>();

			var selectionEnd = Editor.SelectionStart + Editor.SelectionLength;
			var selectionStart = Editor.SelectionStart;
			var isOnlyStatement = true;
			foreach (var validationModel in _documentRepository.ValidationModels.Values)
			{
				var statement = validationModel.Statement;
				if (Editor.SelectionStart > statement.RootNode.SourcePosition.IndexEnd + 1)
				{
					continue;
				}

				if (isOnlyStatement && Editor.SelectionLength == 0)
				{
					if (Editor.SelectionStart < statement.SourcePosition.IndexStart)
					{
						break;
					}

					selectionStart = statement.SourcePosition.IndexStart;
					selectionEnd = statement.RootNode.SourcePosition.IndexEnd + 1;
					isOnlyStatement = false;
				}

				if (selectionEnd < statement.SourcePosition.IndexStart)
				{
					break;
				}

				var selectionStartAfterStatementStart = selectionStart > statement.SourcePosition.IndexStart;
				selectionStart = selectionStartAfterStatementStart
					? selectionStart
					: statement.SourcePosition.IndexStart;
				var selectionEndBeforeStatementEnd = selectionEnd < statement.RootNode.SourcePosition.IndexEnd + 1;
				var statementIndexEnd = selectionEndBeforeStatementEnd
					? selectionEnd
					: statement.RootNode.SourcePosition.IndexEnd + 1;

				string statementText = null;
				if (includeStatementText)
				{
					statementText = Editor.Text.Substring(selectionStart, statementIndexEnd - selectionStart);
					if (statementText.Trim().Length == 0)
					{
						continue;
					}
				}

				var bindVariables = statement.BindVariables
					.Where(bv => bv.Nodes.Any(n => n.SourcePosition.IndexStart >= selectionStart && n.SourcePosition.IndexEnd + 1 <= statementIndexEnd))
					.Select(bv => bv.Name)
					.ToHashSet();

				var bindVariableModels = BindVariables.Where(m => bindVariables.Contains(m.Name));

				executionModels.Add(
					new StatementExecutionModel
					{
						ValidationModel = validationModel,
						IsPartialStatement = selectionStartAfterStatementStart || selectionEndBeforeStatementEnd,
						StatementText = statementText,
						BindVariables = bindVariableModels.ToArray()
					});
			}

			return executionModels;
		}

		public void Dispose()
		{
			ConfigurationProvider.ConfigurationChanged -= ConfigurationChangedHandler;
			App.MainWindow.DocumentTabControl.SelectionChanged -= DocumentTabControlSelectionChangedHandler;
			Application.Current.Deactivated -= ApplicationDeactivatedHandler;

			_documentFileWatcher?.Dispose();

			TabItemContextMenu.CommandBindings.Clear();

			_timerReParse.Stop();

			_parsingCancellationTokenSource?.Dispose();

			DisposeOutputViewers();

			DatabaseModel?.Dispose();
		}

		private void DisposeOutputViewers()
		{
			foreach (var outputViewer in _outputViewers)
			{
				outputViewer.Dispose();
			}

			_outputViewers.Clear();
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
			if (_documentRepository.StatementText != Editor.Text)
			{
				return;
			}

			var sourceNodes = _documentRepository.ValidationModels.Values
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
			var executionContext = ActionExecutionContext.Create(Editor, _documentRepository);
			_navigationService.FindUsages(executionContext);
			AddHighlightSegments(executionContext.SegmentsToReplace);
		}

		private void NavigateToPreviousHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _backgroundRenderer.HighlightSegments
						.Where(s => s.IndextStart < Editor.CaretOffset)
						.OrderByDescending(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToNextHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _backgroundRenderer.HighlightSegments
						.Where(s => s.IndextStart > Editor.CaretOffset)
						.OrderBy(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToUsage(IEnumerable<TextSegment> nextSegments)
		{
			if (!_backgroundRenderer.HighlightSegments.Any())
			{
				return;
			}

			var nextSegment = nextSegments.FirstOrDefault();
			if (!nextSegment.Equals(TextSegment.Empty))
			{
				Editor.CaretOffset = nextSegment.IndextStart;
				Editor.ScrollToCaret();
			}
		}

		private void MultiNodeEditHandler(object sender, ExecutedRoutedEventArgs args)
		{
			try
			{
				if (_multiNodeEditor == null &&
				    MultiNodeEditor.TryCreateMultiNodeEditor(Editor, ActionExecutionContext.Create(Editor, _documentRepository), InfrastructureFactory.CreateMultiNodeEditorDataProvider(), out _multiNodeEditor))
				{
					RedrawMultiEditSegments();
				}
			}
			catch (Exception exception)
			{
				App.CreateErrorLog(exception);
				Messages.ShowError(exception.ToString());
			}
		}

		private void RedrawMultiEditSegments(bool forceRedraw = false)
		{
			if (_multiNodeEditor == null)
			{
				_backgroundRenderer.MasterSegment = null;
				_backgroundRenderer.SynchronizedSegments = null;
			}
			else
			{
				_backgroundRenderer.MasterSegment = _multiNodeEditor.MasterSegment;
				_backgroundRenderer.SynchronizedSegments = _multiNodeEditor.SynchronizedSegments;
			}

			if (forceRedraw || _multiNodeEditor != null)
			{
				Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
			}
		}

		private void PageLoadedHandler(object sender, RoutedEventArgs e)
		{
			if (_isInitializing)
			{
				EditorNavigationService.IsEnabled = false;

				App.MainWindow.DocumentTabControl.SelectionChanged += DocumentTabControlSelectionChangedHandler;
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

				var connectionString = ConfigurationProvider.ConnectionStrings
					.Cast<ConnectionStringSettings>()
					.FirstOrDefault(cs => String.Equals(cs.Name, WorkDocument.ConnectionName));

				CurrentConnection = connectionString ?? ConfigurationProvider.ConnectionStrings[0];

				Editor.Text = WorkDocument.Text;

				if (Editor.Document.TextLength >= WorkDocument.SelectionStart)
				{
					var storedSelectionEndIndex = WorkDocument.SelectionStart + WorkDocument.SelectionLength;
					var textChangedMeanwhile = storedSelectionEndIndex > Editor.Document.TextLength;
					var validSelectionEndIndex = textChangedMeanwhile
						? Editor.Document.TextLength
						: storedSelectionEndIndex;

					if (WorkDocument.SelectionType == SelectionType.Simple)
					{
						Editor.SelectionStart = WorkDocument.SelectionStart;
						Editor.SelectionLength = validSelectionEndIndex - WorkDocument.SelectionStart;
					}
					else if (!textChangedMeanwhile)
					{
						var selectionTopLeft = Editor.Document.GetLocation(WorkDocument.SelectionStart);
						var selectioBottomRight = Editor.Document.GetLocation(validSelectionEndIndex);
						Editor.TextArea.Selection = new RectangleSelection(Editor.TextArea, new TextViewPosition(selectionTopLeft.Line, selectionTopLeft.Column), new TextViewPosition(selectioBottomRight.Line, selectioBottomRight.Column));
					}
				}

				Editor.CaretOffset = Editor.Document.TextLength >= WorkDocument.CursorPosition
					? WorkDocument.CursorPosition
					: Editor.Document.TextLength;

				IsModified = WorkDocument.IsModified;

				_isInitializing = false;

				CurrentSchema = WorkDocument.SchemaName;

				EditorNavigationService.IsEnabled = true;
			}

			Editor.Focus();
		}

		private void ApplicationDeactivatedHandler(object sender, EventArgs eventArgs)
		{
			DynamicPopup.IsOpen = false;
		}

		private void DocumentTabControlSelectionChangedHandler(object sender, SelectionChangedEventArgs selectionChangedEventArgs)
		{
			DynamicPopup.IsOpen = false;
			_isToolTipOpenByCaretChange = false;
			_isToolTipOpenByShortCut = false;
		}

		private void CaretPositionChangedHandler(object sender, EventArgs eventArgs)
		{
			EditorNavigationService.RegisterDocumentCursorPosition(WorkDocument, Editor.CaretOffset);

			_isToolTipOpenByCaretChange = false;

			CloseToolTipWhenNotOpenByShortCutOrCaretChange();

			if (_documentRepository.Statements == null)
			{
				return;
			}

			var correspondingSegments = new List<SourcePosition>();

			if (!_isParsing)
			{
				ShowHideBindVariableList();

				var activeStatement = _documentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
				HighlightActiveStatement(activeStatement);

				var terminal = activeStatement?.GetTerminalAtPosition(Editor.CaretOffset, t => t.Type == NodeType.Terminal && t.Token.Value.In("(", ")", "[", "]", "{", "}"));
				if (terminal != null)
				{
					var childNodes = terminal.ParentNode.ChildNodes;
					var index = terminal.ParentNode.IndexOf(terminal);
					var increment = terminal.Token.Value.In(OpeningParenthesisOrBrackets) ? 1 : -1;
					var otherParenthesis = GetOppositeParenthesisOrBracket(terminal.Token.Value);

					var nestedCounter = 0;
					while (0 <= index && index < childNodes.Count)
					{
						index += increment;

						if (index < 0 || index >= childNodes.Count)
						{
							break;
						}

						var otherParenthesisTerminal = childNodes[index];
						if (otherParenthesisTerminal.Token == null)
						{
							continue;
						}

						if (increment == 1 && otherParenthesisTerminal.Token.Value.In(OpeningParenthesisOrBrackets))
						{
							nestedCounter++;
						}
						else if (increment == -1 && otherParenthesisTerminal.Token.Value.In(ClosingParenthesisOrBrackets))
						{
							nestedCounter++;
						}

						if (String.Equals(otherParenthesisTerminal.Token.Value, otherParenthesis) && nestedCounter-- == 0)
						{
							correspondingSegments.Add(terminal.SourcePosition);
							correspondingSegments.Add(otherParenthesisTerminal.SourcePosition);

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

								DynamicPopup.Child = new TextBox { FontFamily = Editor.FontFamily, FontSize = Editor.FontSize, Text = toolTipBuilder.ToString(), IsReadOnly = true }.AsPopupChild();
								DynamicPopup.Placement = PlacementMode.Relative;
								DynamicPopup.HorizontalOffset = Editor.TextArea.LeftMargins.Sum(m => m.DesiredSize.Width);
								DynamicPopup.VerticalOffset = -28;
								DynamicPopup.IsOpen = true;

								_isToolTipOpenByCaretChange = true;
							}

							break;
						}
					}
				}
				else
				{
					var executionContext = ActionExecutionContext.Create(Editor, _documentRepository);
					correspondingSegments.AddRange(_navigationService.FindCorrespondingSegments(executionContext));
				}
			}

			var oldCorrespondingTerminals = _colorizingTransformer.CorrespondingSegments.ToArray();
			_colorizingTransformer.SetCorrespondingTerminals(correspondingSegments);

			Editor.RedrawSegments(oldCorrespondingTerminals.Concat(correspondingSegments));
		}

		private void HighlightActiveStatement(StatementBase newActiveStatement)
		{
			var previousActiveStatement = _colorizingTransformer.ActiveStatement;
			if (!_colorizingTransformer.SetActiveStatement(newActiveStatement))
			{
				return;
			}

			if (previousActiveStatement?.ParseStatus == ParseStatus.SequenceNotFound || newActiveStatement?.ParseStatus == ParseStatus.SequenceNotFound)
			{
				Editor.TextArea.TextView.Redraw();
			}
			else
			{
				var segments = new List<SourcePosition>();
				if (previousActiveStatement != null)
				{
					segments.Add(previousActiveStatement.SourcePosition);
				}

				if (newActiveStatement != null)
				{
					segments.Add(newActiveStatement.SourcePosition);
				}

				Editor.RedrawSegments(segments);
			}
		}

		private static string GetOppositeParenthesisOrBracket(string parenthesisOrBracket)
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
					throw new ArgumentException("invalid parenthesis symbol", nameof(parenthesisOrBracket));
			}
		}

		private void ShowHideBindVariableList()
		{
			if (_documentRepository.Statements == null || !String.Equals(_documentRepository.StatementText, Editor.Text))
			{
				return;
			}

			var statements = new List<StatementBase>(BuildStatementExecutionModel(false).Select(m => m.ValidationModel.Statement));
			if (statements.All(s => s.BindVariables.Count == 0))
			{
				BindVariables = new BindVariableModel[0];
				_currentBindVariables.Clear();
				return;
			}

			if (ApplyBindVariables(statements))
			{
				return;
			}

			_currentBindVariables.Clear();

			foreach (var bindVariable in statements.SelectMany(s => s.BindVariables).Distinct(v => v.Name))
			{
				_currentBindVariables.Add(bindVariable.Name, bindVariable);
			}
			
			BindVariables = BuildBindVariableModels(_currentBindVariables.Values);
		}

		private bool ApplyBindVariables(IEnumerable<StatementBase> statements)
		{
			var matchedCount = 0;
			var bindVariableCount = 0;
			foreach (var statementVariable in statements.SelectMany(s => s.BindVariables))
			{
				bindVariableCount++;
				
				BindVariableConfiguration currentVariable;
				if (_currentBindVariables.TryGetValue(statementVariable.Name, out currentVariable) &&
				    currentVariable.Nodes.Select(n => n.SourcePosition).SequenceEqual(statementVariable.Nodes.Select(n => n.SourcePosition)))
				{
					matchedCount++;
					statementVariable.DataType = currentVariable.DataType;
					statementVariable.Value = currentVariable.Value;
				}
			}

			return matchedCount == _currentBindVariables.Count && matchedCount == bindVariableCount;
		}

		private IReadOnlyList<BindVariableModel> BuildBindVariableModels(IEnumerable<BindVariableConfiguration> bindVariables)
		{
			var models = new List<BindVariableModel>();
			foreach (var bindVariable in bindVariables)
			{
				var model = new BindVariableModel(bindVariable);
				model.PropertyChanged += (sender, args) => _providerConfiguration.SetBindVariable(model.BindVariable);

				var storedVariable = _providerConfiguration.GetBindVariable(bindVariable.Name);
				if (storedVariable != null)
				{
					model.DataType = model.BindVariable.DataTypes[storedVariable.DataType];
					model.IsFilePath = storedVariable.IsFilePath;
					model.Value = storedVariable.Value;
				}

				models.Add(model);
			}

			return models.AsReadOnly();
		}

		private void TextEnteredHandler(object sender, TextCompositionEventArgs e)
		{
			if (Editor.Document.IsInUpdate)
			{
				Editor.Document.EndUpdate();
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
					pairCharacterHandled = IsNextCharacterBlank() || IsAtValidClosingParenthesis();
					if (pairCharacterHandled)
					{
						InsertPairCharacter("()");
					}

					break;
				case "\"":
					pairCharacterHandled = !PreviousPairCharacterExists(text, '"', '"') && IsNextCharacterBlank() && _documentRepository.CanAddPairCharacter(Editor.CaretOffset, '"');
					if (pairCharacterHandled)
					{
						InsertPairCharacter("\"\"");
					}
					
					break;
				case "'":
					pairCharacterHandled = !PreviousPairCharacterExists(text, '\'', '\'') && IsNextCharacterBlank() && _documentRepository.CanAddPairCharacter(Editor.CaretOffset, '\'');
					if (pairCharacterHandled)
					{
						InsertPairCharacter("''");
					}

					break;
			}

			return pairCharacterHandled;
		}

		private bool IsAtValidClosingParenthesis()
		{
			var activeStatement = _documentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			var terminal = activeStatement?.GetTerminalAtPosition(Editor.CaretOffset, t => t.Type == NodeType.Terminal && String.Equals(t.Token.Value, ")"));
			return terminal != null && terminal.SourcePosition.IndexStart == Editor.CaretOffset &&
			       terminal.ParentNode.ChildNodes.Any(n => n.Type == NodeType.Terminal && n != terminal && String.Equals(n.Token.Value, "("));
		}

		private void TextEnteringHandler(object sender, TextCompositionEventArgs e)
		{
			if (Editor.IsReadOnly)
			{
				TimedNotificationMessage = "Editor is read only please wait until the action is finished. ";
				_timerTimedNotification.Start();
				e.Handled = true;
			}

			if (!Editor.IsMultiSelectionActive)
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
			}

			if (_multiNodeEditor != null && !_multiNodeEditor.Replace(e.Text))
			{
				_multiNodeEditor = null;
			}

			if (e.Text.Length == 1 && _completionWindow != null && e.Text == "\t")
			{
				_completionWindow.CompletionList.RequestInsertion(e);
			}
		}

		internal void ActivateSnippet(int completionSegmentOffset, int completionSegmentLength, CompletionData completionData)
		{
			var activeSnippet = new ActiveSnippet(completionSegmentOffset, completionSegmentLength, Editor, completionData);
			_backgroundRenderer.ActiveSnippet = activeSnippet.ActiveAnchors == null ? null : activeSnippet;
			_multiNodeEditor = activeSnippet.GetMultiNodeEditor();
		}

		private async void CreateCodeCompletionWindow(bool forcedInvokation, int caretOffset)
		{
			IReadOnlyCollection<CompletionData> items;
			try
			{
				items = await Task.Factory.StartNew(
					() =>
						_codeSnippetProvider.GetSnippets(_documentRepository, _documentRepository.StatementText, caretOffset)
							.Select(s => new CompletionData(s, this))
							.Concat(_codeCompletionProvider.ResolveItems(_documentRepository, DatabaseModel, caretOffset, forcedInvokation)
								.Select(i => new CompletionData(i)))
							.ToArray());
			}
			catch (Exception e)
			{
				Trace.WriteLine($"Code completion window item generation failed: {e}");
				App.CreateErrorLog(e);
				return;
			}

			if (!String.Equals(_documentRepository.StatementText, Editor.Text))
			{
				return;
			}

			CreateCompletionWindow(items);
		}

		private void CreateCompletionWindow(IReadOnlyCollection<CompletionData> items)
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
			foreach (var item in items)
			{
				item.InitializeContent();
				listItems.Add(item);
			}
			
			_completionWindow = completionWindow;
			_completionWindow.Closed += CompletionWindowClosedHadler;
			_completionWindow.SizeChanged += CompletionWindowSizeChangedHandler;
			_completionWindow.CompletionList.ListBox.AddHandler(ScrollViewer.ScrollChangedEvent, (RoutedEventHandler)CompletionListScrollChanged);

			if (listItems.Count == 1)
			{
				_completionWindow.CompletionList.ListBox.SelectedIndex = 0;
			}

			DisableCodeCompletion();

			_completionWindow.Show();
		}

		private void CompletionListScrollChanged(object sender, RoutedEventArgs e)
		{
			UpdateCompletionItemHighlight();
		}

		private void CompletionWindowClosedHadler(object sender, EventArgs e)
		{
			_completionWindow.SizeChanged -= CompletionWindowSizeChangedHandler;
			_completionWindow.CompletionList.ListBox.RemoveHandler(ScrollViewer.ScrollChangedEvent, (RoutedEventHandler)CompletionListScrollChanged);
			_completionWindow = null;
		}

		private void CompletionWindowSizeChangedHandler(object sender, SizeChangedEventArgs e)
		{
			if (_completionWindow.MinWidth < _completionWindow.Width)
			{
				_completionWindow.MinWidth = _completionWindow.Width;
			}
		}

		public void ReParse()
		{
			DisableCodeCompletion();

			Parse();
		}

		private void EditorTextChangedHandler(object sender, EventArgs e)
		{
			if (_isInitializing)
			{
				return;
			}

			RedrawMultiEditSegments();

			CheckDocumentModified();

			Parse();

			UpdateCompletionItemHighlight();

			if (_undoExecuted && _backgroundRenderer.ActiveSnippet != null && !_backgroundRenderer.ActiveSnippet.ActiveAnchorsValid)
			{
				_backgroundRenderer.ActiveSnippet = null;
			}

			_undoExecuted = false;
		}

		private void UpdateCompletionItemHighlight()
		{
			if (_completionWindow == null)
			{
				return;
			}

			var firstItem = (CompletionData)_completionWindow.CompletionList.CompletionData[0];
			if (firstItem.Snippet != null || firstItem.Node != null)
			{
				_completionWindow.StartOffset = firstItem.Snippet == null
					? firstItem.Node.SourcePosition.IndexStart
					: firstItem.Snippet.SourceToReplace.IndexStart;
			}

			var length = _completionWindow.EndOffset - _completionWindow.StartOffset;
			if (_completionWindow.StartOffset + length > Editor.Document.TextLength)
			{
				length = Editor.Document.TextLength - _completionWindow.StartOffset;
			}

			var textToHighlight = Editor.Document.GetText(_completionWindow.StartOffset, length);

			foreach (CompletionData item in _completionWindow.CompletionList.CompletionData)
			{
				var listBoxItem = (FrameworkElement)_completionWindow.CompletionList.ListBox.ItemContainerGenerator.ContainerFromItem(item);
				if (listBoxItem == null)
				{
					continue;
				}

				if (_completionWindow.CompletionList.ListBox.IsInViewport(listBoxItem))
				{
					item.Highlight(textToHighlight);
				}
			}
		}

		private void CheckDocumentModified()
		{
			IsModified = WorkDocument.IsModified = IsDirty;
		}

		private void Parse()
		{
			if (_isParsing)
			{
				_parsingCancellationTokenSource.Cancel();

				if (!_timerReParse.IsEnabled)
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
				_documentRepository.UpdateStatements(Editor.Text);
				ParseDoneHandler(true);
			}
			else
			{
				_documentRepository.UpdateStatementsAsync(Editor.Text, _parsingCancellationTokenSource.Token)
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

			_colorizingTransformer.SetDocumentRepository(_documentRepository);

			Dispatcher.Invoke(ParseDoneUiHandler);
		}

		private void ParseDoneUiHandler()
		{
			var hasTextChanged = !String.Equals(_documentRepository.StatementText, Editor.Text);
			if (!hasTextChanged)
			{
				_foldingStrategy.UpdateFoldings(_documentRepository.Statements);
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

			_colorizingTransformer.SetActiveStatement(_documentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset));

			Editor.TextArea.TextView.Redraw();

			ShowHideBindVariableList();

			if (_enableCodeComplete && _completionWindow == null && IsSelectedPage)
			{
				CreateCodeCompletionWindow(false, Editor.CaretOffset);
			}
		}

		void MouseHoverHandler(object sender, MouseEventArgs e)
		{
			if (_isToolTipOpenByShortCut || _isToolTipOpenByCaretChange || DynamicPopup.IsOpen)
			{
				return;
			}

			var visualPosition = e.GetPosition(Editor);
			var position = Editor.GetPositionFromPoint(visualPosition);
			if (!position.HasValue || _documentRepository.Statements == null)
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
				DynamicPopup.Child = new TextBox { Text = toolTipText, FontFamily = Editor.FontFamily, IsReadOnly = true }.AsPopupChild();
			}
			else
			{

				IToolTip toolTip = null;

				try
				{
					toolTip = _toolTipProvider.GetToolTip(_documentRepository, offset);
				}
				catch (Exception exception)
				{
					App.CreateErrorLog(exception);
					Messages.ShowError(exception.ToString());
				}

				if (toolTip == null)
				{
					return;
				}

				toolTip.Pin += ToolTipPinHandler;
				toolTip.Control.FontFamily = new FontFamily("Segoe UI");
				DynamicPopup.Child = toolTip.Control.AsPopupChild();
			}

			DynamicPopup.Placement = PlacementMode.Mouse;
			DynamicPopup.HorizontalOffset = 0;
			DynamicPopup.VerticalOffset = 0;
			DynamicPopup.IsOpen = true;
			e.Handled = true;
		}

		private void ToolTipPinHandler(object sender, EventArgs eventArgs)
		{
			DynamicPopup.IsOpen = false;
			
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
					Owner = App.MainWindow
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
			if (!DynamicPopup.IsOpen)
			{
				return;
			}

			var position = e.GetPosition(DynamicPopup.Child);

			if (position.Y < -Editor.FontSize || position.Y > DynamicPopup.Child.RenderSize.Height || position.X < 0 || position.X > DynamicPopup.Child.RenderSize.Width)
			{
				CloseToolTipWhenNotOpenByShortCutOrCaretChange();
			}
		}

		private void CloseToolTipWhenNotOpenByShortCutOrCaretChange()
		{
			if (!_isToolTipOpenByShortCut && !_isToolTipOpenByCaretChange)
			{
				DynamicPopup.IsOpen = false;
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
					Command = new ContextActionTextEditorCommand(Editor, action)
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
				c => _contextActionProvider.GetContextActions(_documentRepository, c)
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
				c => _codeSnippetProvider.GetCodeGenerationItems(_documentRepository)
					.Select(ci => CreateContextMenuItemFromCodeSnippet(ci, c)));
		}

		private MenuItem CreateContextMenuItemFromCodeSnippet(ICodeSnippet codeSnippet, ActionExecutionContext executionContext)
		{
			var executionHandler =
				new CommandExecutionHandler
				{
					ExecutionHandler =
						c =>
						{
							ActivateSnippet(c.CaretOffset, 0, new CompletionData(codeSnippet, this));
							c.CaretOffset = Editor.CaretOffset;
							c.SelectionLength = Editor.SelectionLength;
						}
				};

			return BuildContextMenuItem(new ContextAction(codeSnippet.Name, executionHandler, executionContext));
		}

		private bool PopulateContextMenuItems(Func<ActionExecutionContext, IEnumerable<MenuItem>> buildMenuItemFunction)
		{
			_contextActionMenu.Items.Clear();

			var executionContext = ActionExecutionContext.Create(Editor, _documentRepository);
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

			if (DynamicPopup != null)
			{
				DynamicPopup.IsOpen = false;
			}

			var isReturn = e.Key == Key.Return;
			var isEscape = e.Key == Key.Escape;
			if (isReturn || isEscape)
			{
				DisableCodeCompletion();

				if (isEscape)
				{
					ClearLastHighlight();
					_backgroundRenderer.ActiveSnippet = null;
				}
				else if (_backgroundRenderer.ActiveSnippet != null)
				{
					if (!_backgroundRenderer.ActiveSnippet.SelectNextParameter())
					{
						_backgroundRenderer.ActiveSnippet = null;
					}

					e.Handled = true;
				}

				if (_multiNodeEditor != null)
				{
					if (isEscape)
					{
						_completionWindow?.Close();
						_multiNodeEditor.Cancel();
					}

					e.Handled = true;
					_multiNodeEditor = null;
					RedrawMultiEditSegments(true);
				}
			}

			var isControlPressed = Keyboard.Modifiers == ModifierKeys.Control;
			if (e.Key == Key.Back || e.Key == Key.Delete || (e.Key.In(Key.V, Key.Z, Key.Insert) && isControlPressed))
			{
				DisableCodeCompletion();

				var clipboardText = Clipboard.GetText();
				if (_multiNodeEditor != null && e.Key.In(Key.V, Key.Insert) && !String.IsNullOrEmpty(clipboardText))
				{
					_multiNodeEditor.Replace(clipboardText);
				}
			}

			_undoExecuted = isControlPressed && e.Key == Key.Z;

			if ((e.Key == Key.Back || e.Key == Key.Delete) && _multiNodeEditor != null)
			{
				Editor.Document.BeginUpdate();

				if (!_multiNodeEditor.RemoveCharacter(e.Key == Key.Back))
				{
					_multiNodeEditor = null;
				}

				RedrawMultiEditSegments(true);
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

		private void AddHighlightSegments(ICollection<TextSegment> segments)
		{
			_backgroundRenderer.AddHighlightSegments(segments);
			Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
		}

		private void ClearLastHighlight()
		{
			AddHighlightSegments(null);
		}

		private bool AreConsencutive(char previousCharacter, char currentCharacter)
		{
			return Editor.Text[Editor.CaretOffset] == currentCharacter && Editor.Text[Editor.CaretOffset - 1] == previousCharacter;
		}

		private async void ExecuteExplainPlanCommandHandler(object sender, ExecutedRoutedEventArgs args)
		{
			var executionModels = BuildStatementExecutionModel(true);
			if (executionModels.Count > 1)
			{
				Messages.ShowInformation("Multiple statements are not supported. ");
			}
			else
			{
				await ActiveOutputViewer.ExecuteExplainPlanAsync(executionModels[0]);
			}
		}

		private void CompilationErrorHandler(object sender, CompilationErrorArgs e)
		{
			var failedStatementSourcePosition = e.CompilationError.Statement.SourcePosition;
			var isStatementUnchanged = _documentRepository.Statements.Any(s => failedStatementSourcePosition == s.SourcePosition);
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
			var executionContext = ActionExecutionContext.Create(Editor, _documentRepository);
			_createHelpProvider.ShowHelp(executionContext);
		}

		private void BindVariableEditorGotFocusHandler(object sender, RoutedEventArgs e)
		{
			var bindVariable = (BindVariableModel)((FrameworkElement)sender).DataContext;
			var executionContext = ActionExecutionContext.Create(Editor, _documentRepository);
			executionContext.CaretOffset = bindVariable.BindVariable.Nodes[0].SourcePosition.IndexStart;
			_navigationService.DisplayBindVariableUsages(executionContext);
			AddHighlightSegments(executionContext.SegmentsToReplace);
		}

		private void BindVariableEditorLostFocus(object sender, RoutedEventArgs e)
		{
			ClearLastHighlight();
		}

		private void CloseOutputViewerClickHandler(object sender, RoutedEventArgs e)
		{
			var outputViewer = (OutputViewer)((Button)e.Source).CommandParameter;
			_outputViewers.Remove(outputViewer);
			if (ActiveOutputViewer == null)
			{
				OutputViewerList.SelectedItem = _outputViewers[0];
			}

			outputViewer.Dispose();
		}

		private void ShowExecutionHistoryExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			App.ShowExecutionHistory(_providerConfiguration.ProviderName);
		}

		private void BindVariableFromFileClickHandler(object sender, RoutedEventArgs e)
		{
			var button = (ToggleButton)sender;
			var bindVariableModel = (BindVariableModel)button.DataContext;
			if (button.IsChecked == true)
			{
				if (!File.Exists(Convert.ToString(bindVariableModel.Value)))
				{
					var dialog = new OpenFileDialog {Filter = "All files (*.*)|*"};
					if (dialog.ShowDialog() != true)
					{
						button.IsChecked = false;
						return;
					}

					bindVariableModel.Value = dialog.FileName;
				}
			}
			else
			{
				bindVariableModel.Value = String.Empty;
			}
		}
	}

	public enum ConnectionStatus
	{
		Disconnected,
		Connecting,
		Connected
	}
}
