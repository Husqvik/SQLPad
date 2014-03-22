using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit.CodeCompletion;
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

		public static RoutedCommand CommandAddColumnAliases = new RoutedCommand();
		public static RoutedCommand CommandWrapAsCommonTableExpression = new RoutedCommand();
		public static RoutedCommand CommandToggleQuotedIdentifier = new RoutedCommand();

		public MainWindow()
		{
			InitializeComponent();

			_infrastructureFactory = ConfigurationProvider.InfrastructureFactory;
			_sqlParser = _infrastructureFactory.CreateSqlParser();
			_codeCompletionProvider = _infrastructureFactory.CreateCodeCompletionProvider();
		}

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			//var oracleTokens = OracleTokenReader.Create(File.OpenText(@"D:\testSql.sql")).GetTokens().ToList();

			//var sql = new OracleSqlParser().Parse(@"select 1 from dual versions between scn minvalue and maxvalue");

			Editor.TextArea.TextView.LineTransformers.Add(_colorizeAvalonEdit);

			Editor.TextArea.TextEntering += TextEnteringHandler;
			Editor.TextArea.TextEntered += TextEnteredHandler;
		}

		private void EditorTextChangedHandler(object sender, EventArgs e)
		{
			TextBlockToken.Text = String.Join(", ", _infrastructureFactory.CreateTokenReader(Editor.Text).GetTokens().Select(t => "{" + t.Value + "}"));
			_colorizeAvalonEdit.SetStatementCollection(_sqlParser.Parse(Editor.Text));
			Editor.TextArea.TextView.Redraw();
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
			if (e.Text != ".")
				return;
			
			// Open code completion after the user has pressed dot:
			CreateCompletionWindow(true);
		}

		void TextEnteringHandler(object sender, TextCompositionEventArgs e)
		{
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
				CreateCompletionWindow(true);
			}
		}

		private void CreateCompletionWindow(bool show)
		{
			_completionWindow = new CompletionWindow(Editor.TextArea);
			var data = _completionWindow.CompletionList.CompletionData;

			foreach (var item in _codeCompletionProvider.ResolveItems(Editor.Text, Editor.CaretOffset))
			{
				data.Add(new CompletionData(item.Name));
			}

			_completionWindow.Closed += delegate { _completionWindow = null; };

			if (show && data.Count > 0)
				_completionWindow.Show();
		}

		private readonly ToolTip _toolTip = new ToolTip();
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
	}
}
