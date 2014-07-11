using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;

namespace SqlPad.Oracle
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		public const string SchemaPublic = "\"PUBLIC\"";
		public const string SchemaSys = "\"SYS\"";
		public const string SchemaSystem = "\"SYSTEM\"";

		private static readonly Type TypeDateTime = typeof(DateTime);

		public abstract ConnectionStringSettings ConnectionString { get; }
		
		public abstract string CurrentSchema { get; set; }
		
		public abstract ICollection<string> Schemas { get; }

		public abstract ICollection<string> AllSchemas { get; }

		public virtual void Dispose() { }

		public abstract void RefreshIfNeeded();
		
		public abstract void Refresh();

		public abstract event EventHandler RefreshStarted;

		public abstract event EventHandler RefreshFinished;

		public abstract int ExecuteStatement(string statementText, bool returnDataset);

		public abstract IEnumerable<object[]> FetchRecords(int rowCount);

		public abstract ICollection<ColumnHeader> GetColumnHeaders();

		public abstract string GetObjectScript(OracleSchemaObject schemaObject);

		public abstract bool CanExecute { get; }
		
		public abstract bool CanFetch { get; }

		public abstract bool IsExecuting { get; }

		public abstract OracleFunctionMetadataCollection AllFunctionMetadata { get; }

		protected abstract ILookup<string, OracleFunctionMetadata> NonPackageBuiltInFunctionMetadata { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

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
				if (AllObjects.TryGetValue(OracleObjectIdentifier.Create(SchemaSys, OracleFunctionMetadataCollection.PackageBuiltInFunction), out schemaObject))
				{
					TryGetSchemaObjectFunctionMetadata(schemaObject, out functionMetadataSource);
				}

				functionMetadataSource.AddRange(NonPackageBuiltInFunctionMetadata[identifier.Name]);

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
			var functions = schemaObject as IFunctionCollection;
			if (functions != null)
			{
				functionMetadata = functions.Functions;
				return true;
			}

			var synonym = schemaObject as OracleSynonym;
			if (synonym != null)
			{
				return TryGetSchemaObjectFunctionMetadata(synonym.SchemaObject, out functionMetadata);
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
