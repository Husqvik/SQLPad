using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	public class DatabaseModelFake : IDatabaseModel
	{
		public static readonly DatabaseModelFake Instance = new DatabaseModelFake();

		private const string CurrentSchemaInternal = "\"HUSQVIK\"";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=MMA_DEV;PERSIST SECURITY INFO=True;USER ID=HUSQVIK", "Oracle.DataAccess.Client");
		public const string SchemaPublic = "\"PUBLIC\"";

		private static readonly HashSet<string> SchemasInternal = new HashSet<string> { "\"SYS\"", "\"SYSTEM\"", CurrentSchemaInternal, SchemaPublic };

		private static readonly HashSet<OracleDatabaseObject> AllObjectsInternal = new HashSet<OracleDatabaseObject>
		{
			new OracleDatabaseObject
			{
				Name = "\"DUAL\"",
				Owner = "\"SYS\"",
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				             {
					             new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1 }
				             }
			},
			new OracleDatabaseObject { Name = "\"V_$SESSION\"", Owner = "\"SYS\"", Type = "VIEW" },
			new OracleDatabaseObject { Name = "\"V$SESSION\"", Owner = SchemaPublic, Type = "SYNONYM" },
			new OracleDatabaseObject
			{
				Name = "\"DUAL\"",
				Owner = SchemaPublic,
				Type = "SYNONYM",
				Columns = new HashSet<OracleColumn>
				             {
					             new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1 }
				             }
			},
			new OracleDatabaseObject
			{
				Name = "\"COUNTRY\"",
				Owner = CurrentSchemaInternal,
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 }
				          }
			},
			new OracleDatabaseObject
			{
				Name = "\"ORDERS\"",
				Owner = CurrentSchemaInternal,
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleDatabaseObject { Name = "\"VIEW_INSTANTSEARCH\"", Owner = CurrentSchemaInternal, Type = "VIEW" },
			new OracleDatabaseObject
			{
				Name = "\"TARGETGROUP\"",
				Owner = CurrentSchemaInternal,
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 }
				          }
			},
			new OracleDatabaseObject
			{
				Name = "\"PROJECT\"",
				Owner = CurrentSchemaInternal,
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleDatabaseObject
			{
				Name = "\"RESPONDENTBUCKET\"",
				Owner = CurrentSchemaInternal,
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
							  new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 }
				          }
			},
			new OracleDatabaseObject
			{
				Name = "\"SELECTION\"",
				Owner = CurrentSchemaInternal,
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
							  new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"SELECTION_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 }
				          }
			},
		};

		private static readonly IDictionary<IObjectIdentifier, IDatabaseObject> AllObjectDictionary = AllObjectsInternal.ToDictionary(o => (IObjectIdentifier)OracleObjectIdentifier.Create(o.Owner, o.Name), o => (IDatabaseObject)o);

		private static readonly IDictionary<IObjectIdentifier, IDatabaseObject> ObjectsInternal = AllObjectDictionary
			.Values.Where(o => o.Owner == SchemaPublic || o.Owner == CurrentSchemaInternal)
			.ToDictionary(o => (IObjectIdentifier)OracleObjectIdentifier.Create(o.Owner, o.Name), o => o);
		
		#region Implementation of IDatabaseModel
		public ConnectionStringSettings ConnectionString { get { return ConnectionStringInternal; } }
		
		public string CurrentSchema { get { return CurrentSchemaInternal; } }
		
		public ICollection<string> Schemas { get { return SchemasInternal; } }

		public IDictionary<IObjectIdentifier, IDatabaseObject> Objects { get { return ObjectsInternal; } }

		public IDictionary<IObjectIdentifier, IDatabaseObject> AllObjects { get { return AllObjectDictionary; } }
		
		public void Refresh()
		{
		}
		#endregion

		private OracleDatabaseObject GetObjectBehindSynonym(OracleDatabaseObject synonym)
		{
			return null;
		}

		public SchemaObjectResult GetObject(OracleObjectIdentifier objectIdentifier)
		{
			OracleDatabaseObject schemaObject = null;
			var schemaFound = false;

			if (String.IsNullOrEmpty(objectIdentifier.NormalizedOwner))
			{
				var currentSchemaObject = OracleObjectIdentifier.Create(CurrentSchema, objectIdentifier.NormalizedName);
				var publicSchemaObject = OracleObjectIdentifier.Create(SchemaPublic, objectIdentifier.NormalizedName);

				if (AllObjectDictionary.ContainsKey(currentSchemaObject))
					schemaObject = (OracleDatabaseObject)AllObjectDictionary[currentSchemaObject];
				else if (AllObjectDictionary.ContainsKey(publicSchemaObject))
					schemaObject = (OracleDatabaseObject)AllObjectDictionary[publicSchemaObject];
			}
			else
			{
				schemaFound = Schemas.Contains(objectIdentifier.NormalizedOwner);

				if (schemaFound && AllObjectDictionary.ContainsKey(objectIdentifier))
					schemaObject = (OracleDatabaseObject)AllObjectDictionary[objectIdentifier];
			}

			return new SchemaObjectResult
			       {
					   SchemaFound = schemaFound,
					   SchemaObject = schemaObject
			       };
		}
	}

	public struct SchemaObjectResult
	{
		public static readonly SchemaObjectResult EmptyResult = new SchemaObjectResult();

		public bool SchemaFound { get; set; }

		public OracleDatabaseObject SchemaObject { get; set; }
	}

	[DebuggerDisplay("DebuggerDisplay (Owner={Owner}; Name={Name}; Type={Type})")]
	public class OracleDatabaseObject : IDatabaseObject
	{
		public OracleDatabaseObject()
		{
			Properties = new List<IDatabaseObjectProperty>();
			Columns = new List<OracleColumn>();
		}

		#region Implementation of IDatabaseObject
		public string Name { get; set; }
		public string Type { get; set; }
		public string Owner { get; set; }
		public ICollection<IDatabaseObjectProperty> Properties { get; set; }

		IEnumerable<IColumn> IDatabaseObject.Columns { get { return Columns; } }
		#endregion

		public ICollection<OracleColumn> Columns { get; set; }
	}

	[DebuggerDisplay("OracleColumn (Name={Name}; Type={Type})")]
	public class OracleColumn : IColumn
	{
		#region Implementation of IColumn
		public string Name { get; set; }
		public string FullTypeName { get { return Type; } }
		#endregion

		public string Type { get; set; }
		public int Precision { get; set; }
		public int Scale { get; set; }
		public int Size { get; set; }
		public bool Nullable { get; set; }
	}
}
