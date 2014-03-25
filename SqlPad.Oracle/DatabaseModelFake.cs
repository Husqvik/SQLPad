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

		private static readonly HashSet<OracleDataObject> AllObjectsInternal = new HashSet<OracleDataObject>
		{
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create("\"SYS\"", "\"DUAL\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				             {
					             new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1 }
				             }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create("\"SYS\"", "\"V_$SESSION\""),
				Type = "VIEW"
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "\"V$SESSION\""),
				Type = "SYNONYM"
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "\"DUAL\""),
				Type = "SYNONYM",
				Columns = new HashSet<OracleColumn>
				             {
					             new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1 }
				             }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"COUNTRY\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"ORDERS\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICES\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICELINES\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"INVOICE_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          },
						  ForeignKeys = new List<OracleForeignKeyConstraint>
				              {
								  new OracleForeignKeyConstraint
					              {
									  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_INVOICELINES_INVOICES\""),
									  SourceColumns = new []{ "\"INVOICE_ID\"" },
									  TargetColumns = new []{ "\"ID\"" },
									  SourceObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICELINES\""),
									  TargetObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICES\"")
					              }
				              }.AsReadOnly()
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"VIEW_INSTANTSEARCH\""),
				Type = "VIEW"
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 }
				          },
						  ForeignKeys = new List<OracleForeignKeyConstraint>
				              {
								  new OracleForeignKeyConstraint
					              {
									  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_TARGETGROUP_PROJECT\""),
									  SourceColumns = new []{ "\"PROJECT_ID\"" },
									  TargetColumns = new []{ "\"PROJECT_ID\"" },
									  SourceObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\""),
									  TargetObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\"")
					              }
				              }.AsReadOnly()
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
							  new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50 }
				          },
						  ForeignKeys = new List<OracleForeignKeyConstraint>
				              {
					              new OracleForeignKeyConstraint
					              {
									  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_RESPONDENTBUCKET_TARGETGROUP\""),
									  SourceColumns = new []{ "\"TARGETGROUP_ID\"" },
									  TargetColumns = new []{ "\"TARGETGROUP_ID\"" },
									  SourceObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\""),
									  TargetObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\"")
					              },
								  new OracleForeignKeyConstraint
					              {
									  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_RESPONDENTBUCKET_PROJECT\""),
									  SourceColumns = new []{ "\"PROJECT_ID\"" },
									  TargetColumns = new []{ "\"PROJECT_ID\"" },
									  SourceObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\""),
									  TargetObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\"")
					              }
				              }.AsReadOnly()
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"SELECTION\""),
				Type = "TABLE",
				Columns = new HashSet<OracleColumn>
				          {
							  new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = true },
					          new OracleColumn { Name = "\"SELECTION_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Nullable = false }
				          },
				ForeignKeys = new List<OracleForeignKeyConstraint>
				              {
					              new OracleForeignKeyConstraint
					              {
									  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_SELECTION_RESPONDENTBUCKET\""),
									  SourceColumns = new []{ "\"RESPONDENTBUCKET_ID\"" },
									  TargetColumns = new []{ "\"RESPONDENTBUCKET_ID\"" },
									  SourceObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"SELECTION\""),
									  TargetObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\"")
					              },
								  new OracleForeignKeyConstraint
					              {
									  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_SELECTION_PROJECT\""),
									  SourceColumns = new []{ "\"PROJECT_ID\"" },
									  TargetColumns = new []{ "\"PROJECT_ID\"" },
									  SourceObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"SELECTION\""),
									  TargetObject = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\"")
					              }
				              }.AsReadOnly()
			},
		};

		private static readonly IDictionary<IObjectIdentifier, IDatabaseObject> AllObjectDictionary = AllObjectsInternal.ToDictionary(o => (IObjectIdentifier)o.FullyQualifiedName, o => (IDatabaseObject)o);

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

		private OracleDataObject GetObjectBehindSynonym(OracleDataObject synonym)
		{
			return null;
		}

		public SchemaObjectResult GetObject(OracleObjectIdentifier objectIdentifier)
		{
			OracleDataObject schemaObject = null;
			var schemaFound = false;

			if (String.IsNullOrEmpty(objectIdentifier.NormalizedOwner))
			{
				var currentSchemaObject = OracleObjectIdentifier.Create(CurrentSchema, objectIdentifier.NormalizedName);
				var publicSchemaObject = OracleObjectIdentifier.Create(SchemaPublic, objectIdentifier.NormalizedName);

				if (AllObjectDictionary.ContainsKey(currentSchemaObject))
					schemaObject = (OracleDataObject)AllObjectDictionary[currentSchemaObject];
				else if (AllObjectDictionary.ContainsKey(publicSchemaObject))
					schemaObject = (OracleDataObject)AllObjectDictionary[publicSchemaObject];
			}
			else
			{
				schemaFound = Schemas.Contains(objectIdentifier.NormalizedOwner);

				if (schemaFound && AllObjectDictionary.ContainsKey(objectIdentifier))
					schemaObject = (OracleDataObject)AllObjectDictionary[objectIdentifier];
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

		public OracleDataObject SchemaObject { get; set; }
	}

	[DebuggerDisplay("DebuggerDisplay (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName}; Type={Type})")]
	public class OracleDataObject : OracleObject, IDatabaseObject
	{
		public OracleDataObject()
		{
			Properties = new List<IDatabaseObjectProperty>();
			Columns = new List<OracleColumn>();
			ForeignKeys = new List<OracleForeignKeyConstraint>();
		}

		#region Implementation of IDatabaseObject
		public string Name { get { return FullyQualifiedName.NormalizedName; } }
		public string Owner { get { return FullyQualifiedName.NormalizedOwner; } }
		#endregion

		#region Implementation of IDatabaseObject
		public ICollection<IDatabaseObjectProperty> Properties { get; set; }

		IEnumerable<IColumn> IDatabaseObject.Columns { get { return Columns; } }
		#endregion

		public ICollection<OracleColumn> Columns { get; set; }

		public ICollection<OracleForeignKeyConstraint> ForeignKeys { get; set; } 
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

	public abstract class OracleObject
	{
		public OracleObjectIdentifier FullyQualifiedName { get; set; }
		public string Type { get; set; }
	}

	[DebuggerDisplay("OracleForeignKeyConstraint (Name={FullyQualifiedName.Name}; Type={Type})")]
	public class OracleForeignKeyConstraint : OracleObject
	{
		public OracleObjectIdentifier TargetObject { get; set; }

		public OracleObjectIdentifier SourceObject { get; set; }
		
		public IList<string> SourceColumns { get; set; }

		public IList<string> TargetColumns { get; set; }
	}
}
