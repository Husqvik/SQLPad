using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleToken (Value={Value}, Index={Index})")]
	public struct OracleToken : IToken
	{
		public static OracleToken Empty = new OracleToken();
		private readonly string _value;
		private readonly int _index;
		private readonly bool _isComment;

		public OracleToken(string value, int index, bool isComment = false)
		{
			_value = value;
			_index = index;
			_isComment = isComment;

#if DEBUG
			//Trace.Write("{" + value + "@" + index + "}");
#endif
		}

		public string Value { get { return _value; } }

		public int Index { get { return _index; } }
		
		public bool IsComment { get { return _isComment; } }
	}
}