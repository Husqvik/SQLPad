namespace SqlRefactor
{
	public interface IToken
	{
		string Value { get; }
		int Index { get; }
	}
}