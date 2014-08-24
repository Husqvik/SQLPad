using System;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleFunctionIdentifier (FullyQualifiedIdentifier={FullyQualifiedIdentifier}; Overload={Overload})")]
	public struct OracleFunctionIdentifier
	{
		public string Owner { get; set; }

		public string Name { get; set; }

		public string Package { get; set; }

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

		public bool EqualsWithOverload(OracleFunctionIdentifier other)
		{
			return string.Equals(Owner, other.Owner) && string.Equals(Name, other.Name) && string.Equals(Package, other.Package) && Overload == other.Overload;
		}

		#region Equality members
		public bool Equals(OracleFunctionIdentifier other)
		{
			return string.Equals(Owner, other.Owner) && string.Equals(Name, other.Name) && string.Equals(Package, other.Package);
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
