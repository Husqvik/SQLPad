using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace SqlPad
{
	public class CompletionData : ICompletionData
	{
		private readonly string _completionText;
		private readonly int _insertOffset;
		private readonly int _caretOffset;
		private int _selectionStartOffset;
		private int _selectionLength;

		public ICodeSnippet Snippet { get; set; }

		public CompletionData(ICodeCompletionItem codeCompletion)
		{
			Text = codeCompletion.Name;
			Content = Text;
			_completionText = codeCompletion.Text;
			Node = codeCompletion.StatementNode;
			_insertOffset = codeCompletion.InsertOffset;
			_caretOffset = codeCompletion.CaretOffset;
			Description = codeCompletion.Category;
		}

		public CompletionData(ICodeSnippet codeSnippet)
		{
			var descriptionText = String.IsNullOrEmpty(codeSnippet.Description) ? null : String.Format("{0}{1}", Environment.NewLine, codeSnippet.Description);

			Snippet = codeSnippet;
			Text = codeSnippet.Name;
			Content = Text;
			var description = new TextBlock();
			description.Inlines.Add(new Bold(new Run("Code Snippet")));
			description.Inlines.Add(new Run(descriptionText));
			Description = description;
			_completionText = FormatSnippetText(codeSnippet, this);
		}

		public void Highlight(string text)
		{
			if (String.IsNullOrEmpty(text))
			{
				Content = Text;
				return;
			}
			
			var startIndex = 0;
			var textBlock = new TextBlock();

			int index;
			while ((index = Text.IndexOf(text, startIndex, StringComparison.OrdinalIgnoreCase)) != -1)
			{
				if (index > startIndex)
				{
					textBlock.Inlines.Add(Text.Substring(startIndex, index - startIndex));
				}

				textBlock.Inlines.Add(new Run { Foreground = Brushes.Red, Text = Text.Substring(index, text.Length) });
				startIndex = index + text.Length;
			}

			if (Text.Length > startIndex)
			{
				textBlock.Inlines.Add(Text.Substring(startIndex));
			}

			Content = textBlock;
		}

		internal static string FormatSnippetText(ICodeSnippet codeSnippet)
		{
			return FormatSnippetText(codeSnippet, null);
		}

		private static string FormatSnippetText(ICodeSnippet codeSnippet, CompletionData completionData)
		{
			var parameters = codeSnippet.Parameters.OrderBy(p => p.Index).Select(p => (object)p.DefaultValue).ToArray();
			if (parameters.Length == 0)
			{
				return codeSnippet.BaseText;
			}
			
			var firstParameter = (string)parameters[0];
			const string substitute = "{0}";
			parameters[0] = substitute;

			var preformattedText = String.Format(codeSnippet.BaseText, parameters);

			if (completionData != null)
			{
				completionData._selectionStartOffset = preformattedText.IndexOf(substitute, StringComparison.InvariantCultureIgnoreCase);
				completionData._selectionLength = firstParameter.Length;
			}

			return String.Format(preformattedText, firstParameter);
		}

		public StatementGrammarNode Node { get; private set; }

		public ImageSource Image
		{
			get { return null; }
		}

		public string Text { get; private set; }

		public object Content { get; private set; }

		public object Description { get; private set; }

		public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
		{
			if (Snippet != null)
			{
				textArea.Document.Replace(completionSegment.Offset, completionSegment.Length, _completionText);
				var selectionStartOffset = completionSegment.Offset + _selectionStartOffset;
				var selectionEndOffset = selectionStartOffset + _selectionLength;
				textArea.Selection = Selection.Create(textArea, selectionStartOffset, selectionEndOffset);
				textArea.Caret.Offset = selectionEndOffset;
				
				return;
			}

			if (Node != null)
			{
				var remainingLength = textArea.Document.TextLength - Node.SourcePosition.IndexStart;
				var replacedLength = Math.Min(Math.Max(Node.SourcePosition.Length, completionSegment.Length), remainingLength);
				textArea.Document.Replace(Node.SourcePosition.IndexStart, replacedLength, _completionText.Trim());
			}
			else
			{
				textArea.Document.Replace(completionSegment, new String(' ', _insertOffset) + _completionText.Trim());
			}

			if (_caretOffset != 0)
			{
				textArea.Caret.Offset += _caretOffset;
			}
		}

		public double Priority { get { return 0; } }
	}
}