using System;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleColumn (Name={Name}; Type={FullTypeName})")]
	public class OracleColumn
	{
		public const string RowId = "ROWID";

		#region Implementation of IColumn
		public string Name { get; set; }

		public string FullTypeName
		{
			get
			{
				var name = Type;
				switch (name)
				{
					case "NVARCHAR2":
					case "NVARCHAR":
					case "VARCHAR2":
					case "VARCHAR":
					case "NCHAR":
					case "CHAR":
						var unit = Unit == DataUnit.Byte ? " BYTE" : " CHAR";
						name = String.Format("{0}({1}{2})", name, CharacterSize, Unit == DataUnit.NotApplicable ? null : unit);
						break;
					case "FLOAT":
					case "NUMBER":
						var decimalScale = Scale > 0 ? String.Format(", {0}", Scale) : null;
						if (Precision > 0 || Scale > 0)
						{
							name = String.Format("{0}({1}{2})", name, Precision == null ? "*" : Convert.ToString(Precision), decimalScale);
						}
						break;
					case "RAW":
						name = String.Format("{0}({1})", name, Size);
						break;
				}

				return name;
			}
		}
		#endregion

		public string Type { get; set; }
		public int? Precision { get; set; }
		public int? Scale { get; set; }
		public int Size { get; set; }
		public int CharacterSize { get; set; }
		public bool Nullable { get; set; }
		public DataUnit Unit { get; set; }

		public OracleColumn Clone(string newName)
		{
			return new OracleColumn
			       {
					   Name = newName,
					   Nullable = Nullable,
					   Type = Type,
					   Size = Size,
					   CharacterSize = CharacterSize,
					   Precision = Precision,
					   Scale = Scale,
					   Unit = Unit
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