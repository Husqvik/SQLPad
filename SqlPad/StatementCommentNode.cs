using System;
using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("StatementCommentNode (SourcePosition=({SourcePosition.IndexStart}-{SourcePosition.IndexEnd}); Text={Token.Value})")]
	public class StatementCommentNode : StatementNode
	{
		public StatementCommentNode(StatementGrammarNode parentNode, IToken token)
			: base(parentNode?.Statement, token)
		{
			if (token == null)
				throw new ArgumentNullException(nameof(token));

			ParentNode = parentNode;
		}

		protected override SourcePosition BuildSourcePosition()
		{
			return new SourcePosition { IndexStart = Token.Index, IndexEnd = Token.Index + Token.Value.Length - 1 };
		}
	}
}