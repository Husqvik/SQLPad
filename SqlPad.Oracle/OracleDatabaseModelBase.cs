using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		private const string ColumnNameColumnValue = "\"COLUMN_VALUE\"";

		public const string SchemaPublic = "\"PUBLIC\"";
		public const string SchemaSys = "\"SYS\"";
		public const string SchemaSystem = "\"SYSTEM\"";
		public const string PackageBuiltInFunction = "\"STANDARD\"";
		public const string PackageDbmsRandom = "\"DBMS_RANDOM\"";
		public const string PackageDbmsCrypto = "\"DBMS_CRYPTO\"";
		public const string SystemParameterNameMaxStringSize = "max_string_size";
		public const int VersionMajorOracle12c = 12;

		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramRound = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "ROUND");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramLevel = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "LEVEL");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramSysContext = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "SYS_CONTEXT");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToChar = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_CHAR");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToDate = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_DATE");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToTimestamp = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_TIMESTAMP");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramToTimestampWithTimeZone = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_TIMESTAMP_TZ");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramTrunc = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TRUNC");
		internal static readonly OracleProgramIdentifier IdentifierBuiltInProgramConvert = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "CONVERT");
		internal static readonly OracleProgramIdentifier IdentifierDbmsRandomString = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageDbmsRandom, "STRING");
		internal static readonly OracleProgramIdentifier IdentifierDbmsCryptoHash = OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageDbmsCrypto, "HASH");
		
		internal static readonly OracleObjectIdentifier BuiltInFunctionPackageIdentifier = OracleObjectIdentifier.Create(SchemaSys, PackageBuiltInFunction);

		public abstract ConnectionStringSettings ConnectionString { get; }
		
		public abstract bool IsInitialized { get; }
		
		public abstract string CurrentSchema { get; set; }
		
		public abstract ICollection<string> Schemas { get; }

		public abstract ICollection<string> AllSchemas { get; }

		public virtual void Dispose() { }

		public abstract Task Initialize();

		public abstract bool IsFresh { get; }
		
		public abstract bool EnableDatabaseOutput { get; set; }

		public abstract void RefreshIfNeeded();

		public abstract Task Refresh(bool force = false);

		public abstract event EventHandler Initialized;

		public abstract event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public abstract event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;

		public abstract event EventHandler RefreshStarted;

		public abstract event EventHandler RefreshCompleted;

		public abstract string DatabaseDomainName { get; }

		public abstract bool HasActiveTransaction { get; }

		public abstract void CommitTransaction();

		public abstract Task RollbackTransaction();
		
		public abstract void CloseActiveReader();

		public abstract StatementExecutionResult ExecuteStatement(StatementExecutionModel executionModel);

		public abstract Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract IEnumerable<object[]> FetchRecords(int rowCount);

		public abstract Task<ExecutionPlanItemCollection> ExplainPlanAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract Task<ExecutionStatisticsPlanItemCollection> GetCursorExecutionStatisticsAsync(CancellationToken cancellationToken);

		public abstract Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true);

		public abstract Task UpdatePartitionDetailsAsync(PartitionDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateSubPartitionDetailsAsync(SubPartitionDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateTableDetailsAsync(OracleObjectIdentifier schemaObject, TableDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task UpdateViewDetailsAsync(OracleObjectIdentifier objectIdentifier, ViewDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract Task<IReadOnlyList<string>> GetRemoteTableColumnsAsync(string databaseLink, OracleObjectIdentifier schemaObject, CancellationToken cancellationToken);
		
		public abstract Task UpdateColumnDetailsAsync(OracleObjectIdentifier schemaObject, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract bool CanFetch { get; }

		public abstract bool IsExecuting { get; }

		public abstract ILookup<string, string> ContextData { get; }

		public abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllFunctionMetadata { get; }

		protected abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata { get; }
		
		protected abstract ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageFunctionMetadata { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get; }

		public abstract ICollection<string> CharacterSets { get; }

		public abstract IDictionary<int, string> StatisticsKeys { get; }
		
		public abstract IDictionary<string, string> SystemParameters { get; }

		public abstract int VersionMajor { get; }
		
		public abstract string VersionString { get; }

		public abstract Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		public static OracleColumn BuildColumnValueColumn(OracleDataType columnType)
		{
			var column =
				new OracleColumn
				{
					Name = ColumnNameColumnValue,
					DataType = columnType,
					Nullable = true
				};

			return column;
		}

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
			return AllSchemas.Contains(schemaName.ToQuotedIdentifier());
		}

		public ProgramMetadataResult GetProgramMetadata(OracleProgramIdentifier identifier, int parameterCount, bool forceBuiltInFunction, bool hasAnalyticClause)
		{
			var result = new ProgramMetadataResult();

			OracleSchemaObject schemaObject;
			IEnumerable<OracleProgramMetadata> programMetadataSource = new List<OracleProgramMetadata>();
			if (String.IsNullOrEmpty(identifier.Package) && (forceBuiltInFunction || String.IsNullOrEmpty(identifier.Owner)))
			{
				if (AllObjects.TryGetValue(BuiltInFunctionPackageIdentifier, out schemaObject))
				{
					var programIdentifier =
						new OracleProgramIdentifier
						{
							Owner = BuiltInFunctionPackageIdentifier.Owner,
							Package = BuiltInFunctionPackageIdentifier.Name,
							Name = identifier.Name
						};
					
					programMetadataSource = BuiltInPackageFunctionMetadata[programIdentifier];
				}

				result.Metadata = TryFindProgramOverload(programMetadataSource, identifier.Name, parameterCount, hasAnalyticClause);

				if (result.Metadata == null)
				{
					var nonSchemaBuiltInFunctionIdentifier = OracleProgramIdentifier.CreateBuiltIn(identifier.Name);
					result.Metadata = TryFindProgramOverload(NonSchemaBuiltInFunctionMetadata[nonSchemaBuiltInFunctionIdentifier], identifier.Name, parameterCount, hasAnalyticClause);
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
				result.Metadata = TryFindProgramOverload(programMetadataSource, identifier.Name, parameterCount, hasAnalyticClause);
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

		private static OracleProgramMetadata TryFindProgramOverload(IEnumerable<OracleProgramMetadata> functionMetadataCollection, string normalizedName, int parameterCount, bool hasAnalyticClause)
		{
			return functionMetadataCollection.Where(m => m != null && m.Type != ProgramType.Procedure && String.Equals(m.Identifier.Name, normalizedName))
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

	public class OracleColumnValueConverter : IColumnValueConverter
	{
		private readonly ColumnHeader _columnHeader;

		public OracleColumnValueConverter(ColumnHeader columnHeader)
		{
			_columnHeader = columnHeader;
		}

		public object ConvertToCellValue(object rawValue)
		{
			if (_columnHeader.DatabaseDataType == "Raw")
			{
				return ((byte[])rawValue).ToHexString();
			}

			return rawValue;
		}
	}
}
