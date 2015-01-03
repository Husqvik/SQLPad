using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	internal class OracleFoldingSectionProvider : IFoldingSectionProvider
	{
		public const string FoldingSectionPlaceholderSubquery = "Subquery";
		public const string FoldingSectionPlaceholderPlSqlBlock = "PL/SQL Block";

		public IEnumerable<FoldingSection> GetFoldingSections(IEnumerable<IToken> tokens)
		{
			return GetFoldingSectionsInteral(tokens).OrderBy(s => s.FoldingStart);
		}
		
		private IEnumerable<FoldingSection> GetFoldingSectionsInteral(IEnumerable<IToken> tokens)
		{
			FoldingSection foldingSection;

			var foldingSectionStack = new Stack<FoldingSection>();
			var innerParentheses = new List<int> { foldingSectionStack.Count };

			var previousToken = OracleToken.Empty;
			foreach (OracleToken token in tokens.Where(t => t.CommentType == CommentType.None))
			{
				var existsPrecedingParenthesis = previousToken.Value == "(";
				var isSelect = token.UpperInvariantValue == "SELECT";
				if (isSelect && (existsPrecedingParenthesis || previousToken.Value == ";" || String.IsNullOrEmpty(previousToken.Value)))
				{
					CreateFoldingSection(FoldingSectionPlaceholderSubquery, token, foldingSectionStack, innerParentheses);
				}
				else if (!isSelect && existsPrecedingParenthesis)
				{
					innerParentheses[foldingSectionStack.Count]++;
				}

				var isClosingParenthesis = token.Value == ")";
				if ((isClosingParenthesis && innerParentheses[foldingSectionStack.Count] == 0) || token.Value == ";" || token.Value == "\n/\n")
				{
					if (TryFinishFoldingSection(foldingSectionStack, isClosingParenthesis ? previousToken : token, FoldingSectionPlaceholderSubquery, out foldingSection))
					{
						yield return foldingSection;
					}
				}
				else if (isClosingParenthesis)
				{
					innerParentheses[foldingSectionStack.Count]--;
				}

				if (token.UpperInvariantValue == "BEGIN")
				{
					CreateFoldingSection(FoldingSectionPlaceholderPlSqlBlock, token, foldingSectionStack, innerParentheses);
				}
				else if (previousToken.UpperInvariantValue == "END" &&
				         token.Value == ";")
				{
					if (TryFinishFoldingSection(foldingSectionStack, token, FoldingSectionPlaceholderPlSqlBlock, out foldingSection))
					{
						yield return foldingSection;
					}
				}

				previousToken = token;
			}

			if (TryFinishFoldingSection(foldingSectionStack, previousToken, null, out foldingSection))
			{
				yield return foldingSection;
			}
		}

		private static void CreateFoldingSection(string placeholder, OracleToken token, Stack<FoldingSection> foldingSectionStack, ICollection<int> innerParentheses)
		{
			var section =
				new FoldingSection
				{
					FoldingStart = token.Index,
					IsNested = foldingSectionStack.Count > 0,
					Placeholder = placeholder
				};

			foldingSectionStack.Push(section);

			if (innerParentheses.Count == foldingSectionStack.Count)
			{
				innerParentheses.Add(0);
			}
		}

		private static bool TryFinishFoldingSection(Stack<FoldingSection> foldingSectionStack, OracleToken token, string placeholder, out FoldingSection foldingSection)
		{
			var sectionExists = foldingSectionStack.Count > 0 &&
			                    (placeholder == null || foldingSectionStack.Peek().Placeholder == placeholder);
			
			if (sectionExists)
			{
				foldingSection = foldingSectionStack.Pop();
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
