using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle.DataDictionary
{
	[DebuggerDisplay("OracleObjectIdentifier (Owner={Owner,nq}; Name={Name,nq})"), Serializable]
	public struct OracleObjectIdentifier
	{
		public const string SchemaPublic = "\"PUBLIC\"";
		public const string SchemaSys = "\"SYS\"";
		public const string SchemaSystem = "\"SYSTEM\"";
		public const string PackageBuiltInFunction = "\"STANDARD\"";
		public const string PackageDbmsStandard = "\"DBMS_STANDARD\"";
		public const string PackageDbmsRandom = "\"DBMS_RANDOM\"";
		public const string PackageDbmsCrypto = "\"DBMS_CRYPTO\"";
		public const string PackageDbmsMetadata = "\"DBMS_METADATA\"";
		public const string PackageUtlHttp = "\"UTL_HTTP\"";

		public static readonly OracleObjectIdentifier Empty = new OracleObjectIdentifier(null, null);

		internal static readonly OracleObjectIdentifier IdentifierBuiltInFunctionPackage = Create(SchemaSys, PackageBuiltInFunction);
		internal static readonly OracleObjectIdentifier IdentifierDbmsStandard = Create(SchemaSys, PackageDbmsStandard);
		internal static readonly OracleObjectIdentifier IdentifierDbmsMetadata = Create(SchemaSys, PackageDbmsMetadata);

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

		public bool HasOwner => !String.IsNullOrEmpty(Owner);

		public static OracleObjectIdentifier Create(string owner, string name)
		{
			return new OracleObjectIdentifier(owner, name);
		}

		public static OracleObjectIdentifier Create(StatementGrammarNode ownerNode, StatementGrammarNode objectNode, StatementGrammarNode aliasNode)
		{
			var ownerName = ownerNode?.Token.Value;
			var tableName = aliasNode == null
				? objectNode?.Token.Value
				: aliasNode.Token.Value;

			return Create(ownerName, tableName);
		}

		public static ICollection<OracleObjectIdentifier> GetUniqueReferences(ICollection<OracleObjectIdentifier> identifiers)
		{
			var sourceIdentifiers = identifiers.ToHashSet();
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

		#region Overrides of ValueType
		public override string ToString()
		{
			var ownerPrefix = !HasOwner ? null : $"{Owner.ToSimpleIdentifier()}.";
			return $"{ownerPrefix}{Name.ToSimpleIdentifier()}";
		}
		#endregion

		public string ToFormattedString()
		{
			var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier;
			var ownerPrefix = !HasOwner ? null : $"{OracleStatementFormatter.FormatTerminalValue(Owner.ToSimpleIdentifier(), formatOption)}.";
			return $"{ownerPrefix}{OracleStatementFormatter.FormatTerminalValue(Name.ToSimpleIdentifier(), formatOption)}";
		}

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
				return ((NormalizedOwner?.GetHashCode() ?? 0) * 397) ^ (NormalizedName?.GetHashCode() ?? 0);
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