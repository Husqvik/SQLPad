using System;
using System.Linq;
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
		private readonly SqlDocumentRepository _documentRepository = new SqlDocumentRepository(new OracleSqlParser(), new OracleStatementValidator(), TestFixture.DatabaseModel);

		[Test(Description = @""), STAThread]
		public void TestColumnTypeToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SELECTION.NAME VARCHAR2(50 BYTE) NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestRowIdPsedoColumnTypeToolTip()
		{
			const string query = "SELECT ROWID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SELECTION.ROWID ROWID NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestDecimalColumnTypeToolTip()
		{
			const string query = "SELECT AMOUNT FROM INVOICELINES";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INVOICELINES.AMOUNT NUMBER(20, 2) NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestScaleWithoutPrecisionColumnTypeToolTip()
		{
			const string query = "SELECT CORRELATION_VALUE FROM INVOICELINES";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("INVOICELINES.CORRELATION_VALUE NUMBER(*, 5) NOT NULL");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTipInSelectListWithInvalidColumn()
		{
			const string query = "SELECT SELECTION.INVALID FROM SELECTION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 10);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("HUSQVIK.SELECTION (Table)");
		}

		[Test(Description = @""), STAThread]
		public void TestSynonymReferencedObjectToolTip()
		{
			const string query = "SELECT * FROM V$SESSION";
			_documentRepository.UpdateStatements(query);

			var toolTip = _toolTipProvider.GetToolTip(_documentRepository, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("\"PUBLIC\".V$SESSION (Synonym) => SYS.V_$SESSION (View)");
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

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("SYS.STANDARD.COALESCE");
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

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("HUSQVIK.SYNONYM_TO_TEST_SEQ (Synonym) => HUSQVIK.TEST_SEQ (Sequence)");
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
			var toolTip = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			toolTip.ViewOverloads.Items.Count.ShouldBe(1);
			toolTip.ViewOverloads.Items[0].ShouldBeTypeOf(typeof(TextBlock));

			var itemText = GetTextFromTextBlock((TextBlock)toolTip.ViewOverloads.Items[0]);
			itemText.ShouldBe("HUSQVIK.SQLPAD.SQLPAD_FUNCTION(P: NUMBER) RETURN: NUMBER");
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

		[Test(Description = @""), STAThread]
		public void TestFunctionOverloadsToolTipNotShowForNonSchemaFunctions()
		{
			const string query = "SELECT MAX(DUMMY) FROM DUAL";
			_documentRepository.UpdateStatements(query);

			var functionOverloads = _codeCompletionProvider.ResolveFunctionOverloads(_documentRepository, 9);
			var toolTip = new FunctionOverloadList { FunctionOverloads = functionOverloads };
			toolTip.ViewOverloads.Items.Count.ShouldBe(0);
		}
	}
}
