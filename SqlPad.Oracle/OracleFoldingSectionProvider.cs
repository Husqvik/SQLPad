using System;
using System.Collections.Generic;
using System.Linq;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	internal class OracleFoldingSectionProvider
	{
		public const string FoldingSectionPlaceholderSubquery = "Subquery";
		public const string FoldingSectionPlaceholderPlSqlBlock = "PL/SQL Block";
		public const string FoldingSectionPlaceholderException = "Exception";

		private const string StackKeySubquery = "Subquery";
		private const string StackKeyPlSql = "PL/SQL";

		public IEnumerable<FoldingSection> GetFoldingSections(IEnumerable<IToken> tokens)
		{
			return GetFoldingSectionsInternal(tokens).OrderBy(s => s.FoldingStart);
		}
		
		private IEnumerable<FoldingSection> GetFoldingSectionsInternal(IEnumerable<IToken> tokens)
		{
			FoldingSection foldingSection;

			var foldingContext = new FoldingContext(TerminalValues.LeftParenthesis, TerminalValues.Begin);
			var precedingToken = OracleToken.Empty;
			var tokenArray = tokens.Where(t => t.CommentType == CommentType.None).ToArray();
			for (var i = 0; i < tokenArray.Length; i++)
			{
				var token = (OracleToken)tokenArray[i];
				var followingToken = i == tokenArray.Length - 1 ? OracleToken.Empty : (OracleToken)tokenArray[i + 1];
				var existsPrecedingParenthesis = String.CompareOrdinal(precedingToken.Value, TerminalValues.LeftParenthesis) == 0;
				if (existsPrecedingParenthesis)
				{
					foldingContext.OpenScope(TerminalValues.LeftParenthesis);
				}

				var isSelect = String.CompareOrdinal(token.UpperInvariantValue, TerminalValues.Select) == 0;
				if (isSelect && (existsPrecedingParenthesis || String.IsNullOrEmpty(precedingToken.Value) || String.CompareOrdinal(precedingToken.Value, TerminalValues.Semicolon) == 0))
				{
					foldingContext.AddFolding(FoldingSectionPlaceholderSubquery, StackKeySubquery, token.Index);
				}

				var isClosingParenthesis = String.CompareOrdinal(token.Value, TerminalValues.RightParenthesis) == 0;
				var isSemicolon = String.CompareOrdinal(token.Value, TerminalValues.Semicolon) == 0;
				if (isClosingParenthesis || isSemicolon || String.CompareOrdinal(token.Value, TerminalValues.SqlPlusTerminator) == 0)
				{
					if (foldingContext.TryFinishFoldingSection(StackKeySubquery, StackKeySubquery, isClosingParenthesis ? precedingToken : token, out foldingSection))
					{
						yield return foldingSection;
					}

					if (isClosingParenthesis)
					{
						foldingContext.CloseScope(TerminalValues.LeftParenthesis);
					}
				}

				if (String.CompareOrdinal(token.UpperInvariantValue, TerminalValues.Begin) == 0)
				{
					foldingContext.OpenScope(TerminalValues.Begin);
					foldingContext.AddFolding(FoldingSectionPlaceholderPlSqlBlock, StackKeyPlSql, token.Index);
				}
				else if ((isSemicolon || (String.CompareOrdinal(followingToken.Value, TerminalValues.Semicolon) == 0) && !OracleGrammarDescription.ReservedWordsPlSqlBody.Contains(token.UpperInvariantValue)) &&
				         String.CompareOrdinal(precedingToken.UpperInvariantValue, TerminalValues.End) == 0)
				{
					if (i > 1 && foldingContext.TryFinishFoldingSection(FoldingSectionPlaceholderException, StackKeyPlSql, (OracleToken)tokenArray[i - 2], out foldingSection))
					{
						yield return foldingSection;
					}

					var foldingEndToken = isSemicolon ? token : followingToken;
					if (foldingContext.TryFinishFoldingSection(FoldingSectionPlaceholderPlSqlBlock, StackKeyPlSql, foldingEndToken, out foldingSection))
					{
						yield return foldingSection;
					}

					foldingContext.CloseScope(TerminalValues.Begin);
				}

				if (String.CompareOrdinal(token.UpperInvariantValue, TerminalValues.Exception) == 0)
				{
					foldingContext.AddFolding(FoldingSectionPlaceholderException, StackKeyPlSql, token.Index);
				}

				precedingToken = token;
			}

			if (foldingContext.TryFinishFoldingSection(precedingToken, out foldingSection))
			{
				yield return foldingSection;
			}
		}

		private class FoldingContext
		{
			private readonly Dictionary<string, Stack<FoldingSection>> _foldingStacks = new Dictionary<string, Stack<FoldingSection>>();
			private readonly Dictionary<string, int> _nestedScopes;
			private readonly Dictionary<FoldingSection, Dictionary<string, int>> _sectionScopes = new Dictionary<FoldingSection, Dictionary<string, int>>();

			public FoldingContext(params string[] scopeKeys)
			{
				_nestedScopes = scopeKeys.ToDictionary(k => k, v => 0);
			}

			public void AddFolding(string placeholder, string stackKey, int indexStart)
			{
				var foldingSectionStack = GetFoldingStack(stackKey);

				var section =
					new FoldingSection
					{
						FoldingStart = indexStart,
						IsNested = foldingSectionStack.Count > 0,
						Placeholder = placeholder
					};

				foldingSectionStack.Push(section);

				_sectionScopes.Add(section, new Dictionary<string, int>(_nestedScopes));
			}

			public bool TryFinishFoldingSection(OracleToken token, out FoldingSection foldingSection)
			{
				foldingSection = _foldingStacks.Values.SelectMany(s => s).FirstOrDefault();
				if (foldingSection == null)
				{
					return false;
				}

				foldingSection.FoldingEnd = token.Index + token.Value.Length;
				return true;
			}

			public bool TryFinishFoldingSection(string placeholder, string stackKey, OracleToken token, out FoldingSection foldingSection)
			{
				var foldingSectionStack = GetFoldingStack(stackKey);
				var sectionExists = IsScopeValid(placeholder, foldingSectionStack);
				if (sectionExists)
				{
					foldingSection = foldingSectionStack.Pop();
					_sectionScopes.Remove(foldingSection);
					foldingSection.FoldingEnd = token.Index + token.Value.Length;
				}
				else
				{
					foldingSection = null;
				}

				return sectionExists;
			}

			private bool IsScopeValid(string placeholder, Stack<FoldingSection> foldingSectionStack)
			{
				if (foldingSectionStack.Count == 0)
				{
					return false;
				}

				var foldingSection = foldingSectionStack.Peek();

				return placeholder == foldingSection.Placeholder && _sectionScopes[foldingSection].All(kvp => _nestedScopes[kvp.Key] == kvp.Value);
			}

			private Stack<FoldingSection> GetFoldingStack(string stackKey)
			{
				Stack<FoldingSection> foldingSectionStack;
				if (!_foldingStacks.TryGetValue(stackKey, out foldingSectionStack))
				{
					_foldingStacks[stackKey] = foldingSectionStack = new Stack<FoldingSection>();
				}

				return foldingSectionStack;
			}

			public void OpenScope(string scopeKey)
			{
				_nestedScopes[scopeKey]++;
			}

			public void CloseScope(string scopeKey)
			{
				_nestedScopes[scopeKey]--;
			}
		}
	}
}
