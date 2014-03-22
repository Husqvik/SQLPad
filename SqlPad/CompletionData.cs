using System;
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
		
		private readonly StatementDescriptionNode _node;

		public CompletionData(ICodeCompletionItem codeCompletion)
		{
			Text = codeCompletion.Name;
			Content = Text;
			_completionText = Text;
			_node = codeCompletion.StatementNode;
		}

		public CompletionData(ICodeSnippet codeSnippet)
		{
			Text = codeSnippet.Name;
			Content = Text;
			_completionText = codeSnippet.BaseText;
		}

		public ImageSource Image
		{
			get { return null; }
		}

		public string Text { get; private set; }

		// Use this property if you want to show a fancy UIElement in the list.
		public object Content { get; private set; }

		public object Description
		{
			get { return "Description for " + Text; }
		}

		public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
		{
			var keyEventArgs = insertionRequestEventArgs as KeyEventArgs;
			if (keyEventArgs != null && keyEventArgs.Key == Key.Tab && _node != null)
			{
				// TODO: Fix offset while typing
				textArea.Document.Replace(_node.SourcePosition.IndexStart, _node.SourcePosition.Length, _completionText.Trim());
			}
			else
			{
				textArea.Document.Replace(completionSegment, _completionText.Trim());
			}
		}

		public double Priority { get { return 0; } }
	}
}