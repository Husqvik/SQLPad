using System;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleDataType (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleDataType : OracleObject
	{
		public static readonly OracleDataType Empty = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, String.Empty) };

		public int? Length { get; set; }

		public int? Precision { get; set; }
		
		public int? Scale { get; set; }

		public DataUnit Unit { get; set; }

		public bool IsPrimitive { get { return String.IsNullOrEmpty(FullyQualifiedName.Owner); } }

		public static string ResolveFullTypeName(OracleDataType dataType, int? characterSize = null)
		{
				if (!dataType.IsPrimitive)
				{
					return dataType.FullyQualifiedName.ToString();
				}

				var name = dataType.FullyQualifiedName.Name.Trim('"');
				var effectiveSize = String.Empty;
				switch (name)
				{
					case "NVARCHAR2":
					case "NVARCHAR":
					case "VARCHAR2":
					case "VARCHAR":
					case "NCHAR":
					case "CHAR":
						var effectiveLength = characterSize ?? dataType.Length;
						if (effectiveLength.HasValue)
						{
							var unit = dataType.Unit == DataUnit.Byte ? " BYTE" : " CHAR";
							effectiveSize = String.Format("({0}{1})", effectiveLength, dataType.Unit == DataUnit.NotApplicable ? null : unit);
						}

						name = String.Format("{0}{1}", name, effectiveSize);
						break;
					case "FLOAT":
					case "NUMBER":
						var decimalScale = dataType.Scale > 0 ? String.Format(", {0}", dataType.Scale) : null;
						if (dataType.Precision > 0 || dataType.Scale > 0)
						{
							name = String.Format("{0}({1}{2})", name, dataType.Precision == null ? "*" : Convert.ToString(dataType.Precision), decimalScale);
						}
						break;
					case "RAW":
						name = String.Format("{0}({1})", name, dataType.Length);
						break;
				}

				return name;
		}
	}
}
