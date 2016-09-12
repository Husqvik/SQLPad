using System.Diagnostics;

namespace SqlPad.Oracle.DataDictionary
{
	[DebuggerDisplay("OracleColumn (Name={Name}; Type={FullTypeName}; Nullable={Nullable})")]
	public class OracleColumn
	{
		private const string ColumnNameColumnValue = "\"COLUMN_VALUE\"";

		public OracleColumn(bool isPseudocolumn = false)
		{
			IsPseudocolumn = isPseudocolumn;
		}

		public OracleDataType DataType { get; set; }

		public string Name { get; set; }

		public string FullTypeName => OracleDataType.ResolveFullTypeName(DataType, CharacterSize);

		public int? CharacterSize { get; set; }

		public bool Nullable { get; set; }
		
		public bool Virtual { get; set; }

		public bool? UserGenerated { get; set; }
		
		public bool Hidden { get; set; }

		public string DefaultValue { get; set; }
		
		public bool IsPseudocolumn { get; private set; }

		public OracleColumn Clone()
		{
			return
				new OracleColumn
				{
					DataType = DataType,
					Name = Name,
					CharacterSize = CharacterSize,
					Nullable = Nullable,
					Hidden = Hidden
				};
		}

		public static OracleColumn BuildColumnValueColumn(OracleDataType columnType)
		{
			return
				new OracleColumn
				{
					Name = ColumnNameColumnValue,
					DataType = columnType,
					Nullable = true
				};
		}
	}

	public enum DataUnit
	{
		NotApplicable,
		Byte,
		Character
	}
}
