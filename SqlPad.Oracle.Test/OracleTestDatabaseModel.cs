using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle.Test
{
	public class OracleTestDatabaseModel : OracleDatabaseModelBase
	{
		public static readonly OracleTestDatabaseModel Instance;

		private const string InitialSchema = "\"HUSQVIK\"";
		private const string OwnerNameSys = "\"SYS\"";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK", "Oracle.DataAccess.Client");

		private static readonly List<ColumnHeader> ColumnHeaders;

		private static readonly HashSet<string> SchemasInternal = new HashSet<string> { OwnerNameSys, "\"SYSTEM\"", InitialSchema };
		private static readonly HashSet<string> AllSchemasInternal = new HashSet<string>(SchemasInternal) { OwnerNameSys, "\"SYSTEM\"", InitialSchema, SchemaPublic };
		private static readonly ILookup<OracleFunctionIdentifier, OracleFunctionMetadata> AllFunctionMetadataInternal;
		private static readonly Dictionary<string, OracleFunctionMetadata> NonSchemaBuiltInFunctionMetadataInternal;

		private readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> _allObjects;

		private static readonly HashSet<OracleDatabaseLink> DatabaseLinksInternal =
			new HashSet<OracleDatabaseLink>
			{
				new OracleDatabaseLink
				{
					Created = DateTime.Now,
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "HQ_PDB_LOOPBACK"),
					Host = "localhost:1521/hq_pdb",
					UserName = "HUSQVIK"
				}
			};

		private readonly IDictionary<OracleObjectIdentifier, OracleDatabaseLink> _databaseLinks = DatabaseLinksInternal.ToDictionary(l => l.FullyQualifiedName, l => l);
		
		private int _generatedRowCount;

		static OracleTestDatabaseModel()
		{
			var columnHeader =
				new ColumnHeader
				{
					ColumnIndex = 0,
					DataType = typeof (String),
					DatabaseDataType = "VARCHAR2",
					Name = "DUMMY"
				};

			columnHeader.ValueConverter = new OracleColumnValueConverter(columnHeader);

			ColumnHeaders = new List<ColumnHeader> { columnHeader };

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

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"SYNONYM_TO_SELECTION\""),
					SchemaObject = AllObjectsInternal.Single(o => o.Name == "\"SELECTION\"" && o.Owner == InitialSchema),
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

			var userCountFunction = (OracleFunction)AllObjectsInternal.Single(o => o.Name == "\"COUNT\"" && o.Owner == InitialSchema);
			userCountFunction.Metadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "COUNT"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			userCountFunction.Metadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));

			var sqlPadFunction = (OracleFunction)AllObjectsInternal.Single(o => o.Name == "\"SQLPAD_FUNCTION\"" && o.Owner == InitialSchema);
			sqlPadFunction.Metadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "SQLPAD_FUNCTION"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			sqlPadFunction.Metadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "VARCHAR2", false));

			var testFunction = (OracleFunction)AllObjectsInternal.Single(o => o.Name == "\"TESTFUNC\"" && o.Owner == InitialSchema);
			testFunction.Metadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "TESTFUNC"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			testFunction.Metadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));
			testFunction.Metadata.Parameters.Add(new OracleFunctionParameterMetadata("PARAM", 1, ParameterDirection.ReturnValue, "NUMBER", false));

			var sqlPadPackage = (OraclePackage)AllObjectsInternal.Single(o => o.Name == "\"SQLPAD\"" && o.Owner == InitialSchema);
			var packageSqlPadFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "SQLPAD", "SQLPAD_FUNCTION"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			packageSqlPadFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));
			packageSqlPadFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("P", 1, ParameterDirection.Input, "NUMBER", false));
			sqlPadPackage.Functions.Add(packageSqlPadFunctionMetadata);

			var asPdfPackage = (OraclePackage)AllObjectsInternal.Single(o => o.Name == "\"AS_PDF3\"" && o.Owner == InitialSchema);
			var asPdfPackageFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "AS_PDF3", "STR_LEN"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleFunctionMetadata.DisplayTypeNormal, false);
			asPdfPackageFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));
			asPdfPackageFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("P_TXT", 1, ParameterDirection.Input, "VARCHAR2", false));
			asPdfPackage.Functions.Add(asPdfPackageFunctionMetadata);

			var builtInFunctionPackage = (OraclePackage)AllObjectsInternal.Single(o => o.Name == PackageBuiltInFunction && o.Owner == OwnerNameSys);
			var truncFunctionMetadata = new OracleFunctionMetadata(IdentifierBuiltInFunctionTrunc, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			truncFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "DATE", false));
			truncFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("LEFT", 1, ParameterDirection.Input, "DATE", false));
			truncFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("RIGHT", 2, ParameterDirection.Input, "VARCHAR2", false));
			builtInFunctionPackage.Functions.Add(truncFunctionMetadata);

			var toCharFunctionMetadata = new OracleFunctionMetadata(IdentifierBuiltInFunctionToChar, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			toCharFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "VARCHAR2", false));
			toCharFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("LEFT", 1, ParameterDirection.Input, "NUMBER", false));
			builtInFunctionPackage.Functions.Add(toCharFunctionMetadata);

			var toCharWithNlsParameterFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(OwnerNameSys, PackageBuiltInFunction, "TO_CHAR", 1), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			toCharWithNlsParameterFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "VARCHAR2", false));
			toCharWithNlsParameterFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("LEFT", 1, ParameterDirection.Input, "NUMBER", false));
			toCharWithNlsParameterFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("FORMAT", 2, ParameterDirection.Input, "VARCHAR2", false));
			toCharWithNlsParameterFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("PARMS", 3, ParameterDirection.Input, "VARCHAR2", false));
			builtInFunctionPackage.Functions.Add(toCharWithNlsParameterFunctionMetadata);

			var roundFunctionOverload1Metadata = new OracleFunctionMetadata(IdentifierBuiltInFunctionRound, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			roundFunctionOverload1Metadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));
			roundFunctionOverload1Metadata.Parameters.Add(new OracleFunctionParameterMetadata("N", 1, ParameterDirection.Input, "NUMBER", false));
			builtInFunctionPackage.Functions.Add(roundFunctionOverload1Metadata);

			var roundFunctionOverload2Metadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "ROUND", 2), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			roundFunctionOverload2Metadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "NUMBER", false));
			roundFunctionOverload2Metadata.Parameters.Add(new OracleFunctionParameterMetadata("N", 1, ParameterDirection.Input, "NUMBER", false));
			roundFunctionOverload2Metadata.Parameters.Add(new OracleFunctionParameterMetadata("F", 2, ParameterDirection.Input, "NUMBER", false));
			builtInFunctionPackage.Functions.Add(roundFunctionOverload2Metadata);

			var dumpFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "DUMP"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			builtInFunctionPackage.Functions.Add(dumpFunctionMetadata);

			var coalesceFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "COALESCE"), false, false, false, true, false, false, 2, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			builtInFunctionPackage.Functions.Add(coalesceFunctionMetadata);

			var greatestFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "GREATEST"), false, false, false, true, false, false, 2, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			builtInFunctionPackage.Functions.Add(greatestFunctionMetadata);

			var noParenthesisFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "SESSIONTIMEZONE"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNoParenthesis, true);
			builtInFunctionPackage.Functions.Add(noParenthesisFunctionMetadata);

			var nvlFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "NVL"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			nvlFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "VARCHAR2", false));
			nvlFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("B1", 1, ParameterDirection.Input, "VARCHAR2", false));
			nvlFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("B2", 2, ParameterDirection.Input, "VARCHAR2", false));
			builtInFunctionPackage.Functions.Add(nvlFunctionMetadata);

			var upperFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "UPPER"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			upperFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "VARCHAR2", false));
			upperFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata("CH", 0, ParameterDirection.Input, "VARCHAR2", false));
			builtInFunctionPackage.Functions.Add(upperFunctionMetadata);

			var sysGuidFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues("SYS", "STANDARD", "SYS_GUID"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeParenthesis, true);
			sysGuidFunctionMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, "RAW", false));
			builtInFunctionPackage.Functions.Add(sysGuidFunctionMetadata);


			AllObjectDictionary = AllObjectsInternal.ToDictionary(o => o.FullyQualifiedName, o => o);

			ObjectsInternal = AllObjectDictionary
				.Values.Where(o => o.Owner == SchemaPublic || o.Owner == InitialSchema)
				.ToDictionary(o => OracleObjectIdentifier.Create(o.Owner, o.Name), o => o);

			AddConstraints();

			var allFunctionMetadata = AllObjectsInternal.OfType<IFunctionCollection>().SelectMany(c => c.Functions).ToList();

			var countFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(null, null, "COUNT"), true, true, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			allFunctionMetadata.Add(countFunctionMetadata);

			var maxFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(null, null, "MAX"), true, true, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			allFunctionMetadata.Add(maxFunctionMetadata);

			var lastValueFunctionMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(null, null, "LAST_VALUE"), true, false, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeNormal, true);
			allFunctionMetadata.Add(lastValueFunctionMetadata);

			AllFunctionMetadataInternal = allFunctionMetadata.ToLookup(m => m.Identifier);
			NonSchemaBuiltInFunctionMetadataInternal = allFunctionMetadata
				.Where(m => String.IsNullOrEmpty(m.Identifier.Owner))
				.ToDictionary(m => m.Identifier.Name, m => m);

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

		public override ILookup<OracleFunctionIdentifier, OracleFunctionMetadata> AllFunctionMetadata { get { return AllFunctionMetadataInternal; } }

		protected override IDictionary<string, OracleFunctionMetadata> NonSchemaBuiltInFunctionMetadata { get { return NonSchemaBuiltInFunctionMetadataInternal; } }

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
			new OracleFunction
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"SQLPAD_FUNCTION\""),
				IsValid = true
			},
			new OracleFunction
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"COUNT\""),
				IsValid = true
			},
			new OracleFunction
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"TESTFUNC\""),
				IsValid = true
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"UNCOMPILABLE_PACKAGE\"")
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"SQLPAD\""),
				IsValid = true
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"AS_PDF3\""),
				IsValid = true
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(OwnerNameSys, PackageBuiltInFunction),
				IsValid = true
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

		internal const string SelectionTableCreateScript =
