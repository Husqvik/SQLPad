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
using ICSharpCode.AvalonEdit.Document;
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
				var backgroundColor = new SolidColorBrush(statement.ProcessingResult == NonTerminalProcessingResult.Success ? Colors.LightGreen : Colors.PaleVioletRed);

				var colorStartOffset = Math.Max(line.Offset, statement.SourcePosition.IndexStart);
				var colorEndOffset = Math.Min(line.EndOffset, statement.SourcePosition.IndexEnd + 1);

				foreach (var nodeValidity in _validator.Validate(statement, _databaseModel).NodeValidity)
				{
					var errorColorStartOffset = Math.Max(line.Offset, nodeValidity.Key.SourcePosition.IndexStart);
					var errorColorEndOffset = Math.Min(line.EndOffset, nodeValidity.Key.SourcePosition.IndexEnd + 1);

					ChangeLinePart(errorColorStartOffset, errorColorEndOffset,
						element =>
						{
							element.TextRunProperties.SetForegroundBrush(nodeValidity.Value ? NormalTextBrush : ErrorBrush);
							//element.TextRunProperties.SetBackgroundBrush(backgroundColor);
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
}
