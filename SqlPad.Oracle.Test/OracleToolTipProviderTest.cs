using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using NUnit.Framework;
using Shouldly;
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

			toolTip.Control.ShouldBeTypeOf<ToolTipColumn>();
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
			dataModel.ClusterName.ShouldBe(null);
			dataModel.ClusterNameVisibility.ShouldBe(Visibility.Collapsed);
			dataModel.Compression.ShouldBe("Disabled");
			dataModel.IsPartitioned.ShouldBe(false);
			dataModel.IsTemporary.ShouldBe(false);
			dataModel.LastAnalyzed.ShouldBe(new DateTime(2014, 8, 19, 6, 18, 12));
			dataModel.Organization.ShouldBe("Index");
			dataModel.RowCount.ShouldBe(8312);
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
			dataModel.IsPartitioned.ShouldBe(false);
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
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Type)");
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
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".HQ_PDB_LOOPBACK (localhost:1521/hq_pdb)");
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
			var documentStore = new SqlDocumentRepository(new OracleSqlParser(), new OracleStatementValidator(), databaseModel, query);
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
			itemText.ShouldBe("SYS.ODCIARGDESCLIST(array of SYS.ODCIARGDESC) RETURN: SYS.ODCIARGDESCLIST");
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
			itemText.ShouldBe("SYS.ODCIRAWLIST(array of RAW) RETURN: SYS.ODCIRAWLIST");
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
			itemText.ShouldBe("SYS.DBMS_XPLAN.DISPLAY_CURSOR(SQL_ID: VARCHAR2, CURSOR_CHILD_NUMBER: NUMBER, FORMAT: VARCHAR2) RETURN: SYS.DBMS_XPLAN_TYPE_TABLE");
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
			toolTipType.Control.DataContext.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Type)");

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 112);
			toolTip.Control.ShouldBeTypeOf<ToolTipSequence>();
			var toolTipSequence = (ToolTipSequence)toolTip.Control;
			toolTipSequence.LabelTitle.Text.ShouldBe("HUSQVIK.SYNONYM_TO_TEST_SEQ (Synonym) => HUSQVIK.TEST_SEQ (Sequence)");
		}

		private static string GetTextFromTextBlock(TextBlock textBlock)
		{
			var inlines = textBlock.Inlines.Select(GetTextFromInline);
			return String.Join(null, inlines);
		}

		private static string GetTextFromInline(Inline inline)
		{
			var run = inline as Run;
			if (run != null)
			{
				return run.Text;
			}
			
			var bold = (Bold)inline;
			return String.Join(null, bold.Inlines.Select(GetTextFromInline));
		}
	}
}
