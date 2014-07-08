using System;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleToolTipProviderTest
	{
		private readonly OracleToolTipProvider _toolTipProvider = new OracleToolTipProvider();
		private readonly SqlDocumentStore _documentStore = new SqlDocumentStore(new OracleSqlParser(), new OracleStatementValidator(), TestFixture.DatabaseModel);

		[Test(Description = @""), STAThread]
		public void TestColumnTypeToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SELECTION.NAME VARCHAR2(50 BYTE) NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestRowIdPsedoColumnTypeToolTip()
		{
			const string query = "SELECT ROWID FROM SELECTION";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SELECTION.ROWID ROWID NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestDecimalColumnTypeToolTip()
		{
			const string query = "SELECT AMOUNT FROM INVOICELINES";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INVOICELINES.AMOUNT NUMBER(20, 2) NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestScaleWithoutPrecisionColumnTypeToolTip()
		{
			const string query = "SELECT CORRELATION_VALUE FROM INVOICELINES";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INVOICELINES.CORRELATION_VALUE NUMBER(*, 5) NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipInSelectListWithInvalidColumn()
		{
			const string query = "SELECT SELECTION.INVALID FROM SELECTION";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestSynonymReferencedObjectToolTip()
		{
			const string query = "SELECT * FROM V$SESSION";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".V$SESSION (Synonym) => SYS.V_$SESSION (View)");
		}

		[Test(Description = @""), STAThread]
		public void TestObjectSemanticErrorToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION, RESPONDENTBUCKET";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (SELECTION, RESPONDENTBUCKET)");
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionSemanticErrorToolTip()
		{
			const string query = "SELECT TO_CHAR FROM SELECTION";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Invalid parameter count");
		}

		[Test(Description = @""), STAThread]
		public void TestAmbiguousColumnNameFromSingleObjectToolTip()
		{
			const string query = "SELECT * FROM (SELECT 1 NAME, 2 NAME, 3 VAL, 4 VAL FROM DUAL)";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 7);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (NAME, VAL)");
		}

		[Test(Description = @""), STAThread]
		public void TestAmbiguousColumnNameInMultipleObjectAsteriskReferences()
		{
			const string query = "SELECT T1.*, T2.* FROM (SELECT 1 C1, 2 C1 FROM DUAL) T1, (SELECT 1 D1, 2 D1 FROM DUAL) T2";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (C1)");

			toolTip = _toolTipProvider.GetToolTip(_documentStore, 17);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (D1)");
		}

		[Test(Description = @""), STAThread]
		public void TestAmbiguousColumnNameFromSingleObjectWithoutAliasToolTip()
		{
			const string query = "SELECT DUMMY FROM (SELECT DUAL.DUMMY, X.DUMMY FROM DUAL, DUAL X)";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 7);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference");
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionIdentifierToolTip()
		{
			const string query = "SELECT COALESCE(NULL, 1) FROM DUAL";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SYS.STANDARD.COALESCE");
		}

		[Test(Description = @""), STAThread]
		public void TestPackageIdentifierToolTip()
		{
			const string query = "SELECT DBMS_RANDOM.STRING('X', 16) FROM DUAL";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".DBMS_RANDOM (Synonym) => SYS.DBMS_RANDOM (Package)");
		}

		[Test(Description = @""), STAThread]
		public void TestTypeIdentifierToolTip()
		{
			const string query = "SELECT XMLTYPE('<Root/>') FROM DUAL";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".XMLTYPE (Synonym) => SYS.XMLTYPE (Type)");
		}

		[Test(Description = @""), STAThread]
		public void TestSequenceIdentifierToolTip()
		{
			const string query = "SELECT SYNONYM_TO_TEST_SEQ.CURRVAL FROM DUAL";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("HUSQVIK.SYNONYM_TO_TEST_SEQ (Synonym) => HUSQVIK.TEST_SEQ (Sequence)");
		}

		[Test(Description = @""), STAThread]
		public void TestSequenceColumnIdentifierToolTip()
		{
			const string query = "SELECT TEST_SEQ.CURRVAL FROM DUAL";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 17);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INTEGER NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestTypeWithCompilationErrorToolTip()
		{
			const string query = "SELECT INVALID_OBJECT_TYPE(DUMMY) FROM DUAL";
			_documentStore.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentStore, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Object is invalid or unusable");
		}

		[Test(Description = @""), STAThread]
		public void TestToolTipBeforeDatabaseModelLoaded()
		{
			const string query = "SELECT S.* FROM SELECTION S";
			_documentStore.UpdateStatements(query);

			var databaseModel = new OracleTestDatabaseModel();
			databaseModel.AllObjects.Clear();
			var documentStore = new SqlDocumentStore(new OracleSqlParser(), new OracleStatementValidator(), databaseModel, query);
			var toolTip = _toolTipProvider.GetToolTip(documentStore, 7);

			toolTip.ShouldBe(null);
		}
	}
}
