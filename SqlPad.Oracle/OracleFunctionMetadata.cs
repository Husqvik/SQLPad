using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;

namespace SqlPad.Oracle
{
	[DataContract(Namespace = Namespaces.SqlPadBaseNamespace)]
	[DebuggerDisplay("OracleFunctionMetadata (Identifier={Identifier.FullyQualifiedIdentifier}; Overload={Identifier.Overload}; DataType={DataType}; IsAnalytic={IsAnalytic}; IsAggregate={IsAggregate}; MinimumArguments={MinimumArguments}; MaximumArguments={MaximumArguments})")]
	public class OracleFunctionMetadata
	{
		public const string DisplayTypeNormal = "NORMAL";
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

	public enum AuthId
	{
		[EnumMember]
		CurrentUser,
		[EnumMember]
		Definer
	}
}
