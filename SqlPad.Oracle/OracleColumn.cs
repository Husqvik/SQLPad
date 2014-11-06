using System;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleColumn (Name={Name}; Type={FullTypeName})")]
	public class OracleColumn
	{
		public const string RowId = "ROWID";

		public OracleDataType DataType { get; set; }

		public string Name { get; set; }

		public string FullTypeName
		{
			get
			{
				if (!DataType.IsPrimitive)
				{
					return DataType.FullyQualifiedName.ToString();
				}

				var name = DataType.FullyQualifiedName.Name.Trim('"');
				switch (name)
				{
					case "NVARCHAR2":
					case "NVARCHAR":
					case "VARCHAR2":
					case "VARCHAR":
					case "NCHAR":
					case "CHAR":
						var unit = DataType.Unit == DataUnit.Byte ? " BYTE" : " CHAR";
						name = String.Format("{0}({1}{2})", name, CharacterSize, DataType.Unit == DataUnit.NotApplicable ? null : unit);
						break;
					case "FLOAT":
					case "NUMBER":
						var decimalScale = DataType.Scale > 0 ? String.Format(", {0}", DataType.Scale) : null;
						if (DataType.Precision > 0 || DataType.Scale > 0)
						{
							name = String.Format("{0}({1}{2})", name, DataType.Precision == null ? "*" : Convert.ToString(DataType.Precision), decimalScale);
						}
						break;
					case "RAW":
						name = String.Format("{0}({1})", name, DataType.Length);
						break;
				}

				return name;
			}
		}

		public int CharacterSize { get; set; }

		public bool Nullable { get; set; }

		public OracleColumn Clone(string newName)
		{
			return new OracleColumn
			       {
					   Name = newName,
					   Nullable = Nullable,
					   DataType = DataType,
					   CharacterSize = CharacterSize,
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