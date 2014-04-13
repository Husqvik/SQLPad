using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleContextActionProviderTest
	{
		private readonly OracleContextActionProvider _actionProvider = new OracleContextActionProvider();

		[Test(Description = @"")]
		public void TestSuggestingAmbiguousColumnReferenceResolutionAtTheNameBeginning()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(query1, 7).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as t2.DUMMY");
			actions[1].Name.ShouldBe("Resolve as Dual.DUMMY");
		}

		[Test(Description = @"")]
		public void TestSuggestingAmbiguousColumnReferenceResolutionAtTheNameEnd()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(query1, 12).ToArray();
			actions.Length.ShouldBe(2);
			actions[0].Name.ShouldBe("Resolve as t2.DUMMY");
			actions[1].Name.ShouldBe("Resolve as Dual.DUMMY");
		}

		[Test(Description = @"")]
		public void TestSuggestingAddTableAlias()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(query1, 49).ToArray();
			actions.Length.ShouldBe(1);
			actions[0].Name.ShouldBe("Add Alias");
		}

		[Test(Description = @"")]
		public void TestAliasNotSuggestedAtNestedTableAlias()
		{
			const string query1 = @"SELECT DUMMY FROM (SELECT DUMMY FROM DUAL) t2, Dual";

			var actions = _actionProvider.GetContextActions(query1, 44).ToArray();
			actions.Length.ShouldBe(0);
		}
	}
}