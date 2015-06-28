using System;
using System.Diagnostics;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.DataDictionary
{
	[DebuggerDisplay("OracleDataType (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleDataType : OracleObject
	{
		public static readonly OracleDataType Empty = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, String.Empty) };
		public static readonly OracleDataType NumberType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, TerminalValues.Number) };
		public static readonly OracleDataType XmlType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(OracleDatabaseModelBase.SchemaSys, OracleTypeBase.TypeCodeXml) };
		public static readonly OracleDataType DynamicCollectionType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, "DYNAMIC"), IsDynamicCollection = true };

		public bool IsDynamicCollection { get; private set; }

		public int? Length { get; set; }

		public int? Precision { get; set; }
		
		public int? Scale { get; set; }

		public DataUnit Unit { get; set; }

		public bool IsPrimitive { get { return !FullyQualifiedName.HasOwner; } }

		public static OracleDataType CreateTimestampDataType(int precision)
		{
			return new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, TerminalValues.Timestamp), Precision = precision };
		}

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
				case TerminalValues.NVarchar2:
				case TerminalValues.NVarchar:
				case TerminalValues.Varchar2:
				case TerminalValues.Varchar:
				case TerminalValues.NChar:
				case TerminalValues.Char:
					var effectiveLength = characterSize ?? dataType.Length;
					if (effectiveLength.HasValue)
					{
						var unit = dataType.Unit == DataUnit.Byte ? " BYTE" : " CHAR";
						effectiveSize = String.Format("({0}{1})", effectiveLength, dataType.Unit == DataUnit.NotApplicable ? null : unit);
					}

					name = String.Format("{0}{1}", name, effectiveSize);
					break;
				case TerminalValues.Float:
				case TerminalValues.Number:
					var decimalScale = dataType.Scale > 0 ? String.Format(", {0}", dataType.Scale) : null;
					if (dataType.Precision > 0 || dataType.Scale > 0)
					{
						name = String.Format("{0}({1}{2})", name, dataType.Precision == null ? "*" : Convert.ToString(dataType.Precision), decimalScale);
					}
					
					break;
				case TerminalValues.Raw:
					name = String.Format("{0}({1})", name, dataType.Length);
					break;
				case TerminalValues.Timestamp:
					if (dataType.Scale.HasValue)
					{
						name = String.Format("{0}({1})", name, dataType.Scale);
					}
					
					break;
			}

			return name;
		}
	}
}
