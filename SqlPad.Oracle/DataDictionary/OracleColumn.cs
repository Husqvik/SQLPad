using System.Diagnostics;

namespace SqlPad.Oracle.DataDictionary
{
	[DebuggerDisplay("OracleColumn (Name={Name}; Type={FullTypeName})")]
	public class OracleColumn
	{
		public OracleColumn(bool isPseudoColumn = false)
		{
			IsPseudoColumn = isPseudoColumn;
		}

		public OracleDataType DataType { get; set; }

		public string Name { get; set; }

		public string FullTypeName
		{
			get { return OracleDataType.ResolveFullTypeName(DataType, CharacterSize); }
		}

		public int? CharacterSize { get; set; }

		public bool Nullable { get; set; }
		
		public bool Virtual { get; set; }

		public bool? UserGenerated { get; set; }
		
		public bool Hidden { get; set; }

		public string DefaultValue { get; set; }
		
		public bool IsPseudoColumn { get; private set; }

		public OracleColumn Clone()
		{
			return
				new OracleColumn
				{
					DataType = DataType,
					Name = Name,
					CharacterSize = CharacterSize,
					Nullable = Nullable
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
