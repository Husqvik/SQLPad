using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleCodeCompletionProviderTest
	{
		private readonly OracleCodeCompletionProvider _codeCompletionProvider = new OracleCodeCompletionProvider();

		[Test(Description = @"")]
		public void Test1()
		{
			const string testQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

			var items = _codeCompletionProvider.ResolveItems(testQuery, 37).ToArray();
			items.Length.ShouldBe(8);
			items[0].Name.ShouldBe("COUNTRY");
			items[0].Text.ShouldBe("COUNTRY");
			items[7].Name.ShouldBe("VIEW_INSTANTSEARCH");
			items[7].Text.ShouldBe("VIEW_INSTANTSEARCH");
		}

		[Test(Description = @"")]
		public void Test2()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I ", 52).ToArray();
			// TODO: Filter out outer types depending of nullable columns
			items.Length.ShouldBe(5);
			items[0].Name.ShouldBe("JOIN");
			items[0].Text.ShouldBe("JOIN");
			items[0].Offset.ShouldBe(0);
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.JoinMethod);
			items[4].Name.ShouldBe("CROSS JOIN");
			items[4].Text.ShouldBe("CROSS JOIN");
			items[4].Offset.ShouldBe(0);
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.JoinMethod);
		}

		[Test(Description = @"")]
		public void Test3()
		{
			const string testQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

			var items = _codeCompletionProvider.ResolveItems(testQuery, 21).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ID");
			items[0].Text.ShouldBe("ID");
		}

		[Test(Description = @"")]
		public void Test4()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 35).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("INVOICELINES");
			items[0].Text.ShouldBe("INVOICELINES");
			//items[0].Offset.ShouldBe(0);
			items[1].Name.ShouldBe("INVOICES");
			items[1].Text.ShouldBe("INVOICES");

			items = _codeCompletionProvider.ResolveItems("SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 57).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("INVOICES");
			items[0].Text.ShouldBe("INVOICES");
		}

		[Test(Description = @"")]
		public void Test5()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P", 50).ToArray();
			items.Length.ShouldBe(0);

			items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ", 51).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ON S.PROJECT_ID = P.PROJECT_ID");
			items[0].Text.ShouldBe("ON S.PROJECT_ID = P.PROJECT_ID");
			items[0].Offset.ShouldBe(0);

			items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON", 53).ToArray();
			items.Length.ShouldBe(0);

			items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON ", 54).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("S.PROJECT_ID = P.PROJECT_ID");
			items[0].Text.ShouldBe("S.PROJECT_ID = P.PROJECT_ID");
			items[0].Offset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test6()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID JOIN RESPONDENTBUCKET B ", 106).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].Text.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].Offset.ShouldBe(0);
			items[1].Name.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].Text.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].Offset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test7()
		{
			const string query1 = @"WITH
	CTE1 AS (SELECT '' NAME, '' DESCRIPTION, 1 ID FROM DUAL),
	CTE2 AS (SELECT '' OTHER_NAME, '' OTHER_DESCRIPTION, 1 ID FROM DUAL)
SELECT
	*
FROM
	CTE1
	JOIN CTE2 ";

			var items = _codeCompletionProvider.ResolveItems(query1, 173).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ON CTE1.ID = CTE2.ID");
			items[0].Text.ShouldBe("ON CTE1.ID = CTE2.ID");
			items[0].Offset.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test8()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN P";

			var items = _codeCompletionProvider.ResolveItems(query1, 34).ToArray();
			items.Length.ShouldBe(4);
			items[0].Name.ShouldBe("PROJECT");
			items[0].Text.ShouldBe("PROJECT");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Name.ShouldBe("RESPONDENTBUCKET");
			items[1].Text.ShouldBe("RESPONDENTBUCKET");
			items[2].Name.ShouldBe("TARGETGROUP");
			items[2].Text.ShouldBe("TARGETGROUP");
			items[3].Name.ShouldBe("PUBLIC");
			items[3].Text.ShouldBe("PUBLIC");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.DatabaseSchema);
		}

		[Test(Description = @"")]
		public void Test9()
		{
			const string query1 = @"WITH
	CTE1 AS (SELECT '' NAME, '' DESCRIPTION, 1 ID FROM DUAL),
	CTE2 AS (SELECT '' OTHER_NAME, '' OTHER_DESCRIPTION, 1 ID FROM DUAL)
SELECT
	*
FROM
	CTE1
	JOIN ";

			var items = _codeCompletionProvider.ResolveItems(query1, 168).ToArray();
			items.Length.ShouldBe(15);
			items[0].Name.ShouldBe("CTE1");
			items[0].Text.ShouldBe("CTE1");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.CommonTableExpression);
			items[1].Name.ShouldBe("CTE2");
			items[1].Text.ShouldBe("CTE2");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.CommonTableExpression);
		}

		[Test(Description = @"")]
		public void Test10()
		{
			const string query1 = @"SELECT * FROM SYS.";

			var items = _codeCompletionProvider.ResolveItems(query1, 18).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("DUAL");
			items[0].Text.ShouldBe("DUAL");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
			items[1].Name.ShouldBe("V_$SESSION");
			items[1].Text.ShouldBe("V_$SESSION");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test(Description = @"")]
		public void Test11()
		{
			const string query1 = @"SELECT 1,  FROM SELECTION S";

			var items = _codeCompletionProvider.ResolveItems(query1, 10).ToArray();
			items.Length.ShouldBe(5);
			items[0].Name.ShouldBe("S.NAME");
			items[0].Text.ShouldBe("S.NAME");
			items[0].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[1].Name.ShouldBe("S.PROJECT_ID");
			items[1].Text.ShouldBe("S.PROJECT_ID");
			items[1].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[2].Name.ShouldBe("S.RESPONDENTBUCKET_ID");
			items[2].Text.ShouldBe("S.RESPONDENTBUCKET_ID");
			items[2].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[3].Name.ShouldBe("S.SELECTION_ID");
			items[3].Text.ShouldBe("S.SELECTION_ID");
			items[3].Category.ShouldBe(OracleCodeCompletionCategory.Column);
			items[4].Name.ShouldBe("S");
			items[4].Text.ShouldBe("S");
			items[4].Category.ShouldBe(OracleCodeCompletionCategory.SchemaObject);
		}

		[Test(Description = @"")]
		public void Test12()
		{
			const string query1 = @"SELECT 1 FROM SYSTEM.C";

			var items = _codeCompletionProvider.ResolveItems(query1, 22).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test13()
		{
			const string query1 = @"SELECT SELECTION. FROM SELECTION, TARGETGROUP";

			var items = _codeCompletionProvider.ResolveItems(query1, 17).ToArray();
			items.Length.ShouldBe(4);
			items[0].Name.ShouldBe("NAME");
			items[0].Text.ShouldBe("NAME");
			items[3].Name.ShouldBe("SELECTION_ID");
			items[3].Text.ShouldBe("SELECTION_ID");

			const string query2 = @"SELECT SELECTION.NAME FROM SELECTION, TARGETGROUP";

			items = _codeCompletionProvider.ResolveItems(query2, 18).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("RESPONDENTBUCKET_ID");
			items[0].Text.ShouldBe("RESPONDENTBUCKET_ID");
			items[1].Name.ShouldBe("SELECTION_ID");
			items[1].Text.ShouldBe("SELECTION_ID");

			const string query3 = @"SELECT SELECTION.NAME FROM SELECTION, TARGETGROUP";

			items = _codeCompletionProvider.ResolveItems(query3, 19).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test14()
		{
			const string query1 = @"SELECT NULL FROM SELECTION S LEFT JOIN RESPONDENTBUCKET ON S.RESPONDENTBUCKET_ID = RESPONDENTBUCKET.RESPONDENTBUCKET_ID ";

			var items = _codeCompletionProvider.ResolveItems(query1, 120).ToArray();
			items.Length.ShouldBe(5);
			items[0].Name.ShouldBe("JOIN");
			items[0].Text.ShouldBe("JOIN");
			items[4].Name.ShouldBe("CROSS JOIN");
			items[4].Text.ShouldBe("CROSS JOIN");
		}

		[Test(Description = @"")]
		public void Test15()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var items = _codeCompletionProvider.ResolveItems(query1, 35).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test16()
		{
			const string query1 = @"SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID";

			var items = _codeCompletionProvider.ResolveItems(query1, 43).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test17()
		{
			const string query1 = @"SELECT D FROM DUAL";

			var items = _codeCompletionProvider.ResolveItems(query1, 8).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("DUAL.DUMMY");
			items[0].Text.ShouldBe("DUAL.DUMMY");

			const string query2 = @"SELECT D FROM DUAL X";

			items = _codeCompletionProvider.ResolveItems(query2, 8).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("X.DUMMY");
			items[0].Text.ShouldBe("X.DUMMY");
		}

		[Test(Description = @"")]
		public void Test18()
		{
			const string query1 = @"SELECT NULL FROM DUAL WHERE DUMMY = (SELECT * FROM DUAL)";

			var items = _codeCompletionProvider.ResolveItems(query1, 53).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test19()
		{
			const string query1 = @"SELECT SELECTION.NAME FROM SELECTION";

			var items = _codeCompletionProvider.ResolveItems(query1, 8).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test20()
		{
			const string query1 = @"SELECT * FROM PROJECT P GROUP BY P.";

			var items = _codeCompletionProvider.ResolveItems(query1, 35).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("NAME");
			items[0].Text.ShouldBe("NAME");
			items[1].Name.ShouldBe("PROJECT_ID");
			items[1].Text.ShouldBe("PROJECT_ID");
		}

		[Test(Description = @"")]
		public void Test21()
		{
			const string query1 = @"SELECT * FROM ";

			var items = _codeCompletionProvider.ResolveItems(query1, 14).ToArray();
			items.Length.ShouldBeGreaterThan(0);
		}

		[Test(Description = @"")]
		public void Test22()
		{
			const string query1 = @"SELECT * FROM ";

			var items = _codeCompletionProvider.ResolveItems(query1, 9).ToArray();
			items.Length.ShouldBe(0);
		}

		[Test(Description = @"")]
		public void Test23()
		{
			const string query1 = @"SELECT NULL FROM SELECTION, ";

			var items = _codeCompletionProvider.ResolveItems(query1, 28).ToArray();
			var currentSchemaTableCount = DatabaseModelFake.Instance.AllObjects.Values.Count(o => o.Owner == DatabaseModelFake.Instance.CurrentSchema);
			var schemaCount = DatabaseModelFake.Instance.Schemas.Count;
			items.Length.ShouldBe(currentSchemaTableCount + schemaCount);
		}

		[Test(Description = @"")]
		public void TestColumnSuggestionWhenTableWithAliasInFromClause()
		{
			const string query1 = @"SELECT  1 FROM SYS.DUAL";

			var items = _codeCompletionProvider.ResolveItems(query1, 7).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("SYS.DUAL.DUMMY");
			items[1].Name.ShouldBe("SYS.DUAL");
		}
	}
}
