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
		private const string OwnerNameSys = "\"SYS\"";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK", "Oracle.DataAccess.Client");

		private static readonly HashSet<string> SchemasInternal = new HashSet<string> { OwnerNameSys, "\"SYSTEM\"", CurrentSchemaInternal, OracleDatabaseModel.SchemaPublic };
		private static readonly OracleFunctionMetadataCollection AllFunctionMetadataInterval;

		static DatabaseModelFake()
		{
			using (var reader = XmlReader.Create(Path.Combine(App.FolderNameApplication, "TestFunctionCollection.xml")))
			{
				AllFunctionMetadataInterval = (OracleFunctionMetadataCollection)Serializer.ReadObject(reader);
			}

			const string tableNameDual = "\"DUAL\"";
			AllObjectsInternal.Add(new OracleSynonym
			                       {
				                       FullyQualifiedName = OracleObjectIdentifier.Create(OracleDatabaseModel.SchemaPublic, tableNameDual),
									   SchemaObject = AllObjectsInternal.Single(o => o.Name == tableNameDual && o.Owner == OwnerNameSys)
			                       });

			AllObjectsInternal.Add(new OracleSynonym
			                       {
				                       FullyQualifiedName = OracleObjectIdentifier.Create(OracleDatabaseModel.SchemaPublic, "\"V$SESSION\""),
									   SchemaObject = AllObjectsInternal.Single(o => o.Name == "\"V_$SESSION\"" && o.Owner == OwnerNameSys)
			                       });

			AllObjectDictionary = AllObjectsInternal.ToDictionary(o => o.FullyQualifiedName, o => o);

			ObjectsInternal = AllObjectDictionary
				.Values.Where(o => o.Owner == OracleDatabaseModel.SchemaPublic || o.Owner == CurrentSchemaInternal)
				.ToDictionary(o => OracleObjectIdentifier.Create(o.Owner, o.Name), o => o);

			AddConstraints();
		}

		private static void AddConstraints()
		{
			var sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\"")];
			sourceObject.ForeignKeys =
				new List<OracleForeignKeyConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_TARGETGROUP_PROJECT\""),
						SourceColumns = new[] { "\"PROJECT_ID\"" },
						TargetColumns = new[] { "\"PROJECT_ID\"" },
						SourceObject = sourceObject,
						TargetObject = AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\"")]
					}
				}.AsReadOnly();

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICELINES\"")];
			sourceObject.ForeignKeys =
				new List<OracleForeignKeyConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_INVOICELINES_INVOICES\""),
						SourceColumns = new[] { "\"INVOICE_ID\"" },
						TargetColumns = new[] { "\"ID\"" },
						SourceObject = sourceObject,
						TargetObject = AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICES\"")]
					}
				}.AsReadOnly();

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\"")];
			sourceObject.ForeignKeys
				= new List<OracleForeignKeyConstraint>
				  {
					  new OracleForeignKeyConstraint
					  {
						  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_RESPONDENTBUCKET_TARGETGROUP\""),
						  SourceColumns = new[] { "\"TARGETGROUP_ID\"" },
						  TargetColumns = new[] { "\"TARGETGROUP_ID\"" },
						  SourceObject = sourceObject,
						  TargetObject = AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\"")]
					  },
					  new OracleForeignKeyConstraint
					  {
						  FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_RESPONDENTBUCKET_PROJECT\""),
						  SourceColumns = new[] { "\"PROJECT_ID\"" },
						  TargetColumns = new[] { "\"PROJECT_ID\"" },
						  SourceObject = sourceObject,
						  TargetObject = AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\"")]
					  }
				  }.AsReadOnly();

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"SELECTION\"")];
			sourceObject.ForeignKeys = new List<OracleForeignKeyConstraint>
			                           {
				                           new OracleForeignKeyConstraint
				                           {
					                           FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_SELECTION_RESPONDENTBUCKET\""),
					                           SourceColumns = new[] { "\"RESPONDENTBUCKET_ID\"" },
					                           TargetColumns = new[] { "\"RESPONDENTBUCKET_ID\"" },
					                           SourceObject = sourceObject,
					                           TargetObject = AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\"")]
				                           },
				                           new OracleForeignKeyConstraint
				                           {
					                           FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_SELECTION_PROJECT\""),
					                           SourceColumns = new[] { "\"PROJECT_ID\"" },
					                           TargetColumns = new[] { "\"PROJECT_ID\"" },
					                           SourceObject = sourceObject,
					                           TargetObject = AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\"")]
				                           }
			                           }.AsReadOnly();
		}

		public OracleFunctionMetadataCollection AllFunctionMetadata { get { return AllFunctionMetadataInterval; } }

		private static readonly HashSet<OracleSchemaObject> AllObjectsInternal = new HashSet<OracleSchemaObject>
		{
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, "\"DUAL\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				             {
					             { "\"DUMMY\"", new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1, Unit = DataUnit.Byte } }
				             }
			},
			new OracleView
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, "\"V_$SESSION\""),
				Organization = OrganizationType.NotApplicable,
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"COUNTRY\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"ID\"", new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"ORDERS\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"ID\"", new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICES\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"ID\"", new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
					          { "\"DUEDATE\"", new OracleColumn { Name = "\"DUEDATE\"", Type = "DATE" } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICELINES\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"ID\"", new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
					          { "\"INVOICE_ID\"", new OracleColumn { Name = "\"INVOICE_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
							  { "\"AMOUNT\"", new OracleColumn { Name = "\"AMOUNT\"", Type = "NUMBER", Precision = 20, Scale = 2 } },
							  { "\"CORRELATION_VALUE\"", new OracleColumn { Name = "\"CORRELATION_VALUE\"", Type = "NUMBER", Scale = 5 } }
				          }
			},
			new OracleView
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"VIEW_INSTANTSEARCH\""),
				Organization = OrganizationType.NotApplicable,
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"TARGETGROUP_ID\"", new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
					          { "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte } },
					          { "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
							  { "\"RESPONDENTBUCKET_ID\"", new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
					          { "\"TARGETGROUP_ID\"", new OracleColumn { Name = "\"TARGETGROUP_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
					          { "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Unit = DataUnit.Byte } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"SELECTION\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
							  { "\"RESPONDENTBUCKET_ID\"", new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = true } },
					          { "\"SELECTION_ID\"", new OracleColumn { Name = "\"SELECTION_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false } },
					          { "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false } },
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, Nullable = false, Unit = DataUnit.Byte } }
				          }
			},
		};

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjectDictionary;

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> ObjectsInternal;
		
		#region Implementation of IDatabaseModel
		public ConnectionStringSettings ConnectionString { get { return ConnectionStringInternal; } }
		
		public string CurrentSchema { get { return CurrentSchemaInternal; } }
		
		public ICollection<string> Schemas { get { return SchemasInternal; } }

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> Objects { get { return ObjectsInternal; } }

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return AllObjectDictionary; } }
		
		public void Refresh()
		{
		}
		#endregion
	}

	public struct SchemaObjectResult<TObject> where TObject : OracleObject
	{
		public static readonly SchemaObjectResult<TObject> EmptyResult = new SchemaObjectResult<TObject>();

		public bool SchemaFound { get; set; }

		public TObject SchemaObject { get; set; }

		public OracleSynonym Synonym { get; set; }
		
		public OracleObjectIdentifier FullyQualifiedName { get; set; }
	}
}
