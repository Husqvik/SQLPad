using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleCodeCompletionProviderTest
	{
		private const string TestQuery = "SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I JOIN HUSQVIK.INVOICES";

		private readonly OracleCodeCompletionProvider _codeCompletionProvider = new OracleCodeCompletionProvider();

		[Test(Description = @"")]
		public void Test1()
		{
			var items = _codeCompletionProvider.ResolveItems(TestQuery, 37).ToArray();
			items.Length.ShouldBe(9);
			items[0].Name.ShouldBe("COUNTRY");
			items[8].Name.ShouldBe("VIEW_INSTANTSEARCH");
		}

		[Test(Description = @"")]
		public void Test2()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT I.*, INVOICES.ID FROM HUSQVIK.INVOICELINES I", 51).ToArray(); // TODO: Add suggestion when join clause is already in place
			// TODO: Filter out outer types depending of nullable columns
			items.Length.ShouldBe(4);
			items[0].Name.ShouldBe("JOIN");
			items[0].Offset.ShouldBe(1);
			items[3].Name.ShouldBe("FULL JOIN");
			items[3].Offset.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void Test3()
		{
			var items = _codeCompletionProvider.ResolveItems(TestQuery, 21).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ID");
		}

		[Test(Description = @"")]
		public void Test4()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 35).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("INVOICELINES");
			//items[0].Offset.ShouldBe(0);
			items[1].Name.ShouldBe("INVOICES");

			items = _codeCompletionProvider.ResolveItems("SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 57).ToArray();
			items[0].Name.ShouldBe("INVOICELINES");
			//items[0].Offset.ShouldBe(0);
			items[1].Name.ShouldBe("INVOICES");
		}

		[Test(Description = @"")]
		public void Test5()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P", 50).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("ON S.PROJECT_ID = P.PROJECT_ID");
			items[0].Offset.ShouldBe(1);

			items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON", 53).ToArray();
			items.Length.ShouldBe(1);
			items[0].Name.ShouldBe("S.PROJECT_ID = P.PROJECT_ID");
			items[0].Offset.ShouldBe(1);
		}

		[Test(Description = @"")]
		public void Test6()
		{
			var items = _codeCompletionProvider.ResolveItems("SELECT S.* FROM SELECTION S JOIN HUSQVIK.PROJECT P ON S.PROJECT_ID = P.PROJECT_ID JOIN RESPONDENTBUCKET B ", 106).ToArray();
			items.Length.ShouldBe(2);
			items[0].Name.ShouldBe("ON P.PROJECT_ID = B.PROJECT_ID");
			items[0].Offset.ShouldBe(0);
			items[1].Name.ShouldBe("ON S.RESPONDENTBUCKET_ID = B.RESPONDENTBUCKET_ID");
			items[1].Offset.ShouldBe(0);
		}
	}
}