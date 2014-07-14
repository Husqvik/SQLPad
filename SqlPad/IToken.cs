namespace SqlPad
{
	public interface IToken
	{
		string Value { get; }
		
		int Index { get; }
		
		bool IsComment { get; }
	}
}