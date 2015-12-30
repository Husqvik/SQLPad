using System;
using System.Globalization;
using System.Linq;
using System.Windows;
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

		[Test(Description = @""), STAThread]
		public void TestColumnTypeToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeTypeOf<ColumnDetailsModel>();
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
			dataModel.HistogramVisibility.ShouldBe(Visibility.Visible);
		}

		[Test(Description = @""), STAThread]
		public void TestColumnTypeToolTipFromObjectReferencedUsingSynonym()
		{
			const string query = "SELECT DUMMY FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeTypeOf<ColumnDetailsModel>();
		}

		[Test(Description = @""), STAThread]
		public void TestRowIdPsedoColumnTypeToolTip()
		{
			const string query = "SELECT ROWID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipViewColumn>();
			toolTip.Control.DataContext.ShouldBeTypeOf<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.SELECTION");
			dataModel.Name.ShouldBe("ROWID");
			dataModel.DataType.ShouldBe("ROWID");
			dataModel.Nullable.ShouldBe(false);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
		}

		[Test(Description = @""), STAThread]
		public void TestNullableColumnTypeToolTip()
		{
			const string query = "SELECT RESPONDENTBUCKET_ID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeTypeOf<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.SELECTION");
			dataModel.Name.ShouldBe("RESPONDENTBUCKET_ID");
			dataModel.DataType.ShouldBe("NUMBER(9)");
			dataModel.Nullable.ShouldBe(true);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
		}

		[Test(Description = @""), STAThread]
		public void TestNonAliasedInlineViewColumnToolTip()
		{
			const string query = "SELECT NAME FROM (SELECT NAME FROM SELECTION)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("NAME VARCHAR2(50 BYTE) NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestSchemaViewColumnToolTip()
		{
			const string query = "SELECT CUSTOMER_ID FROM VIEW_INSTANTSEARCH";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipViewColumn>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.VIEW_INSTANTSEARCH");
			dataModel.Name.ShouldBe("CUSTOMER_ID");
			dataModel.DataType.ShouldBe("NUMBER(9)");
			dataModel.Comment.ShouldBe("This is a column comment. ");
		}

		[Test(Description = @""), STAThread]
		public void TestDecimalColumnTypeToolTip()
		{
			const string query = "SELECT AMOUNT FROM INVOICELINES";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeTypeOf<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.INVOICELINES");
			dataModel.Name.ShouldBe("AMOUNT");
			dataModel.DataType.ShouldBe("NUMBER(20, 2)");
			dataModel.Nullable.ShouldBe(false);
		}

		[Test(Description = @""), STAThread]
		public void TestScaleWithoutPrecisionColumnTypeToolTip()
		{
			const string query = "SELECT CORRELATION_VALUE FROM INVOICELINES";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipColumn>();
			toolTip.Control.DataContext.ShouldBeTypeOf<ColumnDetailsModel>();
			var dataModel = (ColumnDetailsModel)toolTip.Control.DataContext;
			dataModel.Owner.ShouldBe("HUSQVIK.INVOICELINES");
			dataModel.Name.ShouldBe("CORRELATION_VALUE");
			dataModel.DataType.ShouldBe("NUMBER(*, 5)");
			dataModel.Nullable.ShouldBe(false);
			dataModel.Comment.ShouldBe("This is a column comment. ");
		}

		[Test(Description = @""), STAThread]
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

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
		}

		[Test(Description = @""), STAThread]
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

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeTypeOf<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
			dataModel.AverageRowSize.ShouldBe(237);
			dataModel.BlockCount.ShouldBe(544);
			dataModel.AllocatedBytes.ShouldBe(22546891);
			dataModel.LargeObjectBytes.ShouldBe(1546891);
			dataModel.ClusterName.ShouldBe(null);
			dataModel.ClusterNameVisibility.ShouldBe(Visibility.Collapsed);
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
			dataModel.PartitionDetailsVisibility.ShouldBe(Visibility.Visible);
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipUsingSynonym()
		{
			const string query = "SELECT NAME FROM SYNONYM_TO_SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeTypeOf<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SYNONYM_TO_SELECTION (Synonym) => HUSQVIK.SELECTION (Table)");
			dataModel.AverageRowSize.ShouldBe(237);
			dataModel.BlockCount.ShouldBe(544);
			dataModel.ClusterName.ShouldBe(null);
			dataModel.ClusterNameVisibility.ShouldBe(Visibility.Collapsed);
			dataModel.Compression.ShouldBe("Disabled");
			dataModel.PartitionKeys.ShouldBe("COLUMN1, COLUMN2");
			dataModel.SubPartitionKeys.ShouldBe("COLUMN3, COLUMN4");
			dataModel.IsTemporary.ShouldBe(false);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
			dataModel.Organization.ShouldBe("Index");
			dataModel.InMemoryCompression.ShouldBe("Disabled");
			dataModel.RowCount.ShouldBe(8312);
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipInUpdateStatementMainObjectReference()
		{
			const string query = "UPDATE SELECTION SET NAME = 'Dummy selection' WHERE SELECTION_ID = 0";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeTypeOf<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipInDeleteStatementMainObjectReference()
		{
			const string query = "DELETE SELECTION WHERE SELECTION_ID = 0";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeTypeOf<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipInInsertStatementMainObjectReference()
		{
			const string query = "INSERT INTO HUSQVIK.SYNONYM_TO_SELECTION(NAME) VALUES ('Dummy selection')";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 23);

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeTypeOf<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SYNONYM_TO_SELECTION (Synonym) => HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipInSelectListWithInvalidColumn()
		{
			const string query = "SELECT SELECTION.INVALID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeTypeOf<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipInUpdateSetClause()
		{
			const string query = "UPDATE HUSQVIK.SELECTION SET SELECTION.PROJECT_ID = HUSQVIK.SELECTION.PROJECT_ID WHERE 1 = 0";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 32);

			toolTip.Control.ShouldBeTypeOf<ToolTipTable>();
			toolTip.Control.DataContext.ShouldBeTypeOf<TableDetailsModel>();
			var dataModel = (TableDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("HUSQVIK.SELECTION (Table)");
			dataModel.Comment.ShouldBe("This is a table comment. ");
			dataModel.IndexDetails.Count.ShouldBeGreaterThan(0);
		}

		[Test(Description = @""), STAThread]
		public void TestSynonymReferencedObjectToolTip()
		{
			const string query = "SELECT * FROM V$SESSION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipView>();
			toolTip.Control.DataContext.ShouldBeTypeOf<ViewDetailsModel>();
			var dataModel = (ViewDetailsModel)toolTip.Control.DataContext;
			dataModel.Title.ShouldBe("\"PUBLIC\".V$SESSION (Synonym) => SYS.V_$SESSION (View)");
			dataModel.Comment.ShouldBe("This is a view comment. ");
			dataModel.ConstraintDetails.Count.ShouldBeGreaterThan(0);
		}

		[Test(Description = @""), STAThread]
		public void TestObjectSemanticErrorToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION, RESPONDENTBUCKET";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (SELECTION, RESPONDENTBUCKET)");
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionSemanticErrorToolTip()
		{
			const string query = "SELECT TO_CHAR FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Invalid parameter count");
		}

		[Test(Description = @""), STAThread]
		public void TestAmbiguousColumnNameFromSingleObjectToolTip()
		{
			const string query = "SELECT * FROM (SELECT 1 NAME, 2 NAME, 3 VAL, 4 VAL FROM DUAL)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (NAME, VAL)");
		}

		[Test(Description = @""), STAThread]
		public void TestAmbiguousColumnNameInMultipleObjectAsteriskReferences()
		{
			const string query = "SELECT T1.*, T2.* FROM (SELECT 1 C1, 2 C1 FROM DUAL) T1, (SELECT 1 D1, 2 D1 FROM DUAL) T2";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (C1)");

			toolTip = _toolTipProvider.GetToolTip(_documentRepository, 17);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (D1)");
		}

		[Test(Description = @""), STAThread]
		public void TestAmbiguousColumnNameFromSingleObjectWithoutAliasToolTip()
		{
			const string query = "SELECT DUMMY FROM (SELECT DUAL.DUMMY, X.DUMMY FROM DUAL, DUAL X)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference");
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionIdentifierToolTip()
		{
			const string query = "SELECT COALESCE(NULL, 1) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipProgram>();
			var metadata = (OracleProgramMetadata)toolTip.Control.DataContext;
			metadata.Identifier.FullyQualifiedIdentifier.ShouldBe("SYS.STANDARD.COALESCE");
		}

		[Test(Description = @""), STAThread]
		public void TestPackageIdentifierToolTip()
		{
			const string query = "SELECT DBMS_RANDOM.STRING('X', 16) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".DBMS_RANDOM (Synonym) => SYS.DBMS_RANDOM (Package)");
		}

		[Test(Description = @""), STAThread]
		public void TestTypeIdentifierToolTip()
		{
			const string query = "SELECT XMLTYPE('<Root/>') FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Object Type)");
		}

		[Test(Description = @""), STAThread]
		public void TestSequenceIdentifierToolTip()
		{
			const string query = "SELECT SYNONYM_TO_TEST_SEQ.CURRVAL FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipSequence>();
			var toolTipSequence = (ToolTipSequence)toolTip.Control;
			toolTipSequence.LabelTitle.Text.ShouldBe("HUSQVIK.SYNONYM_TO_TEST_SEQ (Synonym) => HUSQVIK.TEST_SEQ (Sequence)");

		}

		[Test(Description = @""), STAThread]
		public void TestSequenceColumnIdentifierToolTip()
		{
			const string query = "SELECT TEST_SEQ.CURRVAL FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 17);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INTEGER NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestTypeWithCompilationErrorToolTip()
		{
			const string query = "SELECT INVALID_OBJECT_TYPE(DUMMY) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Object is invalid or unusable");
		}

		[Test(Description = @""), STAThread]
		public void TestDatabaseLinkToolTip()
		{
			const string query = "SELECT SQLPAD_FUNCTION@HQ_PDB_LOOPBACK(5) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			((TextBlock)((StackPanel)((ContentControl)toolTip.Control).Content).Children[1]).Text.ShouldBe("\"PUBLIC\".HQ_PDB_LOOPBACK (localhost:1521/hq_pdb)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableCollectionExpressionColumnQualifierToolTip()
		{
			const string query = "SELECT COLUMN_VALUE FROM TABLE(SYS.ODCIRAWLIST(NULL)) TEST_TABLE";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("TEST_TABLE.COLUMN_VALUE RAW(2000) NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestPackageToolTipWithinTableCollectionExpression()
		{
			const string query = "SELECT COLUMN_VALUE FROM TABLE(DBMS_XPLAN.DISPLAY_CURSOR()) TEST_TABLE";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 40);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".DBMS_XPLAN (Synonym) => SYS.DBMS_XPLAN (Package)");
		}

		[Test(Description = @""), STAThread]
		public void TestInlineViewToolTip()
		{
			const string query = "SELECT INLINEVIEW.DUMMY FROM (SELECT DUAL.DUMMY FROM DUAL) INLINEVIEW";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 16);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INLINEVIEW (Inline View)");
		}

		[Test(Description = @""), STAThread]
		public void TestXmlTableColumnQualifierToolTip()
		{
			const string query = "SELECT VALUE FROM XMLTABLE('/root' PASSING XMLTYPE('<root>value</root>') COLUMNS VALUE VARCHAR2(10) PATH '.') XML_DATA";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("XML_DATA.VALUE VARCHAR2(10) NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionIdentifierOverDatabaseLinkToolTip()
		{
			const string query = "SELECT SQLPAD_FUNCTION@UNDEFINED_DB_LINK FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 12);

			toolTip.ShouldBe(null);
		}

		[Test(Description = @""), STAThread]
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

		[Test(Description = @""), STAThread]
		public void TestFunctionOverloadsToolTip()
		{
			const string query = "SELECT SQLPAD.SQLPAD_FUNCTION() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 30);
			var functionOverloadList = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeTypeOf(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("HUSQVIK.SQLPAD.SQLPAD_FUNCTION(P: NUMBER) RETURN: NUMBER");
		}

		[Test(Description = @""), STAThread]
		public void TestObjectTypeConstructorToolTip()
		{
			const string query = "SELECT SYS.ODCIARGDESC() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 23);
			var functionOverloadList = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeTypeOf(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.ODCIARGDESC(ARGTYPE: NUMBER, TABLENAME: VARCHAR2, TABLESCHEMA: VARCHAR2, COLNAME: VARCHAR2, TABLEPARTITIONLOWER: VARCHAR2, TABLEPARTITIONUPPER: VARCHAR2, CARDINALITY: NUMBER) RETURN: SYS.ODCIARGDESC");
		}

		[Test(Description = @""), STAThread]
		public void TestCollectionTypeConstructorToolTip()
		{
			const string query = "SELECT SYS.ODCIARGDESCLIST() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 27);
			var functionOverloadList = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeTypeOf(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.ODCIARGDESCLIST([array of SYS.ODCIARGDESC]) RETURN: SYS.ODCIARGDESCLIST");
		}

		[Test(Description = @""), STAThread]
		public void TestPrimitiveTypeCollectionTypeConstructorToolTip()
		{
			const string query = "SELECT SYS.ODCIRAWLIST() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 23);
			var functionOverloadList = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeTypeOf(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.ODCIRAWLIST([array of RAW]) RETURN: SYS.ODCIRAWLIST");
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionOverloadWithFunctionReturningCollection()
		{
			const string query = "SELECT DBMS_XPLAN.DISPLAY_CURSOR() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 33);
			var functionOverloadList = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			functionOverloadList.ViewOverloads.Items.Count.ShouldBe(1);
			functionOverloadList.ViewOverloads.Items[0].ShouldBeTypeOf(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)functionOverloadList.ViewOverloads.Items[0]);
			itemText.ShouldBe("SYS.DBMS_XPLAN.DISPLAY_CURSOR([SQL_ID: VARCHAR2], [CURSOR_CHILD_NUMBER: NUMBER], [FORMAT: VARCHAR2]) RETURN: SYS.DBMS_XPLAN_TYPE_TABLE");
		}

		[Test(Description = @""), STAThread]
		public void TestNoParenthesisFunctionWithParentheses()
		{
			const string query = "SELECT SESSIONTIMEZONE() FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 23);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Non-parenthesis function");
		}

		[Test(Description = @""), STAThread]
		public void TestToolTipOverInvalidDateLiteral()
		{
			const string query = "SELECT DATE'' FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 12);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe(OracleSemanticErrorTooltipText.InvalidDateLiteral);
		}

		[Test(Description = @""), STAThread]
		public void TestToolTipOverInvalidTimestampLiteral()
		{
			const string query = "SELECT TIMESTAMP'' FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 17);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe(OracleSemanticErrorTooltipText.InvalidTimestampLiteral);
		}

		[Test(Description = @""), STAThread]
		public void TestNonQualifiedColumnReferenceOverDatabaseLink()
		{
			const string query = "SELECT NAME FROM SELECTION@HQ_PDB_LOOPBACK, DUAL@HQ_PDB_LOOPBACK";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe(OracleSuggestionType.PotentialDatabaseLink);
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionOverloadsToolTipNotShowForNonSchemaFunctions()
		{
			const string query = "SELECT MAX(DUMMY) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 9);
			var toolTip = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			toolTip.ViewOverloads.Items.Count.ShouldBe(0);
		}

		[Test(Description = @""), STAThread]
		public void TestToolTipInInsertValuesClause()
		{
			const string query = "INSERT INTO SELECTION (SELECTION_ID, SELECTIONNAME, RESPONDENTBUCKET_ID) VALUES (SQLPAD_FUNCTION, XMLTYPE(), SYNONYM_TO_TEST_SEQ.NEXTVAL)";
			_documentRepository.UpdateStatements(query);

			var toolTipFunction = _toolTipProvider.GetToolTip(_documentRepository, 84);

			toolTipFunction.Control.ShouldBeTypeOf<ToolTipProgram>();
			var metadata = (OracleProgramMetadata)toolTipFunction.Control.DataContext;
			metadata.Identifier.FullyQualifiedIdentifier.ShouldBe("HUSQVIK.SQLPAD_FUNCTION");

			var toolTipType = _toolTipProvider.GetToolTip(_documentRepository, 101);
			toolTipType.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTipType.Control.DataContext.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Object Type)");

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 112);
			toolTip.Control.ShouldBeTypeOf<ToolTipSequence>();
			var toolTipSequence = (ToolTipSequence)toolTip.Control;
			toolTipSequence.LabelTitle.Text.ShouldBe("HUSQVIK.SYNONYM_TO_TEST_SEQ (Synonym) => HUSQVIK.TEST_SEQ (Sequence)");
		}

		[Test(Description = @""), STAThread]
		public void TestVarryingArrayConstructorToolTip()
		{
			const string query = "SELECT * FROM TABLE(SYS.ODCIRAWLIST(NULL))";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SYS.ODCIRAWLIST (Object Varrying Array)");
		}

		[Test(Description = @""), STAThread]
		public void TestObjectTableConstructorToolTip()
		{
			const string query = "SELECT * FROM TABLE(SYS.DBMS_XPLAN_TYPE_TABLE())";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SYS.DBMS_XPLAN_TYPE_TABLE (Object Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestSchemaToolTip()
		{
			const string query = "SELECT * FROM HUSQVIK.SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 14);
			toolTip.Control.ShouldBeTypeOf<ToolTipSchema>();
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

		[Test(Description = @""), STAThread]
		public void TestAsteriskTooltip()
		{
			const string query = "SELECT * FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeTypeOf<ToolTipAsterisk>();
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

		[Test(Description = @""), STAThread]
		public void TestAsteriskTooltipWithDoubleCommonTableExpressionDefinition()
		{
			const string query = @"WITH CTE AS (SELECT 1 C1 FROM DUAL), CTE AS (SELECT 1 C2 FROM DUAL) SELECT * FROM CTE";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 75);
			toolTip.Control.ShouldBeTypeOf<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("C1");
			columns[0].FullTypeName.ShouldBe("NUMBER");
			columns[0].RowSourceName.ShouldBe("CTE");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[0].Nullable.ShouldBe(false);
		}
		
		[Test(Description = @""), STAThread]
		public void TestAsteriskTooltipWithDoubleCommonTableExpressionDefinitionInDifferentSubqueries()
		{
			const string query = @"WITH CTE AS (SELECT 1 C1 FROM DUAL) SELECT * FROM (WITH CTE AS (SELECT 1 C2 FROM DUAL) SELECT * FROM CTE) SUBQUERY";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 43);
			toolTip.Control.ShouldBeTypeOf<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("C2");
			columns[0].FullTypeName.ShouldBe("NUMBER");
			columns[0].RowSourceName.ShouldBe("SUBQUERY");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[0].Nullable.ShouldBe(false);
		}

		[Test(Description = @""), STAThread]
		public void TestAsteriskTooltipWithUnnamedColumn()
		{
			const string query = @"SELECT * FROM (SELECT nvl(""column name"", 0) FROM dual) T";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeTypeOf<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("NVL(\"COLUMNNAME\",0)");
			columns[0].FullTypeName.ShouldBe(String.Empty);
			columns[0].RowSourceName.ShouldBe("T");
			columns[0].ColumnIndex.ShouldBe(1);
			columns[0].Nullable.ShouldBe(true);
		}

		[Test(Description = @""), STAThread]
		public void TestAsteriskTooltipWithFunctionWithCursorParameter()
		{
			const string query = @"SELECT * FROM TABLE(SQLPAD.CURSOR_FUNCTION(0, CURSOR(SELECT * FROM SELECTION), NULL)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 7);
			toolTip.Control.ShouldBeTypeOf<ToolTipAsterisk>();
			var toolTipSequence = (ToolTipAsterisk)toolTip.Control;
			var columns = toolTipSequence.Columns.ToArray();
			columns.Length.ShouldBe(1);
			columns[0].Name.ShouldBe("PLAN_TABLE_OUTPUT");
			columns[0].FullTypeName.ShouldBe("VARCHAR2(300 BYTE)");
			columns[0].RowSourceName.ShouldBe(String.Empty);
			columns[0].ColumnIndex.ShouldBe(1);
		}

		[Test(Description = @""), STAThread]
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
			toolTip.Control.ShouldBeTypeOf<ToolTipAsterisk>();
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

		[Test(Description = @""), STAThread]
		public void TestExplicitPartitionTooltip()
		{
			const string query = "SELECT * FROM INVOICES PARTITION (P2015)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 34);
			toolTip.Control.ShouldBeTypeOf<ToolTipPartition>();
			var toolTipPartition = (ToolTipPartition)toolTip.Control;
			toolTipPartition.DataContext.ShouldBeTypeOf<PartitionDetailsModel>();
			var dataModel = (PartitionDetailsModel)toolTipPartition.DataContext;
			dataModel.Name.ShouldBe("P2015");
			dataModel.Owner.ShouldBe(OracleObjectIdentifier.Create("HUSQVIK", "INVOICES"));
		}

		[Test(Description = @""), STAThread]
		public void TestExplicitSubPartitionTooltip()
		{
			const string query = "SELECT * FROM INVOICES SUBPARTITION (P2015_ENTERPRISE)";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 37);
			toolTip.Control.ShouldBeTypeOf<ToolTipPartition>();
			var toolTipPartition = (ToolTipPartition)toolTip.Control;
			toolTipPartition.DataContext.ShouldBeTypeOf<SubPartitionDetailsModel>();
			var dataModel = (SubPartitionDetailsModel)toolTipPartition.DataContext;
			dataModel.Name.ShouldBe("P2015_ENTERPRISE");
			dataModel.Owner.ShouldBe(OracleObjectIdentifier.Create("HUSQVIK", "INVOICES"));
		}

		[Test(Description = @""), STAThread]
		public void TestDataTypeTooltip()
		{
			const string query = "SELECT CAST(NULL AS SYS.ODCIRAWLIST) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SYS.ODCIRAWLIST (Object Varrying Array)");
		}

		[Test(Description = @""), STAThread]
		public void TestParameterTooltip()
		{
			const string query = "SELECT SQLPAD.SQLPAD_FUNCTION(p => NULL) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 30);
			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("P: NUMBER");
		}

		[Test(Description = @""), STAThread]
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
			toolTip.Control.ShouldBeTypeOf<ToolTipProgram>();
			var metadata = (OracleProgramMetadata)toolTip.Control.DataContext;
			metadata.Identifier.FullyQualifiedIdentifier.ShouldBe("SYS.STANDARD.ROUND");
		}

		[Test(Description = @""), STAThread]
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
			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Object Type)");
		}

		[Test(Description = @""), STAThread]
		public void TestPlSqlVariableReferenceTooltip()
		{
			const string query =
@"DECLARE
    test_variable VARCHAR2(255) := 'This' || ' is ' || 'value';
BEGIN
    test_variable := NULL;
END;";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 85);
			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Variable TEST_VARIABLE: VARCHAR2(255) NULL");
		}


		[Test(Description = @""), STAThread]
		public void TestPlSqlConstantReferenceTooltip()
		{
			const string query =
@"DECLARE
    test_constant CONSTANT VARCHAR2(255) NOT NULL := 'This' || ' is ' || 'value';
BEGIN
    test_constant := NULL;
END;";

			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 103);
			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Constant TEST_CONSTANT: VARCHAR2(255) NOT NULL = 'This' || ' is ' || 'value'");
		}

		[Test(Description = @""), STAThread]
		public void TestTooltipOverExplicitColumnListInCommonTableExpression()
		{
			const string query =
@"WITH data(value) AS (
	SELECT 1 FROM DUAL UNION ALL
	SELECT 2 FROM DUAL
)
SELECT * FROM data";

			_documentRepository.UpdateStatements(query);

			Assert.DoesNotThrow(() => _toolTipProvider.GetToolTip(_documentRepository, 12));
		}

		[Test(Description = @""), STAThread]
		public void TestTooltipOverNamedParameterCombinedWithParameterlessFunction()
		{
			const string query = @"SELECT DBMS_RANDOM.VALUE(low => 0, high => 5) FROM DUAL WHERE ROWNUM = 1";

			_documentRepository.UpdateStatements(query);

			Assert.DoesNotThrow(() => _toolTipProvider.GetToolTip(_documentRepository, 25));
		}

		[Test(Description = @""), STAThread]
		public void TestTooltipOverNomExistingColumn()
		{
			const string query = @"SELECT non_existing FROM dual";

			_documentRepository.UpdateStatements(query);

			Assert.DoesNotThrow(() => _toolTipProvider.GetToolTip(_documentRepository, 7));
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
