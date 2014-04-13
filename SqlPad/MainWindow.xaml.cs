using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using SqlPad.Commands;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow
	{
		private readonly ColorizeAvalonEdit _colorizeAvalonEdit = new ColorizeAvalonEdit();
		private readonly ISqlParser _sqlParser;
		private readonly IInfrastructureFactory _infrastructureFactory;
		private readonly ICodeCompletionProvider _codeCompletionProvider;
		private readonly ICodeSnippetProvider _codeSnippetProvider;
		private readonly IContextActionProvider _contextActionProvider;
		
		private readonly ToolTip _toolTip = new ToolTip();

		public static RoutedCommand CommandAddColumnAliases = new RoutedCommand();
		public static RoutedCommand CommandWrapAsCommonTableExpression = new RoutedCommand();
		public static RoutedCommand CommandToggleQuotedIdentifier = new RoutedCommand();

		public MainWindow()
		{
			InitializeComponent();

			_infrastructureFactory = ConfigurationProvider.InfrastructureFactory;
			_sqlParser = _infrastructureFactory.CreateSqlParser();
			_codeCompletionProvider = _infrastructureFactory.CreateCodeCompletionProvider();
			_codeSnippetProvider = _infrastructureFactory.CreateSnippetProvider();
			_contextActionProvider = _infrastructureFactory.CreateContextActionProvider();
		}

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			SqlPad.Resources.Initialize(Resources);

			Editor.TextArea.TextView.LineTransformers.Add(_colorizeAvalonEdit);

			Editor.TextArea.TextEntering += TextEnteringHandler;
			Editor.TextArea.TextEntered += TextEnteredHandler;
		}

		private void EditorTextChangedHandler(object sender, EventArgs e)
		{
			TextBlockToken.Text = String.Join(", ", _infrastructureFactory.CreateTokenReader(Editor.Text).GetTokens().Select(t => "{" + t.Value + "}"));
			var statements = ParseStatements();
			_colorizeAvalonEdit.SetStatementCollection(statements);
			Editor.TextArea.TextView.Redraw();
		}

		private StatementCollection ParseStatements()
		{
			return _sqlParser.Parse(Editor.Text);
		}

		private void AddColumnAliasesExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.Text = _infrastructureFactory.CommandFactory.CreateAddMissingAliasesCommand().Execute(Editor.Text, Editor.CaretOffset);
		}

		private void AddColumnAliasesCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void WrapAsCommonTableExpressionExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.Text = _infrastructureFactory.CommandFactory.CreateWrapAsCommonTableExpressionCommand().Execute(Editor.Text, Editor.CaretOffset, "WRAPPED_QUERY");
		}

		private void WrapAsCommonTableExpressionCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void ToggleQuotedIdentifierExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.Text = _infrastructureFactory.CommandFactory.CreateToggleQuotedIdentifierCommand().Execute(Editor.Text, Editor.CaretOffset);
		}

		private void ToggleQuotedIdentifierCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private CompletionWindow _completionWindow;

		void TextEnteredHandler(object sender, TextCompositionEventArgs e)
		{
			if (_multiNodeEditor != null)
			{
				Editor.Document.EndUpdate();
			}

			var snippets = _codeSnippetProvider.GetSnippets(Editor.Text, Editor.CaretOffset).Select(i => new CompletionData(i)).ToArray();
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
			if (_multiNodeEditor != null)
			{
				Editor.Document.BeginUpdate();

				/*if (Editor.SelectionLength > 0)
				{
					_multiNodeEditor.RemoveCharacter(Editor.CaretOffset != Editor.SelectionStart);
				}

				_multiNodeEditor.InsertText(e.Text);*/
				_multiNodeEditor.Replace(e.Text);
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
			CreateCompletionWindow(() => _codeCompletionProvider.ResolveItems(Editor.Text, Editor.CaretOffset).Select(i => new CompletionData(i)), true);
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
			var pos = Editor.GetPositionFromPoint(e.GetPosition(Editor));
			if (pos == null)
				return;
			
			_toolTip.PlacementTarget = this; // required for property inheritance
			_toolTip.Content = pos.ToString();
			_toolTip.IsOpen = true;
			e.Handled = true;
		}

		void MouseHoverStoppedHandler(object sender, MouseEventArgs e)
		{
			_toolTip.IsOpen = false;
		}

		private void ContextMenuOpeningHandler(object sender, ContextMenuEventArgs args)
		{
			if (!PopulateContextMenu())
				args.Handled = true;
		}

		private bool PopulateContextMenu()
		{
			var menuItems = _contextActionProvider.GetContextActions(Editor.Text, Editor.CaretOffset)
				.Select(a => new MenuItem { Header = a.Name, Command = a.Command });

			Editor.ContextMenu.Items.Clear();

			foreach (var menuItem in menuItems)
			{
				Editor.ContextMenu.Items.Add(menuItem);
			}

			Editor.ContextMenu.PlacementTarget = Editor;
			var position = Editor.TextArea.Caret.CalculateCaretRectangle().TopRight;
			Editor.ContextMenu.HorizontalOffset = position.X - 24;
			Editor.ContextMenu.VerticalOffset = position.Y - 32;

			return Editor.ContextMenu.Items.Count > 0;
		}

		private void EditorKeyDownHandler(object sender, KeyEventArgs e)
		{
			if (e.SystemKey == Key.Return && Keyboard.Modifiers == ModifierKeys.Alt)
			{
				Trace.WriteLine("ALT + ENTER");
				Editor.ContextMenu.IsOpen = PopulateContextMenu();
			}
			else if (e.Key == Key.Return || e.Key == Key.Escape)
			{
				Trace.WriteLine(e.Key);
				_multiNodeEditor = null;
			}
			else if (_multiNodeEditor == null && e.Key == Key.F6 && Keyboard.Modifiers == ModifierKeys.Shift)
			{
				Trace.WriteLine("SHIFT + F6");

				_multiNodeEditor = new MultiNodeEditor(Editor, _infrastructureFactory);
			}
			else if (e.SystemKey == Key.F11 && Keyboard.Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift))
			{
				Trace.WriteLine("ALT SHIFT + F11");
			}

			if ((e.Key == Key.Back || e.Key == Key.Delete) && _multiNodeEditor != null)
			{
				_multiNodeEditor.RemoveCharacter(e.Key == Key.Back);
			}
		}

		private void EditorKeyUpHandler(object sender, KeyEventArgs e)
		{
			
		}

		private MultiNodeEditor _multiNodeEditor;
	}
}
