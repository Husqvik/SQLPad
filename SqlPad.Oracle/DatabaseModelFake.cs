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
				                       Type = OracleDatabaseModel.DataObjectTypeSynonym,
									   SchemaObject = AllObjectsInternal.Single(o => o.Name == tableNameDual && o.Owner == OwnerNameSys)
			                       });

			AllObjectsInternal.Add(new OracleSynonym
			                       {
				                       FullyQualifiedName = OracleObjectIdentifier.Create(OracleDatabaseModel.SchemaPublic, "\"V$SESSION\""),
				                       Type = OracleDatabaseModel.DataObjectTypeSynonym,
									   SchemaObject = AllObjectsInternal.Single(o => o.Name == "\"V_$SESSION\"" && o.Owner == OwnerNameSys)
			                       });

			AllObjectDictionary = AllObjectsInternal.ToDictionary(o => o.FullyQualifiedName, o => o);

			ObjectsInternal = AllObjectDictionary
				.Values.Where(o => o.Owner == OracleDatabaseModel.SchemaPublic || o.Owner == CurrentSchemaInternal)
				.ToDictionary(o => OracleObjectIdentifier.Create(o.Owner, o.Name), o => o);
		}

		public OracleFunctionMetadataCollection AllFunctionMetadata { get { return AllFunctionMetadataInterval; } }

		private static readonly HashSet<OracleObject> AllObjectsInternal = new HashSet<OracleObject>
		{
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, "\"DUAL\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				             {
					             new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1, Unit = DataUnit.Byte }
				             }
			},
			new OracleView
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, "\"V_$SESSION\""),
				Organization = OrganizationType.NotApplicable,
				Type = "VIEW"
			},
			new OracleTable
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
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"ORDERS\""),
				Type = OracleDatabaseModel.DataObjectTypeTable,
				Organization = OrganizationType.Heap,
				Columns = new HashSet<OracleColumn>
				          {
					          new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 }
				          }
			},
			new OracleTable
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
			new OracleTable
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
			new OracleView
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"VIEW_INSTANTSEARCH\""),
				Organization = OrganizationType.NotApplicable,
				Type = "VIEW"
			},
			new OracleTable
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
			new OracleTable
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
			new OracleTable
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
			new OracleTable
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

		private static readonly IDictionary<OracleObjectIdentifier, OracleObject> AllObjectDictionary;

		private static readonly IDictionary<OracleObjectIdentifier, OracleObject> ObjectsInternal;
		
		#region Implementation of IDatabaseModel
		public ConnectionStringSettings ConnectionString { get { return ConnectionStringInternal; } }
		
		public string CurrentSchema { get { return CurrentSchemaInternal; } }
		
		public ICollection<string> Schemas { get { return SchemasInternal; } }

		public IDictionary<OracleObjectIdentifier, OracleObject> Objects { get { return ObjectsInternal; } }

		public IDictionary<OracleObjectIdentifier, OracleObject> AllObjects { get { return AllObjectDictionary; } }
		
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
