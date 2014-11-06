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
	}
}