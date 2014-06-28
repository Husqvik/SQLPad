using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace SqlPad.Oracle
{
	[DataContract(Namespace = Namespaces.SqlPadBaseNamespace)]
	[DebuggerDisplay("OracleFunctionMetadataCollection (Count={SqlFunctions.Count})")]
	public class OracleFunctionMetadataCollection
	{
		public const string PackageBuiltInFunction = "\"STANDARD\"";

		internal OracleFunctionMetadataCollection(IEnumerable<OracleFunctionMetadata> metadata)
		{
			SqlFunctions = metadata.ToArray();
			Timestamp = DateTime.UtcNow;
		}

		[DataMember]
		public ICollection<OracleFunctionMetadata> SqlFunctions { get; private set; }

		public string Rdbms
		{
			get { return "Oracle"; }
		}

		public string Version
		{
			get { return "12.1.0.1.0"; }
		}

		[DataMember]
		public DateTime Timestamp { get; private set; }
	}

	[DataContract(Namespace = Namespaces.SqlPadBaseNamespace)]
	[DebuggerDisplay("OracleFunctionIdentifier (FullyQualifiedIdentifier={FullyQualifiedIdentifier}; Overload={Overload})")]
	public struct OracleFunctionIdentifier
	{
		[DataMember]
		public string Owner { get; set; }

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public string Package { get; set; }

		[DataMember]
		public int Overload { get; set; }

		public string FullyQualifiedIdentifier
		{
			get
			{
				return (String.IsNullOrEmpty(Owner) ? null : Owner.ToSimpleIdentifier() + ".") +
					   (String.IsNullOrEmpty(Package) ? null : Package.ToSimpleIdentifier() + ".") +
					   Name.ToSimpleIdentifier();
			}
		}

		public static OracleFunctionIdentifier CreateFromValues(string owner, string package, string name, int overload = 0)
		{
			return new OracleFunctionIdentifier
			{
				Owner = owner.ToQuotedIdentifier(),
				Package = package.ToQuotedIdentifier(),
				Name = name.ToQuotedIdentifier(),
				Overload = overload
			};
		}

		public bool EqualsWithAnyOverload(OracleFunctionIdentifier other)
		{
			return string.Equals(Owner, other.Owner) && string.Equals(Name, other.Name) && string.Equals(Package, other.Package);
		}

		#region Equality members
		public bool Equals(OracleFunctionIdentifier other)
		{
			return string.Equals(Owner, other.Owner) && string.Equals(Name, other.Name) && string.Equals(Package, other.Package) && Overload == other.Overload;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is OracleFunctionIdentifier && Equals((OracleFunctionIdentifier)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (Owner != null ? Owner.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Package != null ? Package.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ Overload;
				return hashCode;
			}
		}

		public static bool operator ==(OracleFunctionIdentifier left, OracleFunctionIdentifier right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(OracleFunctionIdentifier left, OracleFunctionIdentifier right)
		{
			return !left.Equals(right);
		}
		#endregion
	}
}
