namespace SqlPad
{
	public interface IObjectIdentifier
	{
		string Owner { get; }
		string Name { get; }
	}
}