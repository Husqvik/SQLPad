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
	/// <summary>
	/// Interaction logic for DocumentPage.xaml
	/// </summary>
	public partial class DocumentPage : IDisposable
	{
		private const int RowBatchSize = 100;
		
		private SqlDocumentRepository _sqlDocumentRepository;
		private IInfrastructureFactory _infrastructureFactory;
		private ICodeCompletionProvider _codeCompletionProvider;
		private ICodeSnippetProvider _codeSnippetProvider;
		private IContextActionProvider _contextActionProvider;
		private IStatementFormatter _statementFormatter;
		private IDatabaseModel _databaseModel;
		private IToolTipProvider _toolTipProvider;
		private INavigationService _navigationService;

		private MultiNodeEditor _multiNodeEditor;
		private readonly SqlDocumentColorizingTransformer _colorizingTransformer = new SqlDocumentColorizingTransformer();
		private CancellationTokenSource _cancellationTokenSource;

		private static readonly CellValueConverter CellValueConverter = new CellValueConverter();
		private static readonly Style CellStyleRightAlign = new Style(typeof(DataGridCell));
		private static readonly Style HeaderStyleRightAlign = new Style(typeof(DataGridColumnHeader));
		
		private readonly ToolTip _toolTip = new ToolTip();
		private bool _isToolTipOpenByShortCut;
		private CompletionWindow _completionWindow;
		private readonly PageModel _pageModel;
		private bool _isParsing;
		private readonly Timer _timerReParse = new Timer(100);
		private readonly Timer _timerExecutionMonitor = new Timer(100);
		private readonly Stopwatch _stopWatch = new Stopwatch();
		private readonly string _initialDocumentHeader = "New*";
		private readonly List<CommandBinding> _specificCommandBindings = new List<CommandBinding>(); 
		
		public EventHandler ParseFinished = delegate { };

		internal bool IsParsingSynchronous { get; set; }
		
		public TextEditorAdapter EditorAdapter { get; private set; }
		
		public FileInfo File { get; private set; }

		public string DocumentHeader { get { return File == null ? _initialDocumentHeader : File.Name + (IsDirty ? "*" : null); } }

		public bool IsDirty { get { return Editor.IsModified; } }
		
		public IDatabaseModel DatabaseModel { get { return _databaseModel; } }

		static DocumentPage()
		{
			CellStyleRightAlign.Setters.Add(new Setter(Block.TextAlignmentProperty, TextAlignment.Right));
			HeaderStyleRightAlign.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Right));
		}
		
		public DocumentPage(FileInfo file, bool recoveryMode = false)
		{
			InitializeComponent();

			ComboBoxConnection.IsEnabled = ConfigurationProvider.ConnectionStrings.Count > 1;
			ComboBoxConnection.ItemsSource = ConfigurationProvider.ConnectionStrings;
			ComboBoxConnection.SelectedIndex = 0;

			InitializeGenericCommandBindings();

			_timerReParse.Elapsed += (sender, args) => Dispatcher.Invoke(ReParse);
			_timerExecutionMonitor.Elapsed += (sender, args) => Dispatcher.Invoke(() => TextExecutionTime.Text = FormatElapsedMilliseconds(_stopWatch.Elapsed));

			_pageModel = new PageModel(this) { CurrentConnection = ConfigurationProvider.ConnectionStrings[0] };

			ConfigureEditor();

			if (file != null)
			{
				File = file;
				Editor.Load(file.FullName);
			}

			if (recoveryMode)
			{
				_initialDocumentHeader = "RecoveredDocument*";
				File = null;
				Editor.IsModified = true;
			}

			_pageModel.DocumentHeader = DocumentHeader;

			DataContext = _pageModel;
		}

		private void ConfigureEditor()
		{
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
			if (_databaseModel != null)
			{
				_databaseModel.Dispose();
			}

			_infrastructureFactory = ConfigurationProvider.GetInfrastructureFactory(connectionString.Name);
			_codeCompletionProvider = _infrastructureFactory.CreateCodeCompletionProvider();
			_codeSnippetProvider = _infrastructureFactory.CreateSnippetProvider();
			_contextActionProvider = _infrastructureFactory.CreateContextActionProvider();
			_statementFormatter = _infrastructureFactory.CreateSqlFormatter(new SqlFormatterOptions());
			_toolTipProvider = _infrastructureFactory.CreateToolTipProvider();
			_navigationService = _infrastructureFactory.CreateNavigationService();
			_databaseModel = _infrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings[connectionString.Name]);
			_sqlDocumentRepository = new SqlDocumentRepository(_infrastructureFactory.CreateParser(), _infrastructureFactory.CreateStatementValidator(), _databaseModel);

			_colorizingTransformer.SetParser(_infrastructureFactory.CreateParser());

			InitializeSpecificCommandBindings();

			_databaseModel.RefreshStarted += DatabaseModelRefreshStartedHandler;
			_databaseModel.RefreshFinished += DatabaseModelRefreshFinishedHandler;
			_databaseModel.RefreshIfNeeded();
		}

		private ScrollViewer GetResultGridScrollViewer()
		{
			var border = VisualTreeHelper.GetChild(ResultGrid, 0) as Decorator;
			if (border == null)
				return null;

			return border.Child as ScrollViewer;
		}

		public void SaveCommandExecutedHandler(object sender, ExecutedRoutedEventArgs args)
		{
			Save();
		}

		public bool Save()
		{
			if (File == null)
				return SaveAs();

			if (!IsDirty)
				return true;

			Editor.Save(File.FullName);
			_pageModel.DocumentHeader = File.Name;
			return true;
		}

		public bool SaveAs()
		{
			var dialog = new SaveFileDialog { Filter = "SQL Files (*.sql)|*.sql|All (*.*)|*" };
			if (dialog.ShowDialog() != true)
			{
				return false;
			}

			File = new FileInfo(dialog.FileName);
			Editor.Save(File.FullName);
			_pageModel.DocumentHeader = File.Name;
			return true;
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
			commandBindings.Add(new CommandBinding(GenericCommands.ShowFunctionOverloadCommand, ShowFunctionOverloads));
			commandBindings.Add(new CommandBinding(GenericCommands.DuplicateTextCommand, GenericCommandHandler.DuplicateText));
			commandBindings.Add(new CommandBinding(GenericCommands.BlockCommentCommand, GenericCommandHandler.HandleBlockComments));
			commandBindings.Add(new CommandBinding(GenericCommands.LineCommentCommand, GenericCommandHandler.HandleLineComments));
			commandBindings.Add(new CommandBinding(GenericCommands.ListContextActionCommand, (sender, args) => Editor.ContextMenu.IsOpen = PopulateContextActionMenu()));
			commandBindings.Add(new CommandBinding(GenericCommands.MultiNodeEditCommand, EditMultipleNodes));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToPreviousUsageCommand, NavigateToPreviousHighlightedUsage));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToNextUsageCommand, NavigateToNextHighlightedUsage));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToQueryBlockRootCommand, NavigateToQueryBlockRoot));
			commandBindings.Add(new CommandBinding(GenericCommands.NavigateToDefinitionRootCommand, NavigateToDefinition));
			commandBindings.Add(new CommandBinding(GenericCommands.ExecuteDatabaseCommandCommand, ExecuteDatabaseCommand, (sender, args) => args.CanExecute = !_databaseModel.IsExecuting));
			commandBindings.Add(new CommandBinding(GenericCommands.SaveCommand, SaveCommandExecutedHandler));
			commandBindings.Add(new CommandBinding(GenericCommands.FormatStatementCommand, FormatStatement));
			commandBindings.Add(new CommandBinding(GenericCommands.FindUsagesCommand, FindUsages));
			commandBindings.Add(new CommandBinding(GenericCommands.CancelStatementCommand, CancelStatementHandler, (sender, args) => args.CanExecute = _databaseModel.IsExecuting));

			commandBindings.Add(new CommandBinding(DiagnosticCommands.ShowTokenCommand, ShowTokenCommandExecutionHandler));

			ResultGrid.CommandBindings.Add(new CommandBinding(GenericCommands.FetchNextRowsCommand, FetchNextRows, CanFetchNextRows));
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

		private void CanFetchNextRows(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.ContinueRouting = ResultGrid.SelectedIndex < ResultGrid.Items.Count - 1 || !_databaseModel.CanFetch;
			canExecuteRoutedEventArgs.CanExecute = !canExecuteRoutedEventArgs.ContinueRouting;
		}

		private async void FetchNextRows(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var nextRowBatch = _databaseModel.FetchRecords(RowBatchSize);
			await SafeActionAsync(() => Task.Factory.StartNew(() => Dispatcher.Invoke(() => _pageModel.ResultRowItems.AddRange(nextRowBatch))));

			TextMoreRowsExist.Visibility = _databaseModel.CanFetch ? Visibility.Visible : Visibility.Collapsed;
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

		private void ShowFunctionOverloads(object sender, ExecutedRoutedEventArgs args)
		{
			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_sqlDocumentRepository, Editor.CaretOffset);
			if (functionOverloads.Count <= 0)
				return;

			_toolTip.Content = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			_isToolTipOpenByShortCut = true;

			var rectangle = Editor.TextArea.Caret.CalculateCaretRectangle();
			_toolTip.PlacementTarget = this;
			_toolTip.Placement = PlacementMode.Relative;
			_toolTip.HorizontalOffset = rectangle.Left;
			_toolTip.VerticalOffset = rectangle.Top + Editor.TextArea.TextView.DefaultLineHeight;

			_toolTip.IsOpen = true;
		}

		private async void ExecuteDatabaseCommand(object sender, ExecutedRoutedEventArgs args)
		{
			if (_sqlDocumentRepository.StatementText != Editor.Text)
				return;

			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			if (statement == null)
				return;

			_pageModel.ResultRowItems.Clear();
			GridLabel.Visibility = Visibility.Collapsed;
			TextMoreRowsExist.Visibility = Visibility.Collapsed;

			var commandText = Editor.SelectionLength > 0 ? Editor.SelectedText : statement.RootNode.GetStatementSubstring(Editor.Text);
			_cancellationTokenSource = new CancellationTokenSource();
			var actionResult = await SafeTimedActionAsync(() => _databaseModel.ExecuteStatementAsync(commandText, statement.ReturnDataset, _cancellationTokenSource.Token));

			if (!actionResult.IsSuccessful)
				return;

			TextExecutionTime.Text = FormatElapsedMilliseconds(actionResult.Elapsed);

			ResultGrid.Columns.Clear();

			if (_databaseModel.CanFetch)
			{
				GridLabel.Visibility = Visibility.Visible;
				InitializeResultGrid();
				FetchNextRows(null, null);

				if (ResultGrid.Items.Count > 0)
				{
					ResultGrid.SelectedIndex = 0;
				}
			}
		}

		private async Task<ActionResult> SafeTimedActionAsync(Func<Task> action)
		{
			var actionResult = new ActionResult();

			_stopWatch.Restart();
			_timerExecutionMonitor.Start();

			actionResult.IsSuccessful = await SafeActionAsync(action);
			actionResult.Elapsed = _stopWatch.Elapsed;
			
			_timerExecutionMonitor.Stop();
			_stopWatch.Stop();
			
			return actionResult;
		}

		private async Task<bool> SafeActionAsync(Func<Task> action)
		{
			try
			{
				await action();

				return true;
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return false;
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

		private void InitializeResultGrid()
		{
			foreach (var columnHeader in _databaseModel.GetColumnHeaders())
			{
				var columnTemplate =
					new DataGridTextColumn
					{
						Header = columnHeader.Name.Replace("_", "__"),
						Binding = new Binding(String.Format("[{0}]", columnHeader.ColumnIndex)) { Converter = CellValueConverter, ConverterParameter = columnHeader }
					};

				if (columnHeader.DataType.In(typeof(Decimal), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Byte)))
				{
					columnTemplate.HeaderStyle = HeaderStyleRightAlign;
					columnTemplate.CellStyle = CellStyleRightAlign;
				}

				ResultGrid.Columns.Add(columnTemplate);
			}
		}

		public void Dispose()
		{
			_timerReParse.Dispose();
			_timerExecutionMonitor.Dispose();
			_databaseModel.Dispose();
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
				MultiNodeEditor.TryCreateMultiNodeEditor(Editor, _infrastructureFactory.CreateMultiNodeEditorDataProvider(), _databaseModel, out _multiNodeEditor);
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

			_pageModel.DocumentHeader = DocumentHeader;

			var snippets = _codeSnippetProvider.GetSnippets(_sqlDocumentRepository, Editor.Text, Editor.CaretOffset).Select(i => new CompletionData(i)).ToArray();
			if (_completionWindow == null && snippets.Length > 0)
			{
				CreateSnippetCompletionWindow(snippets);
				_completionWindow.Closed += (o, args) =>
					// Workaround to display completion menu after the snippet is inserted
					Task.Factory.StartNew(() =>
					{
						Thread.Sleep(20);
						Dispatcher.Invoke(CreateCodeCompletionWindow);
					});
				return;
			}

			if (Editor.Text.Length == Editor.CaretOffset || Editor.Text[Editor.CaretOffset].In(' ', '\t', '\r', '\n'))
			{
				switch (e.Text)
				{
					case "(":
						InsertPairCharacter(")");
						break;
					case "\"":
						InsertPairCharacter("\"");
						break;
					case "'":
						InsertPairCharacter("'");
						break;
				}
			}

			if (e.Text != "." && e.Text != " " && e.Text != "\n")
			{
				if (_completionWindow != null && _completionWindow.CompletionList.ListBox.Items.Count == 0)
				{
					_completionWindow.Close();
				}

				return;
			}

			// Open code completion after the user has pressed dot:
			CreateCodeCompletionWindow();
		}

		private void InsertPairCharacter(string pairCharacter)
		{
			Editor.Document.Insert(Editor.CaretOffset, pairCharacter);
			Editor.CaretOffset--;
		}

		private void TextEnteringHandler(object sender, TextCompositionEventArgs e)
		{
			if ((Keyboard.IsKeyDown(Key.Oem2) || Keyboard.IsKeyDown(Key.D)) && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
			{
				e.Handled = true;
			}

			if (e.Text == ")" && Editor.Text.Length > Editor.CaretOffset && Editor.CaretOffset >= 1 && Editor.Text[Editor.CaretOffset] == ')' && Editor.Text[Editor.CaretOffset - 1] == '(')
			{
				Editor.CaretOffset++;
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
				if (!Char.IsLetterOrDigit(e.Text[0]))
				{
					// Whenever a non-letter is typed while the completion window is open,
					// insert the currently selected element.
					_completionWindow.CompletionList.RequestInsertion(e);
				}
			}

			// Do not set e.Handled=true.
			// We still want to insert the character that was typed.
			if (e.Text == " " && Keyboard.Modifiers == ModifierKeys.Control)
			{
				e.Handled = true;
				CreateCodeCompletionWindow();
			}
		}

		private void CreateCodeCompletionWindow()
		{
			CreateCompletionWindow(() => _codeCompletionProvider.ResolveItems(_sqlDocumentRepository, _databaseModel, Editor.Text, Editor.CaretOffset).Select(i => new CompletionData(i)), true);
		}

		private void CreateSnippetCompletionWindow(IEnumerable<ICompletionData> items)
		{
			CreateCompletionWindow(() => items, true);
		}

		private void CreateCompletionWindow(Func<IEnumerable<ICompletionData>> getCompletionDataFunc, bool show)
		{
			_completionWindow = new CompletionWindow(Editor.TextArea) { SizeToContent = SizeToContent.WidthAndHeight };
			var data = _completionWindow.CompletionList.CompletionData;

			foreach (var item in getCompletionDataFunc())
			{
				data.Add(item);
			}

			_completionWindow.Closed += delegate { _completionWindow = null; };

			if (show && data.Count > 0)
			{
				if (data.Count == 1)
				{
					_completionWindow.CompletionList.ListBox.SelectedIndex = 0;
				}

				_completionWindow.Show();
			}
		}

		public void ReParse()
		{
			EditorTextChangedHandler(this, EventArgs.Empty);
		}

		private void EditorTextChangedHandler(object sender, EventArgs e)
		{
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
				ExecuteParse(Editor.Text);
			}
			else
			{
				Task.Factory.StartNew(ExecuteParse, Editor.Text);
			}
		}

		private void ExecuteParse(object text)
		{
			_sqlDocumentRepository.UpdateStatements((string)text);
			_colorizingTransformer.SetDocumentRepository(_sqlDocumentRepository);

			Dispatcher.Invoke(() =>
			{
				Editor.TextArea.TextView.Redraw();
				_isParsing = false;
				ParseFinished(this, EventArgs.Empty);
			});

			_timerReParse.Stop();
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
				_multiNodeEditor = null;

				if (e.Key == Key.Escape)
				{
					_colorizingTransformer.SetHighlightSegments(null);
					Editor.TextArea.TextView.Redraw();
				}
			}

			if ((e.Key == Key.Back || e.Key == Key.Delete) && _multiNodeEditor != null)
			{
				Editor.Document.BeginUpdate();
				if (!_multiNodeEditor.RemoveCharacter(e.Key == Key.Back))
					_multiNodeEditor = null;
			}
			else if (e.Key == Key.Back && Editor.Text.Length > Editor.CaretOffset)
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

			if (scrollViewer.ScrollableHeight == scrollViewer.VerticalOffset && _databaseModel.CanFetch)
			{
				FetchNextRows(sender, null);
			}
		}

		private void ResultGridPreviewMouseDownHandler(object sender, MouseButtonEventArgs e)
		{
			
		}
	}

	public struct LargeBinaryValue
	{
		public string Preview { get; set; }
		
		public byte[] Data { get; set; }

		public override string ToString()
		{
			return Preview;
		}
	}

	public struct LargeTextValue
	{
		public string Preview { get; set; }
		
		public string Value { get; set; }

		public override string ToString()
		{
			return Preview;
		}
	}

	internal struct ActionResult
	{
		public bool IsSuccessful { get; set; }

		public TimeSpan Elapsed { get; set; }
	}
}
