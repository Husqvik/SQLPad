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
		private const string OwnerBuiltInFunction = "\"SYS\"";
		private const string PackageBuiltInFunction = "\"STANDARD\"";

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

		public OracleFunctionMetadata GetSqlFunctionMetadata(OracleFunctionIdentifier identifier, int parameterCount, bool forceBuiltInFunction)
		{
			OracleFunctionMetadata metadata = null;
			if (String.IsNullOrEmpty(identifier.Package) && (forceBuiltInFunction || String.IsNullOrEmpty(identifier.Owner)))
			{
				var builtInPackageIdentifier = new OracleFunctionIdentifier { Name = identifier.Name, Package = PackageBuiltInFunction, Owner = OwnerBuiltInFunction };
				var nonPackageBuiltInIdentifier = new OracleFunctionIdentifier { Name = identifier.Name, Package = String.Empty, Owner = String.Empty };
				metadata = TryFindFunctionOverload(parameterCount, builtInPackageIdentifier, nonPackageBuiltInIdentifier);
			}

			return metadata ?? TryFindFunctionOverload(parameterCount, identifier);
		}

		private OracleFunctionMetadata TryFindFunctionOverload(int parameterCount, params OracleFunctionIdentifier[] identifiers)
		{
			return SqlFunctions.Where(m => identifiers.Any(i => i.EqualsWithAnyOverload(m.Identifier)))
				.OrderBy(m => Math.Abs(parameterCount - m.Parameters.Count + 1))
				.FirstOrDefault();
		}
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

	[DataContract(Namespace = Namespaces.SqlPadBaseNamespace)]
	[DebuggerDisplay("OracleFunctionMetadata (Identifier={Identifier.FullyQualifiedIdentifier}; Overload={Identifier.Overload}; DataType={DataType}; IsAnalytic={IsAnalytic}; IsAggregate={IsAggregate}; MinimumArguments={MinimumArguments}; MaximumArguments={MaximumArguments})")]
	public class OracleFunctionMetadata
	{
		public const string DisplayTypeParenthesis = "PARENTHESIS";

		[DataMember]
		private int? _metadataMinimumArguments;

		[DataMember]
		private int? _metadataMaximumArguments;

		internal OracleFunctionMetadata(OracleFunctionIdentifier identifier, bool isAnalytic, bool isAggregate, bool isPipelined, bool isOffloadable, bool parallelSupport, bool isDeterministic, int? metadataMinimumArguments, int? metadataMaximumArguments, AuthId authId, string displayType, bool isBuiltIn)
		{
			Identifier = identifier;
			IsAnalytic = isAnalytic;
			IsAggregate = isAggregate;
			IsPipelined = isPipelined;
			IsOffloadable = isOffloadable;
			ParallelSupport = parallelSupport;
			IsDeterministic = isDeterministic;
			_metadataMinimumArguments = metadataMinimumArguments;
			_metadataMaximumArguments = metadataMaximumArguments;
			AuthId = authId;
			DisplayType = displayType;
			IsBuiltIn = isBuiltIn;
			Parameters = new List<OracleFunctionParameterMetadata>();
		}

		[DataMember]
		public ICollection<OracleFunctionParameterMetadata> Parameters { get; private set; }

		[DataMember]
		public bool IsBuiltIn { get; private set; }

		[DataMember]
		public OracleFunctionIdentifier Identifier { get; private set; }

		[DataMember]
		public string DataType { get; private set; }

		[DataMember]
		public bool IsAnalytic { get; private set; }

		[DataMember]
		public bool IsAggregate { get; private set; }

		[DataMember]
		public bool IsPipelined { get; private set; }

		[DataMember]
		public bool IsOffloadable { get; private set; }

		[DataMember]
		public bool ParallelSupport { get; private set; }

		[DataMember]
		public bool IsDeterministic { get; private set; }

		public int MinimumArguments
		{
			get { return Parameters.Count > 1 ? Parameters.Count(p => !p.IsOptional) - 1 : (_metadataMinimumArguments ?? 0); }
		}

		public int MaximumArguments
		{
			get { return Parameters.Count > 1 && _metadataMaximumArguments == null ? Parameters.Count - 1 : (_metadataMaximumArguments ?? 0); }
		}

		public bool IsPackageFunction
		{
			get { return !String.IsNullOrEmpty(Identifier.Package); }
		}

		[DataMember]
		public AuthId AuthId { get; private set; }

		[DataMember]
		public string DisplayType { get; private set; }
	}

	public enum AuthId
	{
		[EnumMember]
		CurrentUser,
		[EnumMember]
		Definer
	}

	[DataContract(Namespace = Namespaces.SqlPadBaseNamespace)]
	[DebuggerDisplay("OracleFunctionParameterMetadata (Name={Name}; Position={Position}; DataType={DataType}; Direction={Direction}; IsOptional={IsOptional})")]
	public class OracleFunctionParameterMetadata
	{
		internal OracleFunctionParameterMetadata(string name, int position, ParameterDirection direction, string dataType, bool isOptional)
		{
			Name = name;
			Position = position;
			DataType = dataType;
			Direction = direction;
			IsOptional = isOptional;
		}

		[DataMember]
		public string Name { get; private set; }

		[DataMember]
		public int Position { get; private set; }

		[DataMember]
		public string DataType { get; private set; }

		[DataMember]
		public ParameterDirection Direction { get; private set; }

		[DataMember]
		public bool IsOptional { get; private set; }
	}

	public enum ParameterDirection
	{
		[EnumMember]
		Input,
		[EnumMember]
		Output,
		[EnumMember]
		InputOutput,
		[EnumMember]
		ReturnValue,
	}
}