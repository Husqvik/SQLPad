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
		
		private readonly ICodeSnippet _snippet;

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

		public CompletionData(ICodeSnippet codeSnippet)
		{
			var description = String.IsNullOrEmpty(codeSnippet.Description) ? null : String.Format("{0}{1}", Environment.NewLine, codeSnippet.Description);

			_snippet = codeSnippet;
			Text = codeSnippet.Name;
			Content = Text;
			Description = "Code Snippet" + description;
			_completionText = FormatSnippetText(codeSnippet);
		}

		internal static string FormatSnippetText(ICodeSnippet codeSnippet)
		{
			return String.Format(codeSnippet.BaseText, codeSnippet.Parameters.OrderBy(p => p.Index).Select(p => p.DefaultValue).Cast<object>().ToArray());
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
			if (_snippet != null)
			{
				var offsetToReplace = _snippet.SourceToReplace.Length - 1;
				var startOffset = completionSegment.Offset - offsetToReplace;
				textArea.Document.Replace(startOffset, offsetToReplace + completionSegment.Length, _completionText);
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