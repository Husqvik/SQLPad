﻿using System;
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
		public void TestSemanticErrorToolTip()
		{
			const string query = "SELECT NAME FROM SELECTION, RESPONDENTBUCKET";
			_document.UpdateStatements(_oracleSqlParser.Parse(query));

			var toolTip = _toolTipProvider.GetToolTip(TestFixture.DatabaseModel, _document, 8);

			toolTip.Control.ShouldBeTypeOf<ToolTipObject>();
			toolTip.Control.DataContext.ShouldBe("Ambiguous reference");
		}
	}
}
