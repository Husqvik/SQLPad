using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	public class MiscellaneousTest
	{
		[TestFixture]
		public class OracleExtensionsTest
		{
			[Test(Description = @"")]
			public void TestQuotedToSimpleIdentifierStartingWithNonLetter()
			{
				const string quotedIdentifier = "\"_IDENTIFIER\"";
				var simpleIdentifier = quotedIdentifier.ToSimpleIdentifier();
				simpleIdentifier.ShouldBe(quotedIdentifier);
			}
		}
	}
}