using System;

namespace SqlPad
{
	public class StatementCommentNode : StatementNode
	{
		public StatementCommentNode(StatementBase statement, IToken token) : base(statement, token)
		{
			if (token == null)
				throw new ArgumentNullException("token");
		}

		protected override SourcePosition BuildSourcePosition()
		{
			return new SourcePosition { IndexStart = Token.Index, IndexEnd = Token.Index + Token.Value.Length - 1 };
		}
	}
}