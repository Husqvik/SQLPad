using System.Text;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	public class MiscellaneousTest
	{
		[TestFixture]
		public class ExtensionsTest
		{
			[Test]
			public void TestToHexString()
			{
				var bytes = Encoding.UTF8.GetBytes("d676čžřčýžřáýíýžážřá");
				var hextString = bytes.ToHexString();

				const string expectedResult = "64363736C48DC5BEC599C48DC3BDC5BEC599C3A1C3BDC3ADC3BDC5BEC3A1C5BEC599C3A1";
				hextString.ShouldBe(expectedResult);
			}
		}
	}
}