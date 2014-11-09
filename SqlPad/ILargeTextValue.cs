using System;
using System.Collections;

namespace SqlPad
{
	public interface ILargeValue
	{
		string DataTypeName { get; }

		bool IsEditable { get; }

		long Length { get; }

		void Prefetch();
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

	public interface ICollectionValue : ILargeValue
	{
		IList Records { get; }

		Type ItemType { get; }
	}
}
