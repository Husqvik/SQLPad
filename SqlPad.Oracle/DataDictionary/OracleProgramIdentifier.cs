using System;
using System.Diagnostics;
using System.Text;

namespace SqlPad.Oracle.DataDictionary
{
	[DebuggerDisplay("OracleProgramIdentifier (FullyQualifiedIdentifier={FullyQualifiedIdentifier}; Overload={Overload})")]
	public struct OracleProgramIdentifier
	{
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramCast = CreateFromValues(null, null, "CAST");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramExtract = CreateFromValues(null, null, "EXTRACT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramLnNvl = CreateFromValues(null, null, "LNNVL");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRatioToReport = CreateFromValues(null, null, "RATIO_TO_REPORT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramSysConnectByPath = CreateFromValues(null, null, "SYS_CONNECT_BY_PATH");

		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRound = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "ROUND");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramLevel = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "LEVEL");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramNextDay = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "NEXT_DAY");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRegularExpressionCount = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "REGEXP_COUNT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRegularExpressionInstr = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "REGEXP_INSTR");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRegularExpressionReplace = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "REGEXP_REPLACE");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRegularExpressionSubstring = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "REGEXP_SUBSTR");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRowNum = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "ROWNUM");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramSysContext = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "SYS_CONTEXT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToChar = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "TO_CHAR");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToDate = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "TO_DATE");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToTimestamp = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "TO_TIMESTAMP");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToTimestampWithTimeZone = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "TO_TIMESTAMP_TZ");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramTrunc = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "TRUNC");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramConvert = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "CONVERT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramNumberToYearToMonthInterval = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "NUMTOYMINTERVAL");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramNumberToDayToSecondInterval = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageBuiltInFunction, "NUMTODSINTERVAL");
		internal static readonly OracleProgramIdentifier IdentifierDbmsRandomString = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageDbmsRandom, "STRING");
		internal static readonly OracleProgramIdentifier IdentifierDbmsCryptoHash = CreateFromValues(OracleObjectIdentifier.SchemaSys, OracleObjectIdentifier.PackageDbmsCrypto, "HASH");

		public string Owner { get; private set; }

		public string Name { get; private set; }

		public string Package { get; private set; }

		public int Overload { get; private set; }

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

		private OracleProgramIdentifier(string owner, string package, string name, int overload)
		{
			Owner = owner;
			Package = package;
			Name = name;
			Overload = overload;
		}

		public static OracleProgramIdentifier CreateBuiltIn(string name)
		{
			return new OracleProgramIdentifier(String.Empty, String.Empty, name, 0);
		}

		public static OracleProgramIdentifier CreateFromValues(string owner, string package, string name, int overload = 0)
		{
			return new OracleProgramIdentifier(owner.ToQuotedIdentifier(), package.ToQuotedIdentifier(), name.ToQuotedIdentifier(), overload);
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
				var hashCode = Owner?.GetHashCode() ?? 0;
				hashCode = (hashCode * 397) ^ (Name?.GetHashCode() ?? 0);
				hashCode = (hashCode * 397) ^ (Package?.GetHashCode() ?? 0);
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
