using System;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleObjectIdentifier (Owner={Owner,nq}; Name={Name,nq})")]
	public struct OracleObjectIdentifier : IObjectIdentifier
	{
		public static readonly OracleObjectIdentifier Empty = new OracleObjectIdentifier(null, null);

		public string Owner { get; private set; }

		public string Name { get; private set; }

		public string NormalizedOwner { get; private set; }

		public string NormalizedName { get; private set; }

		public OracleObjectIdentifier(string owner, string name) : this()
		{
			Owner = owner;
			Name = name;
			NormalizedOwner = owner.ToOracleIdentifier();
			NormalizedName = name.ToOracleIdentifier();
		}

		public static OracleObjectIdentifier Create(string owner, string name)
		{
			return new OracleObjectIdentifier(owner, name);
		}

		public static OracleObjectIdentifier Create(StatementDescriptionNode ownerNode, StatementDescriptionNode tableNode, StatementDescriptionNode aliasNode)
		{
			var ownerName = ownerNode == null ? null : ownerNode.Token.Value;
			var tableName = aliasNode == null
				? tableNode == null
					? null
					: tableNode.Token.Value
				: aliasNode.Token.Value;

			return Create(ownerName, tableName);
		}

		#region Overrides of ValueType
		public override string ToString()
		{
			var ownerPrefix = String.IsNullOrEmpty(Owner) ? null : Owner.ToSimpleIdentifier() + ".";
			return ownerPrefix + Name.ToSimpleIdentifier();
		}
		#endregion

		public string ToNormalizedString()
		{
			var ownerPrefix = String.IsNullOrEmpty(NormalizedOwner) ? null : NormalizedOwner + ".";
			return ownerPrefix + NormalizedName;
		}

		#region Equality members
		public bool Equals(OracleObjectIdentifier other)
		{
			return string.Equals(NormalizedOwner, other.NormalizedOwner) && string.Equals(NormalizedName, other.NormalizedName);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is OracleObjectIdentifier && Equals((OracleObjectIdentifier)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((NormalizedOwner != null ? NormalizedOwner.GetHashCode() : 0) * 397) ^ (NormalizedName != null ? NormalizedName.GetHashCode() : 0);
			}
		}

		public static bool operator ==(OracleObjectIdentifier left, OracleObjectIdentifier right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(OracleObjectIdentifier left, OracleObjectIdentifier right)
		{
			return !left.Equals(right);
		}
		#endregion
	}
}