@"CREATE TABLE ""HUSQVIK"".""SELECTION"" 
(""SELECTION_ID"" NUMBER, 
	""CURRENTSTARTED"" NUMBER, 
	""CURRENTCOMPLETES"" NUMBER, 
	""STATUS"" NUMBER, 
	""RESPONDENTBUCKET_ID"" NUMBER, 
	""PROJECT_ID"" NUMBER, 
	""NAME"" VARCHAR2(100)
   ) SEGMENT CREATION IMMEDIATE 
PCTFREE 10 PCTUSED 40 INITRANS 1 MAXTRANS 255 
NOCOMPRESS LOGGING
STORAGE(INITIAL 65536 NEXT 1048576 MINEXTENTS 1 MAXEXTENTS 2147483645
PCTINCREASE 0 FREELISTS 1 FREELIST GROUPS 1
BUFFER_POOL DEFAULT FLASH_CACHE DEFAULT CELL_FLASH_CACHE DEFAULT)
TABLESPACE ""TBS_HQ_PDB""";

		internal const string DummyPlanText =
		@"SQL_ID  9g6pyx7qz035v, child number 0
-------------------------------------
SELECT * FROM DUAL
 
Plan hash value: 272002086
 
---------------------------------------------------------------------------
| Id  | Operation         | Name | E-Rows |E-Bytes| Cost (%CPU)| E-Time   |
---------------------------------------------------------------------------
|   0 | SELECT STATEMENT  |      |        |       |     2 (100)|          |
|   1 |  TABLE ACCESS FULL| DUAL |      1 |     2 |     2   (0)| 00:00:01 |
---------------------------------------------------------------------------
 
Query Block Name / Object Alias (identified by operation id):
-------------------------------------------------------------
 
   1 - SEL$1 / DUAL@SEL$1
 
Outline Data
-------------
 
  /*+
      BEGIN_OUTLINE_DATA
      IGNORE_OPTIM_EMBEDDED_HINTS
      OPTIMIZER_FEATURES_ENABLE('12.1.0.1')
      DB_VERSION('12.1.0.1')
      ALL_ROWS
      OUTLINE_LEAF(@""SEL$1"")
      FULL(@""SEL$1"" ""DUAL""@""SEL$1"")
      END_OUTLINE_DATA
  */
 
Column Projection Information (identified by operation id):
-----------------------------------------------------------
 
   1 - ""DUAL"".""DUMMY""[VARCHAR2,1]
 
Note
-----
   - Warning: basic plan statistics not available. These are only collected when:
       * hint 'gather_plan_statistics' is used for the statement or
       * parameter 'statistics_level' is set to 'ALL', at session or system level
";

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjectDictionary;

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> ObjectsInternal;
		
		public override ConnectionStringSettings ConnectionString { get { return ConnectionStringInternal; } }

		public override string CurrentSchema { get; set; }
		
		public override ICollection<string> Schemas { get { return SchemasInternal; } }
		
		public override ICollection<string> AllSchemas { get { return AllSchemasInternal; } }

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> Objects { get { return ObjectsInternal; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _allObjects; } }

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get { return _databaseLinks; } }

		public override void RefreshIfNeeded()
		{
			Refresh();
		}

		public override Task Refresh(bool force = false)
		{
			RefreshStarted(this, EventArgs.Empty);
			RefreshFinished(this, EventArgs.Empty);
			var taskCompletionSource = new TaskCompletionSource<object>();
			taskCompletionSource.SetResult(null);
			return taskCompletionSource.Task;
		}

		public override bool IsModelFresh { get { return true; } }

		public override event EventHandler RefreshStarted = delegate { };

		public override event EventHandler RefreshFinished = delegate { };

		public override StatementExecutionResult ExecuteStatement(StatementExecutionModel executionModel)
		{
			return new StatementExecutionResult { ExecutedSucessfully = true };
		}

		public override Task<StatementExecutionResult> ExecuteStatementAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			return Task.Factory.StartNew(() => new StatementExecutionResult { ExecutedSucessfully = true }, cancellationToken);
		}

		public override Task UpdateTableDetailsAsync(OracleObjectIdentifier objectIdentifier, TableDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var taskCompletionSource = new TaskCompletionSource<TableDetailsModel>();

			dataModel.AverageRowSize = 237;
			dataModel.LastAnalyzed = new DateTime(2014, 8, 19, 6, 18, 12);
			dataModel.BlockCount = 544;
			dataModel.RowCount = 8312;
			dataModel.AllocatedBytes = 22546891;
			dataModel.Compression = "Disabled";
			dataModel.Organization = "Index";

			taskCompletionSource.SetResult(null);
			return taskCompletionSource.Task;
		}

		public override Task UpdateColumnDetailsAsync(OracleObjectIdentifier objectIdentifier, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken)
		{
			var taskCompletionSource = new TaskCompletionSource<ColumnDetailsModel>();

			dataModel.DistinctValueCount = 567;
			dataModel.LastAnalyzed = new DateTime(2014, 8, 19, 6, 18, 12);
			dataModel.SampleSize = 12346;
			dataModel.AverageValueSize = 7;
			dataModel.NullValueCount = 1344;
			dataModel.HistogramBucketCount = 6;
			dataModel.HistogramType = "Frequency";

			var previousValue = 0d;
			dataModel.HistogramValues = Enumerable.Repeat(new Random(), dataModel.HistogramBucketCount).Select(r => (previousValue += r.NextDouble())).ToArray();

			taskCompletionSource.SetResult(null);
			return taskCompletionSource.Task;
		}
		
		public override IEnumerable<object[]> FetchRecords(int rowCount)
		{
			yield return new object[] { "Dummy Value " + ++_generatedRowCount};
		}

		public override ICollection<ColumnHeader> GetColumnHeaders()
		{
			return ColumnHeaders;
		}

		public override Task<StatementExecutionModel> ExplainPlanAsync(string statement, CancellationToken cancellationToken)
		{
			var statementExecutionModel =
				new StatementExecutionModel
				{
					StatementText = String.Format(DatabaseCommands.ExplainPlanBase, OracleObjectIdentifier.Create(InitialSchema, "TARGET_PLAN_TABLE")),
					BindVariables = new[] { new BindVariableModel(new BindVariableConfiguration { DataType = "Varchar2", Name = "STATEMENT_ID", Value = "DummyStatementId" }) }
				};

			return CreateFinishedTask(statementExecutionModel);
		}

		public override Task<string> GetActualExecutionPlanAsync(CancellationToken cancellationToken)
		{
			return CreateFinishedTask(DummyPlanText);
		}

		public override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true)
		{
			return CreateFinishedTask(SelectionTableCreateScript);
		}

		private Task<TResult> CreateFinishedTask<TResult>(TResult result)
		{
			var source = new TaskCompletionSource<TResult>();
			source.SetResult(result);
			return source.Task;
		}

		public override bool CanFetch { get { return true; } }
		
		public override bool IsExecuting { get { return false; } }
	}
}
