using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		public const string SchemaPublic = "\"PUBLIC\"";
		public const string SchemaSys = "\"SYS\"";
		public const string SchemaSystem = "\"SYSTEM\"";
		public const string PackageBuiltInFunction = "\"STANDARD\"";
		public const string SystemParameterNameDatabaseDomain = "db_domain";
		public const int VersionMajorOracle12c = 12;

		internal static OracleFunctionIdentifier IdentifierBuiltInFunctionRound = OracleFunctionIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "ROUND");
		internal static OracleFunctionIdentifier IdentifierBuiltInFunctionSysContext = OracleFunctionIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "SYS_CONTEXT");
		internal static OracleFunctionIdentifier IdentifierBuiltInFunctionToChar = OracleFunctionIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_CHAR");
		internal static OracleFunctionIdentifier IdentifierBuiltInFunctionTrunc = OracleFunctionIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TRUNC");
		internal static OracleFunctionIdentifier IdentifierBuiltInFunctionConvert = OracleFunctionIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "CONVERT");

		protected static readonly OracleObjectIdentifier BuiltInFunctionPackageIdentifier = OracleObjectIdentifier.Create(SchemaSys, PackageBuiltInFunction);

		public abstract ConnectionStringSettings ConnectionString { get; }
		
		public abstract bool IsInitialized { get; }
		
		public abstract string CurrentSchema { get; set; }
		
		public abstract ICollection<string> Schemas { get; }

		public abstract ICollection<string> AllSchemas { get; }

		public virtual void Dispose() { }

		public abstract void Initialize();

		public abstract bool IsModelFresh { get; }

		public abstract void RefreshIfNeeded();

		public abstract Task Refresh(bool force = false);

		public abstract event EventHandler Initialized;

		public abstract event EventHandler<DatabaseModelInitializationFailedArgs> InitializationFailed;

		public abstract event EventHandler RefreshStarted;

		public abstract event EventHandler RefreshFinished;

		public abstract string DatabaseDomainName { get; }

		public abstract bool HasActiveTransaction { get; }

		public abstract void CommitTransaction();

		public abstract void RollbackTransaction();

		public abstract StatementExecutionResult ExecuteStatement(StatementExecutionModel executionModel);

		public abstract Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract IEnumerable<object[]> FetchRecords(int rowCount);

		public abstract Task<ExplainPlanResult> ExplainPlanAsync(string statement, CancellationToken cancellationToken);

		public abstract Task<string> GetActualExecutionPlanAsync(CancellationToken cancellationToken);

		public abstract Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true);

		public abstract Task UpdateTableDetailsAsync(OracleObjectIdentifier schemaObject, TableDetailsModel dataModel, CancellationToken cancellationToken);
		
		public abstract Task UpdateColumnDetailsAsync(OracleObjectIdentifier schemaObject, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken);

		public abstract bool CanFetch { get; }

		public abstract bool IsExecuting { get; }

		public abstract ILookup<string, string> ContextData { get; }

		public abstract ILookup<OracleFunctionIdentifier, OracleFunctionMetadata> AllFunctionMetadata { get; }

		protected abstract IDictionary<string, OracleFunctionMetadata> NonSchemaBuiltInFunctionMetadata { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get; }

		public abstract ICollection<string> CharacterSets { get; }

		public abstract IDictionary<int, string> StatisticsKeys { get; }
		
		public abstract IDictionary<string, string> SystemParameters { get; }

		public abstract int VersionMajor { get; }
		
		public abstract string VersionString { get; }

		public abstract Task<ICollection<SessionExecutionStatisticsRecord>> GetExecutionStatisticsAsync(CancellationToken cancellationToken);

		public OracleObjectIdentifier[] GetPotentialSchemaObjectIdentifiers(OracleObjectIdentifier identifier)
		{
			return String.IsNullOrEmpty(identifier.Owner)
				? GetCurrentAndPublicSchemaIdentifiers(identifier.NormalizedName)
				: new[] { identifier };
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

		public FunctionMetadataResult GetFunctionMetadata(OracleFunctionIdentifier identifier, int parameterCount, bool forceBuiltInFunction)
		{
			OracleSchemaObject schemaObject;
			ICollection<OracleFunctionMetadata> functionMetadataSource = new List<OracleFunctionMetadata>();
			if (String.IsNullOrEmpty(identifier.Package) && (forceBuiltInFunction || String.IsNullOrEmpty(identifier.Owner)))
			{
				if (AllObjects.TryGetValue(BuiltInFunctionPackageIdentifier, out schemaObject))
				{
					TryGetSchemaObjectFunctionMetadata(schemaObject, out functionMetadataSource);
				}

				var metadata = TryFindFunctionOverload(functionMetadataSource, identifier.Name, parameterCount);

				if (metadata == null)
				{
					OracleFunctionMetadata nonSchemaFunctionMetadata;
					if (NonSchemaBuiltInFunctionMetadata.TryGetValue(identifier.Name, out nonSchemaFunctionMetadata))
					{
						metadata = TryFindFunctionOverload(Enumerable.Repeat(nonSchemaFunctionMetadata, 1), identifier.Name, parameterCount);
					}
				}

				if (metadata != null)
					return new FunctionMetadataResult { Metadata = metadata, SchemaObject = schemaObject };
			}

			var result = new FunctionMetadataResult();
			var schemaObjectFound = (String.IsNullOrWhiteSpace(identifier.Package) && AllObjects.TryGetValue(OracleObjectIdentifier.Create(identifier.Owner, identifier.Name), out schemaObject)) ||
			                        AllObjects.TryGetValue(OracleObjectIdentifier.Create(identifier.Owner, identifier.Package), out schemaObject);
			if (!schemaObjectFound || !TryGetSchemaObjectFunctionMetadata(schemaObject, out functionMetadataSource))
				return result;

			result.SchemaObject = schemaObject;
			result.Metadata = TryFindFunctionOverload(functionMetadataSource, identifier.Name, parameterCount);

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

		private static bool TryGetSchemaObjectFunctionMetadata(OracleSchemaObject schemaObject, out ICollection<OracleFunctionMetadata> functionMetadata)
		{
			var targetObject = schemaObject.GetTargetSchemaObject();
			var functions = targetObject as IFunctionCollection;
			if (functions != null)
			{
				functionMetadata = functions.Functions;
				return true;
			}

			functionMetadata = new OracleFunctionMetadata[0];
			return false;
		}

		private static OracleFunctionMetadata TryFindFunctionOverload(IEnumerable<OracleFunctionMetadata> functionMetadataCollection, string normalizedName, int parameterCount)
		{
			return functionMetadataCollection.Where(m => m.Identifier.Name == normalizedName)
				.OrderBy(m => Math.Abs(parameterCount - m.Parameters.Count + 1))
				.FirstOrDefault();
		}
	}

	public struct FunctionMetadataResult
	{
		public OracleFunctionMetadata Metadata { get; set; }

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
