using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace SqlPad
{
	internal class ActiveSnippet
	{
		private static readonly Regex RegexNewLine = new Regex(@"\r?\n", RegexOptions.CultureInvariant | RegexOptions.Compiled);

		private readonly TextEditor _editor;
		private readonly List<List<Tuple<TextAnchor, TextAnchor>>> _followingAnchorGroups = new List<List<Tuple<TextAnchor, TextAnchor>>>();

		public Tuple<TextAnchor, TextAnchor> ActiveAnchors { get; private set; }

		public IReadOnlyList<IReadOnlyList<Tuple<TextAnchor, TextAnchor>>> FollowingAnchorGroups => _followingAnchorGroups;

		public bool ActiveAnchorsValid => !ActiveAnchors.Item1.IsDeleted && !ActiveAnchors.Item2.IsDeleted;

		public ActiveSnippet(int completionSegmentOffset, int completionSegmentLength, TextEditor editor, CompletionData completionData)
		{
			var lineBeginOffset = editor.Document.GetLineByOffset(completionSegmentOffset).Offset;
			var snippetBaseText = completionData.Snippet.BaseText;
			if (lineBeginOffset < completionSegmentOffset)
			{
				var indentationText = editor.Document.GetText(lineBeginOffset, completionSegmentOffset - lineBeginOffset);
				snippetBaseText = RegexNewLine.Replace(snippetBaseText, m => $"{m.Value}{TextHelper.ReplaceAllNonBlankCharactersWithSpace(indentationText)}");
			}

			editor.Document.BeginUpdate();
			editor.Document.Replace(completionSegmentOffset, completionSegmentLength, snippetBaseText);

			if (completionData.Snippet.Parameters.Count > 0)
			{
				_editor = editor;

				var text = snippetBaseText;
				foreach (var parameter in completionData.Snippet.Parameters.OrderBy(p => p.Index))
				{
					var anchorGroup = new List<Tuple<TextAnchor, TextAnchor>>();

					int parameterOffset;
					var parameterPlaceholder = $"{{{parameter.Index}}}";
					var regex = new Regex(Regex.Escape(parameterPlaceholder), RegexOptions.CultureInvariant);
					while ((parameterOffset = text.IndexOf(parameterPlaceholder, StringComparison.InvariantCulture)) != -1)
					{
						var documentStartOffset = completionSegmentOffset + parameterOffset;
						text = regex.Replace(text, parameter.DefaultValue, 1);
						editor.Document.Replace(documentStartOffset, 3, parameter.DefaultValue);

						var anchorStart = editor.Document.CreateAnchor(documentStartOffset);
						anchorStart.MovementType = AnchorMovementType.BeforeInsertion;
						var anchorEnd = editor.Document.CreateAnchor(anchorStart.Offset + parameter.DefaultValue.Length);
						anchorGroup.Add(Tuple.Create(anchorStart, anchorEnd));
					}

					if (anchorGroup.Count > 0)
					{
						_followingAnchorGroups.Add(anchorGroup);
					}
				}

				SelectNextParameter(false);
			}

			editor.Document.EndUpdate();
		}

		public MultiNodeEditor GetMultiNodeEditor()
		{
			return _followingAnchorGroups.Count > 0 && _followingAnchorGroups[0].Count > 0
				? new MultiNodeEditor(_editor, ActiveAnchors.Item1, ActiveAnchors.Item2, _followingAnchorGroups[0].Select(t => t.Item1))
				: null;
		}

		public bool SelectNextParameter()
		{
			return SelectNextParameter(true);
		}

		private bool SelectNextParameter(bool removeAnchorGroup)
		{
			if (removeAnchorGroup && _followingAnchorGroups.Count > 0)
			{
				_followingAnchorGroups.RemoveAt(0);
			}

			if (_followingAnchorGroups.Count == 0 ||
			    (ActiveAnchors = _followingAnchorGroups[0][0]).Item1.IsDeleted || ActiveAnchors.Item2.IsDeleted)
			{
				SelectText(_editor.CaretOffset, _editor.CaretOffset);
				return false;
			}

			SelectText(ActiveAnchors.Item1.Offset, ActiveAnchors.Item2.Offset);
			return true;
		}

		private void SelectText(int startOffset, int endOffset)
		{
			_editor.TextArea.Selection = Selection.Create(_editor.TextArea, startOffset, endOffset);
			_editor.CaretOffset = endOffset;

			if (_followingAnchorGroups.Count > 0)
			{
				_followingAnchorGroups[0].RemoveAt(0);
			}
		}
	}
}
