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
			items[3].Name.ShouldBe("FULL JOIN");
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
			items[1].Name.ShouldBe("INVOICES");

			items = _codeCompletionProvider.ResolveItems("SELECT * FROM INVOICES JOIN INVOICE;SELECT * FROM INVOICELINES JOIN INVOICE", 57).ToArray();
			items[0].Name.ShouldBe("INVOICELINES");
			items[1].Name.ShouldBe("INVOICES");
		}
	}
}