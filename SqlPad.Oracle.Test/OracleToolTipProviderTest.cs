using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Documents;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleToolTipProviderTest
	{
		private readonly OracleToolTipProvider _toolTipProvider = new OracleToolTipProvider();
		private readonly OracleCodeCompletionProvider _codeCompletionProvider = new OracleCodeCompletionProvider();
		private readonly SqlDocumentRepository _documentRepository = TestFixture.CreateDocumentRepository();

		[Test, Apartment(ApartmentState.STA)]
		public void TestColumnTypeToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.SELECTION");
			dataModel.Name.ShouldBe("NAME");
			dataModel.DataType.ShouldBe("VARCHAR2(50 BYTE)");
			dataModel.Nullable.ShouldBe(false);
			dataModel.Virtual.ShouldBe(false);
			dataModel.DefaultValue.ShouldBe("\"DBMS_RANDOM\".\"STRING\"('X', 50)");
			dataModel.DistinctValueCount.ShouldBe(567);
			dataModel.SampleSize.ShouldBe(12346);
			dataModel.AverageValueSize.ShouldBe(7);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
			dataModel.HistogramType.ShouldBe("Frequency");
			dataModel.HistogramBucketCount.ShouldBe(6);
			dataModel.HistogramPoints.Count.ShouldBeGreaterThan(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestColumnTypeToolTipFromObjectReferencedUsingSynonym()
		{
			const string query = "SELECT DUMMY FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<ColumnDetailsModel>();
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestRowIdPsedoColumnTypeToolTip()
		{
			const string query = "SELECT ROWID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipViewColumn>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.SELECTION");
			dataModel.Name.ShouldBe("ROWID");
			dataModel.DataType.ShouldBe("ROWID");
			dataModel.Nullable.ShouldBe(false);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestNullableColumnTypeToolTip()
		{
			const string query = "SELECT RESPONDENTBUCKET_ID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.SELECTION");
			dataModel.Name.ShouldBe("RESPONDENTBUCKET_ID");
			dataModel.DataType.ShouldBe("NUMBER(9)");
			dataModel.Nullable.ShouldBe(true);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestNonAliasedInlineViewColumnToolTip()
		{
			const string query = "SELECT NAME FROM (SELECT NAME FROM SELECTION)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("NAME VARCHAR2(50 BYTE) NOT NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSchemaViewColumnToolTip()
		{
			const string query = "SELECT CUSTOMER_ID FROM VIEW_INSTANTSEARCH";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipViewColumn>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.VIEW_INSTANTSEARCH");
			dataModel.Name.ShouldBe("CUSTOMER_ID");
			dataModel.DataType.ShouldBe("NUMBER(9)");
			dataModel.Comment.ShouldBe("This is a column comment. ");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestDecimalColumnTypeToolTip()
		{
			const string query = "SELECT AMOUNT FROM INVOICELINES";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.INVOICELINES");
			dataModel.Name.ShouldBe("AMOUNT");
			dataModel.DataType.ShouldBe("NUMBER(20, 2)");
			dataModel.Nullable.ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestScaleWithoutPrecisionColumnTypeToolTip()
		{
			const string query = "SELECT CORRELATION_VALUE FROM INVOICELINES";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.INVOICELINES");
			dataModel.Name.ShouldBe("CORRELATION_VALUE");
			dataModel.DataType.ShouldBe("NUMBER(*, 5)");
			dataModel.Nullable.ShouldBe(false);
			dataModel.Comment.ShouldBe("This is a column comment. ");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestRecursiveSearchColumnReferenceToolTip()
		{
			const string query =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 5
)
SEARCH DEPTH FIRST BY VAL SET SEQ#
SELECT * FROM CTE";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 119);

			toolTip.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("CTE.VAL NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestObjectReferenceToolTipInRecursiveAnchorQueryBlock()
		{
			const string query =
@"WITH CTE(VAL) AS (
	SELECT 1 FROM DUAL
	UNION ALL
	SELECT VAL + 1 FROM CTE WHERE VAL < 5
)
SELECT * FROM CTE";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 35);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableObjectToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
			dataModel.AverageRowSize.ShouldBe(237);
			dataModel.BlockCount.ShouldBe(544);
			dataModel.AllocatedBytes.ShouldBe(22546891);
			dataModel.LargeObjectBytes.ShouldBe(1546891);
			dataModel.ClusterName.ShouldBe(null);
			dataModel.Compression.ShouldBe("Disabled");
			dataModel.ParallelDegree.ShouldBe("Default");
			dataModel.PartitionKeys.ShouldBe("COLUMN1, COLUMN2");
			dataModel.SubPartitionKeys.ShouldBe("COLUMN3, COLUMN4");
			dataModel.IsTemporary.ShouldBe(false);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
			dataModel.Organization.ShouldBe("Index");
			dataModel.RowCount.ShouldBe(8312);
			dataModel.SampleRows.ShouldBe(5512);
			dataModel.Logging.ShouldBe(true);

			dataModel.PartitionCount.ShouldBe(2);
			dataModel.VisiblePartitionDetails.Count.ShouldBe(2);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableObjectToolTipUsingSynonym()
		{
			const string query = "SELECT NAME FROM SYNONYM_TO_SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SYNONYM_TO_SELECTION (Synonym) => HUSQVIK.SELECTION (Table)");
			dataModel.AverageRowSize.ShouldBe(237);
			dataModel.BlockCount.ShouldBe(544);
			dataModel.ClusterName.ShouldBe(null);
			dataModel.Compression.ShouldBe("Disabled");
			dataModel.PartitionKeys.ShouldBe("COLUMN1, COLUMN2");
			dataModel.SubPartitionKeys.ShouldBe("COLUMN3, COLUMN4");
			dataModel.IsTemporary.ShouldBe(false);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
			dataModel.Organization.ShouldBe("Index");
			dataModel.InMemoryCompression.ShouldBe("Disabled");
			dataModel.RowCount.ShouldBe(8312);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableObjectToolTipInUpdateStatementMainObjectReference()
		{
			const string query = "UPDATE SELECTION SET NAME = 'Dummy selection' WHERE SELECTION_ID = 0";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableObjectToolTipInDeleteStatementMainObjectReference()
		{
			const string query = "DELETE SELECTION WHERE SELECTION_ID = 0";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableObjectToolTipInInsertStatementMainObjectReference()
		{
			const string query = "INSERT INTO HUSQVIK.SYNONYM_TO_SELECTION(NAME) VALUES ('Dummy selection')";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 23);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SYNONYM_TO_SELECTION (Synonym) => HUSQVIK.SELECTION (Table)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableObjectToolTipInSelectListWithInvalidColumn()
		{
			const string query = "SELECT SELECTION.INVALID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableObjectToolTipInUpdateSetClause()
		{
			const string query = "UPDATE HUSQVIK.SELECTION SET SELECTION.PROJECT_ID = HUSQVIK.SELECTION.PROJECT_ID WHERE 1 = 0";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 32);

			toolTip.Control.ShouldBeAssignableTo<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
			dataModel.Comment.ShouldBe("This is a table comment. ");
			dataModel.IndexDetails.Count.ShouldBeGreaterThan(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSynonymReferencedObjectToolTip()
		{
			const string query = "SELECT * FROM V$SESSION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			toolTip.Control.DataContext.ShouldBeAssignableTo<ObjectDetailsModel>();
			var dataModel = (ObjectDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".V$SESSION (Synonym) => SYS.V_$SESSION (View)");
			dataModel.Comment.ShouldBe("V$SESSION displays session information for each current session.");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestObjectSemanticErrorToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION, RESPONDENTBUCKET";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (SELECTION, RESPONDENTBUCKET)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestFunctionSemanticErrorToolTip()
		{
			const string query = "SELECT TO_CHAR FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Invalid parameter count");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAmbiguousColumnNameFromSingleObjectToolTip()
		{
			const string query = "SELECT * FROM (SELECT 1 NAME, 2 NAME, 3 VAL, 4 VAL FROM DUAL)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (NAME, VAL)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAmbiguousColumnNameInMultipleObjectAsteriskReferences()
		{
			const string query = "SELECT T1.*, T2.* FROM (SELECT 1 C1, 2 C1 FROM DUAL) T1, (SELECT 1 D1, 2 D1 FROM DUAL) T2";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (C1)");

			toolTip = _toolTipProvider.GetToolTip(_documentRepository, 17);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (D1)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAmbiguousColumnNameFromSingleObjectWithoutAliasToolTip()
		{
			const string query = "SELECT DUMMY FROM (SELECT DUAL.DUMMY, X.DUMMY FROM DUAL, DUAL X)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestFunctionIdentifierToolTip()
		{
			const string query = "SELECT COALESCE(NULL, 1) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipProgram>();
			var metadata = (OracleProgramMetadata)toolTip.Control.DataContext;
			metadata.Identifier.FullyQualifiedIdentifier.ShouldBe("SYS.STANDARD.COALESCE");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPackageIdentifierToolTip()
		{
			const string query = "SELECT DBMS_RANDOM.STRING('X', 16) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)((ToolTipView)toolTip.Control).DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".DBMS_RANDOM (Synonym) => SYS.DBMS_RANDOM (Package)");
			dataModel.Comment.ShouldBe("The DBMS_RANDOM package provides a built-in random number generator. DBMS_RANDOM is not intended for cryptography.");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTypeIdentifierToolTip()
		{
			const string query = "SELECT XMLTYPE('<Root/>') FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Object Type)");
			dataModel.Object.ShouldNotBe(null);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSequenceIdentifierToolTip()
		{
			const string query = "SELECT SYNONYM_TO_TEST_SEQ.CURRVAL FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeAssignableTo<ToolTipSequence>();
			var toolTipSequence = (ToolTipSequence)toolTip.Control;
			toolTipSequence.LabelTitle.Text.ShouldBe("HUSQVIK.SYNONYM_TO_TEST_SEQ (Synonym) => HUSQVIK.TEST_SEQ (Sequence)");

		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSequenceColumnIdentifierToolTip()
		{
			const string query = "SELECT TEST_SEQ.CURRVAL FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 17);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INTEGER NOT NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTypeWithCompilationErrorToolTip()
		{
			const string query = "SELECT INVALID_OBJECT_TYPE(DUMMY) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Object is invalid or unusable");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestDatabaseLinkToolTip()
		{
			const string query = "SELECT SQLPAD_FUNCTION@HQ_PDB_LOOPBACK(5) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);

			toolTip.Control.ShouldBeAssignableTo<ToolTipDatabaseLink>();
			var databaseLink = (OracleDatabaseLink)((ToolTipDatabaseLink)toolTip.Control).DataContext;
			databaseLink.FullyQualifiedName.ShouldBe(OracleObjectIdentifier.Create("\"PUBLIC\"", "HQ_PDB_LOOPBACK"));
			databaseLink.Host.ShouldBe("localhost:1521/hq_pdb");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableCollectionExpressionColumnQualifierToolTip()
		{
			const string query = "SELECT COLUMN_VALUE FROM TABLE(SYS.ODCIRAWLIST(NULL)) TEST_TABLE";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("TEST_TABLE.COLUMN_VALUE RAW(2000) NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPackageToolTipWithinTableCollectionExpression()
		{
			const string query = "SELECT COLUMN_VALUE FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR()) TEST_TABLE";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 40);

			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)((ToolTipView)toolTip.Control).DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".DBMS_XPLAN (Synonym) => SYS.DBMS_XPLAN (Package)");
			dataModel.Comment.ShouldBe("The DBMS_XPLAN package provides an easy way to display the output of the EXPLAIN PLAN command in several, predefined formats. You can also use the DBMS_XPLAN package to display the plan of a statement stored in the Automatic Workload Repository (AWR) or stored in a SQL tuning set. It further provides a way to display the SQL execution plan and SQL execution runtime statistics for cached SQL cursors based on the information stored in the V$SQL_PLAN and V$SQL_PLAN_STATISTICS_ALL fixed views. Finally, it displays plans from a SQL plan baseline.");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestInlineViewToolTip()
		{
			const string query = "SELECT INLINEVIEW.DUMMY FROM (SELECT DUAL.DUMMY FROM DUAL) INLINEVIEW";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 16);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INLINEVIEW (Inline View)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestXmlTableColumnQualifierToolTip()
		{
			const string query = "SELECT VALUE FROM XMLTABLE('/root' PASSING XMLTYPE('<root>value</root>') COLUMNS VALUE VARCHAR2(10) PATH '.') XML_DATA";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("XML_DATA.VALUE VARCHAR2(10) NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTableCollectionToolTip()
		{
			const string query = "SELECT table_collection.* FROM TABLE (dbms_xplan.display_cursor) table_collection";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("table_collection (Table collection)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestFunctionIdentifierOverDatabaseLinkToolTip()
		{
			const string query = "SELECT SQLPAD_FUNCTION@UNDEFINED_DB_LINK FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 12);

			toolTip.ShouldBe(null);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToolTipBeforeDatabaseModelLoaded()
		{
			const string query = "SELECT S.* FROM SELECTION S";
			_documentRepository.UpdateStatements(query);

			var databaseModel = new OracleTestDatabaseModel();
			databaseModel.AllObjects.Clear();
			var documentStore = new SqlDocumentRepository(OracleSqlParser.Instance, new OracleStatementValidator(), databaseModel, query);
			var toolTip = _toolTipProvider.GetToolTip(documentStore, 7);

			toolTip.ShouldBe(null);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestFunctionOverloadsToolTip()
		{
			const string query = "SELECT SQLPAD.SQLPAD_FUNCTION() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(_documentRepository, 30);
			var functionOverloadList = new ProgramOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeAssignableTo(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("HUSQVIK.SQLPAD.SQLPAD_FUNCTION(P: NUMBER) RETURN: NUMBER");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestObjectTypeConstructorToolTip()
		{
			const string query = "SELECT SYS.ODCIARGDESC() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(_documentRepository, 23);
			var functionOverloadList = new ProgramOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeAssignableTo(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.ODCIARGDESC(ARGTYPE: NUMBER, TABLENAME: VARCHAR2, TABLESCHEMA: VARCHAR2, COLNAME: VARCHAR2, TABLEPARTITIONLOWER: VARCHAR2, TABLEPARTITIONUPPER: VARCHAR2, CARDINALITY: NUMBER) RETURN: SYS.ODCIARGDESC");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestCollectionTypeConstructorToolTip()
		{
			const string query = "SELECT SYS.ODCIARGDESCLIST() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(_documentRepository, 27);
			var functionOverloadList = new ProgramOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeAssignableTo(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.ODCIARGDESCLIST([array of SYS.ODCIARGDESC]) RETURN: SYS.ODCIARGDESCLIST");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPrimitiveTypeCollectionTypeConstructorToolTip()
		{
			const string query = "SELECT SYS.ODCIRAWLIST() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(_documentRepository, 23);
			var functionOverloadList = new ProgramOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeAssignableTo(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.ODCIRAWLIST([array of RAW]) RETURN: SYS.ODCIRAWLIST");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestFunctionOverloadWithFunctionReturningCollection()
		{
			const string query = "SELECT DBMS_XPLAN.DISPLAY_CURSOR() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(_documentRepository, 33);
			var functionOverloadList = new ProgramOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeAssignableTo(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.DBMS_XPLAN.DISPLAY_CURSOR([SQL_ID: VARCHAR2], [CURSOR_CHILD_NUMBER: NUMBER], [FORMAT: VARCHAR2]) RETURN: SYS.DBMS_XPLAN_TYPE_TABLE");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestFunctionOverloadWithFunctionWithDifferentParameterTypes()
		{
			const string query = "SELECT TESTFUNC() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(_documentRepository, 16);
			var functionOverloadList = new ProgramOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeAssignableTo(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("HUSQVIK.TESTFUNC(PARAM1: NUMBER, PARAM2 (IN/OUT): RAW, PARAM3 (OUT): VARCHAR2) RETURN: NUMBER");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestNoParenthesisFunctionWithParentheses()
		{
			const string query = "SELECT SESSIONTIMEZONE() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 23);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Non-parenthesis function");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToolTipOverInvalidDateLiteral()
		{
			const string query = "SELECT DATE'' FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 12);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe(OracleSemanticErrorTooltipText.InvalidDateLiteral);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToolTipOverInvalidTimestampLiteral()
		{
			const string query = "SELECT TIMESTAMP'' FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 17);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe(OracleSemanticErrorTooltipText.InvalidTimestampLiteral);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestNonQualifiedColumnReferenceOverDatabaseLink()
		{
			const string query = "SELECT NAME FROM SELECTION@HQ_PDB_LOOPBACK, DUAL@HQ_PDB_LOOPBACK";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe(OracleSuggestionType.PotentialDatabaseLink);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestDatabaseLinkToolTipOverSpecialCharacter()
		{
			const string query = "SELECT * FROM dual@HQ_PDB_LOOPBACK@LOOPBACK";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 34);

			var databaseLink = (OracleDatabaseLink)((ToolTipDatabaseLink)toolTip.Control).DataContext;
			databaseLink.FullyQualifiedName.ShouldBe(OracleObjectIdentifier.Create("\"PUBLIC\"", "HQ_PDB_LOOPBACK"));
			databaseLink.Host.ShouldBe("localhost:1521/hq_pdb");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestFunctionOverloadsToolTipNotShowForNonSchemaFunctions()
		{
			const string query = "SELECT MAX(DUMMY) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveProgramOverloads(_documentRepository, 9);
			var toolTip = new ProgramOverloadList { FunctionOverloads = functionOverloads };
			toolTip.ViewOverloads.Items.Count.ShouldBe(0);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToolTipInInsertValuesClause()
		{
			const string query = "INSERT INTO SELECTION (SELECTION_ID, SELECTIONNAME, RESPONDENTBUCKET_ID) VALUES (SQLPAD_FUNCTION, XMLTYPE(), SYNONYM_TO_TEST_SEQ.NEXTVAL)";
			_documentRepository.UpdateStatements(query);

			var toolTipFunction = _toolTipProvider.GetToolTip(_documentRepository, 84);

			toolTipFunction.Control.ShouldBeAssignableTo<ToolTipProgram>();
			var metadata = (OracleProgramMetadata)toolTipFunction.Control.DataContext;
			metadata.Identifier.FullyQualifiedIdentifier.ShouldBe("HUSQVIK.SQLPAD_FUNCTION");

			var toolTipType = _toolTipProvider.GetToolTip(_documentRepository, 101);
			toolTipType.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)toolTipType.Control.DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Object Type)");
			dataModel.Object.ShouldNotBe(null);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 112);
			toolTip.Control.ShouldBeAssignableTo<ToolTipSequence>();
			var toolTipSequence = (ToolTipSequence)toolTip.Control;
			toolTipSequence.LabelTitle.Text.ShouldBe("HUSQVIK.SYNONYM_TO_TEST_SEQ (Synonym) => HUSQVIK.TEST_SEQ (Sequence)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestVarryingArrayConstructorToolTip()
		{
			const string query = "SELECT * FROM TABLE(SYS.ODCIRAWLIST(NULL))";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("SYS.ODCIRAWLIST (Object Varrying Array)");
			dataModel.Object.ShouldNotBe(null);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestObjectTableConstructorToolTip()
		{
			const string query = "SELECT * FROM TABLE(SYS.DBMS_XPLAN_TYPE_TABLE())";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("SYS.DBMS_XPLAN_TYPE_TABLE (Object Table)");
			dataModel.Object.ShouldNotBe(null);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSchemaToolTip()
		{
			const string query = "SELECT * FROM HUSQVIK.SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 14);
			toolTip.Control.ShouldBeAssignableTo<ToolTipSchema>();
			var schema = (OracleSchemaModel)toolTip.Control.DataContext;
			schema.Schema.Name.ShouldBe("\"HUSQVIK\"");
			schema.Schema.Created.ShouldBe(new DateTime(2014, 9, 28, 0, 25, 43));
			schema.AuthenticationType.ShouldBe("Password");
			schema.AccountStatus.ShouldBe("Open");
			schema.Profile.ShouldBe("DEFAULT");
			schema.DefaultTablespace.ShouldBe("TEST_TABLESPACE");
			schema.TemporaryTablespace.ShouldBe("TEMP");
			schema.EditionsEnabled.ShouldBe(true);
			schema.LastLogin.ShouldBe(new DateTime(2015, 7, 13, 22, 47, 30));
			schema.LockDate.ShouldBe(new DateTime(2015, 7, 13, 22, 47, 31));
			schema.ExpiryDate.ShouldBe(new DateTime(2015, 7, 13, 22, 47, 32));
		}

		public class ProgramTypeConverterTests
		{
			private readonly ProgramTypeConverter _converter = new ProgramTypeConverter();

			[Test]
			public void TestNullValue()
			{
				Convert(null).ShouldBe(ValueConverterBase.ValueNotAvailable);
			}

			[Test]
			public void TestSqlFunctiom()
			{
				Convert(BuildProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues(null, null, "COUNT"), true)).ShouldBe("SQL function");
			}

			[Test]
			public void TestBuiltInPackageFunctiom()
			{
				Convert(BuildProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("SYS", "STANDARD", "TRUNC"), true)).ShouldBe("Built-in package function");
			}

			[Test]
			public void TestSchemaFunction()
			{
				Convert(BuildProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("HUSQVIK", null, "SQLPAD_FUNCTION"), false)).ShouldBe("Schema function");
			}

			[Test]
			public void TestSchemaProcedure()
			{
				Convert(BuildProgramMetadata(ProgramType.Procedure, OracleProgramIdentifier.CreateFromValues("HUSQVIK", null, "SQLPAD_PROCEDURE"), false)).ShouldBe("Schema procedure");
			}

			[Test]
			public void TestPackageFunction()
			{
				Convert(BuildProgramMetadata(ProgramType.Function, OracleProgramIdentifier.CreateFromValues("HUSQVIK", "SQLPAD", "SQLPAD_FUNCTION"), false)).ShouldBe("Package function");
			}

			[Test]
			public void TestPackageProcedure()
			{
				Convert(BuildProgramMetadata(ProgramType.Procedure, OracleProgramIdentifier.CreateFromValues("HUSQVIK", "SQLPAD", "SQLPAD_PROCEDURE"), false)).ShouldBe("Package procedure");
			}

			private static OracleProgramMetadata BuildProgramMetadata(ProgramType programType, OracleProgramIdentifier programIdentifier, bool isBuiltIn)
			{
				return new OracleProgramMetadata(programType, programIdentifier, false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, isBuiltIn);
			}

			private object Convert(OracleProgramMetadata metadata)
			{
				return _converter.Convert(metadata, typeof(string), null, CultureInfo.InvariantCulture);
			}
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAsteriskTooltip()
		{
			const string query = "SELECT * FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeAssignableTo<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(4);
			columns[0].Name.ShouldBe("RESPONDENTBUCKET_ID");
			columns[0].FullTypeName.ShouldBe("NUMBER(9)");
			columns[0].RowSourceName.ShouldBe("SELECTION");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[1].Name.ShouldBe("SELECTION_ID");
			columns[1].FullTypeName.ShouldBe("NUMBER(9)");
			columns[2].Name.ShouldBe("PROJECT_ID");
			columns[2].FullTypeName.ShouldBe("NUMBER(9)");
			columns[3].Name.ShouldBe("NAME");
			columns[3].FullTypeName.ShouldBe("VARCHAR2(50 BYTE)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAsteriskTooltipRowSourceCase()
		{
			const string query = "SELECT * FROM dual";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeAssignableTo<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("DUMMY");
			columns[0].FullTypeName.ShouldBe("VARCHAR2(1 BYTE)");
			columns[0].RowSourceName.ShouldBe("DUAL");
			columns[0].ColumnIndex.ShouldBe(1);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAsteriskTooltipWithDoubleCommonTableExpressionDefinition()
		{
			const string query = @"WITH CTE AS (SELECT 1 C1 FROM DUAL), CTE AS (SELECT 1 C2 FROM DUAL) SELECT * FROM CTE";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 75);
			toolTip.Control.ShouldBeAssignableTo<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("C1");
			columns[0].FullTypeName.ShouldBe("NUMBER");
			columns[0].RowSourceName.ShouldBe("CTE");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[0].Nullable.ShouldBe(false);
		}
		
		[Test, Apartment(ApartmentState.STA)]
		public void TestAsteriskTooltipWithDoubleCommonTableExpressionDefinitionInDifferentSubqueries()
		{
			const string query = @"WITH CTE AS (SELECT 1 C1 FROM DUAL) SELECT * FROM (WITH CTE AS (SELECT 1 C2 FROM DUAL) SELECT * FROM CTE) SUBQUERY";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 43);
			toolTip.Control.ShouldBeAssignableTo<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("C2");
			columns[0].FullTypeName.ShouldBe("NUMBER");
			columns[0].RowSourceName.ShouldBe("SUBQUERY");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[0].Nullable.ShouldBe(false);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAsteriskTooltipWithUnnamedColumn()
		{
			const string query = @"SELECT * FROM (SELECT nvl(""column name"", 0) FROM dual) T";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeAssignableTo<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("NVL(\"COLUMNNAME\",0)");
			columns[0].FullTypeName.ShouldBe(String.Empty);
			columns[0].RowSourceName.ShouldBe("T");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[0].Nullable.ShouldBe(true);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAsteriskTooltipWithFunctionWithCursorParameter()
		{
			const string query = @"SELECT * FROM TABLE(SQLPAD.CURSOR_FUNCTION(0, CURSOR(SELECT * FROM SELECTION), NULL)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeAssignableTo<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("PLAN_TABLE_OUTPUT");
			columns[0].FullTypeName.ShouldBe("VARCHAR2(300 BYTE)");
			columns[0].RowSourceName.ShouldBe(String.Empty);
			columns[0].ColumnIndex.ShouldBe(1);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestAsteriskTooltipOnQualifiedReferenceToPivotTable()
		{
			const string query =
@"SELECT
	PIVOT_TABLE.*
FROM (
		(SELECT * FROM SELECTION)
		PIVOT (
			COUNT(PROJECT_ID) PROJECT_COUNT
			FOR (PROJECT_ID)
				IN (0)
		) PIVOT_TABLE
	)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 21);
			toolTip.Control.ShouldBeAssignableTo<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(4);
			columns[0].Name.ShouldBe("RESPONDENTBUCKET_ID");
			columns[0].FullTypeName.ShouldBe("NUMBER(9)");
			columns[0].RowSourceName.ShouldBe("PIVOT_TABLE");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[1].Name.ShouldBe("SELECTION_ID");
			columns[1].FullTypeName.ShouldBe("NUMBER(9)");
			columns[1].RowSourceName.ShouldBe("PIVOT_TABLE");
			columns[1].ColumnIndex.ShouldBe(2);
			columns[2].Name.ShouldBe("NAME");
			columns[2].FullTypeName.ShouldBe("VARCHAR2(50 BYTE)");
			columns[2].RowSourceName.ShouldBe("PIVOT_TABLE");
			columns[2].ColumnIndex.ShouldBe(3);
			columns[3].Name.ShouldBe("\"0_PROJECT_COUNT\"");
			columns[3].FullTypeName.ShouldBe(String.Empty);
			columns[3].RowSourceName.ShouldBe("PIVOT_TABLE");
			columns[3].ColumnIndex.ShouldBe(4);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExplicitPartitionTooltip()
		{
			const string query = "SELECT * FROM INVOICES PARTITION (P2015)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 34);
			toolTip.Control.ShouldBeAssignableTo<ToolTipPartition>();
			var toolTipPartition = (ToolTipPartition)toolTip.Control;
			toolTipPartition.DataContext.ShouldBeAssignableTo<PartitionDetailsModel>();
			var dataModel = (PartitionDetailsModel)toolTipPartition.DataContext;
			dataModel.Name.ShouldBe("P2015");
			dataModel.Owner.ShouldBe(OracleObjectIdentifier.Create("HUSQVIK", "INVOICES"));
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestExplicitSubPartitionTooltip()
		{
			const string query = "SELECT * FROM INVOICES SUBPARTITION (P2015_ENTERPRISE)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 37);
			toolTip.Control.ShouldBeAssignableTo<ToolTipPartition>();
			var toolTipPartition = (ToolTipPartition)toolTip.Control;
			toolTipPartition.DataContext.ShouldBeAssignableTo<SubPartitionDetailsModel>();
			var dataModel = (SubPartitionDetailsModel)toolTipPartition.DataContext;
			dataModel.Name.ShouldBe("P2015_ENTERPRISE");
			dataModel.Owner.ShouldBe(OracleObjectIdentifier.Create("HUSQVIK", "INVOICES"));
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestDataTypeTooltip()
		{
			const string query = "SELECT CAST(NULL AS SYS.ODCIRAWLIST) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SYS.ODCIRAWLIST (Object Varrying Array)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestParameterTooltip()
		{
			const string query = "SELECT SQLPAD.SQLPAD_FUNCTION(p => NULL) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("P: NUMBER");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestProgramTooltipInRecursiveAnchorQueryBlock()
		{
			const string query =
@"WITH sampleData(c) AS (
	SELECT ROUND(0, 0) FROM DUAL UNION ALL
	SELECT c FROM sampleData
)
SELECT * FROM sampleData";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 33);
			toolTip.Control.ShouldBeAssignableTo<ToolTipProgram>();
			var metadata = (OracleProgramMetadata)toolTip.Control.DataContext;
			metadata.Identifier.FullyQualifiedIdentifier.ShouldBe("SYS.STANDARD.ROUND");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPackageTooltipWhenMetadataNotResolved()
		{
			const string query = @"SELECT dbms_output.put_line('value') FROM dual";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)((ToolTipView)toolTip.Control).DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".DBMS_OUTPUT (Synonym) => SYS.DBMS_OUTPUT (Package)");
			dataModel.Comment.ShouldBe("The DBMS_OUTPUT package enables you to send messages from stored procedures, packages, and triggers. The package is especially useful for displaying PL/SQL debugging information.");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTypeTooltipInRecursiveAnchorQueryBlock()
		{
			const string query =
@"WITH sampleData(c) AS (
	SELECT XMLTYPE('<root/>') FROM DUAL UNION ALL
	SELECT c FROM sampleData
)
SELECT * FROM sampleData";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 33);
			toolTip.Control.ShouldBeAssignableTo<ToolTipView>();
			var dataModel = (ObjectDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Object Type)");
			dataModel.Object.ShouldNotBe(null);
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPlSqlVariableReferenceTooltip()
		{
			const string plSqlCode =
@"DECLARE
    test_variable VARCHAR2(255) := 'This' || ' is ' || 'value';
BEGIN
    test_variable := NULL;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 85);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Variable TEST_VARIABLE: VARCHAR2(255) NULL");
		}


		[Test, Apartment(ApartmentState.STA)]
		public void TestPlSqlConstantReferenceTooltip()
		{
			const string plSqlCode =
@"DECLARE
    test_constant CONSTANT VARCHAR2(255) NOT NULL := 'This' || ' is ' || 'value';
	test_variable VARCHAR2(255);
BEGIN
    test_variable := test_constant;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 151);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Constant TEST_CONSTANT: VARCHAR2(255) NOT NULL = 'This' || ' is ' || 'value'");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPlSqlCursorReferenceTooltipInNestedPlSqlBlock()
		{
			const string plSqlCode =
@"DECLARE
	CURSOR test_cursor IS SELECT dummy FROM dual;
BEGIN
	BEGIN
		OPEN test_cursor;
	END;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 79);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Cursor TEST_CURSOR: SELECT dummy FROM dual");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPlSqlExceptionReferenceTooltip()
		{
			const string plSqlCode =
@"DECLARE
    test_exception EXCEPTION;
BEGIN
    RAISE test_exception;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 57);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Exception TEST_EXCEPTION");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestPlSqlExceptionReferenceWithExceptionInitPragmaTooltip()
		{
			const string plSqlCode =
@"DECLARE
	deadlock_detected EXCEPTION;
	PRAGMA EXCEPTION_INIT(deadlock_detected, -60);
BEGIN
    NULL;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 63);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Exception DEADLOCK_DETECTED (Error code -60)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestSqlToolTipInNestedPlSqlBlock()
		{
			const string plSqlCode =
@"BEGIN
	BEGIN
		FOR implicit_cursor IN (SELECT dummy FROM dual) LOOP
			NULL;
		END LOOP;
	END;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 59);
			toolTip.ShouldNotBe(null);
		}


		[Test, Apartment(ApartmentState.STA)]
		public void TestToolTipOverTypedVariable()
		{
			const string plSqlCode =
@"BEGIN
	FOR i IN 1..10 LOOP
		dbms_output.put_line(i);
	END LOOP;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 52);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Variable I: BINARY_INTEGER NOT NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestToolTipOverImplicitCursorColumn()
		{
			const string plSqlCode =
@"BEGIN
	FOR implicit_cursor IN (SELECT dummy FROM dual) LOOP
		dbms_output.put_line(implicit_cursor.dummy);
	END LOOP;
END;";

			_documentRepository.UpdateStatements(plSqlCode);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 101);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Cursor column IMPLICIT_CURSOR.DUMMY: VARCHAR2(1 BYTE)");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverExplicitColumnListInCommonTableExpression()
		{
			const string query =
@"WITH data(value) AS (
	SELECT 1 FROM DUAL UNION ALL
	SELECT 2 FROM DUAL
)
SELECT * FROM data";

			_documentRepository.UpdateStatements(query);

			Should.NotThrow(() => _toolTipProvider.GetToolTip(_documentRepository, 12));
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverHierarchicalPseudocolumn()
		{
			const string query =
@"WITH edges (child_id, parent_id) AS (
    SELECT 2, 1 FROM DUAL UNION ALL
    SELECT 3, 2 FROM DUAL UNION ALL
    SELECT 4, 2 FROM DUAL UNION ALL
    SELECT 5, 4 FROM DUAL
)
SELECT
    child_id,
    CONNECT_BY_ROOT parent_id,
    CONNECT_BY_ISLEAF,
    CONNECT_BY_ISCYCLE
FROM
    edges
CONNECT BY NOCYCLE
    PRIOR child_id = parent_id";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 239);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("CONNECT_BY_ISLEAF NUMBER NOT NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverColumnInAnchorQueryBlock()
		{
			const string query =
@"WITH data AS (SELECT 1 c1 FROM dual),
generator (c2) AS (
	SELECT c1 FROM data UNION ALL
	SELECT c2 + 1 FROM generator WHERE c2 < 3
)
SELECT * FROM generator";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 68);
			toolTip.Control.ShouldBeAssignableTo<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("DATA.C1 NUMBER NOT NULL");
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverNamedParameterCombinedWithParameterlessFunction()
		{
			const string query = @"SELECT DBMS_RANDOM.VALUE(low => 0, high => 5) FROM DUAL WHERE ROWNUM = 1";

			_documentRepository.UpdateStatements(query);

			Should.NotThrow(() => _toolTipProvider.GetToolTip(_documentRepository, 25));
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverSchemaWithinPlSqlVariableDeclaration()
		{
			const string query =
@"DECLARE
	variable2 sys.odcirawlist := sys.odcirawlist(0);
BEGIN
	NULL;
END;";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);
			toolTip.Control.ShouldBeAssignableTo<ToolTipSchema>();
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverSchemaWithinPlSqlVariableDeclarationValue()
		{
			const string query =
@"DECLARE
	variable2 sys.odcirawlist := sys.odcirawlist(0);
BEGIN
	NULL;
END;";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 39);
			toolTip.Control.ShouldBeAssignableTo<ToolTipSchema>();
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverNonExistingColumn()
		{
			const string query = @"SELECT non_existing FROM dual";

			_documentRepository.UpdateStatements(query);

			Should.NotThrow(() => _toolTipProvider.GetToolTip(_documentRepository, 7));
		}

		[Test, Apartment(ApartmentState.STA)]
		public void TestTooltipOverAggregateFunctionWithinPivotClause()
		{
			const string query =
@"SELECT
	NULL
FROM
	dual
PIVOT (
    max(dummy)
    FOR (dummy) IN ('X')
)";

			_documentRepository.UpdateStatements(query);

			Should.NotThrow(() => _toolTipProvider.GetToolTip(_documentRepository, 43));
		}

		private static string GetTextFromTextBlock(TextBlock textBlock)
		{
			var inlines = textBlock.Inlines.Select(GetTextFromInline);
			return String.Concat(inlines);
		}

		private static string GetTextFromInline(Inline inline)
		{
			var run = inline as Run;
			if (run != null)
			{
				return run.Text;
			}
			
			var bold = (Bold)inline;
			return String.Concat(bold.Inlines.Select(GetTextFromInline));
		}
	}
}
