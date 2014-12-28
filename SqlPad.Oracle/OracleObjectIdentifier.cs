using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleObjectIdentifier (Owner={Owner,nq}; Name={Name,nq})")]
	public struct OracleObjectIdentifier
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
			NormalizedOwner = owner.ToQuotedIdentifier();
			NormalizedName = name.ToQuotedIdentifier();
		}

		public bool HasOwner
		{
			get { return !String.IsNullOrEmpty(Owner); }
		}

		public static OracleObjectIdentifier Create(string owner, string name)
		{
			return new OracleObjectIdentifier(owner, name);
		}

		public static OracleObjectIdentifier Create(StatementGrammarNode ownerNode, StatementGrammarNode objectNode, StatementGrammarNode aliasNode)
		{
			var ownerName = ownerNode == null ? null : ownerNode.Token.Value;
			var tableName = aliasNode == null
				? objectNode == null
					? null
					: objectNode.Token.Value
				: aliasNode.Token.Value;

			return Create(ownerName, tableName);
		}

		public static ICollection<OracleObjectIdentifier> GetUniqueReferences(ICollection<OracleObjectIdentifier> identifiers)
		{
			var sourceIdentifiers = new HashSet<OracleObjectIdentifier>(identifiers);
			var uniqueIdentifiers = new HashSet<OracleObjectIdentifier>();

			var references = identifiers.Where(i => !String.IsNullOrEmpty(i.NormalizedName))
				.GroupBy(i => i.NormalizedName)
				.ToDictionary(g => g.Key, g => g.GroupBy(o => o.NormalizedOwner).ToDictionary(go => go.Key, go => go.Count()));

			if (references.Count == 0)
				return identifiers;

			foreach (var nameOwners in references)
			{
				var ownerSpecified = nameOwners.Value.Any(no => !String.IsNullOrEmpty(no.Key));
				foreach (var ownerCounts in nameOwners.Value.Where(oc => oc.Value == 1))
				{
					if (!ownerSpecified || !String.IsNullOrEmpty(ownerCounts.Key))
						uniqueIdentifiers.Add(sourceIdentifiers.Single(i => i == Create(ownerCounts.Key, nameOwners.Key)));
				}
			}

			return uniqueIdentifiers;
		}

		public static string MergeIdentifiersIntoSimpleString(string identifier1, string identifier2)
		{
			var ownerPrefix = String.IsNullOrEmpty(identifier1) ? null : String.Format("{0}.", identifier1.ToSimpleIdentifier());
			return String.Format("{0}{1}", ownerPrefix, identifier2.ToSimpleIdentifier());
		}

		#region Overrides of ValueType
		public override string ToString()
		{
			var ownerPrefix = !HasOwner ? null : Owner.ToSimpleIdentifier() + ".";
			return ownerPrefix + Name.ToSimpleIdentifier();
		}
		#endregion

		public string ToNormalizedString()
		{
			var ownerPrefix = !HasOwner ? null : NormalizedOwner + ".";
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