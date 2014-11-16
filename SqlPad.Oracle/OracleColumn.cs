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
			get { return OracleDataType.ResolveFullTypeName(DataType, CharacterSize); }
		}

		public int? CharacterSize { get; set; }

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
