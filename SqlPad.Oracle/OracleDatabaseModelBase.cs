using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace SqlPad.Oracle
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		public const string SchemaPublic = "\"PUBLIC\"";
		public const string SchemaSys = "\"SYS\"";
		public const string SchemaSystem = "\"SYSTEM\"";

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

		public abstract bool CanExecute { get; }
		
		public abstract bool CanFetch { get; }

		public abstract bool IsExecuting { get; }

		public abstract OracleFunctionMetadataCollection AllFunctionMetadata { get; }

		protected abstract OracleFunctionMetadataCollection NonPackageBuiltInFunctionMetadata { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

		public SchemaObjectResult<TObject> GetObject<TObject>(OracleObjectIdentifier objectIdentifier) where TObject : OracleSchemaObject
		{
			OracleSchemaObject schemaObject = null;
			var schemaFound = false;

			if (String.IsNullOrEmpty(objectIdentifier.NormalizedOwner))
			{
				var currentSchemaObject = OracleObjectIdentifier.Create(CurrentSchema, objectIdentifier.NormalizedName);
				var publicSchemaObject = OracleObjectIdentifier.Create(SchemaPublic, objectIdentifier.NormalizedName);

				AllObjects.TryGetFirstValue(out schemaObject, currentSchemaObject, publicSchemaObject);
			}
			else
			{
				schemaFound = AllSchemas.Contains(objectIdentifier.NormalizedOwner);

				if (schemaFound)
				{
					AllObjects.TryGetValue(objectIdentifier, out schemaObject);
				}
			}

			var synonym = schemaObject as OracleSynonym;
			var fullyQualifiedName = OracleObjectIdentifier.Empty;
			
			if (synonym != null)
			{
				schemaObject = synonym.SchemaObject;
				fullyQualifiedName = synonym.FullyQualifiedName;
			}
			else if (schemaObject != null)
			{
				fullyQualifiedName = schemaObject.FullyQualifiedName;
			}

			var typedObject = schemaObject as TObject;
			
			return new SchemaObjectResult<TObject>
			{
				SchemaFound = schemaFound,
				SchemaObject = typedObject,
				Synonym = typedObject == null ? null : synonym,
				FullyQualifiedName = fullyQualifiedName
			};
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
				
				functionMetadataSource.AddRange(NonPackageBuiltInFunctionMetadata.SqlFunctions);

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

		internal static object ValueConverterFunction(ColumnHeader columnHeader, object value)
		{
			return columnHeader.DatabaseDataType == "Raw"
				? ((byte[])value).ToHexString()
				: value;
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

	public struct SchemaObjectResult<TObject> where TObject : OracleObject
	{
		public static readonly SchemaObjectResult<TObject> EmptyResult = new SchemaObjectResult<TObject>();

		public bool SchemaFound { get; set; }

		public TObject SchemaObject { get; set; }

		public OracleSynonym Synonym { get; set; }

		public OracleObjectIdentifier FullyQualifiedName { get; set; }
	}

	public struct FunctionMetadataResult
	{
		public OracleFunctionMetadata Metadata { get; set; }

		public OracleSchemaObject SchemaObject { get; set; }
	}
}
