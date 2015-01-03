using System;
using System.Collections.Generic;
using System.Linq;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	internal class OracleFoldingSectionProvider : IFoldingSectionProvider
	{
		public const string FoldingSectionPlaceholderSubquery = "Subquery";
		public const string FoldingSectionPlaceholderPlSqlBlock = "PL/SQL Block";
		public const string FoldingSectionPlaceholderException = "Exception";

		private const string StackKeySubquery = "Subquery";
		private const string StackKeyPlSql = "PL/SQL";

		public IEnumerable<FoldingSection> GetFoldingSections(IEnumerable<IToken> tokens)
		{
			return GetFoldingSectionsInteral(tokens).OrderBy(s => s.FoldingStart);
		}
		
		private IEnumerable<FoldingSection> GetFoldingSectionsInteral(IEnumerable<IToken> tokens)
		{
			FoldingSection foldingSection;

			var foldingContext = new FoldingContext(TerminalValues.LeftParenthesis, TerminalValues.Begin);
			var previousToken = OracleToken.Empty;
			var tokenArray = tokens.Where(t => t.CommentType == CommentType.None).ToArray();
			for (int i = 0; i < tokenArray.Length; i++)
			{
				var token = (OracleToken)tokenArray[i];
				var existsPrecedingParenthesis = previousToken.Value == TerminalValues.LeftParenthesis;
				if (existsPrecedingParenthesis)
				{
					foldingContext.OpenScope(TerminalValues.LeftParenthesis);
				}

				var isSelect = token.UpperInvariantValue == TerminalValues.Select;
				if (isSelect && (existsPrecedingParenthesis || previousToken.Value == TerminalValues.Semicolon || String.IsNullOrEmpty(previousToken.Value)))
				{
					foldingContext.AddFolding(FoldingSectionPlaceholderSubquery, StackKeySubquery, token.Index);
				}

				var isClosingParenthesis = token.Value == TerminalValues.RightParenthesis;
				if (isClosingParenthesis || token.Value == TerminalValues.Semicolon || token.Value == TerminalValues.SqlPlusTerminator)
				{
					if (foldingContext.TryFinishFoldingSection(StackKeySubquery, StackKeySubquery, isClosingParenthesis ? previousToken : token, out foldingSection))
					{
						yield return foldingSection;
					}

					if (isClosingParenthesis)
					{
						foldingContext.CloseScope(TerminalValues.LeftParenthesis);
					}
				}

				if (token.UpperInvariantValue == TerminalValues.Begin)
				{
					foldingContext.OpenScope(TerminalValues.Begin);
					foldingContext.AddFolding(FoldingSectionPlaceholderPlSqlBlock, StackKeyPlSql, token.Index);
				}
				else if (previousToken.UpperInvariantValue == TerminalValues.End && token.Value == TerminalValues.Semicolon)
				{
					if (i > 1 && foldingContext.TryFinishFoldingSection(FoldingSectionPlaceholderException, StackKeyPlSql, (OracleToken)tokenArray[i - 2], out foldingSection))
					{
						yield return foldingSection;
					}

					if (foldingContext.TryFinishFoldingSection(FoldingSectionPlaceholderPlSqlBlock, StackKeyPlSql, token, out foldingSection))
					{
						yield return foldingSection;
					}

					foldingContext.CloseScope(TerminalValues.Begin);
				}

				if (token.UpperInvariantValue == TerminalValues.Exception)
				{
					foldingContext.AddFolding(FoldingSectionPlaceholderException, StackKeyPlSql, token.Index);
				}

				previousToken = token;
			}

			if (foldingContext.TryFinishFoldingSection(previousToken, out foldingSection))
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
