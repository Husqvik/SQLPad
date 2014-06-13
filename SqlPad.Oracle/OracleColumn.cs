using System;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleColumn (Name={Name}; Type={FullTypeName})")]
	public class OracleColumn : IColumn
	{
		public const string RowId = "ROWID";

		#region Implementation of IColumn
		public string Name { get; set; }

		public string FullTypeName
		{
			get
			{
				var name = Type;
				if (Unit != DataUnit.NotApplicable)
				{
					var unit = Unit == DataUnit.Byte ? "BYTE" : "CHAR";
					name = String.Format("{0}({1} {2})", name, Size, unit);
				}
				else
				{
					var decimalScale = Scale > 0 ? String.Format(", {0}", Scale) : null;
					if (Precision > 0 || Scale > 0)
					{
						name = String.Format("{0}({1}{2})", name, Precision == null ? "*" : Convert.ToString(Precision), decimalScale);
					}
				}

				return name;
			}
		}
		#endregion

		public string Type { get; set; }
		public int? Precision { get; set; }
		public int? Scale { get; set; }
		public int Size { get; set; }
		public bool Nullable { get; set; }
		public DataUnit Unit { get; set; }
	}

	public enum DataUnit
	{
		NotApplicable,
		Byte,
		Character
	}
}