using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
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
			data.Add(new MyCompletionData("Item1"));
			data.Add(new MyCompletionData("Item2"));
			data.Add(new MyCompletionData("Item3"));
			
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

	public class ColorizeAvalonEdit : DocumentColorizingTransformer
	{
		private ICollection<OracleStatement> _parsedStatements = new List<OracleStatement>();
		private readonly OracleSemanticValidator _validator = new OracleSemanticValidator();
		private readonly DatabaseModelFake _databaseModel = new DatabaseModelFake();
		private static readonly SolidColorBrush ErrorBrush = new SolidColorBrush(Colors.Red);
		private static readonly SolidColorBrush NormalTextBrush = new SolidColorBrush(Colors.Black);

		public void SetStatementCollection(ICollection<OracleStatement> statements)
		{
			_parsedStatements = statements;
		}

		protected override void ColorizeLine(DocumentLine line)
		{
			var statementsAtLine = _parsedStatements.Where(s => s.SourcePosition.IndexStart <= line.EndOffset && s.SourcePosition.IndexEnd >= line.Offset);

			foreach (var statement in statementsAtLine)
			{
				var backgroundColor = new SolidColorBrush(statement.ProcessingStatus == ProcessingStatus.Success ? Colors.LightGreen : Colors.PaleVioletRed);

				var colorStartOffset = Math.Max(line.Offset, statement.SourcePosition.IndexStart);
				var colorEndOffset = Math.Min(line.EndOffset, statement.SourcePosition.IndexEnd + 1);

				foreach (var nodeValidity in _validator.Validate(statement, _databaseModel).NodeValidity)
				{
					if (line.Offset > nodeValidity.Key.SourcePosition.IndexEnd + 1 ||
						line.EndOffset < nodeValidity.Key.SourcePosition.IndexStart)
						continue;

					var errorColorStartOffset = Math.Max(line.Offset, nodeValidity.Key.SourcePosition.IndexStart);
					var errorColorEndOffset = Math.Min(line.EndOffset, nodeValidity.Key.SourcePosition.IndexEnd + 1);

					ChangeLinePart(errorColorStartOffset, errorColorEndOffset,
						element =>
						{
							element.TextRunProperties.SetForegroundBrush(nodeValidity.Value ? NormalTextBrush : ErrorBrush);
						});
				}

				ChangeLinePart(
					colorStartOffset,
					colorEndOffset,
					element =>
					{
						element.BackgroundBrush = backgroundColor;

						/*
						// This lambda gets called once for every VisualLineElement
						// between the specified offsets.
						var tf = element.TextRunProperties.Typeface;
						// Replace the typeface with a modified version of
						// the same typeface
						element.TextRunProperties.SetTypeface(new Typeface(
							tf.FontFamily,
							FontStyles.Italic,
							FontWeights.Bold,
							tf.Stretch
						));*/
					});
			}
		}
	}
	public class MyCompletionData : ICompletionData
	{
		public MyCompletionData(string text)
		{
			Text = text;
		}

		public ImageSource Image
		{
			get { return null; }
		}

		public string Text { get; private set; }

		// Use this property if you want to show a fancy UIElement in the list.
		public object Content
		{
			get { return Text; }
		}

		public object Description
		{
			get { return "Description for " + Text; }
		}

		public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
		{
			textArea.Document.Replace(completionSegment, Text);
		}

		public double Priority { get { return 0; } }
	}
}
