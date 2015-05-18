using System;
using System.Diagnostics;
using System.Text;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleProgramIdentifier (FullyQualifiedIdentifier={FullyQualifiedIdentifier}; Overload={Overload})")]
	public struct OracleProgramIdentifier
	{
		public string Owner { get; set; }

		public string Name { get; set; }

		public string Package { get; set; }

		public int Overload { get; set; }

		public string FullyQualifiedIdentifier
		{
			get
			{
				var builder = new StringBuilder(100);
				if (!String.IsNullOrEmpty(Owner))
				{
					builder.Append(Owner.ToSimpleIdentifier());
					builder.Append('.');
				}

				if (!String.IsNullOrEmpty(Package))
				{
					builder.Append(Package.ToSimpleIdentifier());
					builder.Append('.');
				}

				builder.Append(Name.ToSimpleIdentifier());
				return builder.ToString();
			}
		}

		public static OracleProgramIdentifier CreateBuiltIn(string name)
		{
			return
				new OracleProgramIdentifier
				{
					Owner = String.Empty,
					Package = String.Empty,
					Name = name
				};
		}

		public static OracleProgramIdentifier CreateFromValues(string owner, string package, string name, int overload = 0)
		{
			return new OracleProgramIdentifier
			{
				Owner = owner.ToQuotedIdentifier(),
				Package = package.ToQuotedIdentifier(),
				Name = name.ToQuotedIdentifier(),
				Overload = overload
			};
		}

		#region Equality members
		public bool Equals(OracleProgramIdentifier other)
		{
			return String.Equals(Owner, other.Owner) && String.Equals(Name, other.Name) && String.Equals(Package, other.Package);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			return obj is OracleProgramIdentifier && Equals((OracleProgramIdentifier)obj);
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

		public static bool operator ==(OracleProgramIdentifier left, OracleProgramIdentifier right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(OracleProgramIdentifier left, OracleProgramIdentifier right)
		{
			return !left.Equals(right);
		}
		#endregion
	}
}
