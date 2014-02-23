using System.Collections.Generic;

namespace SqlPad
{
	public interface ITokenReader
	{
		IEnumerable<IToken> GetTokens(bool includeCommentBlocks = false);
	}
}