using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	internal class OracleFoldingSectionProvider : IFoldingSectionProvider
	{
		public IEnumerable<FoldingSection> GetFoldingSections(IEnumerable<IToken> tokens)
		{
			return GetFoldingSectionsInteral(tokens).OrderBy(s => s.FoldingStart);
		}
		
		private IEnumerable<FoldingSection> GetFoldingSectionsInteral(IEnumerable<IToken> tokens)
		{
			FoldingSection foldingSection;

			var foldingSectionQueue = new Stack<FoldingSection>();
			var innerParentheses = new List<int> { foldingSectionQueue.Count };

			var previousToken = OracleToken.Empty;
			foreach (OracleToken token in tokens.Where(t => t.CommentType == CommentType.None))
			{
				var existsPrecedingParenthesis = previousToken.Value == "(";
				var isSelect = token.UpperInvariantValue == "SELECT";
				if (isSelect && (existsPrecedingParenthesis || previousToken.Value == ";" || String.IsNullOrEmpty(previousToken.Value)))
				{
					var section =
						new FoldingSection
						{
							FoldingStart = token.Index,
							IsNested = foldingSectionQueue.Count > 0,
							Placeholder = "Subquery"
						};

					foldingSectionQueue.Push(section);

					if (innerParentheses.Count == foldingSectionQueue.Count)
					{
						innerParentheses.Add(0);
					}
				}
				else if (!isSelect && existsPrecedingParenthesis)
				{
					innerParentheses[foldingSectionQueue.Count]++;
				}

				var isClosingParenthesis = token.Value == ")";
				if ((isClosingParenthesis && innerParentheses[foldingSectionQueue.Count] == 0) || token.Value == ";" || token.Value == "\n/\n")
				{
					if (TryFinishFoldingSection(foldingSectionQueue, isClosingParenthesis ? previousToken : token, out foldingSection))
					{
						yield return foldingSection;
					}
				}
				else if (isClosingParenthesis)
				{
					innerParentheses[foldingSectionQueue.Count]--;
				}

				previousToken = token;
			}

			if (TryFinishFoldingSection(foldingSectionQueue, previousToken, out foldingSection))
			{
				yield return foldingSection;
			}
		}

		private static bool TryFinishFoldingSection(Stack<FoldingSection> foldingSectionQueue, OracleToken token, out FoldingSection foldingSection)
		{
			var sectionExists = foldingSectionQueue.Count > 0;
			if (sectionExists)
			{
				foldingSection = foldingSectionQueue.Pop();
				foldingSection.FoldingEnd = token.Index + token.Value.Length;
			}
			else
			{
				foldingSection = null;
			}

			return sectionExists;
		}
	}
}
