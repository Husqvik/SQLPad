using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ToolTips;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.DatabaseConnection
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		public const string SchemaPublic = "\"PUBLIC\"";
		public const string SchemaSys = "\"SYS\"";
		public const string SchemaSystem = "\"SYSTEM\"";
		public const string PackageBuiltInFunction = "\"STANDARD\"";
		public const string PackageDbmsStandard = "\"DBMS_STANDARD\"";
		public const string PackageDbmsRandom = "\"DBMS_RANDOM\"";
		public const string PackageDbmsCrypto = "\"DBMS_CRYPTO\"";
		public const string SystemParameterNameMaxStringSize = "max_string_size";
		public const string BuiltInDataTypeIntervalYearToMonth = "INTERVAL YEAR TO MONTH";
		public const string BuiltInDataTypeIntervalDayToSecond = "INTERVAL DAY TO SECOND";
		public const string BuiltInDataTypeTimestampWithTimeZone = "TIMESTAMP WITH TIME ZONE";
		public const string BuiltInDataTypeTimestampWithLocalTimeZone = "TIMESTAMP WITH LOCAL TIME ZONE";
		public const int VersionMajorOracle12C = 12;
		public const int DefaultMaxLengthVarchar = 4000;
		public const int DefaultMaxLengthNVarchar = 2000;
		public const int DefaultMaxLengthRaw = 2000;

		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramCast = OracleProgramIdentifier.CreateFromValues(null, null, "CAST");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramExtract = OracleProgramIdentifier.CreateFromValues(null, null, "EXTRACT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramLnNvl = OracleProgramIdentifier.CreateFromValues(null, null, "LNNVL");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRatioToReport = OracleProgramIdentifier.CreateFromValues(null, null, "RATIO_TO_REPORT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramSysConnectByPath = OracleProgramIdentifier.CreateFromValues(null, null, "SYS_CONNECT_BY_PATH");

		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRound = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "ROUND");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramLevel = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "LEVEL");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRowNum = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "ROWNUM");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramNextDay = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "NEXT_DAY");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramSysContext = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "SYS_CONTEXT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToChar = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_CHAR");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToDate = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_DATE");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToTimestamp = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_TIMESTAMP");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToTimestampWithTimeZone = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_TIMESTAMP_TZ");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramTrunc = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TRUNC");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramConvert = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "CONVERT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramNumberToYearToMonthInterval = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "NUMTOYMINTERVAL");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramNumberToDayToSecondInterval = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "NUMTODSINTERVAL");
		internal static readonly OracleProgramIdentifier IdentifierDbmsRandomString = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageDbmsRandom, "STRING");
		internal static readonly OracleProgramIdentifier IdentifierDbmsCryptoHash = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageDbmsCrypto, "HASH");
		
		internal static readonly OracleObjectIdentifier BuiltInFunctionPackageIdentifier = OracleObjectIdentifier.Create(SchemaSys, PackageBuiltInFunction);
		internal static readonly OracleObjectIdentifier IdentifierDbmsStandard = OracleObjectIdentifier.Create(SchemaSys, PackageDbmsStandard);

		internal static readonly ICollection<string> BuiltInDataTypes =
			new HashSet<string>
			{
				TerminalValues.BinaryDouble,
				TerminalValues.BinaryFloat,
				TerminalValues.Blob,
				TerminalValues.Char,
				TerminalValues.NChar,
				TerminalValues.Clob,
				"NCLOB",
				TerminalValues.Date,
				TerminalValues.Decimal,
				"DOUBLE PRECISION",
				TerminalValues.Float,
				TerminalValues.Integer,
				BuiltInDataTypeIntervalDayToSecond,
				BuiltInDataTypeIntervalYearToMonth,
				TerminalValues.Number,
				TerminalValues.Raw,
				"REAL",
				TerminalValues.Smallint,
				TerminalValues.Table,
				TerminalValues.Timestamp,
				BuiltInDataTypeTimestampWithLocalTimeZone,
				BuiltInDataTypeTimestampWithTimeZone,
				TerminalValues.UniversalRowId,
				TerminalValues.Varchar2,
				TerminalValues.NVarchar2,
			};

		public abstract ConnectionStringSettings ConnectionString { get; }
		
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

		public abstract event EventHandler<DatabaseModelPasswordArgs> PasswordRequired;

		public abstract event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public abstract event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;

		public abstract event EventHandler RefreshStarted;

		public abstract event EventHandler<DatabaseModelRefreshStatusChangedArgs> RefreshStatusChanged;

		public abstract event EventHandler RefreshCompleted;

		public abstract string DatabaseDomainName { get; }

		public abstract IConnectionAdapter CreateConnectionAdapter();

		public abstract Task<ExecutionPlanItemCollection> ExplainPlanAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true);

		public abstract Task UpdatePartitionDetailsAsync(PartitionDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateSubPartitionDetailsAsync(SubPartitionDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateTableDetailsAsync(OracleObjectIdentifier schemaObject, TableDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateViewDetailsAsync(OracleObjectIdentifier objectIdentifier, ViewDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<string>> GetRemoteTableColumnsAsync(string databaseLink, OracleObjectIdentifier schemaObject, CancellationToken cancellationToken);
		
		public abstract Task UpdateColumnDetailsAsync(OracleObjectIdentifier schemaObject, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateUserDetailsAsync(OracleSchemaModel dataModel, CancellationToken cancellationToken);

		public abstract Task<ILookup<string, string>> GetContextData(CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<string>> GetWeekdayNames(CancellationToken cancellationToken);

		public abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllFunctionMetadata { get; }

		public abstract ILookup<OracleObjectIdentifier, OracleReferenceConstraint> UniqueConstraintReferringReferenceConstraints { get; }

		protected abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata { get; }
		
		protected abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageFunctionMetadata { get; }

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
					OracleObjectIdentifier.Create(SchemaPublic, name)
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
				var programIdentifier =
					new OracleProgramIdentifier
					{
						Owner = BuiltInFunctionPackageIdentifier.Owner,
						Package = BuiltInFunctionPackageIdentifier.Name,
						Name = identifier.Name
					};

				if (AllObjects.TryGetValue(BuiltInFunctionPackageIdentifier, out schemaObject))
				{
					programMetadataSource = BuiltInPackageFunctionMetadata[programIdentifier];
				}

				result.Metadata = TryFindProgramOverload(programMetadataSource, identifier.Name, parameterCount, hasAnalyticClause, !forceBuiltInFunction && includePlSqlObjects);

				if (result.Metadata == null)
				{
					if (includePlSqlObjects && AllObjects.TryGetValue(IdentifierDbmsStandard, out schemaObject))
					{
						programIdentifier.Package = PackageDbmsStandard;
						programMetadataSource = BuiltInPackageFunctionMetadata[programIdentifier];
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
					return result;

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
			var functionContainer = targetObject as IFunctionCollection;
			if (functionContainer != null)
			{
				functionMetadata = functionContainer.Functions;
				return true;
			}

			functionMetadata = new OracleProgramMetadata[0];
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
