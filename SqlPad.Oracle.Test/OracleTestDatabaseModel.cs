using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ExecutionPlan;
using SqlPad.Oracle.ToolTips;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.Test
{
	public class OracleTestDatabaseModel : OracleDatabaseModelBase
	{
		public static readonly OracleTestDatabaseModel Instance;

		private const string InitialSchema = "\"HUSQVIK\"";
		private const string TableNameDual = "\"DUAL\"";
		private const string DatabaseDomainNameInternal = "sqlpad.husqvik.com";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK", "Oracle.DataAccess.Client");
		private static readonly Version InitialTestDatabaseVersion = new Version(12, 1, 0, 2);

		private static readonly HashSet<string> SchemasInternal = new HashSet<string> { SchemaSys, "\"SYSTEM\"", InitialSchema };
		private static readonly Dictionary<string, OracleSchema> AllSchemasInternal =
			new Dictionary<string, OracleSchema>
			{
				{ SchemaSys, new OracleSchema { Name = SchemaSys } },
				{ SchemaSystem, new OracleSchema { Name = SchemaSystem } },
				{ InitialSchema, new OracleSchema { Name = InitialSchema, Created = new DateTime(2014, 9, 28, 0, 25, 43) } },
				{ SchemaPublic, new OracleSchema { Name = SchemaPublic } }
			};

		private static readonly ILookup<OracleObjectIdentifier, OracleReferenceConstraint> UniqueConstraintReferringReferenceConstraintsInternal;
		private static readonly ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllProgramMetadataInternal;
		private static readonly ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadataInternal;
		private static readonly ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageProgramMetadataInternal;
		private static readonly HashSet<string> CharacterSetsInternal = new HashSet<string> { "US7ASCII", "WE8ISO8859P1" };

		private const int StatisticsCodeSessionLogicalReads = 12;
		private const int StatisticsCodePhysicalReadTotalBytes = 53;
		private const int StatisticsCodeConsistentGets = 79;
		private const int StatisticsCodeBytesSentViaSqlNetToClient = 779;
		private const int StatisticsCodeBytesReceivedViaSqlNetFromClient = 780;
		private const int StatisticsCodeSqlNetRoundtripsToOrFromClient = 781;

		internal const string StatisticsDescriptionSessionLogicalReads = "session logical reads";
		internal const string StatisticsDescriptionPhysicalReadTotalBytes = "physical read total bytes";
		internal const string StatisticsDescriptionConsistentGets = "consistent gets";
		internal const string StatisticsDescriptionBytesSentViaSqlNetToClient = "bytes sent via SQL*Net to client";
		internal const string StatisticsDescriptionBytesReceivedViaSqlNetFromClient = "bytes received via SQL*Net from client";
		internal const string StatisticsDescriptionSqlNetRoundtripsToOrFromClient = "SQL*Net roundtrips to/from client";

		private static readonly ILookup<string, string> ContextDataInternal =
			new[]
			{
				new KeyValuePair<string, string>("TEST_CONTEXT_1", "TestAttribute1"),
				new KeyValuePair<string, string>("TEST_CONTEXT_2", "TestAttribute2"),
				new KeyValuePair<string, string>("TEST_CONTEXT_1", "TestAttribute3"),
				new KeyValuePair<string, string>("TEST_CONTEXT_1", "Special'Attribute'4"),
				new KeyValuePair<string, string>("SPECIAL'CONTEXT", "Special'Attribute'5")
			}.ToLookup(r => r.Key, r => r.Value);

		private static readonly IReadOnlyList<string> WeekdayNamesInternal =
			new[]
			{
				"Friday",
				"Monday",
				"Saturday",
				"Sunday",
				"Thursday",
				"Tuesday",
				"Wednesday"
			};

		private static readonly Dictionary<int, string> StatisticsKeysInternal =
			new Dictionary<int, string>
			{
				{ StatisticsCodeSessionLogicalReads, StatisticsDescriptionSessionLogicalReads },
				{ StatisticsCodePhysicalReadTotalBytes, StatisticsDescriptionPhysicalReadTotalBytes },
				{ StatisticsCodeConsistentGets, StatisticsDescriptionConsistentGets },
				{ StatisticsCodeBytesSentViaSqlNetToClient, StatisticsDescriptionBytesSentViaSqlNetToClient },
				{ StatisticsCodeBytesReceivedViaSqlNetFromClient, StatisticsDescriptionBytesReceivedViaSqlNetFromClient },
				{ StatisticsCodeSqlNetRoundtripsToOrFromClient, StatisticsDescriptionSqlNetRoundtripsToOrFromClient }
			};

		internal string CurrentDatabaseDomainNameInternal = DatabaseDomainNameInternal;

		private static readonly Dictionary<string, string> SystemParametersInternal =
		new Dictionary<string, string>
			{
				{ SystemParameterNameMaxStringSize, "STANDARD" }
			};

	    private static readonly HashSet<OracleDatabaseLink> DatabaseLinksInternal =
			new HashSet<OracleDatabaseLink>
			{
				new OracleDatabaseLink
				{
					Created = DateTime.Now,
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "HQ_PDB_LOOPBACK"),
					Host = "localhost:1521/hq_pdb",
					UserName = "HUSQVIK"
				},
				new OracleDatabaseLink
				{
					Created = DateTime.Now,
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "TESTHOST.SQLPAD.HUSQVIK.COM@HQINSTANCE"),
					Host = "sqlpad.husqvik.com:1521/servicename",
					UserName = "HUSQVIK"
				}
			};

	    internal Version TestDatabaseVersion = InitialTestDatabaseVersion;

		static OracleTestDatabaseModel()
		{
			#region object synonyms
			var synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, TableNameDual),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, TableNameDual) && String.Equals(o.Owner, SchemaSys)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			/*synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "SYNONYM_TO_HQ_PDB_LOOPBACK"),
					SchemaObject = DatabaseLinksInternal.Single(o => String.Equals(o.Name, "\"HQ_PDB_LOOPBACK\"") && String.Equals(o.Owner, SchemaSys)),
					IsValid = true
				};

			synonym.SchemaObject.Synonyms.Add(synonym);*/

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "V$SESSION"),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, "\"V_$SESSION\"") && String.Equals(o.Owner, SchemaSys)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "XMLTYPE"),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, "\"XMLTYPE\"") && String.Equals(o.Owner, SchemaSys)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "SYNONYM_TO_TEST_SEQ"),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, "\"TEST_SEQ\"") && String.Equals(o.Owner, InitialSchema)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "SYNONYM_TO_SELECTION"),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, "\"SELECTION\"") && String.Equals(o.Owner, InitialSchema)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "PUBLIC_SYNONYM_TO_SELECTION"),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, "\"SELECTION\"") && String.Equals(o.Owner, InitialSchema)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "RAWLIST"),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, "\"ODCIRAWLIST\"") && String.Equals(o.Owner, SchemaSys)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);
			#endregion

			#region SYS.DBMS_RANDOM
			var dbmsRandom = (OraclePackage)AllObjectsInternal.Single(o => o.Name == PackageDbmsRandom && String.Equals(o.Owner, SchemaSys));
			var randomStringFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierDbmsRandomString, false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			randomStringFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			randomStringFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"OPT\"", 1, 1, 0, ParameterDirection.Input, "CHAR", OracleObjectIdentifier.Empty, false));
			randomStringFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"LEN\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			randomStringFunctionMetadata.Owner = dbmsRandom;
			dbmsRandom.Programs.Add(randomStringFunctionMetadata);
			var randomNormalFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageDbmsRandom, "NORMAL"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			randomNormalFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			randomNormalFunctionMetadata.Owner = dbmsRandom;
			dbmsRandom.Programs.Add(randomNormalFunctionMetadata);
			var randomValueFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageDbmsRandom, "VALUE"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			randomValueFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			randomValueFunctionMetadata.Owner = dbmsRandom;
			dbmsRandom.Programs.Add(randomValueFunctionMetadata);
			var randomValueTwoParameterFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageDbmsRandom, "VALUE"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			randomValueTwoParameterFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			randomValueTwoParameterFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"LOW\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			randomValueTwoParameterFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"HIGH\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			randomValueTwoParameterFunctionMetadata.Owner = dbmsRandom;
			dbmsRandom.Programs.Add(randomValueTwoParameterFunctionMetadata);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "DBMS_RANDOM"),
					SchemaObject = dbmsRandom,
					IsValid = true
				};
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);
			#endregion

			#region SYS.DBMS_XPLAN
			var dbmsXPlan = (OraclePackage)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"DBMS_XPLAN\"") && String.Equals(o.Owner, SchemaSys));
			var displayCursorFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "DBMS_XPLAN", "DISPLAY_CURSOR"), false, false, true, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, false);
			displayCursorFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, OracleTypeCollection.OracleCollectionTypeNestedTable, OracleObjectIdentifier.Create(SchemaSys, "DBMS_XPLAN_TYPE_TABLE"), false));
			displayCursorFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 1, 1, 1, ParameterDirection.ReturnValue, OracleTypeBase.TypeCodeObject, OracleObjectIdentifier.Create(SchemaSys, "DBMS_XPLAN_TYPE"), false));
			displayCursorFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"SQL_ID\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, true));
			displayCursorFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"CURSOR_CHILD_NUMBER\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, true));
			displayCursorFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"FORMAT\"", 3, 3, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, true));
			displayCursorFunctionMetadata.Owner = dbmsXPlan;
			dbmsXPlan.Programs.Add(displayCursorFunctionMetadata);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "DBMS_XPLAN"),
					SchemaObject = dbmsXPlan,
					IsValid = true
				};
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);
			#endregion

			#region SYS.DBMS_CRYPTO
			var dbmsCrypto = (OraclePackage)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"DBMS_CRYPTO\"") && String.Equals(o.Owner, SchemaSys));
			var randomBytesFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "DBMS_CRYPTO", "RANDOMBYTES"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			randomBytesFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Raw, OracleObjectIdentifier.Empty, false));
			randomBytesFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"NUMBER_BYTES\"", 1, 1, 0, ParameterDirection.Input, "BINARY_INTEGER", OracleObjectIdentifier.Empty, false));
			randomBytesFunctionMetadata.Owner = dbmsCrypto;
			dbmsCrypto.Programs.Add(randomBytesFunctionMetadata);
			var dbmsCryptoHashMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "DBMS_CRYPTO", "HASH"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			dbmsCryptoHashMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Raw, OracleObjectIdentifier.Empty, false));
			dbmsCryptoHashMetadata.AddParameter(new OracleProgramParameterMetadata("\"SRC\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Raw, OracleObjectIdentifier.Empty, false));
			dbmsCryptoHashMetadata.AddParameter(new OracleProgramParameterMetadata("\"TYP\"", 2, 2, 0, ParameterDirection.Input, "BINARY_INTEGER", OracleObjectIdentifier.Empty, false));
			dbmsCryptoHashMetadata.Owner = dbmsCrypto;
			dbmsCrypto.Programs.Add(dbmsCryptoHashMetadata);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "DBMS_CRYPTO"),
					SchemaObject = dbmsCrypto,
					IsValid = true
				};
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);
			#endregion

			#region SYS.DBMS_OUTPUT
			var dbmsOutput = (OraclePackage)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"DBMS_OUTPUT\"") && String.Equals(o.Owner, SchemaSys));
			var putLineProcedureMetadata = new OracleProgramMetadata(ProgramType.Procedure, OracleProgramIdentifier.CreateFromValues("SYS", "DBMS_OUTPUT", "PUT_LINE"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			putLineProcedureMetadata.AddParameter(new OracleProgramParameterMetadata("\"A\"", 1, 1, 0, ParameterDirection.Input, "VARCHAR2", OracleObjectIdentifier.Empty, false));
			putLineProcedureMetadata.Owner = dbmsOutput;
			dbmsOutput.Programs.Add(putLineProcedureMetadata);

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(SchemaPublic, "DBMS_OUTPUT"),
					SchemaObject = dbmsOutput,
					IsValid = true
				};
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);
			#endregion

			var uncompilableFunction = (OracleFunction)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"UNCOMPILABLE_FUNCTION\"") && String.Equals(o.Owner, InitialSchema));
			uncompilableFunction.Metadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "UNCOMPILABLE_FUNCTION"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			uncompilableFunction.Metadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			uncompilableFunction.Metadata.Owner = uncompilableFunction;

			var uncompilablePackage = (OraclePackage)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"UNCOMPILABLE_PACKAGE\"") && String.Equals(o.Owner, InitialSchema));
			var uncompilablePackageFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "UNCOMPILABLE_PACKAGE", "FUNCTION"), false, false, false, false, true, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			uncompilablePackageFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			uncompilablePackage.Programs.Add(uncompilablePackageFunctionMetadata);
			uncompilablePackageFunctionMetadata.Owner = uncompilablePackage;

			var userCountFunction = (OracleFunction)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"COUNT\"") && String.Equals(o.Owner, InitialSchema));
			userCountFunction.Metadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "COUNT"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			userCountFunction.Metadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			userCountFunction.Metadata.Owner = userCountFunction;

			var sqlPadFunction = (OracleFunction)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"SQLPAD_FUNCTION\"") && String.Equals(o.Owner, InitialSchema));
			sqlPadFunction.Metadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "SQLPAD_FUNCTION"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			sqlPadFunction.Metadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			sqlPadFunction.Metadata.Owner = sqlPadFunction;

			synonym =
				new OracleSynonym
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "SYNONYM_TO_SQLPAD_FUNCTION"),
					SchemaObject = AllObjectsInternal.Single(o => String.Equals(o.Name, "\"SQLPAD_FUNCTION\"") && String.Equals(o.Owner, InitialSchema)),
					IsValid = true
				};
			
			synonym.SchemaObject.Synonyms.Add(synonym);

			AllObjectsInternal.Add(synonym);

			var testFunction = (OracleFunction)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"TESTFUNC\"") && String.Equals(o.Owner, InitialSchema));
			testFunction.Metadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "TESTFUNC"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			testFunction.Metadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			testFunction.Metadata.AddParameter(new OracleProgramParameterMetadata("\"PARAM\"", 1, 1, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			testFunction.Metadata.Owner = testFunction;

			var sqlPadPackage = (OraclePackage)AllObjectsInternal.Single(o => String.Equals(o.Name, "\"SQLPAD\"") && String.Equals(o.Owner, InitialSchema));
			var packageSqlPadFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "SQLPAD", "SQLPAD_FUNCTION"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			packageSqlPadFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			packageSqlPadFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"P\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			packageSqlPadFunctionMetadata.Owner = sqlPadPackage;
			sqlPadPackage.Programs.Add(packageSqlPadFunctionMetadata);

			var packageSqlPadPipelinedFunctionWithCursorParameterMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "SQLPAD", "CURSOR_FUNCTION"), false, false, true, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			packageSqlPadPipelinedFunctionWithCursorParameterMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 1, 0, ParameterDirection.ReturnValue, OracleTypeCollection.OracleCollectionTypeNestedTable, OracleObjectIdentifier.Create(SchemaSys, "DBMS_XPLAN_TYPE_TABLE"), false));
			packageSqlPadPipelinedFunctionWithCursorParameterMetadata.AddParameter(new OracleProgramParameterMetadata(null, 1, 2, 1, ParameterDirection.ReturnValue, OracleTypeBase.TypeCodeObject, OracleObjectIdentifier.Create(SchemaSys, "DBMS_XPLAN_TYPE"), false));
			packageSqlPadPipelinedFunctionWithCursorParameterMetadata.AddParameter(new OracleProgramParameterMetadata("\"I\"", 1, 3, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			packageSqlPadPipelinedFunctionWithCursorParameterMetadata.AddParameter(new OracleProgramParameterMetadata("\"C1\"", 2, 4, 0, ParameterDirection.Input, "REF CURSOR", OracleObjectIdentifier.Empty, false));
			packageSqlPadPipelinedFunctionWithCursorParameterMetadata.AddParameter(new OracleProgramParameterMetadata("\"C2\"", 3, 28, 0, ParameterDirection.Input, "REF CURSOR", OracleObjectIdentifier.Empty, false));
			packageSqlPadPipelinedFunctionWithCursorParameterMetadata.Owner = sqlPadPackage;
			sqlPadPackage.Programs.Add(packageSqlPadPipelinedFunctionWithCursorParameterMetadata);

			var packageSqlPadPipelinedFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "SQLPAD", "PIPELINED_FUNCTION"), false, false, true, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			packageSqlPadPipelinedFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, OracleTypeCollection.OracleCollectionTypeNestedTable, OracleObjectIdentifier.Create(SchemaSys, "ODCIDATELIST"), false));
			packageSqlPadPipelinedFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 1, 1, 1, ParameterDirection.ReturnValue, TerminalValues.Date, OracleObjectIdentifier.Empty, false));
			packageSqlPadPipelinedFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"DATE_FROM\"", 1, 2, 0, ParameterDirection.Input, TerminalValues.Date, OracleObjectIdentifier.Empty, false));
			packageSqlPadPipelinedFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"DATE_TO\"", 2, 3, 0, ParameterDirection.Input, TerminalValues.Date, OracleObjectIdentifier.Empty, false));
			packageSqlPadPipelinedFunctionMetadata.Owner = sqlPadPackage;
			sqlPadPackage.Programs.Add(packageSqlPadPipelinedFunctionMetadata);

			var sqlPadProcedureMetadata = new OracleProgramMetadata(ProgramType.Procedure, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), null, "SQLPAD_PROCEDURE"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			sqlPadProcedureMetadata.Owner = sqlPadPackage;
			sqlPadPackage.Programs.Add(sqlPadProcedureMetadata);

			var asPdfPackage = (OraclePackage)AllObjectsInternal.Single(o => o.Name == "\"AS_PDF3\"" && o.Owner == InitialSchema);
			var asPdfPackageFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(InitialSchema.ToSimpleIdentifier(), "AS_PDF3", "STR_LEN"), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
			asPdfPackageFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			asPdfPackageFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"P_TXT\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			asPdfPackage.Programs.Add(asPdfPackageFunctionMetadata);
			asPdfPackageFunctionMetadata.Owner = asPdfPackage;

			#region SYS.STANDARD
			var builtInFunctionPackage = (OraclePackage)AllObjectsInternal.Single(o => o.FullyQualifiedName == BuiltInFunctionPackageIdentifier);
			var truncFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramTrunc, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			truncFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Date, OracleObjectIdentifier.Empty, false));
			truncFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"LEFT\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Date, OracleObjectIdentifier.Empty, false));
			truncFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"RIGHT\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			truncFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(truncFunctionMetadata);

			var toCharFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramToChar, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			toCharFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			toCharFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"LEFT\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			toCharFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(toCharFunctionMetadata);

			var sysContextFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramSysContext, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			sysContextFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			sysContextFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"NAMESPACE\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			sysContextFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"ATTRIBUTE\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			sysContextFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(sysContextFunctionMetadata);

			var toCharWithNlsParameterFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(SchemaSys, PackageBuiltInFunction, "TO_CHAR", 1), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			toCharWithNlsParameterFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			toCharWithNlsParameterFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"LEFT\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			toCharWithNlsParameterFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"FORMAT\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			toCharWithNlsParameterFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"PARMS\"", 3, 3, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			toCharWithNlsParameterFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(toCharWithNlsParameterFunctionMetadata);

			var roundFunctionOverload1Metadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramRound, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			roundFunctionOverload1Metadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			roundFunctionOverload1Metadata.AddParameter(new OracleProgramParameterMetadata("\"LEFT\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			roundFunctionOverload1Metadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(roundFunctionOverload1Metadata);

			var roundFunctionOverload2Metadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "ROUND", 2), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			roundFunctionOverload2Metadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			roundFunctionOverload2Metadata.AddParameter(new OracleProgramParameterMetadata("\"LEFT\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			roundFunctionOverload2Metadata.AddParameter(new OracleProgramParameterMetadata("\"RIGHT\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			roundFunctionOverload2Metadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(roundFunctionOverload2Metadata);

			var convertFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramConvert, false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			convertFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			convertFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"SRC\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			convertFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"DESTCSET\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			convertFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"SRCCSET\"", 3, 3, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			convertFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(convertFunctionMetadata);

			var dumpFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "DUMP"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			dumpFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(dumpFunctionMetadata);

			var coalesceFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "COALESCE"), false, false, false, true, false, false, 2, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			coalesceFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(coalesceFunctionMetadata);

			var greatestFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "GREATEST"), false, false, false, true, false, false, 2, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			greatestFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(greatestFunctionMetadata);

			var noParenthesisFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "SESSIONTIMEZONE"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNoParenthesis, true);
			noParenthesisFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(noParenthesisFunctionMetadata);

			var reservedWordFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "ROWNUM"), false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			reservedWordFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			reservedWordFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(reservedWordFunctionMetadata);

			var levelFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramLevel, false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			levelFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			levelFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(levelFunctionMetadata);

			var nvlFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "NVL"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			nvlFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			nvlFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"B1\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			nvlFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"B2\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			nvlFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(nvlFunctionMetadata);

			var hexToRawFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "HEXTORAW"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			hexToRawFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Raw, OracleObjectIdentifier.Empty, false));
			hexToRawFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"C\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			hexToRawFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(hexToRawFunctionMetadata);

			var upperFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "UPPER"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			upperFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			upperFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"CH\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			upperFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(upperFunctionMetadata);

			var sysGuidFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "SYS_GUID"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeParenthesis, true);
			sysGuidFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Raw, OracleObjectIdentifier.Empty, false));
			sysGuidFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(sysGuidFunctionMetadata);

			var nextDayFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "NEXT_DAY"), false, false, false, true, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeParenthesis, true);
			nextDayFunctionMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TerminalValues.Date, OracleObjectIdentifier.Empty, false));
			nextDayFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"LEFT\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Date, OracleObjectIdentifier.Empty, false));
			nextDayFunctionMetadata.AddParameter(new OracleProgramParameterMetadata("\"RIGHT\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			nextDayFunctionMetadata.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(nextDayFunctionMetadata);

			var numberToYearToMonthInterval = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramNumberToYearToMonthInterval, false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeParenthesis, true);
			numberToYearToMonthInterval.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, OracleDatabaseModelBase.BuiltInDataTypeIntervalYearToMonth, OracleObjectIdentifier.Empty, false));
			numberToYearToMonthInterval.AddParameter(new OracleProgramParameterMetadata("\"NUMERATOR\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			numberToYearToMonthInterval.AddParameter(new OracleProgramParameterMetadata("\"UNITS\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			numberToYearToMonthInterval.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(numberToYearToMonthInterval);

			var numberToDayToSecondInterval = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramNumberToDayToSecondInterval, false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeParenthesis, true);
			numberToDayToSecondInterval.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, OracleDatabaseModelBase.BuiltInDataTypeIntervalYearToMonth, OracleObjectIdentifier.Empty, false));
			numberToDayToSecondInterval.AddParameter(new OracleProgramParameterMetadata("\"NUMERATOR\"", 1, 1, 0, ParameterDirection.Input, TerminalValues.Number, OracleObjectIdentifier.Empty, false));
			numberToDayToSecondInterval.AddParameter(new OracleProgramParameterMetadata("\"UNITS\"", 2, 2, 0, ParameterDirection.Input, TerminalValues.Varchar2, OracleObjectIdentifier.Empty, false));
			numberToDayToSecondInterval.Owner = builtInFunctionPackage;
			builtInFunctionPackage.Programs.Add(numberToDayToSecondInterval);
			#endregion

			AllObjectDictionary = AllObjectsInternal.ToDictionary(o => o.FullyQualifiedName, o => o);

			ObjectsInternal = AllObjectDictionary
				.Values.Where(o => o.Owner == SchemaPublic || o.Owner == InitialSchema)
				.ToDictionary(o => OracleObjectIdentifier.Create(o.Owner, o.Name), o => o);

			AddConstraints();

			#region non-schema built-in functions
			var allProgramMetadata = AllObjectsInternal.OfType<IFunctionCollection>().SelectMany(c => c.Programs).ToList();

			var countFunctionAggregateMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(null, null, "COUNT"), false, true, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			countFunctionAggregateMetadata.AddParameter(new OracleProgramParameterMetadata(null, 1, 1, 0, ParameterDirection.Input, "EXPR", OracleObjectIdentifier.Empty, false));
			allProgramMetadata.Add(countFunctionAggregateMetadata);

			var countFunctionAnalyticMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(null, null, "COUNT"), true, false, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			countFunctionAnalyticMetadata.AddParameter(new OracleProgramParameterMetadata(null, 1, 1, 0, ParameterDirection.Input, "EXPR", OracleObjectIdentifier.Empty, false));
			allProgramMetadata.Add(countFunctionAnalyticMetadata);

			var maxFunctionAnalyticMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(null, null, "MAX"), true, false, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			allProgramMetadata.Add(maxFunctionAnalyticMetadata);

			var castFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramCast, false, false, false, true, false, false, 1, 0, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			allProgramMetadata.Add(castFunctionMetadata);

			var maxFunctionAggregateMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(null, null, "MAX"), false, true, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			maxFunctionAggregateMetadata.AddParameter(new OracleProgramParameterMetadata(null, 1, 1, 0, ParameterDirection.Input, "EXPR", OracleObjectIdentifier.Empty, false));
			allProgramMetadata.Add(maxFunctionAggregateMetadata);

			var lastValueFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(null, null, "LAST_VALUE"), true, false, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			allProgramMetadata.Add(lastValueFunctionMetadata);

			var sysDateFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(null, null, "SYSDATE"), false, false, false, false, false, false, 0, 0, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNoParenthesis, true);
			allProgramMetadata.Add(sysDateFunctionMetadata);

			var lnNvlFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramLnNvl, false, false, false, true, false, false, 1, 0, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNoParenthesis, true);
			allProgramMetadata.Add(lnNvlFunctionMetadata);

			var extractFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramExtract, false, false, false, false, false, false, 1, 0, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			allProgramMetadata.Add(extractFunctionMetadata);

			var ratioToReportFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramRatioToReport, true, false, false, false, false, false, 1, 1, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			allProgramMetadata.Add(ratioToReportFunctionMetadata);

			var sysConnectByPathFunctionMetadata = new OracleProgramMetadata(ProgramType.Function, IdentifierBuiltInProgramSysConnectByPath, false, false, false, false, false, false, 2, 2, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, true);
			allProgramMetadata.Add(sysConnectByPathFunctionMetadata);

			AllProgramMetadataInternal = allProgramMetadata.ToLookup(m => m.Identifier);
			NonSchemaBuiltInFunctionMetadataInternal = allProgramMetadata
				.Where(m => String.IsNullOrEmpty(m.Identifier.Owner))
				.ToLookup(m => m.Identifier);

			BuiltInPackageProgramMetadataInternal = allProgramMetadata
				.Where(m => m.Owner != null && m.Owner.FullyQualifiedName == BuiltInFunctionPackageIdentifier)
				.ToLookup(m => m.Identifier);
			#endregion

			UniqueConstraintReferringReferenceConstraintsInternal = BuildUniqueConstraintReferringReferenceConstraintLookup(AllObjectsInternal);

			Instance = new OracleTestDatabaseModel { CurrentSchema = InitialSchema };
		}

		public OracleTestDatabaseModel(bool isInitialized = true)
		{
			IsInitialized = isInitialized;
			AllObjects = isInitialized
				? new Dictionary<OracleObjectIdentifier, OracleSchemaObject>(AllObjectDictionary)
				: new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();
		}

		private static void AddConstraints()
		{
			var projectTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"PROJECT\"")];
			var projectPrimaryKey = new OraclePrimaryKeyConstraint
			                                 {
				                                 FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_PROJECT\""),
				                                 Columns = new[] { "\"PROJECT_ID\"" },
				                                 OwnerObject = projectTable
			                                 };
			
			projectTable.Constraints = new List<OracleConstraint>{ projectPrimaryKey }.AsReadOnly();

			var sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"TARGETGROUP\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleReferenceConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_TARGETGROUP_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						OwnerObject = sourceObject,
						TargetObject = projectTable
					}
				};

			var invoiceTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"INVOICES\"")];
			var invoicePrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_INVOICES\""),
				Columns = new[] { "\"ID\"" },
				OwnerObject = invoiceTable
			};

			invoiceTable.Constraints = new List<OracleConstraint> { invoicePrimaryKey }.AsReadOnly();

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"INVOICELINES\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleReferenceConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_INVOICELINES_INVOICES\""),
						Columns = new[] { "\"INVOICE_ID\"" },
						ReferenceConstraint = invoicePrimaryKey,
						OwnerObject = sourceObject,
						TargetObject = invoiceTable
					}
				}.AsReadOnly();

			var targetGroupTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"TARGETGROUP\"")];
			var targetGroupPrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_TARGETGROUP\""),
				Columns = new[] { "\"TARGETGROUP_ID\"" },
				OwnerObject = targetGroupTable
			};

			targetGroupTable.Constraints.Add(targetGroupPrimaryKey);

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"RESPONDENTBUCKET\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleReferenceConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_RESPONDENTBUCKET_TARGETGROUP\""),
						Columns = new[] { "\"TARGETGROUP_ID\"" },
						ReferenceConstraint = targetGroupPrimaryKey,
						OwnerObject = sourceObject,
						TargetObject = targetGroupTable
					},
					new OracleReferenceConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_RESPONDENTBUCKET_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						OwnerObject = sourceObject,
						TargetObject = projectTable
					}
				};

			var respondentBucketTable = (OracleDataObject)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"RESPONDENTBUCKET\"")];
			var respondentBucketPrimaryKey = new OraclePrimaryKeyConstraint
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PK_RESPONDENTBUCKET\""),
				Columns = new[] { "\"RESPONDENTBUCKET_ID\"" },
				OwnerObject = respondentBucketTable
			};

			respondentBucketTable.Constraints.Add(respondentBucketPrimaryKey);

			sourceObject = (OracleTable)AllObjectDictionary[OracleObjectIdentifier.Create(InitialSchema, "\"SELECTION\"")];
			sourceObject.Constraints =
				new List<OracleConstraint>
				{
					new OracleReferenceConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_SELECTION_RESPONDENTBUCKET\""),
						Columns = new[] { "\"RESPONDENTBUCKET_ID\"" },
						ReferenceConstraint = respondentBucketPrimaryKey,
						OwnerObject = sourceObject,
						TargetObject = respondentBucketTable
					},
					new OracleReferenceConstraint
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"FK_SELECTION_PROJECT\""),
						Columns = new[] { "\"PROJECT_ID\"" },
						ReferenceConstraint = projectPrimaryKey,
						OwnerObject = sourceObject,
						TargetObject = projectTable
					}
				}.AsReadOnly();
		}

		public override ILookup<OracleProgramIdentifier, OracleProgramMetadata> AllProgramMetadata => AllProgramMetadataInternal;

		public override ILookup<OracleObjectIdentifier, OracleReferenceConstraint> UniqueConstraintReferringReferenceConstraints => UniqueConstraintReferringReferenceConstraintsInternal;

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> NonSchemaBuiltInFunctionMetadata => NonSchemaBuiltInFunctionMetadataInternal;

		protected override ILookup<OracleProgramIdentifier, OracleProgramMetadata> BuiltInPackageProgramMetadata => BuiltInPackageProgramMetadataInternal;

		private static readonly HashSet<OracleSchemaObject> AllObjectsInternal = new HashSet<OracleSchemaObject>
		{
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"DUAL\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"DUMMY\"", new OracleColumn { Name = "\"DUMMY\"", CharacterSize = 1, DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 1, dataUnit: DataUnit.Byte), Nullable = true } }
					}
			},
			new OracleView
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"V_$SESSION\""),
				Organization = OrganizationType.NotApplicable,
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"COUNTRY\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"ID\"", new OracleColumn { Name = "\"ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"NAME\"", new OracleColumn { Name = "\"NAME\"", CharacterSize = 50, DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 50, dataUnit: DataUnit.Byte) } }
					}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"ORDERS\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"ID\"", new OracleColumn { Name = "\"ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } }
					}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"INVOICES\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"ID\"", new OracleColumn { Name = "\"ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"DUEDATE\"", new OracleColumn { Name = "\"DUEDATE\"", DataType = BuildPrimitiveDataType(TerminalValues.Date) } }
					},
				PartitionKeyColumns = new List<string> { "\"ID\"" },
				SubPartitionKeyColumns = new List<string> { "\"DUEDATE\"" },
				Partitions =
				{
					{
						"\"P2014\"",
						new OraclePartition
						{
							Name = "\"P2014\"",
							Position = 1,
							SubPartitions =
							{
								{
									"\"P2014_PRIVATE\"",
									new OracleSubPartition
									{
										Name = "\"P2014_PRIVATE\"",
										Position = 1
									}
								},
								{
									"\"P2014_ENTERPRISE\"",
									new OracleSubPartition
									{
										Name = "\"P2014_ENTERPRISE\"",
										Position = 2
									}
								}
							}
						}
					},
					{
						"\"P2015\"",
						new OraclePartition
						{
							Name = "\"P2015\"",
							Position = 1,
							SubPartitions =
							{
								{
									"\"P2015_PRIVATE\"",
									new OracleSubPartition
									{
										Name = "\"P2015_PRIVATE\"",
										Position = 1
									}
								},
								{
									"\"P2015_ENTERPRISE\"",
									new OracleSubPartition
									{
										Name = "\"P2015_ENTERPRISE\"",
										Position = 2
									}
								}
							}
						}
					}
				}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"INVOICELINES\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"ID\"", new OracleColumn { Name = "\"ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"INVOICE_ID\"", new OracleColumn { Name = "\"INVOICE_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"AMOUNT\"", new OracleColumn { Name = "\"AMOUNT\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 20, scale: 2) } },
						{ "\"CORRELATION_VALUE\"", new OracleColumn { Name = "\"CORRELATION_VALUE\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, scale: 5) } },
						{ "\"CaseSensitiveColumn\"", new OracleColumn { Name = "\"CaseSensitiveColumn\"", DataType = BuildPrimitiveDataType(TerminalValues.NVarchar2), CharacterSize = 30 } },
						{ "\"DASH-COLUMN\"", new OracleColumn { Name = "\"DASH-COLUMN\"", DataType = BuildPrimitiveDataType(TerminalValues.Raw, 8) } }
					}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"CaseSensitiveTable\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"CaseSensitiveColumn\"", new OracleColumn { Name = "\"CaseSensitiveColumn\"", DataType = BuildPrimitiveDataType(TerminalValues.Raw, 4000) } },
						{ "\"HIDDEN_COLUMN\"", new OracleColumn { Name = "\"HIDDEN_COLUMN\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 50, dataUnit: DataUnit.Byte), CharacterSize = 50, Hidden = true } },
						{ "\"VIRTUAL_COLUMN\"", new OracleColumn { Name = "\"VIRTUAL_COLUMN\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 50, dataUnit: DataUnit.Byte), CharacterSize = 50, Virtual = true } }
					}
			},
			new OracleView
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"VIEW_INSTANTSEARCH\""),
				Organization = OrganizationType.NotApplicable,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"CUSTOMER_ID\"", new OracleColumn { Name = "\"CUSTOMER_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9) } }
					}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"TARGETGROUP\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"TARGETGROUP_ID\"", new OracleColumn { Name = "\"TARGETGROUP_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"NAME\"", new OracleColumn { Name = "\"NAME\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 50, dataUnit: DataUnit.Byte), CharacterSize = 50 } }
					}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"PROJECT\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"NAME\"", new OracleColumn { Name = "\"NAME\"", CharacterSize = 50, DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 50, dataUnit: DataUnit.Byte) } },
						{ "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } }
					}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"RESPONDENTBUCKET\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"RESPONDENTBUCKET_ID\"", new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"TARGETGROUP_ID\"", new OracleColumn { Name = "\"TARGETGROUP_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0) } },
						{ "\"NAME\"", new OracleColumn { Name = "\"NAME\"", CharacterSize = 50, DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 50, dataUnit: DataUnit.Byte) } }
					}
			},
			new OracleTable
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"SELECTION\""),
				Organization = OrganizationType.Heap,
				Columns =
					new Dictionary<string, OracleColumn>
					{
						{ "\"RESPONDENTBUCKET_ID\"", new OracleColumn { Name = "\"RESPONDENTBUCKET_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0), Nullable = true } },
						{ "\"SELECTION_ID\"", new OracleColumn { Name = "\"SELECTION_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0), Nullable = false } },
						{ "\"PROJECT_ID\"", new OracleColumn { Name = "\"PROJECT_ID\"", DataType = BuildPrimitiveDataType(TerminalValues.Number, precision: 9, scale: 0), Nullable = false } },
						{ "\"NAME\"", new OracleColumn { Name = "\"NAME\"", CharacterSize = 50, Nullable = false, DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 50, dataUnit: DataUnit.Byte), DefaultValue = "\"DBMS_RANDOM\".\"STRING\"('X', 50)" } }
					}
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, PackageDbmsRandom),
				IsValid = true
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"DBMS_CRYPTO\""),
				IsValid = true
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"DBMS_OUTPUT\""),
				IsValid = true
			},
			new OraclePackage
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"DBMS_XPLAN\""),
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
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, PackageBuiltInFunction),
				IsValid = true
			},
			new OracleTypeObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"XMLTYPE\""),
				IsValid = true
			}.WithTypeCode(OracleTypeBase.TypeCodeXml),
			new OracleTypeCollection
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"ODCIARGDESCLIST\""),
				IsValid = true,
				CollectionType = OracleCollectionType.VarryingArray,
				ElementDataType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"ODCIARGDESC\"") },
				UpperBound = 32767
			},
			new OracleTypeCollection
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"DBMS_XPLAN_TYPE_TABLE\""),
				IsValid = true,
				CollectionType = OracleCollectionType.Table,
				ElementDataType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"DBMS_XPLAN_TYPE\"") }
			},
			new OracleTypeObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"DBMS_XPLAN_TYPE\""),
				IsValid = true,
				Attributes =
					new []
					{
						new OracleTypeAttribute { Name = "\"PLAN_TABLE_OUTPUT\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 300, dataUnit: DataUnit.Byte) }
					}
			},
			new OracleTypeCollection
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"ODCIRAWLIST\""),
				IsValid = true,
				CollectionType = OracleCollectionType.VarryingArray,
				ElementDataType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(null, "\"RAW\""), Length = 2000 },
				UpperBound = 32767
			},
			new OracleTypeCollection
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"ODCIDATELIST\""),
				IsValid = true,
				CollectionType = OracleCollectionType.VarryingArray,
				ElementDataType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(null, "\"DATE\"") },
				UpperBound = 32767
			},
			new OracleTypeObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(SchemaSys, "\"ODCIARGDESC\""),
				IsValid = true,
				Attributes =
					new []
					{
						new OracleTypeAttribute { Name = "\"ARGTYPE\"", DataType = BuildPrimitiveDataType(TerminalValues.Number) },
						new OracleTypeAttribute { Name = "\"TABLENAME\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 128, dataUnit: DataUnit.Byte) },
						new OracleTypeAttribute { Name = "\"TABLESCHEMA\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 128, dataUnit: DataUnit.Byte) },
						new OracleTypeAttribute { Name = "\"COLNAME\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 4000, dataUnit: DataUnit.Byte) },
						new OracleTypeAttribute { Name = "\"TABLEPARTITIONLOWER\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 128, dataUnit: DataUnit.Byte) },
						new OracleTypeAttribute { Name = "\"TABLEPARTITIONUPPER\"", DataType = BuildPrimitiveDataType(TerminalValues.Varchar2, 128, dataUnit: DataUnit.Byte) },
						new OracleTypeAttribute { Name = "\"CARDINALITY\"", DataType = BuildPrimitiveDataType(TerminalValues.Number) }
					}
			},
			new OracleTypeObject
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"INVALID_OBJECT_TYPE\"")
			},
			new OracleSequence
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"TEST_SEQ\""),
				IsValid = true,
				CacheSize = 20, CurrentValue = 1234, MinimumValue = 1, MaximumValue = Decimal.MaxValue, CanCycle = true
			},
			new OracleSynonym
			{
				FullyQualifiedName = OracleObjectIdentifier.Create(InitialSchema, "\"SYNONYM_TO_INACESSIBLE_OBJECT\""),
				IsValid = true
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

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjectDictionary;

		private static readonly IDictionary<OracleObjectIdentifier, OracleSchemaObject> ObjectsInternal;

		public override ConnectionStringSettings ConnectionString => ConnectionStringInternal;

		public override string CurrentSchema { get; set; }
		
		public override bool IsInitialized { get; }

		public override bool IsMetadataAvailable { get; } = true;

		public override ICollection<string> Schemas => SchemasInternal;

		public override IReadOnlyDictionary<string, OracleSchema> AllSchemas => AllSchemasInternal;

		public IDictionary<OracleObjectIdentifier, OracleSchemaObject> Objects => ObjectsInternal;

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get; }

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get; } = DatabaseLinksInternal.ToDictionary(l => l.FullyQualifiedName, l => l);

		public override IReadOnlyCollection<string> CharacterSets => CharacterSetsInternal;

		public override IDictionary<int, string> StatisticsKeys => StatisticsKeysInternal;

		public override IDictionary<string, string> SystemParameters { get; } = new Dictionary<string, string>(SystemParametersInternal);

		public override Version Version => TestDatabaseVersion;

		public override Task<ILookup<string, string>> GetContextData(CancellationToken cancellationToken)
		{
			return Task.FromResult(ContextDataInternal);
		}

		public override Task<IReadOnlyList<string>> GetWeekdayNames(CancellationToken cancellationToken)
		{
			return Task.FromResult(WeekdayNamesInternal);
		}

		public override Task Refresh(bool force = false)
		{
			RefreshStarted(this, EventArgs.Empty);
			RefreshCompleted(this, EventArgs.Empty);
			return Task.FromResult<object>(null);
		}

		public override Task Initialize()
		{
			Initialized(this, EventArgs.Empty);
			return Task.FromResult<object>(null);
		}

		public override void Dispose()
		{
			Initialized = null;
			Disconnected = null;
			InitializationFailed = null;
			RefreshStarted = null;
			RefreshStatusChanged = null;
			RefreshCompleted = null;
		}

		public override bool IsFresh => true;

		public override event EventHandler Initialized = delegate { };

		public override event EventHandler<DatabaseModelPasswordArgs> PasswordRequired;

		public override event EventHandler<DatabaseModelConnectionErrorArgs> InitializationFailed;

		public override event EventHandler<DatabaseModelConnectionErrorArgs> Disconnected;

		public override event EventHandler RefreshStarted = delegate { };

		public override event EventHandler<DatabaseModelRefreshStatusChangedArgs> RefreshStatusChanged;

		public override event EventHandler RefreshCompleted = delegate { };

		public override IConnectionAdapter CreateConnectionAdapter()
		{
			return OracleTestConnectionAdapter.Instance;
		}

		public override Task UpdatePartitionDetailsAsync(PartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			SetPartitionDetails(dataModel);

			return Task.FromResult<object>(null);
		}

		public override Task UpdateSubPartitionDetailsAsync(SubPartitionDetailsModel dataModel, CancellationToken cancellationToken)
		{
			SetPartitionDetails(dataModel);

			return Task.FromResult<object>(null);
		}

		public override Task UpdateTableDetailsAsync(OracleObjectIdentifier objectIdentifier, TableDetailsModel dataModel, CancellationToken cancellationToken)
		{
			dataModel.AverageRowSize = 237;
			dataModel.LastAnalyzed = new DateTime(2014, 8, 19, 6, 18, 12);
			dataModel.BlockCount = 544;
			dataModel.RowCount = 8312;
			dataModel.SampleRows = 5512;
			dataModel.Logging = true;
			dataModel.AllocatedBytes = 22546891;
			dataModel.LargeObjectBytes = 1546891;
			dataModel.Compression = "Disabled";
			dataModel.InMemoryCompression = "Disabled";
			dataModel.Organization = "Index";
			dataModel.PartitionKeys = "COLUMN1, COLUMN2";
			dataModel.SubPartitionKeys = "COLUMN3, COLUMN4";
			dataModel.IsTemporary = false;
			dataModel.Comment = "This is a table comment. ";
			dataModel.ParallelDegree = "Default";

			var indexDetails =
				new IndexDetailsModel
				{
					Blocks = 123,
					Bytes = 123456,
					ClusteringFactor = 444,
					Compression = "Enabled",
					DegreeOfParallelism = 2,
					DistinctKeys = 1444,
					IsUnique = false,
					LastAnalyzed = new DateTime(2015, 1, 20, 21, 28, 12),
					LeafBlocks = 114,
					Logging = true,
					Name = "TEST_INDEX",
					Owner = "HUSQVIK",
					PrefixLength = 2,
					Rows = 2000,
					SampleRows = 333,
					Status = "Valid",
					Type = "Normal",
					TablespaceName = "TEST_TABLESPACE",
					Columns =
					{
						new IndexColumnModel { ColumnName = "COLUMN1", SortOrder = SortOrder.Descending },
						new IndexColumnModel { ColumnName = "COLUMN2" }
					}
				};

			var partition1Details =
				new PartitionDetailsModel
				{
					Name = "PARTITION_1"
				};

			SetPartitionDetails(partition1Details);
			SetTablespaceDetails(dataModel.TablespaceDataModel);

			var partition2Details =
				new PartitionDetailsModel
				{
					Name = "PARTITION_2",
					TablespaceName = "TEST_TABLESPACE_2",
					AverageRowSize = 237,
					LastAnalyzed = new DateTime(2015, 2, 22, 16, 22, 14),
					BlockCount = 272,
					RowCount = 4162,
					SampleRows = 4162,
					Compression = "Disabled",
					Logging = true,
					HighValue = "'Partition key 2', 2",
					InMemoryCompression = "Disabled"
				};

			dataModel.IndexDetails.Add(indexDetails);
			dataModel.AddPartition(partition1Details);
			dataModel.AddPartition(partition2Details);

			return Task.FromResult<object>(null);
		}

		private static void SetTablespaceDetails(TablespaceDetailModel dataModel)
		{
			dataModel.BlockSize = 8192;
			dataModel.InitialExtent = 65536;
			dataModel.MinimumExtents = 1;
			dataModel.MaximumExtents = 2147483645;
			dataModel.SegmentMaximumSizeBlocks = 2147483645;
			dataModel.MinimumExtentSizeBytes = 65536;
			dataModel.Status = "Online";
			dataModel.Contents = "Permanent";
			dataModel.Logging = true;
			dataModel.ExtentManagement = "Local";
			dataModel.AllocationType = "System";
			dataModel.SegmentSpaceManagement = "Auto";
			dataModel.DefaultTableCompression = "Disabled";
			dataModel.Retention = "Not Apply";
			dataModel.PredicateEvaluation = "Host";
		}

		private static void SetPartitionDetails(PartitionDetailsModelBase dataModel)
		{
			dataModel.TablespaceName = "TEST_TABLESPACE_1";
			dataModel.AverageRowSize = 237;
			dataModel.LastAnalyzed = new DateTime(2015, 2, 22, 16, 22, 13);
			dataModel.BlockCount = 272;
			dataModel.RowCount = 4162;
			dataModel.SampleRows = 4162;
			dataModel.Compression = "Basic";
			dataModel.HighValue = "'Partition key 1', 2";
			dataModel.InMemoryCompression = "Disabled";
		}

		public override Task UpdateViewDetailsAsync(OracleObjectIdentifier schemaObject, ViewDetailsModel dataModel, CancellationToken cancellationToken)
		{
			dataModel.Comment = "This is a view comment. ";

			var constraint =
				new ConstraintDetailsModel
				{
					DeleteRule = "Cascade",
					IsDeferrable = true,
					IsDeferred = true,
					IsEnabled = true,
					IsValidated = true,
					Reliability = "Enforced",
					LastChange = new DateTime(2015, 1, 20, 21, 31, 12),
					Name = "TEST_CONSTRAINT",
					Owner = "HUSQVIK",
					Type = "Reference integrity"
				};

			dataModel.ConstraintDetails.Add(constraint);

			return Task.FromResult<object>(null);
		}

		public override Task UpdateColumnDetailsAsync(OracleObjectIdentifier objectIdentifier, string columnName, ColumnDetailsModel dataModel, CancellationToken cancellationToken)
		{
			dataModel.DistinctValueCount = 567;
			dataModel.LastAnalyzed = new DateTime(2014, 8, 19, 6, 18, 12);
			dataModel.SampleSize = 12346;
			dataModel.AverageValueSize = 7;
			dataModel.NullValueCount = 1344;
			dataModel.Comment = "This is a column comment. ";
			dataModel.HistogramBucketCount = 6;
			dataModel.HistogramType = "Frequency";

			var previousValue = 0d;
			dataModel.HistogramValues = Enumerable.Repeat(new Random(), dataModel.HistogramBucketCount).Select(r => (previousValue += r.NextDouble())).ToArray();

			return Task.FromResult<object>(null);
		}

		public override Task UpdateUserDetailsAsync(OracleSchemaModel dataModel, CancellationToken cancellationToken)
		{
			dataModel.AccountStatus = "Open";
			dataModel.AuthenticationType = "Password";
			dataModel.DefaultTablespace = "TEST_TABLESPACE";
			dataModel.TemporaryTablespace = "TEMP";
			dataModel.Profile = "DEFAULT";
			dataModel.EditionsEnabled = true;
			dataModel.LastLogin = new DateTime(2015, 7, 13, 22, 47, 30);
			dataModel.LockDate = new DateTime(2015, 7, 13, 22, 47, 31);
			dataModel.ExpiryDate = new DateTime(2015, 7, 13, 22, 47, 32);

			SetTablespaceDetails(dataModel.DefaultTablespaceModel);
			SetTablespaceDetails(dataModel.TemporaryTablespaceModel);

			return Task.FromResult((object)null);
		}

		public override Task<IReadOnlyList<string>> GetRemoteTableColumnsAsync(string databaseLink, OracleObjectIdentifier schemaObject, CancellationToken cancellationToken)
		{
			var remoteColumns =
				new List<string>
				{
					"\"REMOTE_COLUMN1\"",
					"\"RemoteColumn2\""
				};

			return Task.FromResult((IReadOnlyList<string>)remoteColumns.AsReadOnly());
		}

		public override bool HasDbaPrivilege { get; } = false;

	    public override string DatabaseDomainName => CurrentDatabaseDomainNameInternal;

	    public override Task<ExecutionPlanItemCollection> ExplainPlanAsync(StatementExecutionModel executionModel, CancellationToken cancellationToken)
		{
			var rootItem = new ExecutionPlanItem();
			OracleTestConnectionAdapter.SetBasePlanItemData(rootItem);

			var planItemCollection = new ExecutionPlanItemCollection { rootItem };
			planItemCollection.Freeze();

			return Task.FromResult(planItemCollection);
		}

		public override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true)
		{
			return Task.FromResult(SelectionTableCreateScript);
		}

		private static OracleDataType BuildPrimitiveDataType(string typeName, int? length = null, int? precision = null, int? scale = null, DataUnit dataUnit = DataUnit.NotApplicable)
		{
			return
				new OracleDataType
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(null, typeName),
					Length = length,
					Precision = precision,
					Scale = scale,
					Unit = dataUnit
				};
		}
	}
}
