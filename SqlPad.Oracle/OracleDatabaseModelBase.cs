using System;
using System.Collections.Generic;
using System.Configuration;

namespace SqlPad.Oracle
{
	public abstract class OracleDatabaseModelBase : IDatabaseModel
	{
		public const string SchemaPublic = "\"PUBLIC\"";
		public const string DataObjectTypeTable = "TABLE";
		public const string DataObjectTypeView = "VIEW";
		public const string DataObjectTypeSynonym = "SYNONYM";

		public abstract ConnectionStringSettings ConnectionString { get; }
		
		public abstract string CurrentSchema { get; }
		
		public abstract ICollection<string> Schemas { get; }
		
		public abstract void Refresh();

		public abstract OracleFunctionMetadataCollection AllFunctionMetadata { get; }

		public abstract IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

		public SchemaObjectResult<TObject> GetObject<TObject>(OracleObjectIdentifier objectIdentifier) where TObject : OracleSchemaObject
		{
			OracleSchemaObject schemaObject = null;
			var schemaFound = false;

			if (String.IsNullOrEmpty(objectIdentifier.NormalizedOwner))
			{
				var currentSchemaObject = OracleObjectIdentifier.Create(CurrentSchema, objectIdentifier.NormalizedName);
				var publicSchemaObject = OracleObjectIdentifier.Create(SchemaPublic, objectIdentifier.NormalizedName);

				if (!AllObjects.TryGetValue(currentSchemaObject, out schemaObject))
				{
					AllObjects.TryGetValue(publicSchemaObject, out schemaObject);
				}
			}
			else
			{
				schemaFound = Schemas.Contains(objectIdentifier.NormalizedOwner);

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
	}
}
