namespace SqlPad
{
	public interface IToken
	{
		string Value { get; }
		
		int Index { get; }

		CommentType CommentType { get; }
	}

	public enum CommentType
	{
		None,
		Line,
		Block
	}
}