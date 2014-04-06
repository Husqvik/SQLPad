using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleToken (Value={Value}, Index={Index})")]
	public struct OracleToken : IToken
	{
		public static OracleToken Empty = new OracleToken();
		private readonly string _value;
		private readonly int _index;

		public OracleToken(string value, int index)
		{
			_value = value;
			_index = index;

#if DEBUG
			//Trace.Write("{" + value + "@" + index + "}");
#endif
		}

		public string Value { get { return _value; } }

		public int Index { get { return _index; } }
	}
}