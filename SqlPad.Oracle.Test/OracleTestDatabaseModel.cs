using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace SqlPad.Oracle.Test
{
	public class OracleTestDatabaseModel : OracleDatabaseModelBase
	{
		public static readonly OracleTestDatabaseModel Instance = new OracleTestDatabaseModel();
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));

		private const string CurrentSchemaInternal = "\"HUSQVIK\"";
		private const string OwnerNameSys = "\"SYS\"";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK", "Oracle.DataAccess.Client");

		private static readonly HashSet<string> SchemasInternal = new HashSet<string> { OwnerNameSys, "\"SYSTEM\"", CurrentSchemaInternal };
		private static readonly HashSet<string> AllSchemasInternal = new HashSet<string>(SchemasInternal) { SchemaPublic };
		private static readonly OracleFunctionMetadataCollection AllFunctionMetadataInterval;

		static OracleTestDatabaseModel()
		{
			using (var builtInFunctionReader = XmlReader.Create(File.OpenRead(@"TestFiles\OracleSqlFunctionMetadataCollection_12_1_0_1_0.xml")))
			{
				var builtInFunctionMetadata = (OracleFunctionMetadataCollection)Serializer.ReadObject(builtInFunctionReader);

				using (var reader = XmlReader.Create(Path.Combine(App.FolderNameApplication, @"TestFiles\TestFunctionCollection.xml")))
				{
					AllFunctionMetadataInterval = new OracleFunctionMetadataCollection(builtInFunctionMetadata.SqlFunctions.Concat(((OracleFunctionMetadataCollection)Serializer.ReadObject(reader)).SqlFunctions));
				}
			}

			const string tableNameDual = "\"DUAL\"";
			AllObjectsInternal.Add(new OracleSynonym
			                       {
				                       FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, tableNameDual),
									   SchemaObject = AllObjectsInternal.Single(o => o.Name == tableNameDual && o.Owner == OwnerNameSys)
			                       });

			AllObjectsInternal.Add(new OracleSynonym
			                       {
				                       FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "\"V$SESSION\""),
									   SchemaObject = AllObjectsInternal.Single(o => o.Name == "\"V_$SESSION\"" && o.Owner == OwnerNameSys)
			                       });

			AllObjectDictionary = AllObjectsInternal.ToDictionary(o => o.FullyQualifiedName, o => o);

			ObjectsInternal = AllObjectDictionary
				.Values.Where(o => o.Owner == SchemaPublic || o.Owner == CurrentSchemaInternal)
				.ToDictionary(o => OracleObjectIdentifier.Create(o.Owner, o.Name), o => o);

			AddConstraints();
		}

		private static void AddConstraints()
		{
			var projectTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\"")];
			var projectPrimaryKey = new OraclePrimaryKeyConstraint
			                                 {
				                                 FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PK_PROJECT\""),
				                                 Columns = new[] { "\"PROJECT_ID\"" },
				                                 Owner = projectTable
			                                 };
			
			projectTable.Constraints = new List<OracleConstraint>{ projectPrimaryKey }.AsReadOnly();

			var sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_TARGETGROUP_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						Owner = sourceObject
					}
				};

			var invoiceTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICES\"")];
			var invoicePrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PK_INVOICES\""),
				Columns = new[] { "\"ID\"" },
				Owner = invoiceTable
			};

			invoiceTable.Constraints = new List<OracleConstraint> { invoicePrimaryKey }.AsReadOnly();

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"INVOICELINES\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_INVOICELINES_INVOICES\""),
						Columns = new[] { "\"INVOICE_ID\"" },
						ReferenceConstraint = invoicePrimaryKey,
						Owner = sourceObject,
						TargetObject = invoiceTable
					}
				}.AsReadOnly();

			var targetGroupTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"TARGETGROUP\"")];
			var targetGroupPrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PK_TARGETGROUP\""),
				Columns = new[] { "\"TARGETGROUP_ID\"" },
				Owner = targetGroupTable
			};

			targetGroupTable.Constraints.Add(targetGroupPrimaryKey);

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_RESPONDENTBUCKET_TARGETGROUP\""),
						Columns = new[] { "\"TARGETGROUP_ID\"" },
						ReferenceConstraint = targetGroupPrimaryKey,
						Owner = sourceObject,
						TargetObject = targetGroupTable
					},
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_RESPONDENTBUCKET_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						Owner = sourceObject,
						TargetObject = projectTable
					}
				};

			var respondentBucketTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"RESPONDENTBUCKET\"")];
			var respondentBucketPrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PK_RESPONDENTBUCKET\""),
				Columns = new[] { "\"RESPONDENTBUCKET_ID\"" },
				Owner = respondentBucketTable
			};

			respondentBucketTable.Constraints.Add(respondentBucketPrimaryKey);

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"SELECTION\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_SELECTION_RESPONDENTBUCKET\""),
						Columns = new[] { "\"RESPONDENTBUCKET_ID\"" },
						ReferenceConstraint = respondentBucketPrimaryKey,
						Owner = sourceObject,
						TargetObject = respondentBucketTable
					},
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"FK_SELECTION_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						Owner = sourceObject,
						TargetObject = projectTable
					}
				}.AsReadOnly();
		}

		public override OracleFunctionMetadataCollection AllFunctionMetadata { get { return AllFunctionMetadataInterval; } }

		private static readonly HashSet<OracleSchemaObject> AllObjectsInternal = new HashSet<OracleSchemaObject>
		{
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, "\"DUAL\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				             {
					             { "\"DUMMY\"", new OracleColumn { Name = "\"DUMMY\"", Type = "VARCHAR2", Size = 1, CharacterSize = 1, Unit = DataUnit.Byte } }
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
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Unit = DataUnit.Byte } }
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
							  { "\"CORRELATION_VALUE\"", new OracleColumn { Name = "\"CORRELATION_VALUE\"", Type = "NUMBER", Scale = 5 } },
							  { "\"CaseSensitiveColumn\"", new OracleColumn { Name = "\"CaseSensitiveColumn\"", Type = "NVARCHAR2", CharacterSize = 30 } }
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
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Unit = DataUnit.Byte } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(CurrentSchemaInternal, "\"PROJECT\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Unit = DataUnit.Byte } },
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
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Unit = DataUnit.Byte } }
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
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Nullable = false, Unit = DataUnit.Byte } }
				          }
			},
		};

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjectDictionary;

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> ObjectsInternal;
		
		public override ConnectionStringSettings ConnectionString { get { return ConnectionStringInternal; } }

		public override string CurrentSchema
		{
			get { return CurrentSchemaInternal; }
			set { throw new NotSupportedException("Test model schema cannot be changed. "); }
		}
		
		public override ICollection<string> Schemas { get { return SchemasInternal; } }
		
		public override ICollection<string> AllSchemas { get { return AllSchemasInternal; } }

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> Objects { get { return ObjectsInternal; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return AllObjectDictionary; } }
		
		public override void Refresh() { }

		public override event EventHandler RefreshStarted = delegate { };

		public override event EventHandler RefreshFinished = delegate { };
	}
}
