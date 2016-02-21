using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle.DataDictionary
{
	[DebuggerDisplay("OracleProgramMetadata (Identifier={Identifier.FullyQualifiedIdentifier}; Overload={Identifier.Overload}; IsAnalytic={IsAnalytic}; IsAggregate={IsAggregate}; MinimumArguments={MinimumArguments}; MaximumArguments={MaximumArguments})")]
	public class OracleProgramMetadata
	{
		public const string DisplayTypeNormal = "NORMAL";
		public const string DisplayTypeParenthesis = "PARENTHESIS";
		public const string DisplayTypeNoParenthesis = "NOPARENTHESIS";

		private readonly int? _metadataMinimumArguments;
		private readonly int? _metadataMaximumArguments;

		private string _documentation;
		private List<OracleProgramParameterMetadata> _parameters;
		private Dictionary<string, OracleProgramParameterMetadata> _parameterDictionary;

		internal OracleProgramMetadata(ProgramType type, OracleProgramIdentifier identifier, bool isAnalytic, bool isAggregate, bool isPipelined, bool isOffloadable, bool parallelSupport, bool isDeterministic, int? metadataMinimumArguments, int? metadataMaximumArguments, AuthId authId, string displayType, bool isBuiltIn)
		{
			Type = type;
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
		}

		public IReadOnlyList<OracleProgramParameterMetadata> Parameters
		{
			get
			{
				EnsureParameterCollection();

				return _parameters;
			}
		}

		public IReadOnlyDictionary<string, OracleProgramParameterMetadata> NamedParameters => _parameterDictionary ?? BuildParameterDictionary();

	    public void AddParameter(OracleProgramParameterMetadata parameterMetadata)
		{
			AddParameters(Enumerable.Repeat(parameterMetadata, 1));
		}

		public void AddParameters(IEnumerable<OracleProgramParameterMetadata> parameterMetadata)
		{
			EnsureParameterCollection();

			_parameters.AddRange(parameterMetadata);
		}

		private void EnsureParameterCollection()
		{
			if (_parameters == null)
			{
				_parameters = new List<OracleProgramParameterMetadata>();
			}
		}

		private IReadOnlyDictionary<string, OracleProgramParameterMetadata> BuildParameterDictionary()
		{
			return _parameterDictionary = Parameters
				.Where(p => p.Direction != ParameterDirection.ReturnValue && !String.IsNullOrEmpty(p.Name))
				.ToDictionary(p => p.Name);
		}

		public OracleProgramParameterMetadata ReturnParameter => Type == ProgramType.Procedure || Parameters.Count == 0 || Parameters[0].Direction != ParameterDirection.ReturnValue ? null : Parameters[0];

	    public bool IsBuiltIn { get; private set; }
		
		public ProgramType Type { get; private set; }

		public OracleProgramIdentifier Identifier { get; private set; }

		public bool IsAnalytic { get; private set; }

		public bool IsAggregate { get; private set; }

		public bool IsPipelined { get; private set; }

		public bool IsOffloadable { get; private set; }

		public bool ParallelSupport { get; private set; }

		public bool IsDeterministic { get; private set; }

		public int MinimumArguments => _minimumArguments ?? (_minimumArguments = ResolveMinimumArguments()).Value;

		private int? _minimumArguments;

		private int ResolveMinimumArguments()
		{
			if (Type == ProgramType.Procedure)
			{
				return MinimumParameterCount;
			}

			var properMetadataMinimumArgumentCount = Parameters.Count > 0 && Parameters[0].Direction == ParameterDirection.ReturnValue
					? MinimumParameterCount - 1
					: (int?)null;
			return properMetadataMinimumArgumentCount > 0 && (_metadataMinimumArguments == null || properMetadataMinimumArgumentCount < _metadataMinimumArguments)
				? properMetadataMinimumArgumentCount.Value
				: (_metadataMinimumArguments ?? 0);
		}

		private int MinimumParameterCount => RootParameters.Count(p => !p.IsOptional);

		public int MaximumArguments => _maximumArguments ?? (_maximumArguments = ResolveMaximumArguments()).Value;

		private int? _maximumArguments;

		private int ResolveMaximumArguments()
		{
			if (Type == ProgramType.Procedure)
			{
				return RootParameters.Count();
			}

			return Parameters.Count > 1 && _metadataMaximumArguments != 0 && Parameters[0].Direction == ParameterDirection.ReturnValue
					? RootParameters.Count() - 1
					: (_metadataMaximumArguments ?? 0);
		}

		private IEnumerable<OracleProgramParameterMetadata> RootParameters => Parameters.Where(p => p.DataLevel == 0);

		public bool IsPackageFunction => !String.IsNullOrEmpty(Identifier.Package);

	    public AuthId AuthId { get; private set; }

		public string DisplayType { get; private set; }
		
		public OracleSchemaObject Owner { get; set; }

		public string Documentation => _documentation ?? (_documentation = BuildDocumentation());

		private string BuildDocumentation()
		{
			if ((String.IsNullOrEmpty(Identifier.Owner) || String.Equals(Identifier.Package, OracleObjectIdentifier.PackageBuiltInFunction)) &&
			    Type != ProgramType.StatementFunction && OracleHelpProvider.SqlFunctionDocumentation[Identifier.Name].Any())
			{
				return OracleHelpProvider.GetBuiltInSqlFunctionDocumentation(Identifier.Name);
			}

			DocumentationPackageSubProgram documentation;
			return OracleHelpProvider.PackageProgramDocumentations.TryGetValue(Identifier, out documentation)
				? documentation.Description
				: String.Empty;
		}
	}

	[DebuggerDisplay("OracleProgramParameterMetadata (Name={Name}; Position={Position}; Sequence={Sequence}; DataLevel={DataLevel}; DataType={DataType}; CustomDataType={CustomDataType}; Direction={Direction}; IsOptional={IsOptional})")]
	public class OracleProgramParameterMetadata
	{
		internal OracleProgramParameterMetadata(string name, int position, int sequence, int dataLevel, ParameterDirection direction, string dataType, OracleObjectIdentifier customDataType, bool isOptional)
		{
			Name = name;
			Position = position;
			Sequence = sequence;
			DataLevel = dataLevel;
			DataType = dataType;
			CustomDataType = customDataType;
			Direction = direction;
			IsOptional = isOptional;
		}

		public string Name { get; private set; }

		public int Position { get; private set; }
		
		public int Sequence { get; private set; }
		
		public int DataLevel { get; private set; }

		public string DataType { get; private set; }
		
		public OracleObjectIdentifier CustomDataType { get; private set; }

		public ParameterDirection Direction { get; private set; }

		public bool IsOptional { get; private set; }

		public string FullDataTypeName => !CustomDataType.HasOwner ? DataType.Trim('"') : CustomDataType.ToString();
	}

	public enum ParameterDirection
	{
		Input,
		Output,
		InputOutput,
		ReturnValue,
	}

	public enum AuthId
	{
		CurrentUser,
		Definer
	}

	public enum ProgramType
	{
		Procedure,
		Function,
		StatementFunction,
		ObjectConstructor,
		CollectionConstructor,
		MemberFunction,
		PackageFunction,
		PackageProcedure
	}
}
