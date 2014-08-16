using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad.Oracle
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		public const string SchemaPublic = "\"PUBLIC\"";
		public const string SchemaSys = "\"SYS\"";
		public const string SchemaSystem = "\"SYSTEM\"";

		private static readonly Type TypeDateTime = typeof(DateTime);
		protected static readonly OracleObjectIdentifier BuiltInFunctionPackageIdentifier = OracleObjectIdentifier.Create(SchemaSys, OracleFunctionMetadataCollection.PackageBuiltInFunction);

		public abstract ConnectionStringSettings ConnectionString { get; }
		
		public abstract string CurrentSchema { get; set; }
		
		public abstract ICollection<string> Schemas { get; }

		public abstract ICollection<string> AllSchemas { get; }

		public virtual void Dispose() { }

		public abstract bool IsModelFresh { get; }

		public abstract void RefreshIfNeeded();

		public abstract Task Refresh(bool force = false);

		public abstract event EventHandler RefreshStarted;

		public abstract event EventHandler RefreshFinished;

		public abstract StatementExecutionResult ExecuteStatement(StatementExecutionModel executionModel);

		public abstract Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken);

		public abstract IEnumerable<object[]> FetchRecords(int rowCount);

		public abstract ICollection<ColumnHeader> GetColumnHeaders();

		public abstract Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true);

		public abstract bool CanFetch { get; }

		public abstract bool IsExecuting { get; }

		public abstract OracleFunctionMetadataCollection AllFunctionMetadata { get; }

		protected abstract ILookup<string, OracleFunctionMetadata> NonSchemaBuiltInFunctionMetadata { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get; }

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

				functionMetadataSource.AddRange(NonSchemaBuiltInFunctionMetadata[identifier.Name]);

				var metadata = TryFindFunctionOverload(functionMetadataSource, identifier.Name, parameterCount);
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

		internal static object ValueConverterFunction(ColumnHeader columnHeader, object value)
		{
			if (columnHeader.DatabaseDataType == "Raw")
			{
				return ((byte[])value).ToHexString();
			}

			if (columnHeader.DataType == TypeDateTime)
			{
				return ((DateTime)value).ToString(CultureInfo.CurrentUICulture);
			}
			
			return value;
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
}
