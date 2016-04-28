using System.Diagnostics;

namespace SqlPad
{
	[DebuggerDisplay("ResultInfo (ResultIdentifier={ResultIdentifier}; Type={Type})")]
	public struct ResultInfo
	{
		public readonly string Title;
		public readonly string ResultIdentifier;
		public readonly ResultIdentifierType Type;

		public ResultInfo(string resultIdentifier, string title, ResultIdentifierType type)
		{
			ResultIdentifier = resultIdentifier;
			Title = title;
			Type = type;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is ResultInfo && Equals((ResultInfo)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((ResultIdentifier?.GetHashCode() ?? 0) * 397) ^ Type.GetHashCode();
			}
		}

		private bool Equals(ResultInfo other)
		{
			return string.Equals(ResultIdentifier, other.ResultIdentifier) && Type == other.Type;
		}
	}

	public enum ResultIdentifierType
	{
		SystemGenerated,
		UserDefined
	}
}