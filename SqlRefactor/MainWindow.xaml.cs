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

namespace SqlRefactor
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private void WindowLoadedHandler(object sender, RoutedEventArgs e)
		{
			//var oracleTokens = OracleTokenReader.Create(File.OpenText(@"D:\testSql.sql")).GetTokens().ToList();

			//var tokenReader = OracleTokenReader.Create(File.OpenText(@"D:\testSql.sql"));
			//var sql = new OracleSqlParser().Parse(@"SELECT 1 FROM DUAL,");
			//var sql = new OracleSqlParser().Parse(@"WITH 1 AS (");

			Editor.TextArea.TextView.LineTransformers.Add(new ColorizeAvalonEdit());
		}

		private void EditorTextChangedHandler(object sender, EventArgs e)
		{
			TextBlockToken.Text = String.Join(", ", OracleTokenReader.Create(Editor.Text).GetTokens().Select(t => "{" + t.Value + "}"));
		}
	}

	public class ColorizeAvalonEdit : DocumentColorizingTransformer
	{
		private readonly OracleSqlParser _sqlParser = new OracleSqlParser();

		#region Overrides of DocumentColorizingTransformer
		protected override void Colorize(ITextRunConstructionContext context)
		{
			var sqlCollection = _sqlParser.Parse(context.Document.Text);
			var backgroundColor = sqlCollection.Count > 0 && sqlCollection.First().ProcessingResult == NonTerminalProcessingResult.Success ? Colors.LightGreen : Colors.PaleVioletRed;

			ChangeVisualElements(0, context.VisualLine.VisualLength,
				line =>
				{
					line.BackgroundBrush = new SolidColorBrush(backgroundColor);
				});
		}
		#endregion

		protected override void ColorizeLine(DocumentLine line)
		{
			/*var lineStartOffset = line.Offset;
			var text = CurrentContext.Document.GetText(line);
			var start = 0;
			int index;
			while ((index = text.IndexOf("AvalonEdit", start)) >= 0)
			{
				ChangeLinePart(
					lineStartOffset + index, // startOffset
					lineStartOffset + index + 10, // endOffset
					element =>
					{
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
						));
					});
				start = index + 1; // search for next occurrence
			}*/
		}
	}
}
