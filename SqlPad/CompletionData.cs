using System;
using System.Linq;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace SqlPad
{
	public class CompletionData : ICompletionData
	{
		private readonly string _completionText;
		private readonly int _offset;
		private readonly int _caretOffset;
		
		private readonly ICodeSnippet _snippet;

		public CompletionData(ICodeCompletionItem codeCompletion)
		{
			Text = codeCompletion.Name;
			Content = Text;
			_completionText = codeCompletion.Text;
			Node = codeCompletion.StatementNode;
			_offset = codeCompletion.InsertOffset;
			_caretOffset = codeCompletion.CaretOffset;
			Description = codeCompletion.Category;
		}

		public CompletionData(ICodeSnippet codeSnippet)
		{
			var description = String.IsNullOrEmpty(codeSnippet.Description) ? null : (Environment.NewLine + codeSnippet.Description);

			_snippet = codeSnippet;
			Text = codeSnippet.Name;
			Content = Text;
			Description = "Code Snippet" + description;
			_completionText = String.Format(codeSnippet.BaseText, codeSnippet.Parameters.OrderBy(p => p.Index).Select(p => p.DefaultValue).Cast<object>().ToArray());
		}

		public StatementGrammarNode Node { get; private set; }

		public ImageSource Image
		{
			get { return null; }
		}

		public string Text { get; private set; }

		// Use this property if you want to show a fancy UIElement in the list.
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
				textArea.Document.Replace(completionSegment, new String(' ', _offset) + _completionText.Trim());
			}

			if (_caretOffset != 0)
			{
				textArea.Caret.Offset += _caretOffset;
			}
		}

		public double Priority { get { return 0; } }
	}
}