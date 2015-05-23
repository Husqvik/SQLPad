using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace SqlPad
{
	public interface IValue
	{
		bool IsNull { get; }

		string ToLiteral();
	}

	public interface ILargeValue : IValue
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

		void GetChunk(StringBuilder stringBuilder, int offset, int length);
	}

	public interface ILargeBinaryValue : ILargeValue
	{
		byte[] Value { get; }

		byte[] GetChunk(int bytes);
	}

	public interface ICollectionValue : ILargeValue
	{
		IList Records { get; }

		ColumnHeader ColumnHeader { get; }
	}

	public interface IComplexType : ILargeValue
	{
		ICollection<CustomTypeAttributeValue> Attributes { get; }
	}

	public struct CustomTypeAttributeValue
	{
		public ColumnHeader ColumnHeader { get; set; }
		
		public object Value { get; set; }
	}
}
