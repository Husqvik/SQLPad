using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SqlPad.Oracle
{
	public static class OracleBindVariable
	{
		public const string DataTypeChar = "CHAR";
		public const string DataTypeClob = "CLOB";
		public const string DataTypeDate = "DATE";
		public const string DataTypeUnicodeChar = "NCHAR";
		public const string DataTypeUnicodeClob = "NCLOB";
		public const string DataTypeNumber = "NUMBER";
		public const string DataTypeUnicodeVarchar2 = "NVARCHAR2";
		public const string DataTypeVarchar2 = "VARCHAR2";

		private static readonly Dictionary<string, Type> BindDataTypesInternal =
			new Dictionary<string, Type>
			{
				{ DataTypeChar, typeof(string) },
				{ DataTypeClob, typeof(string) },
				{ DataTypeDate, typeof(DateTime) },
				{ DataTypeUnicodeChar, typeof(string) },
				{ DataTypeUnicodeClob, typeof(string) },
				{ DataTypeNumber, typeof(string) },
				{ DataTypeUnicodeVarchar2, typeof(string) },
				{ DataTypeVarchar2, typeof(string) }
			};

		private static readonly IDictionary<string, Type> BindDataTypesDictionary =
			new ReadOnlyDictionary<string, Type>(BindDataTypesInternal);

		public static IDictionary<string, Type> DataTypes
		{
			get { return BindDataTypesDictionary; }
		}
	}
}
