using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleToken (Value={Value}, Index={Index})")]
	public struct OracleToken : IToken
	{
		public static OracleToken Empty = new OracleToken();
		private readonly string _value;
		private readonly int _index;
		private readonly CommentType _commentType;

		public OracleToken(string value, int index, CommentType commentType = CommentType.None)
		{
			_value = value;
			_index = index;
			_commentType = commentType;
		}

		public string Value { get { return _value; } }

		public int Index { get { return _index; } }

		public CommentType CommentType { get { return _commentType; } }
	}
}