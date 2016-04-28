using System;
using System.Collections.Generic;
using System.Windows.Data;

namespace SqlPad
{
	public class ColumnHeader
	{
		public int ColumnIndex { get; set; }

		public string Name { get; set; }

		public string DatabaseDataType { get; set; }

		public Type DataType { get; set; }

		public IValueConverter CustomConverter { get; set; }

		public IReadOnlyCollection<IReferenceDataSource> ParentReferenceDataSources { get; set; }

		public bool IsNumeric => DataType.In(typeof(Decimal), typeof(Int16), typeof(Int32), typeof(Int64), typeof(Byte), typeof(Single), typeof(Double));

		public override string ToString()
		{
			return Name;
		}
	}
}