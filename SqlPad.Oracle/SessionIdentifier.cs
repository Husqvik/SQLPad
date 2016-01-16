using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("SessionIdentifier (Instance={Instance}; SessionId={SessionId})")]
	public struct SessionIdentifier
	{
		public int Instance { get; }

		public int SessionId { get; }

		public SessionIdentifier(int instance, int sessionId)
		{
			Instance = instance;
			SessionId = sessionId;
		}

		public bool Equals(SessionIdentifier other)
		{
			return Instance == other.Instance && SessionId == other.SessionId;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}

			return obj is SessionIdentifier && Equals((SessionIdentifier)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (Instance * 397) ^ SessionId;
			}
		}

		public static bool operator ==(SessionIdentifier left, SessionIdentifier right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(SessionIdentifier left, SessionIdentifier right)
		{
			return !left.Equals(right);
		}
	}
}
