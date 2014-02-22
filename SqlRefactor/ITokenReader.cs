using System.Collections.Generic;

namespace SqlRefactor
{
	public interface ITokenReader
	{
		IEnumerable<IToken> GetTokens(bool includeCommentBlocks = false);
	}
}