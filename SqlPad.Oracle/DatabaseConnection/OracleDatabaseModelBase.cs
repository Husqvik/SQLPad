using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ToolTips;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.DatabaseConnection
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		public const string SystemParameterNameMaxStringSize = "max_string_size";
		public const string BuiltInDataTypeBFile = "BFILE";
		public const string BuiltInDataTypeInt = "INT";
		public const string BuiltInDataTypeReal = "REAL";
		public const string BuiltInDataTypeDoublePrecision = "DOUBLE PRECISION";
		public const string BuiltInDataTypeIntervalYearToMonth = "INTERVAL YEAR TO MONTH";
		public const string BuiltInDataTypeIntervalDayToSecond = "INTERVAL DAY TO SECOND";
		public const string BuiltInDataTypeTimestampWithTimeZone = "TIMESTAMP WITH TIME ZONE";
		public const string BuiltInDataTypeTimestampWithLocalTimeZone = "TIMESTAMP WITH LOCAL TIME ZONE";
		public const int VersionMajorOracle12C = 12;
		public const int DefaultMaxLengthVarchar = 4000;
		public const int DefaultMaxLengthNVarchar = 2000;
		public const int DefaultMaxLengthRaw = 2000;
		public const int PlSqlMaxLengthVarchar = 32767;
		public const int PlSqlMaxLengthNVarchar = 16383;
		public const int PlSqlMaxLengthRaw = 32767;

		private static readonly OracleProgramMetadata[] EmptyMetadataCollection = new OracleProgramMetadata[0];

		internal static readonly ICollection<string> BuiltInDataTypes =
			new HashSet<string>
			{
				BuiltInDataTypeBFile,
				TerminalValues.BinaryDouble,
				TerminalValues.BinaryFloat,
				TerminalValues.Blob,
				TerminalValues.Char,
				TerminalValues.NChar,
				TerminalValues.Clob,
				"NCLOB",
				TerminalValues.Date,
				TerminalValues.Decimal,
				BuiltInDataTypeDoublePrecision,
				TerminalValues.Float,
				BuiltInDataTypeInt,
				TerminalValues.Integer,
				BuiltInDataTypeIntervalDayToSecond,
				BuiltInDataTypeIntervalYearToMonth,
				TerminalValues.Number,
				TerminalValues.Raw,
				BuiltInDataTypeReal,
				TerminalValues.Smallint,
				TerminalValues.Table,
				TerminalValues.Timestamp,
				BuiltInDataTypeTimestampWithLocalTimeZone,
				BuiltInDataTypeTimestampWithTimeZone,
				TerminalValues.UniversalRowId,
				TerminalValues.Varchar2,
				TerminalValues.NVarchar2,
			};

		internal static readonly IReadOnlyCollection<string> BuiltInPlSqlDataTypes =
			new[]
			{
				TerminalValues.BinaryInteger,
				TerminalValues.PlsInteger,
				"BOOLEAN"
			};

		public abstract ConnectionStringSettings ConnectionString { get; }

		public abstract IOracleObjectScriptExtractor ObjectScriptExtractor { get; }
		
		public abstract bool IsInitialized { get; }
		
		public abstract bool IsMetadataAvailable { get; }
		
		public abstract bool HasDbaPrivilege { get; }
		
		public abstract string CurrentSchema { get; set; }
		
		public abstract ICollection<string> Schemas { get; }

		public abstract IReadOnlyDictionary<string, OracleSchema> AllSchemas { get; }

		public virtual void Dispose() { }

		public abstract Task Initialize();

		public abstract bool IsFresh { get; }

		public virtual void RefreshIfNeeded() { }

		public abstract Task Refresh(bool force = false);

		public abstract event EventHandler Initialized;

		public abstract event EventHandler CurrentSchemaChanged;

		public abstract event EventHandler<DatabaseModelPasswordArgs> PasswordRequired;

		public abstract event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public abstract event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;

		public abstract event EventHandler RefreshStarted;

		public abstract event EventHandler<DatabaseModelRefreshStatusChangedArgs> RefreshStatusChanged;

		public abstract event EventHandler RefreshCompleted;

		public abstract string DatabaseDomainName { get; }

		public abstract IConnectionAdapter CreateConnectionAdapter();

		public abstract Task UpdatePartitionDetailsAsync(PartitionDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateSubPartitionDetailsAsync(SubPartitionDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateTableDetailsAsync(OracleObjectIdentifier schemaObject, TableDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateViewDetailsAsync(OracleObjectIdentifier objectIdentifier, ObjectDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<string>> GetRemoteTableColumnsAsync(string databaseLink, OracleObjectIdentifier schemaObject, CancellationToken cancellationToken);
		
		public abstract Task UpdateColumnDetailsAsync(OracleObjectIdentifier schemaObject, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateUserDetailsAsync(OracleSchemaModel dataModel, CancellationToken cancellationToken);

		public abstract Task<ILookup<string, string>> GetContextData(CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<string>> GetWeekdayNames(CancellationToken cancellationToken);

		public abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllProgramMetadata { get; }

		public abstract ILookup<OracleObjectIdentifier, OracleReferenceConstraint> UniqueConstraintReferringReferenceConstraints { get; }

		protected abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata { get; }
		
		protected abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageProgramMetadata { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get; }

		public abstract IReadOnlyCollection<string> CharacterSets { get; }

		public abstract IDictionary<int, string> StatisticsKeys { get; }
		
		public abstract IDictionary<string, string> SystemParameters { get; }

		public abstract Version Version { get; }

		public OracleObjectIdentifier[] GetPotentialSchemaObjectIdentifiers(OracleObjectIdentifier identifier)
		{
			return identifier.HasOwner
				? new[] { identifier }
				: GetCurrentAndPublicSchemaIdentifiers(identifier.NormalizedName);
		}

		public OracleObjectIdentifier[] GetPotentialSchemaObjectIdentifiers(string owner, string name)
		{
			return String.IsNullOrEmpty(owner)
				? GetCurrentAndPublicSchemaIdentifiers(name)
				: new[] { OracleObjectIdentifier.Create(owner, name) };
		}

		private OracleObjectIdentifier[] GetCurrentAndPublicSchemaIdentifiers(string name)
		{
			return
				new[]
				{
					OracleObjectIdentifier.Create(CurrentSchema, name),
					OracleObjectIdentifier.Create(OracleObjectIdentifier.SchemaPublic, name)
				};
		}

		public bool ExistsSchema(string schemaName)
		{
			return AllSchemas.ContainsKey(schemaName.ToQuotedIdentifier());
		}

		public ProgramMetadataResult GetProgramMetadata(OracleProgramIdentifier identifier, int parameterCount, bool forceBuiltInFunction, bool hasAnalyticClause, bool includePlSqlObjects)
		{
			var result = new ProgramMetadataResult();

			OracleSchemaObject schemaObject;
			IEnumerable<OracleProgramMetadata> programMetadataSource = new List<OracleProgramMetadata>();
			if (String.IsNullOrEmpty(identifier.Package) && (forceBuiltInFunction || String.IsNullOrEmpty(identifier.Owner)))
			{
				var builtInProgramPackage = OracleObjectIdentifier.IdentifierBuiltInFunctionPackage;
				var programIdentifier = OracleProgramIdentifier.CreateFromValues(builtInProgramPackage.Owner, builtInProgramPackage.Name, identifier.Name);

				if (AllObjects.TryGetValue(builtInProgramPackage, out schemaObject))
				{
					programMetadataSource = BuiltInPackageProgramMetadata[programIdentifier];
				}

				result.Metadata = TryFindProgramOverload(programMetadataSource, identifier.Name, parameterCount, hasAnalyticClause, !forceBuiltInFunction && includePlSqlObjects);

				if (result.Metadata == null)
				{
					if (includePlSqlObjects && AllObjects.TryGetValue(OracleObjectIdentifier.IdentifierDbmsStandard, out schemaObject))
					{
						programIdentifier = OracleProgramIdentifier.CreateFromValues(builtInProgramPackage.Owner, OracleObjectIdentifier.PackageDbmsStandard, identifier.Name);
						programMetadataSource = BuiltInPackageProgramMetadata[programIdentifier];
						result.Metadata = TryFindProgramOverload(programMetadataSource, identifier.Name, parameterCount, hasAnalyticClause, true);
					}

					if (result.Metadata == null)
					{
						var nonSchemaBuiltInFunctionIdentifier = OracleProgramIdentifier.CreateBuiltIn(identifier.Name);
						result.Metadata = TryFindProgramOverload(NonSchemaBuiltInFunctionMetadata[nonSchemaBuiltInFunctionIdentifier], identifier.Name, parameterCount, hasAnalyticClause, includePlSqlObjects);
					}
				}

				result.SchemaObject = schemaObject;
			}

			if (result.Metadata == null)
			{
				var schemaObjectFound = (String.IsNullOrWhiteSpace(identifier.Package) && AllObjects.TryGetValue(OracleObjectIdentifier.Create(identifier.Owner, identifier.Name), out schemaObject)) ||
				                        AllObjects.TryGetValue(OracleObjectIdentifier.Create(identifier.Owner, identifier.Package), out schemaObject);
				if (!schemaObjectFound || !TryGetSchemaObjectProgramMetadata(schemaObject, out programMetadataSource))
				{
					return result;
				}

				result.SchemaObject = schemaObject;
				var programName = String.IsNullOrEmpty(identifier.Package) ? schemaObject.GetTargetSchemaObject().Name : identifier.Name;
				result.Metadata = TryFindProgramOverload(programMetadataSource, programName, parameterCount, hasAnalyticClause, includePlSqlObjects);
			}

			return result;
		}

		public OracleDatabaseLink GetFirstDatabaseLink(params OracleObjectIdentifier[] identifiers)
		{
			OracleDatabaseLink databaseLink;
			DatabaseLinks.TryGetFirstValue(out databaseLink, identifiers);

			if (databaseLink == null)
			{
				foreach (var link in DatabaseLinks.Values)
				{
					var databaseLinkNormalizedName = link.FullyQualifiedName.NormalizedName;
					var instanceQualifierIndex = databaseLinkNormalizedName.IndexOf("@", StringComparison.InvariantCulture);
					if (instanceQualifierIndex == -1)
					{
						continue;
					}

					var shortName = databaseLinkNormalizedName.Substring(1, instanceQualifierIndex - 1).ToQuotedIdentifier();
					var shortIdentifier = OracleObjectIdentifier.Create(link.FullyQualifiedName.Owner, shortName);
					if (identifiers.Any(i => i == shortIdentifier))
					{
						databaseLink = link;
						break;
					}
				}
			}

			return databaseLink;
		}

		public OracleSchemaObject GetFirstSchemaObject<T>(params OracleObjectIdentifier[] identifiers) where T : OracleSchemaObject
		{
			OracleSchemaObject schemaObject;
			AllObjects.TryGetFirstValue(out schemaObject, identifiers);
			var type = schemaObject.GetTargetSchemaObject() as T;
			return type == null
				? null
				: schemaObject;
		}

		private int _maximumVarcharLength;
		private int _maximumNVarcharLength;
		private int _maximumRawLength;

		public int MaximumVarcharLength
		{
			get
			{
				EnsureMaximumDataTypeLimits();
				return _maximumVarcharLength;
			}
		}

		public int MaximumNVarcharLength
		{
			get
			{
				EnsureMaximumDataTypeLimits();
				return _maximumNVarcharLength;
			}
		}

		public int MaximumRawLength
		{
			get
			{
				EnsureMaximumDataTypeLimits();
				return _maximumRawLength;
			}
		}

		private void EnsureMaximumDataTypeLimits()
		{
			if (_maximumVarcharLength != 0)
			{
				return;
			}

			string maxStringSize;
			if (!SystemParameters.TryGetValue(SystemParameterNameMaxStringSize, out maxStringSize) || String.Equals(maxStringSize, "STANDARD"))
			{
				_maximumVarcharLength = DefaultMaxLengthVarchar;
				_maximumNVarcharLength = _maximumRawLength = DefaultMaxLengthNVarchar;
			}
			else
			{
				_maximumVarcharLength = _maximumRawLength = 32767;
				_maximumNVarcharLength = 16383;
			}
		}

		protected static ILookup<OracleObjectIdentifier, OracleReferenceConstraint> BuildUniqueConstraintReferringReferenceConstraintLookup(IEnumerable<OracleSchemaObject> allObjects)
		{
			return allObjects
				.OfType<OracleDataObject>()
				.SelectMany(o => o.ReferenceConstraints)
				.ToLookup(c => c.ReferenceConstraint.FullyQualifiedName);
		}

		private static bool TryGetSchemaObjectProgramMetadata(OracleSchemaObject schemaObject, out IEnumerable<OracleProgramMetadata> functionMetadata)
		{
			var targetObject = schemaObject.GetTargetSchemaObject();
			var functionContainer = targetObject as IProgramCollection;
			if (functionContainer != null)
			{
				functionMetadata = functionContainer.Programs;
				return true;
			}

			functionMetadata = EmptyMetadataCollection;
			return false;
		}

		private static OracleProgramMetadata TryFindProgramOverload(IEnumerable<OracleProgramMetadata> functionMetadataCollection, string normalizedName, int parameterCount, bool hasAnalyticClause, bool includePlSqlObjects)
		{
			return functionMetadataCollection.Where(m => m != null && (includePlSqlObjects || m.Type != ProgramType.Procedure) && String.Equals(m.Identifier.Name, normalizedName))
				.OrderBy(m => Math.Abs(parameterCount - m.Parameters.Count + 1))
				.ThenBy(m => hasAnalyticClause ? !m.IsAnalytic : m.IsAnalytic)
				.FirstOrDefault();
		}
	}

	public struct ProgramMetadataResult
	{
		public OracleProgramMetadata Metadata { get; set; }

		public OracleSchemaObject SchemaObject { get; set; }
	}
}
