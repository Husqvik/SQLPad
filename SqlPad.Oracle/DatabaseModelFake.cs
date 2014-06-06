using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace SqlPad.Oracle
{
	public class DatabaseModelFake : IDatabaseModel
	{
		public static readonly DatabaseModelFake Instance = new DatabaseModelFake();
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));

		private const string CurrentSchemaInternal = "\"HUSQVIK\"";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK", "Oracle.DataAccess.Client");

		private static readonly HashSet<string> SchemasInternal = new HashSet<string> { "\"SYS\"", "\"SYSTEM\"", CurrentSchemaInternal, OracleDatabaseModel.SchemaPublic };
		private static readonly OracleFunctionMetadataCollection AllFunctionMetadataInterval;

		static DatabaseModelFake()
		{
			using (var reader = XmlReader.Create(Path.Combine(App.FolderNameApplication, "TestFunctionCollection.xml")))
			{
				AllFunctionMetadataInterval = (OracleFunctionMetadataCollection)Serializer.ReadObject(reader);
			}
		}

		public OracleFunctionMetadataCollection AllFunctionMetadata { get { return AllFunctionMetadataInterval; } }

		private static readonly HashSet<OracleDataObject> AllObjectsInternal = new HashSet<OracleDataObject>
		{
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create("\"SYS\"", "\"DUAL\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				             {
					             new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1, Unit = DataUnit.Byte }
				             }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create("\"SYS\"", "\"V_$SESSION\""),
				Organization = OrganizationType.NotApplicable,
				Type = "VIEW"
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OracleDatabaseModel.SchemaPublic, "\"V$SESSION\""),
				Organization = OrganizationType.NotApplicable,
				Type = "SYNONYM"
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OracleDatabaseModel.SchemaPublic, "\"DUAL\""),
				Organization = OrganizationType.Heap,
				Type = "SYNONYM",
				Columns = new HashSet<OracleColumn>
				             {
					             new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1, Unit = DataUnit.Byte }
				             }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"COUNTRY\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"ORDERS\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICES\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"DUEDATE\"", Type = "DATE" }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICELINES\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"INVOICE_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"AMOUNT\"", Type = "NUMBER", Precision = 20, Scale = 2 },
							  new OracleColumn { Name = "\"CORRELATION_VALUE\"", Type = "NUMBER", Scale = 5 }
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
				Organization = OrganizationType.NotApplicable,
				Type = "VIEW"
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte }
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
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleDataObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
							  new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte }
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
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
							  new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = true },
					          new OracleColumn { Name = "\"SELECTION_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false },
					          new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false },
							  new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Nullable = false, Unit = DataUnit.Byte }
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
			.Values.Where(o => o.Owner == OracleDatabaseModel.SchemaPublic || o.Owner == CurrentSchemaInternal)
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
	}

	public struct SchemaObjectResult
	{
		public static readonly SchemaObjectResult EmptyResult = new SchemaObjectResult();

		public bool SchemaFound { get; set; }

		public OracleDataObject SchemaObject { get; set; }
	}

	[DebuggerDisplay("OracleDataObject (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName}; Type={Type})")]
	public class OracleDataObject : OracleObject, IDatabaseObject
	{
		public OracleDataObject()
		{
			Properties = new List<IDatabaseObjectProperty>();
			Columns = new List<OracleColumn>();
			ForeignKeys = new List<OracleForeignKeyConstraint>();
		}

		public string Name { get { return FullyQualifiedName.NormalizedName; } }

		public string Owner { get { return FullyQualifiedName.NormalizedOwner; } }

		public ICollection<IDatabaseObjectProperty> Properties { get; set; }

		IEnumerable<IColumn> IDatabaseObject.Columns { get { return Columns; } }

		public ICollection<OracleColumn> Columns { get; set; }

		public ICollection<OracleForeignKeyConstraint> ForeignKeys { get; set; }

		public OrganizationType Organization { get; set; }

		public bool IsInternal { get; set; }

		public OracleColumn RowIdPseudoColumn
		{
			get
			{
				return Organization.In(OrganizationType.Heap, OrganizationType.Index)
					? new OracleColumn
					  {
						  Name = OracleColumn.RowId.ToQuotedIdentifier(),
						  Type = Organization == OrganizationType.Index ? "UROWID" : OracleColumn.RowId
					  }
					: null;
			}
		}
	}

	[DebuggerDisplay("OracleColumn (Name={Name}; Type={Type})")]
	public class OracleColumn : IColumn
	{
		public const string RowId = "ROWID";

		#region Implementation of IColumn
		public string Name { get; set; }

		public string FullTypeName
		{
			get
			{
				var name = Type;
				if (Unit != DataUnit.NotApplicable)
				{
					var unit = Unit == DataUnit.Byte ? "BYTE" : "CHAR";
					name = String.Format("{0}({1} {2})", name, Size, unit);
				}
				else
				{
					var decimalScale = Scale > 0 ? String.Format(", {0}", Scale) : null;
					if (Precision > 0 || Scale > 0)
					{
						name = String.Format("{0}({1}{2})", name, Precision == null ? "*" : Convert.ToString(Precision), decimalScale);
					}
				}

				return name;
			}
		}
		#endregion

		public string Type { get; set; }
		public int? Precision { get; set; }
		public int? Scale { get; set; }
		public int Size { get; set; }
		public bool Nullable { get; set; }
		public DataUnit Unit { get; set; }
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

	public enum OrganizationType
	{
		NotApplicable,
		Heap,
		Index,
		External
	}

	public enum DataUnit
	{
		NotApplicable,
		Byte,
		Character
	}
}
