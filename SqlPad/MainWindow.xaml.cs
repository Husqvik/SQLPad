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
		private readonly OracleSqlParser _sqlParser = new OracleSqlParser();
		private readonly ColorizeAvalonEdit _colorizeAvalonEdit = new ColorizeAvalonEdit();

		public static RoutedCommand CommandAddColumnAliases = new RoutedCommand();
		public static RoutedCommand CommandWrapAsCommonTableExpression = new RoutedCommand();
		public static RoutedCommand CommandToggleQuotedIdentifier = new RoutedCommand();

		public MainWindow()
		{
			InitializeComponent();
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
			TextBlockToken.Text = String.Join(", ", OracleTokenReader.Create(Editor.Text).GetTokens().Select(t => "{" + t.Value + "}"));
			_colorizeAvalonEdit.SetStatementCollection(_sqlParser.Parse(Editor.Text));
			Editor.TextArea.TextView.Redraw();
		}

		private void AddColumnAliasesExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.Text = new AddMissingAliasesCommand().Execute(Editor.Text, Editor.CaretOffset);
		}

		private void AddColumnAliasesCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void WrapAsCommonTableExpressionExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.Text = new WrapAsCommonTableExpressionCommand().Execute(Editor.Text, Editor.CaretOffset, "WRAPPED_QUERY");
		}

		private void WrapAsCommonTableExpressionCanExecuteHandler(object sender, CanExecuteRoutedEventArgs e)
		{
			e.CanExecute = true;
		}

		private void ToggleQuotedIdentifierExecutedHandler(object sender, ExecutedRoutedEventArgs e)
		{
			Editor.Text = new TogleQuotedIdentifierCommand().Execute(Editor.Text, Editor.CaretOffset);
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
			_completionWindow = new CompletionWindow(Editor.TextArea);
			var data = _completionWindow.CompletionList.CompletionData;
			data.Add(new CompletionData("Item1"));
			data.Add(new CompletionData("Item2"));
			data.Add(new CompletionData("Item3"));
			
			_completionWindow.Show();
			_completionWindow.Closed += delegate { _completionWindow = null; };
		}

		void TextEnteringHandler(object sender, TextCompositionEventArgs e)
		{
			if (e.Text.Length > 0 && _completionWindow != null)
			{
				if (!char.IsLetterOrDigit(e.Text[0]))
				{
					// Whenever a non-letter is typed while the completion window is open,
					// insert the currently selected element.
					_completionWindow.CompletionList.RequestInsertion(e);
				}
			}
			// Do not set e.Handled=true.
			// We still want to insert the character that was typed.
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
