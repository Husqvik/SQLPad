using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using SqlPad.Commands;
using SqlPad.FindReplace;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		private readonly ColorizeAvalonEdit _colorizeAvalonEdit = new ColorizeAvalonEdit();
		private readonly SqlDocument _sqlDocument = new SqlDocument();
		private readonly ISqlParser _sqlParser;
		private readonly IInfrastructureFactory _infrastructureFactory;
		private readonly ICodeCompletionProvider _codeCompletionProvider;
		private readonly ICodeSnippetProvider _codeSnippetProvider;
		private readonly IContextActionProvider _contextActionProvider;
		private readonly IStatementFormatter _statementFormatter;
		private readonly IDatabaseModel _databaseModel;
		private readonly IToolTipProvider _toolTipProvider;
		private readonly INavigationService _navigationService;
		
		private readonly ToolTip _toolTip = new ToolTip();
		private bool _isToolTipOpenByShortCut;

		private MultiNodeEditor _multiNodeEditor;
		private FindReplaceManager _findReplaceManager;

		public MainWindow()
		{
			InitializeComponent();

			_infrastructureFactory = ConfigurationProvider.InfrastructureFactory;
			_sqlParser = _infrastructureFactory.CreateSqlParser();
			_codeCompletionProvider = _infrastructureFactory.CreateCodeCompletionProvider();
			_codeSnippetProvider = _infrastructureFactory.CreateSnippetProvider();
			_contextActionProvider = _infrastructureFactory.CreateContextActionProvider();
			_statementFormatter = _infrastructureFactory.CreateSqlFormatter(new SqlFormatterOptions());
			_toolTipProvider = _infrastructureFactory.CreateToolTipProvider();
			_navigationService = _infrastructureFactory.CreateNavigationService();
			_databaseModel = _infrastructureFactory.CreateDatabaseModel(ConfigurationProvider.ConnectionStrings["Default"]);

			_findReplaceManager = (FindReplaceManager)Resources["FindReplaceManager"];
			_findReplaceManager.OwnerWindow = this;
			_findReplaceManager.CurrentEditor = new TextEditorAdapter(Editor);
			
			_timer.Elapsed += TimerOnElapsed;

			InitializeGenericCommands();

			foreach (var handler in _infrastructureFactory.CommandFactory.CommandHandlers)
			{
				var command = new RoutedCommand(handler.Name, typeof(TextEditor), handler.DefaultGestures);
				var routedHandlerMethod = GenericCommandHandler.CreateRoutedEditCommandHandler(handler, () => _sqlDocument.StatementCollection, _databaseModel);
				Editor.TextArea.DefaultInputHandler.Editing.CommandBindings.Add(new CommandBinding(command, routedHandlerMethod));
			}
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

			var moveContentToLeftCommand = new RoutedCommand("MoveContentToLeft", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Left, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) });
			var moveContentToRightCommand = new RoutedCommand("MoveContentToRight", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.Right, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) });

			var executeDatabaseCommandCommand = new RoutedCommand("ExecuteDatabaseCommand", typeof(TextEditor), new InputGestureCollection { new KeyGesture(Key.F9) });
			commandBindings.Add(new CommandBinding(executeDatabaseCommandCommand, ExecuteDatabaseCommand));

			var formatStatementCommand = new RoutedCommand(_statementFormatter.ExecutionHandler.Name, typeof(TextEditor), _statementFormatter.ExecutionHandler.DefaultGestures);
			var formatStatementRoutedHandlerMethod = GenericCommandHandler.CreateRoutedEditCommandHandler(_statementFormatter.ExecutionHandler, () => _sqlDocument.StatementCollection, _databaseModel);
			commandBindings.Add(new CommandBinding(formatStatementCommand, formatStatementRoutedHandlerMethod));

			var findUsagesCommandHandler = _infrastructureFactory.CommandFactory.FindUsagesCommandHandler;
			var findUsagesCommand = new RoutedCommand(findUsagesCommandHandler.Name, typeof(TextEditor), findUsagesCommandHandler.DefaultGestures);
			commandBindings.Add(new CommandBinding(findUsagesCommand, FindUsages));
		}

		private void ChangeDeleteLineCommandInputGesture()
		{
			var deleteLineCommand = (RoutedCommand)Editor.TextArea.DefaultInputHandler.Editing.CommandBindings
				.Single(b => b.Command == AvalonEditCommands.DeleteLine)
				.Command;

			deleteLineCommand.InputGestures[0] = new KeyGesture(Key.L, ModifierKeys.Control);
		}

		private void TimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
		{
			if (_isParsing)
				return;
			
			_timer.Stop();

			Editor.Dispatcher.Invoke(() => Task.Factory.StartNew(DoWork, Editor.Text));
		}

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			SqlPad.Resources.Initialize(Resources);

			Editor.TextArea.TextView.LineTransformers.Add(_colorizeAvalonEdit);

			Editor.TextArea.TextEntering += TextEnteringHandler;
			Editor.TextArea.TextEntered += TextEnteredHandler;
			
			Editor.TextArea.Caret.PositionChanged += CaretOnPositionChanged;

			Editor.Focus();
		}

		private void CaretOnPositionChanged(object sender, EventArgs eventArgs)
		{
			var parenthesisNodes = new List<StatementDescriptionNode>();

			if (!_isParsing)
			{
				var parenthesisTerminal = _sqlDocument.StatementCollection == null
					? null
					: _sqlDocument.ExecuteStatementAction(s => s.GetTerminalAtPosition(Editor.CaretOffset, n => n.Token.Value.In("(", ")")));

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

		private bool _isParsing;
		private readonly System.Timers.Timer _timer = new System.Timers.Timer(100);

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

			Task.Factory.StartNew(DoWork, Editor.Text);
		}

		private void DoWork(object text)
		{
			var statementText = (string)text;
			var statements = _sqlParser.Parse(statementText);
			_sqlDocument.UpdateStatements(statements, statementText);
			_colorizeAvalonEdit.SetStatementCollection(statements);

			Dispatcher.Invoke(() =>
			                  {
				                  TextBlockToken.Text = String.Join(", ", statements.SelectMany(s => s.AllTerminals).Select(t => "{" + t.Token.Value + "}"));
								  Editor.TextArea.TextView.Redraw();
								  _isParsing = false;
			                  });
		}

		private CompletionWindow _completionWindow;

		void TextEnteredHandler(object sender, TextCompositionEventArgs e)
		{
			if (Editor.Document.IsInUpdate)
			{
				Editor.Document.EndUpdate();
			}

			var snippets = _codeSnippetProvider.GetSnippets(_sqlDocument, Editor.Text, Editor.CaretOffset).Select(i => new CompletionData(i)).ToArray();
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

			if (e.Text == "(" &&
				(Editor.Text.Length == Editor.CaretOffset || Editor.Text[Editor.CaretOffset].In(' ', '\t', '\n')))
			{
				Editor.Document.Insert(Editor.CaretOffset, ")");
				Editor.CaretOffset--;
			}

			if (e.Text != "." && e.Text != " " && e.Text != "\n")
			{
				if (_completionWindow != null && _completionWindow.CompletionList.ListBox.Items.Count == 0)
					_completionWindow.Close();

				return;
			}

			// Open code completion after the user has pressed dot:
			CreateCodeCompletionWindow();
		}

		void TextEnteringHandler(object sender, TextCompositionEventArgs e)
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
			CreateCompletionWindow(() => _codeCompletionProvider.ResolveItems(_sqlDocument, _databaseModel, Editor.Text, Editor.CaretOffset).Select(i => new CompletionData(i)), true);
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

		void MouseHoverHandler(object sender, MouseEventArgs e)
		{
			if (_isToolTipOpenByShortCut)
				return;

			var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
			if (!position.HasValue || _sqlDocument.StatementCollection == null)
				return;

			var offset = Editor.Document.GetOffset(position.Value.Line, position.Value.Column);
			//var lineByOffset = Editor.Document.GetLineByOffset(offset);

			var toolTip = _toolTipProvider.GetToolTip(_databaseModel, _sqlDocument, offset);
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
					Command = new ContextActionCommand(action.ExecutionHandler, action.ExecutionContext),
					CommandParameter = Editor
				};

			Editor.ContextMenu.Items.Add(menuItem);
		}

		private bool PopulateContextActionMenu()
		{
			_contextActionProvider.GetContextActions(_databaseModel, _sqlDocument, Editor.SelectionStart, Editor.SelectionLength)
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

		private void EditorKeyDownHandler(object sender, KeyEventArgs e)
		{
			_isToolTipOpenByShortCut = false;

			//Console.WriteLine(e.Key);

			if (_toolTip != null)
			{
				_toolTip.IsOpen = false;
			}

			if (e.Key == Key.Return || e.Key == Key.Escape)
			{
				Trace.WriteLine(e.Key);
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
				if (Editor.Text[Editor.CaretOffset] == ')' && Editor.Text[Editor.CaretOffset - 1] == '(')
				{
					Editor.Document.Remove(Editor.CaretOffset, 1);
				}
			}
		}

		private void NavigateToQueryBlockRoot(object sender, ExecutedRoutedEventArgs args)
		{
			var queryBlockRootIndex = _navigationService.NavigateToQueryBlockRoot(_sqlDocument.StatementCollection, Editor.CaretOffset);
			if (queryBlockRootIndex.HasValue)
			{
				Editor.CaretOffset = queryBlockRootIndex.Value;
				Editor.ScrollToCaret();
			}
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

		private void ExecuteDatabaseCommand(object sender, ExecutedRoutedEventArgs args)
		{
			if (_sqlDocument.StatementCollection == null)
				return;

			var statement = _sqlDocument.StatementCollection.GetStatementAtPosition(Editor.CaretOffset);
			if (statement == null)
				return;
		}

		private void FindUsages(object sender, ExecutedRoutedEventArgs args)
		{
			var findUsagesCommandHandler = _infrastructureFactory.CommandFactory.FindUsagesCommandHandler;
			var executionContext = CommandExecutionContext.Create(Editor, _sqlDocument.StatementCollection, _databaseModel);
			findUsagesCommandHandler.ExecutionHandler(executionContext);
			_colorizeAvalonEdit.SetHighlightSegments(executionContext.SegmentsToReplace);
			Editor.TextArea.TextView.Redraw();
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

		private void ShowFunctionOverloads(object sender, ExecutedRoutedEventArgs args)
		{
			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_sqlDocument.StatementCollection, _databaseModel, Editor.CaretOffset);
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

		private void EditorKeyUpHandler(object sender, KeyEventArgs e)
		{
			if (Editor.Document.IsInUpdate)
			{
				Editor.Document.EndUpdate();
			}
		}

		private void DropObjectHandler(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop))
				return;

			var files = (string[])e.Data.GetData(DataFormats.FileDrop);
			Editor.Text = File.ReadAllText(files[0]);
		}
	}
}
