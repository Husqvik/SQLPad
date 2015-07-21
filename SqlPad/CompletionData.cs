using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
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
		private readonly List<Run> _inlines = new List<Run>();

		public ICodeSnippet Snippet { get; set; }

		public CompletionData(ICodeCompletionItem codeCompletion)
		{
			Text = codeCompletion.Name;
			_completionText = codeCompletion.Text;
			Node = codeCompletion.StatementNode;
			_insertOffset = codeCompletion.InsertOffset;
			_caretOffset = codeCompletion.CaretOffset;
			Description = codeCompletion.Category;
		}

		public CompletionData(ICodeSnippet codeSnippet)
		{
			Snippet = codeSnippet;
			Text = codeSnippet.Name;
			Application.Current.Dispatcher.Invoke(BuildDecription);
			_completionText = FormatSnippetText(codeSnippet, this);
		}

		private void BuildDecription()
		{
			var descriptionText = String.IsNullOrEmpty(Snippet.Description) ? null : $"{Environment.NewLine}{Snippet.Description}";
			var description = new TextBlock();
			description.Inlines.Add(new Bold(new Run("Code Snippet")));
			description.Inlines.Add(new Run(descriptionText));
			Description = description;
		}

		public void Highlight(string text)
		{
			var startIndex = 0;
			var textBlock = (TextBlock)Content;
			if (textBlock == null)
			{
				Content = textBlock = new TextBlock();
			}

			var inlineCount = _inlines.Count;
			var inlineIndex = 0;
			if (String.IsNullOrEmpty(text))
			{
				SetInline(textBlock, Text, ref inlineIndex, ref inlineCount, false);
			}
			else
			{
				int index;
				while ((index = Text.IndexOf(text, startIndex, StringComparison.OrdinalIgnoreCase)) != -1)
				{
					if (index > startIndex)
					{
						var normalText = Text.Substring(startIndex, index - startIndex);
						SetInline(textBlock, normalText, ref inlineIndex, ref inlineCount, false);
					}

					SetInline(textBlock, Text.Substring(index, text.Length), ref inlineIndex, ref inlineCount, true);
					startIndex = index + text.Length;
				}

				if (Text.Length > startIndex)
				{
					SetInline(textBlock, Text.Substring(startIndex), ref inlineIndex, ref inlineCount, false);
				}
			}
			
			for (var i = inlineIndex; i < inlineCount; i++)
			{
				_inlines[i].Text = String.Empty;
			}
		}

		private void SetInline(TextBlock textBlock, string text, ref int inlineIndex, ref int inlineCount, bool isHighlight)
		{
			Run run;
			if (inlineIndex + 1 > inlineCount)
			{
				run = new Run();
				_inlines.Add(run);
				textBlock.Inlines.Add(run);
				inlineCount++;
			}
			else
			{
				run = _inlines[inlineIndex];
			}

			run.Text = text;
			run.Foreground = isHighlight ? Brushes.Red : Brushes.Black;
			inlineIndex++;
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

		public StatementGrammarNode Node { get; }

		public ImageSource Image => null;

	    public string Text { get; }

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

		public double Priority => 0;
	}
}
