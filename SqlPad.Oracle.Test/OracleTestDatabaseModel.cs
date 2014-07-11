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
		public static readonly OracleTestDatabaseModel Instance;
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));

		private const string InitialSchema = "\"HUSQVIK\"";
		private const string OwnerNameSys = "\"SYS\"";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK", "Oracle.DataAccess.Client");

		private static readonly List<ColumnHeader> ColumnHeaders =
			new List<ColumnHeader>
			{
				new ColumnHeader
				{
					ColumnIndex = 0,
					DataType = typeof(String),
					DatabaseDataType = "VARCHAR2",
					Name = "DUMMY",
					ValueConverterFunction = ValueConverterFunction
				}
			};

		private static readonly HashSet<string> SchemasInternal = new HashSet<string> { OwnerNameSys, "\"SYSTEM\"", InitialSchema };
		private static readonly HashSet<string> AllSchemasInternal = new HashSet<string>(SchemasInternal) { OwnerNameSys, "\"SYSTEM\"", InitialSchema, SchemaPublic };
		private static readonly OracleFunctionMetadataCollection AllFunctionMetadataInternal;
		private static readonly ILookup<string, OracleFunctionMetadata> NonPackageBuiltInFunctionMetadataInternal;

		private readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjects;
		private int _generatedRowCount;

		static OracleTestDatabaseModel()
		{
			using (var builtInFunctionReader = XmlReader.Create(File.OpenRead(@"TestFiles\OracleSqlFunctionMetadataCollection_12_1_0_1_0.xml")))
			{
				var builtInFunctionMetadata = (OracleFunctionMetadataCollection)Serializer.ReadObject(builtInFunctionReader);
				NonPackageBuiltInFunctionMetadataInternal = builtInFunctionMetadata.SqlFunctions
					.Where(f => String.IsNullOrEmpty(f.Identifier.Owner))
					.ToLookup(f => f.Identifier.Name, f => f);

				var testFolder = Path.GetDirectoryName(typeof(OracleTestDatabaseModel).Assembly.CodeBase);
				using (var reader = XmlReader.Create(Path.Combine(testFolder, @"TestFiles\TestFunctionCollection.xml")))
				{
					AllFunctionMetadataInternal = new OracleFunctionMetadataCollection(builtInFunctionMetadata.SqlFunctions.Concat(((OracleFunctionMetadataCollection)Serializer.ReadObject(reader)).SqlFunctions));
				}
			}

			foreach (var function in AllFunctionMetadataInternal.SqlFunctions.Where(f => !String.IsNullOrEmpty(f.Identifier.Owner)))
			{
				var objectName = function.IsPackageFunction ? function.Identifier.Package : function.Identifier.Name;
				var schemaObject = AllObjectsInternal.SingleOrDefault(o => o.Owner == function.Identifier.Owner && o.Name == objectName);

				if (schemaObject == null)
				{
					var objectType = function.IsPackageFunction ? OracleSchemaObjectType.Package : OracleSchemaObjectType.Function;
					schemaObject = OracleObjectFactory.CreateSchemaObjectMetadata(objectType, function.Identifier.Owner, objectName, true, DateTime.Now, DateTime.Now, false);
					AllObjectsInternal.Add(schemaObject);
				}

				if (function.IsPackageFunction)
				{
					((OraclePackage)schemaObject).Functions.Add(function);
				}
				else
				{
					((OracleFunction)schemaObject).Metadata = function;
				}
			}

			const string tableNameDual = "\"DUAL\"";
			var synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, tableNameDual),
					SchemaObject = AllObjectsInternal.Single(o => o.Name == tableNameDual && o.Owner == OwnerNameSys),
					IsValid = true
				};
			synonym.SchemaObject.Synonym = synonym;
			
			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "\"V$SESSION\""),
					SchemaObject = AllObjectsInternal.Single(o => o.Name == "\"V_$SESSION\"" && o.Owner == OwnerNameSys),
					IsValid = true
				};
			synonym.SchemaObject.Synonym = synonym;

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "\"XMLTYPE\""),
					SchemaObject = AllObjectsInternal.Single(o => o.Name == "\"XMLTYPE\"" && o.Owner == OwnerNameSys),
					IsValid = true
				};
			synonym.SchemaObject.Synonym = synonym;

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"SYNONYM_TO_TEST_SEQ\""),
					SchemaObject = AllObjectsInternal.Single(o => o.Name == "\"TEST_SEQ\"" && o.Owner == InitialSchema),
					IsValid = true
				};
			synonym.SchemaObject.Synonym = synonym;

			AllObjectsInternal.Add(synonym);

			var dbmsRandom = (OraclePackage)AllObjectsInternal.Single(o => o.Name == "\"DBMS_RANDOM\"" && o.Owner == OwnerNameSys);
			var randomStringFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "DBMS_RANDOM", "STRING"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			randomStringFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "VARCHAR2", false));
			randomStringFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("OPT", 1, ParameterDirection.Input, "CHAR", false));
			randomStringFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("LEN", 2, ParameterDirection.Input, "NUMBER", false));
			dbmsRandom.Functions.Add(randomStringFunctionMetadata);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "\"DBMS_RANDOM\""),
					SchemaObject = dbmsRandom,
					IsValid = true
				};
			synonym.SchemaObject.Synonym = synonym;

			AllObjectsInternal.Add(synonym);

			var uncompilableFunction = (OracleFunction)AllObjectsInternal.Single(o => o.Name == "\"UNCOMPILABLE_FUNCTION\"" && o.Owner == InitialSchema);
			uncompilableFunction.Metadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "UNCOMPILABLE_FUNCTION"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			uncompilableFunction.Metadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));

			var uncompilablePackage = (OraclePackage)AllObjectsInternal.Single(o => o.Name == "\"UNCOMPILABLE_PACKAGE\"" && o.Owner == InitialSchema);
			var uncompilablePackageFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "UNCOMPILABLE_PACKAGE", "FUNCTION"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			uncompilablePackageFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));
			uncompilablePackage.Functions.Add(uncompilablePackageFunctionMetadata);

			AllObjectDictionary = AllObjectsInternal.ToDictionary(o => o.FullyQualifiedName, o => o);

			ObjectsInternal = AllObjectDictionary
				.Values.Where(o => o.Owner == SchemaPublic || o.Owner == InitialSchema)
				.ToDictionary(o => OracleObjectIdentifier.Create(o.Owner, o.Name), o => o);

			AddConstraints();

			Instance = new OracleTestDatabaseModel { CurrentSchema = InitialSchema };
		}

		public OracleTestDatabaseModel()
		{
			_allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>(AllObjectDictionary);
		}

		private static void AddConstraints()
		{
			var projectTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"PROJECT\"")];
			var projectPrimaryKey = new OraclePrimaryKeyConstraint
			                                 {
				                                 FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_PROJECT\""),
				                                 Columns = new[] { "\"PROJECT_ID\"" },
				                                 Owner = projectTable
			                                 };
			
			projectTable.Constraints = new List<OracleConstraint>{ projectPrimaryKey }.AsReadOnly();

			var sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"TARGETGROUP\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_TARGETGROUP_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						Owner = sourceObject,
						TargetObject = projectTable
					}
				};

			var invoiceTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"INVOICES\"")];
			var invoicePrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_INVOICES\""),
				Columns = new[] { "\"ID\"" },
				Owner = invoiceTable
			};

			invoiceTable.Constraints = new List<OracleConstraint> { invoicePrimaryKey }.AsReadOnly();

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"INVOICELINES\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_INVOICELINES_INVOICES\""),
						Columns = new[] { "\"INVOICE_ID\"" },
						ReferenceConstraint = invoicePrimaryKey,
						Owner = sourceObject,
						TargetObject = invoiceTable
					}
				}.AsReadOnly();

			var targetGroupTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"TARGETGROUP\"")];
			var targetGroupPrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_TARGETGROUP\""),
				Columns = new[] { "\"TARGETGROUP_ID\"" },
				Owner = targetGroupTable
			};

			targetGroupTable.Constraints.Add(targetGroupPrimaryKey);

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"RESPONDENTBUCKET\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_RESPONDENTBUCKET_TARGETGROUP\""),
						Columns = new[] { "\"TARGETGROUP_ID\"" },
						ReferenceConstraint = targetGroupPrimaryKey,
						Owner = sourceObject,
						TargetObject = targetGroupTable
					},
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_RESPONDENTBUCKET_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						Owner = sourceObject,
						TargetObject = projectTable
					}
				};

			var respondentBucketTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"RESPONDENTBUCKET\"")];
			var respondentBucketPrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_RESPONDENTBUCKET\""),
				Columns = new[] { "\"RESPONDENTBUCKET_ID\"" },
				Owner = respondentBucketTable
			};

			respondentBucketTable.Constraints.Add(respondentBucketPrimaryKey);

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"SELECTION\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_SELECTION_RESPONDENTBUCKET\""),
						Columns = new[] { "\"RESPONDENTBUCKET_ID\"" },
						ReferenceConstraint = respondentBucketPrimaryKey,
						Owner = sourceObject,
						TargetObject = respondentBucketTable
					},
					new OracleForeignKeyConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_SELECTION_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						Owner = sourceObject,
						TargetObject = projectTable
					}
				}.AsReadOnly();
		}

		public override OracleFunctionMetadataCollection AllFunctionMetadata { get { return AllFunctionMetadataInternal; } }

		protected override ILookup<string, OracleFunctionMetadata> NonPackageBuiltInFunctionMetadata { get { return NonPackageBuiltInFunctionMetadataInternal; } }

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
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"COUNTRY\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"ID\"", new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Unit = DataUnit.Byte } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"ORDERS\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"ID\"", new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"INVOICES\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"ID\"", new OracleColumn { Name = "\"ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } },
					          { "\"DUEDATE\"", new OracleColumn { Name = "\"DUEDATE\"", Type = "DATE" } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"INVOICELINES\""),
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
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"CaseSensitiveTable\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"CaseSensitiveColumn\"", new OracleColumn { Name = "\"CaseSensitiveColumn\"", Type = "RAW", Size = 4000 } }
				          }
			},
			new OracleView
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"VIEW_INSTANTSEARCH\""),
				Organization = OrganizationType.NotApplicable,
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"TARGETGROUP\""),
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
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PROJECT\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
					          { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Unit = DataUnit.Byte } },
					          { "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0 } }
				          }
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"RESPONDENTBUCKET\""),
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
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"SELECTION\""),
				Organization = OrganizationType.Heap,
				Columns = new Dictionary<string, OracleColumn>
				          {
							  { "\"RESPONDENTBUCKET_ID\"", new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = true } },
					          { "\"SELECTION_ID\"", new OracleColumn { Name = "\"SELECTION_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false } },
					          { "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", Type = "NUMBER", Precision = 9, Scale = 0, Nullable = false } },
							  { "\"NAME\"", new OracleColumn { Name = "\"NAME\"", Type = "VARCHAR2", Size = 50, CharacterSize = 50, Nullable = false, Unit = DataUnit.Byte } }
				          }
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, "\"DBMS_RANDOM\""),
				IsValid = true
			},
			new OracleFunction
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"UNCOMPILABLE_FUNCTION\"")
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"UNCOMPILABLE_PACKAGE\"")
			},
			new OracleObjectType
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, "\"XMLTYPE\""),
				IsValid = true
			},
			new OracleObjectType
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"INVALID_OBJECT_TYPE\"")
			},
			new OracleSequence
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"TEST_SEQ\""),
				IsValid = true,
				CacheSize = 20, CurrentValue = 1234, MinimumValue = 1, MaximumValue = Decimal.MaxValue, CanCycle = true
			}
		};

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjectDictionary;

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> ObjectsInternal;
		
		public override ConnectionStringSettings ConnectionString { get { return ConnectionStringInternal; } }

		public override string CurrentSchema { get; set; }
		
		public override ICollection<string> Schemas { get { return SchemasInternal; } }
		
		public override ICollection<string> AllSchemas { get { return AllSchemasInternal; } }

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> Objects { get { return ObjectsInternal; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _allObjects; } }

		public override void RefreshIfNeeded() { }

		public override void Refresh() { }

		public override event EventHandler RefreshStarted = delegate { };

		public override event EventHandler RefreshFinished = delegate { };

		public override int ExecuteStatement(string statementText, bool returnDataset)
		{
			return 0;
		}

		public override IEnumerable<object[]> FetchRecords(int rowCount)
		{
			yield return new object[] { "Dummy Value " + ++_generatedRowCount};
		}

		public override ICollection<ColumnHeader> GetColumnHeaders()
		{
			return ColumnHeaders;
		}

		public override string GetObjectScript(OracleSchemaObject schemaObject)
		{
			throw new NotImplementedException();
		}

		public override bool CanExecute { get { return true; } }

		public override bool CanFetch { get { return true; } }
		
		public override bool IsExecuting { get { return false; } }
	}
}
