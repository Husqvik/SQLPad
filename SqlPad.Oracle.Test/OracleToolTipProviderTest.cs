using System;
using NUnit.Framework;
using Shouldly;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleToolTipProviderTest
	{
		private readonly SqlDocument _document = new SqlDocument();
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();
		private readonly OracleToolTipProvider _toolTipProvider = new OracleToolTipProvider();

		[Test(Description = @""), STAThread]
		public void TestColumnTypeToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_document.UpdateStatements(_oracleSqlParser.Parse(query));

			var toolTip = _toolTipProvider.GetToolTip(TestFixture.DatabaseModel, _document, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("VARCHAR2 (NOT NULL)");
		}

		[Test(Description = @""), STAThread]
		public void TestTableObjectToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION";
			_document.UpdateStatements(_oracleSqlParser.Parse(query));

			var toolTip = _toolTipProvider.GetToolTip(TestFixture.DatabaseModel, _document, 20);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("HUSQVIK.SELECTION (Schema Object)");
		}

		[Test(Description = @""), STAThread]
		public void TestObjectSemanticErrorToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION, RESPONDENTBUCKET";
			_document.UpdateStatements(_oracleSqlParser.Parse(query));

			var toolTip = _toolTipProvider.GetToolTip(TestFixture.DatabaseModel, _document, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (SELECTION, RESPONDENTBUCKET)");
		}

		[Test(Description = @""), STAThread]
		public void TestFunctionSemanticErrorToolTip()
		{
			const string query = "SELECT COUNT FROM SELECTION";
			_document.UpdateStatements(_oracleSqlParser.Parse(query));

			var toolTip = _toolTipProvider.GetToolTip(TestFixture.DatabaseModel, _document, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Invalid parameter count");
		}

		[Test(Description = @""), STAThread]
		public void TestAmbiguousColumnNameFromSingleObjectToolTip()
		{
			const string query = "SELECT * FROM (SELECT 1 NAME, 2 NAME, 3 VAL, 4 VAL FROM DUAL)";
			_document.UpdateStatements(_oracleSqlParser.Parse(query));

			var toolTip = _toolTipProvider.GetToolTip(TestFixture.DatabaseModel, _document, 7);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference (NAME, VAL)");
		}
	}
}
