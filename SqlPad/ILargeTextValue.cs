namespace SqlPad
{
	public interface ILargeValue
	{
		string DataTypeName { get; }

		bool IsEditable { get; }

		long Length { get; }
	}

	public interface ILargeTextValue: ILargeValue
	{
		string Preview { get; }

		string Value { get; }

		string GetChunk(int offset, int length);
	}

	public interface ILargeBinaryValue : ILargeValue
	{
		byte[] Value { get; }

		byte[] GetChunk(int bytes);
	}
}