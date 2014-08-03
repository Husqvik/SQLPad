namespace SqlPad
{
	public interface ILargeTextValue
	{
		bool IsEditable { get; }

		string Preview { get; }

		long Length { get; }

		string Value { get; }

		string GetChunk(int offset, int length);
	}

	public interface ILargeBinaryValue
	{
		bool IsEditable { get; }

		long Length { get; }

		byte[] Value { get; }

		byte[] GetChunk(int offset, int length);
	}
}