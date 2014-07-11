﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Microsoft.Win32;
using SqlPad.Commands;
using SqlPad.FindReplace;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for DocumentPage.xaml
	/// </summary>
	public partial class DocumentPage
	{
		private const int RowBatchSize = 100;
		internal const string ExecuteDatabaseCommandName = "ExecuteDatabaseCommand";
		
		private readonly SqlDocumentRepository _sqlDocumentRepository;
		private readonly IInfrastructureFactory _infrastructureFactory;
		private readonly ICodeCompletionProvider _codeCompletionProvider;
		private readonly ICodeSnippetProvider _codeSnippetProvider;
		private readonly IContextActionProvider _contextActionProvider;
		private readonly IStatementFormatter _statementFormatter;
		private readonly IDatabaseModel _databaseModel;
		private readonly IToolTipProvider _toolTipProvider;
		private readonly INavigationService _navigationService;

		private MultiNodeEditor _multiNodeEditor;
		private readonly ColorizeAvalonEdit _colorizeAvalonEdit;

		private static readonly CellValueConverter CellValueConverter = new CellValueConverter();
		private readonly ToolTip _toolTip = new ToolTip();
		private bool _isToolTipOpenByShortCut;
		private CompletionWindow _completionWindow;
		private readonly PageModel _pageModel;
		private bool _isParsing;
		private readonly System.Timers.Timer _timer = new System.Timers.Timer(100);
		private readonly string _initialDocumentHeader = "New*";
		private static readonly Style CellStyleRightAlign = new Style(typeof(DataGridCell));
		
		public EventHandler ParseFinished = delegate { };

		internal bool IsParsingSynchronous { get; set; }
		
		public TextEditorAdapter EditorAdapter { get; private set; }
		
		public FileInfo File { get; private set; }

		public string DocumentHeader { get { return File == null ? _initialDocumentHeader : File.Name + (IsDirty ? "*" : null); } }

		public bool IsDirty { get { return Editor.IsModified; } }

		static DocumentPage()
		{
			CellStyleRightAlign.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Right));
		}
		
		public DocumentPage(IInfrastructureFactory infrastructureFactory, FileInfo file, bool recoveryMode = false)
		{
			if (infrastructureFactory == null)
				throw new ArgumentNullException("infrastructureFactory");

			_infrastructureFactory = infrastructureFactory;
			_codeCompletionProvider = _infrastructureFactory.CreateCodeCompletionProvider();
			_codeSnippetProvider = _infrastructureFactory.CreateSnippetProvider();
			_contextActionProvider = _infrastructureFactory.CreateContextActionProvider();
			_statementFormatter = _infrastructureFactory.CreateSqlFormatter(new SqlFormatterOptions());
			_toolTipProvider = _infrastructureFactory.CreateToolTipProvider();
			_navigationService = _infrastructureFactory.CreateNavigationService();
			_databaseModel = _infrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings["Default"]);
			_sqlDocumentRepository = new SqlDocumentRepository(_infrastructureFactory.CreateSqlParser(), _infrastructureFactory.CreateStatementValidator(), _databaseModel);
			_colorizeAvalonEdit = new ColorizeAvalonEdit();

			_timer.Elapsed += (sender, args) => Dispatcher.Invoke(ReParse);

			InitializeComponent();

			Editor.TextArea.TextView.LineTransformers.Add(_colorizeAvalonEdit);

			Editor.TextArea.TextEntering += TextEnteringHandler;
			Editor.TextArea.TextEntered += TextEnteredHandler;

			Editor.TextArea.Caret.PositionChanged += CaretOnPositionChanged;
			Editor.TextArea.SelectionChanged += SelectionChangedHandler;

			ComboBoxSchema.ItemsSource = _databaseModel.Schemas.OrderBy(s => s);

			EditorAdapter = new TextEditorAdapter(Editor);

			_pageModel = new PageModel(_databaseModel, ReParse);

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

			_databaseModel.RefreshStarted += (sender, args) => Dispatcher.Invoke(() => ProgressBar.IsIndeterminate = true);
			_databaseModel.RefreshFinished += DatabaseModelRefreshFinishedHandler;
			_databaseModel.RefreshIfNeeded();

			InitializeGenericCommands();

			foreach (var handler in _infrastructureFactory.CommandFactory.CommandHandlers)
			{
				var command = new RoutedCommand(handler.Name, typeof(TextEditor), handler.DefaultGestures);
				var routedHandlerMethod = GenericCommandHandler.CreateRoutedEditCommandHandler(handler, () => _sqlDocumentRepository, _databaseModel);
				Editor.TextArea.DefaultInputHandler.Editing.CommandBindings.Add(new CommandBinding(command, routedHandlerMethod));
			}
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

		private void DatabaseModelRefreshFinishedHandler(object sender, EventArgs eventArgs)
		{
			Dispatcher.Invoke(() =>
			                  {
				                  ProgressBar.IsIndeterminate = false;
								  ReParse();
			                  });
		}

		private void InitializeGenericCommands()
		{
			ChangeDeleteLineCommandInputGesture();

			var commandBindings = Editor.TextArea.DefaultInputHandler.Editing.CommandBindings;
			var showFunctionOverloadCommand = new RoutedCommand("ShowFunctionOverloads", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Space, ModifierKeys.Control | ModifierKeys.Shift) });
			commandBindings.Add(new CommandBinding(showFunctionOverloadCommand, ShowFunctionOverloads));

			var duplicateTextCommand = new RoutedCommand("DuplicateText", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.D, ModifierKeys.Control) });
			commandBindings.Add(new CommandBinding(duplicateTextCommand, GenericCommandHandler.DuplicateText));

			var blockCommentCommand = new RoutedCommand("BlockComment", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Oem2, ModifierKeys.Control | ModifierKeys.Shift) });
			commandBindings.Add(new CommandBinding(blockCommentCommand, GenericCommandHandler.HandleBlockComments));

			var lineCommentCommand = new RoutedCommand("LineComment", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Oem2, ModifierKeys.Control | ModifierKeys.Alt) });
			commandBindings.Add(new CommandBinding(lineCommentCommand, GenericCommandHandler.HandleLineComments));

			var listContextActionCommand = new RoutedCommand("ListContextActions", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Enter, ModifierKeys.Alt) });
			commandBindings.Add(new CommandBinding(listContextActionCommand, (sender, args) => Editor.ContextMenu.IsOpen = PopulateContextActionMenu()));

			var multiNodeEditCommand = new RoutedCommand("EditMultipleNodes", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F6, ModifierKeys.Shift) });
			commandBindings.Add(new CommandBinding(multiNodeEditCommand, EditMultipleNodes));

			var navigateToPreviousUsageCommand = new RoutedCommand("NavigateToPreviousHighlightedUsage", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.PageUp, ModifierKeys.Control | ModifierKeys.Alt) });
			commandBindings.Add(new CommandBinding(navigateToPreviousUsageCommand, NavigateToPreviousHighlightedUsage));

			var navigateToNextUsageCommand = new RoutedCommand("NavigateToNextHighlightedUsage", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.PageDown, ModifierKeys.Control | ModifierKeys.Alt) });
			commandBindings.Add(new CommandBinding(navigateToNextUsageCommand, NavigateToNextHighlightedUsage));

			var navigateToQueryBlockRootCommand = new RoutedCommand("NavigateToQueryBlockRoot", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Home, ModifierKeys.Control | ModifierKeys.Alt) });
			commandBindings.Add(new CommandBinding(navigateToQueryBlockRootCommand, NavigateToQueryBlockRoot));

			var navigateToDefinitionRootCommand = new RoutedCommand("NavigateToDefinition", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F12) });
			commandBindings.Add(new CommandBinding(navigateToDefinitionRootCommand, NavigateToDefinition));

			var executeDatabaseCommandCommand = new RoutedCommand(ExecuteDatabaseCommandName, typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F9), new KeyGesture(Key.Enter, ModifierKeys.Control) });
			commandBindings.Add(new CommandBinding(executeDatabaseCommandCommand, ExecuteDatabaseCommand));

			var saveCommand = new RoutedCommand("Save", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) });
			commandBindings.Add(new CommandBinding(saveCommand, SaveCommandExecutedHandler));

			var formatStatementCommand = new RoutedCommand(_statementFormatter.ExecutionHandler.Name, typeof(TextEditor), _statementFormatter.ExecutionHandler.DefaultGestures);
			var formatStatementRoutedHandlerMethod = GenericCommandHandler.CreateRoutedEditCommandHandler(_statementFormatter.ExecutionHandler, () => _sqlDocumentRepository, _databaseModel);
			commandBindings.Add(new CommandBinding(formatStatementCommand, formatStatementRoutedHandlerMethod));

			var findUsagesCommandHandler = _infrastructureFactory.CommandFactory.FindUsagesCommandHandler;
			var findUsagesCommand = new RoutedCommand(findUsagesCommandHandler.Name, typeof(TextEditor), findUsagesCommandHandler.DefaultGestures);
			commandBindings.Add(new CommandBinding(findUsagesCommand, FindUsages));

			var fetchNextRowsCommand = new RoutedCommand("FetchNextRows", typeof(DataGrid), new InputGestureCollection { new KeyGesture(Key.PageDown), new KeyGesture(Key.Down) });
			ResultGrid.CommandBindings.Add(new CommandBinding(fetchNextRowsCommand, FetchNextRows, CanFetchNextRows));
		}

		private void CanFetchNextRows(object sender, CanExecuteRoutedEventArgs canExecuteRoutedEventArgs)
		{
			canExecuteRoutedEventArgs.ContinueRouting = ResultGrid.SelectedIndex < ResultGrid.Items.Count - 1 || !_databaseModel.CanFetch;
			canExecuteRoutedEventArgs.CanExecute = !canExecuteRoutedEventArgs.ContinueRouting;
		}

		private void FetchNextRows(object sender, ExecutedRoutedEventArgs executedRoutedEventArgs)
		{
			var nextRowBatch = _databaseModel.FetchRecords(RowBatchSize);
			SaveAction(() => _pageModel.ResultRowItems.AddRange(nextRowBatch));

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

		private void ExecuteDatabaseCommand(object sender, ExecutedRoutedEventArgs args)
		{
			if (_sqlDocumentRepository.Statements == null)
				return;

			var statement = _sqlDocumentRepository.Statements.GetStatementAtPosition(Editor.CaretOffset);
			if (statement == null)
				return;

			_pageModel.ResultRowItems.Clear();
			GridLabel.Visibility = Visibility.Collapsed;
			TextMoreRowsExist.Visibility = Visibility.Collapsed;

			var actionSuccess = SaveAction(() => _databaseModel.ExecuteStatement(Editor.SelectionLength > 0 ? Editor.SelectedText : statement.RootNode.GetStatementSubstring(Editor.Text), statement.ReturnDataset), true);
			if (!actionSuccess)
				return;

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

		private bool SaveAction(Action action, bool showExecutionTime = false)
		{
			try
			{
				var stopwatch = Stopwatch.StartNew();
				
				action();
				
				stopwatch.Stop();

				if (showExecutionTime)
				{
					TextExecutionTime.Text = FormatElapsedMilliseconds(stopwatch.ElapsedMilliseconds);
				}

				return true;
			}
			catch (Exception e)
			{
				MessageBox.Show(e.Message, "Error");
				return false;
			}
		}

		private static string FormatElapsedMilliseconds(long milliseconds)
		{
			string unit;
			decimal value;
			if (milliseconds > 1000)
			{
				unit = "s";
				value = Math.Round((decimal)milliseconds / 1000, 1);
			}
			else
			{
				unit = "ms";
				value = milliseconds;
			}

			return String.Format("{0} {1}", value, unit);
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
					columnTemplate.CellStyle = CellStyleRightAlign;
				}

				ResultGrid.Columns.Add(columnTemplate);
			}
		}

		private void FindUsages(object sender, ExecutedRoutedEventArgs args)
		{
			var findUsagesCommandHandler = _infrastructureFactory.CommandFactory.FindUsagesCommandHandler;
			var executionContext = CommandExecutionContext.Create(Editor, _sqlDocumentRepository);
			findUsagesCommandHandler.ExecutionHandler(executionContext);
			_colorizeAvalonEdit.SetHighlightSegments(executionContext.SegmentsToReplace);
			Editor.TextArea.TextView.Redraw();
		}
		private void NavigateToPreviousHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _colorizeAvalonEdit.HighlightSegments
						.Where(s => s.IndextStart < Editor.CaretOffset)
						.OrderByDescending(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToNextHighlightedUsage(object sender, ExecutedRoutedEventArgs args)
		{
			var nextSegments = _colorizeAvalonEdit.HighlightSegments
						.Where(s => s.IndextStart > Editor.CaretOffset)
						.OrderBy(s => s.IndextStart);

			NavigateToUsage(nextSegments);
		}

		private void NavigateToUsage(IEnumerable<TextSegment> nextSegments)
		{
			if (!_colorizeAvalonEdit.HighlightSegments.Any())
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
		}

		private void CaretOnPositionChanged(object sender, EventArgs eventArgs)
		{
			var parenthesisNodes = new List<StatementDescriptionNode>();

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

			var oldNodes = _colorizeAvalonEdit.HighlightParenthesis.ToArray();
			_colorizeAvalonEdit.SetHighlightParenthesis(parenthesisNodes);

			RedrawNodes(oldNodes.Concat(parenthesisNodes));
		}

		private void RedrawNodes(IEnumerable<StatementDescriptionNode> nodes)
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
				if (!_timer.Enabled)
				{
					_timer.Start();
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
			_colorizeAvalonEdit.SetStatementCollection(_sqlDocumentRepository);

			Dispatcher.Invoke(() =>
			{
				//TextBlockToken.Text = String.Join(", ", statements.SelectMany(s => s.AllTerminals).Select(t => "{" + t.Token.Value + "}"));
				Editor.TextArea.TextView.Redraw();
				_isParsing = false;
				ParseFinished(this, EventArgs.Empty);
			});

			_timer.Stop();
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
					_colorizeAvalonEdit.SetHighlightSegments(null);
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

			if (scrollViewer.ScrollableHeight == scrollViewer.VerticalOffset)
			{
				FetchNextRows(sender, null);
			}
		}

		private void ResultGridPreviewMouseDownHandler(object sender, MouseButtonEventArgs e)
		{
			
		}
	}
}
