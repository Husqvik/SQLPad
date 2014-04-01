using System;
using System.Linq;
using System.Windows.Input;
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
		
		private readonly StatementDescriptionNode _node;
		private readonly ICodeSnippet _snippet;

		public CompletionData(ICodeCompletionItem codeCompletion)
		{
			Text = codeCompletion.Name;
			Content = Text;
			_completionText = Text;
			_node = codeCompletion.StatementNode;
			_offset = codeCompletion.Offset;
			Description = codeCompletion.Category;
		}

		public CompletionData(ICodeSnippet codeSnippet)
		{
			_snippet = codeSnippet;
			Text = codeSnippet.Name;
			Content = Text;
			_completionText = String.Format(codeSnippet.BaseText, codeSnippet.Parameters.OrderBy(p => p.Index).Select(p => p.DefaultValue).Cast<object>().ToArray());
		}

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
				textArea.Document.Replace(startOffset, startOffset + offsetToReplace + completionSegment.Length, _completionText);
				return;
			}

			//var keyEventArgs = insertionRequestEventArgs as KeyEventArgs;
			if (/*keyEventArgs != null && keyEventArgs.Key == Key.Tab &&*/ _node != null)
			{
				textArea.Document.Replace(_node.SourcePosition.IndexStart, _node.SourcePosition.Length + completionSegment.Length, /*new String(' ', _offset) +*/ _completionText.Trim());
			}
			else
			{
				textArea.Document.Replace(completionSegment, new String(' ', _offset) + _completionText.Trim());
			}
		}

		public double Priority { get { return 0; } }
	}
}