using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Oracle.Test
{
	[TestFixture]
	public class OracleFoldingSectionProviderTest
    {
		private readonly OracleFoldingSectionProvider _provider = new OracleFoldingSectionProvider();

		[Test(Description = @"")]
		public void TestFoldingSectionBasics()
		{
			const string statement =
@"SELECT
	*
FROM
	(SELECT XMLTYPE('<value>value 1</value>') XML_DATA1 FROM DUAL)
	CROSS JOIN
		XMLTABLE('/root' PASSING '<root>' || XML_DATA1 || XML_DATA2 || '</root>')
	CROSS JOIN
		(SELECT XMLTYPE('<value>value 2</value>') XML_DATA2 FROM DUAL);";

			var tokens = ((ITokenReader)OracleTokenReader.Create(statement)).GetTokens();

			var foldingSections = _provider.GetFoldingSections(tokens).ToArray();
			foldingSections.Length.ShouldBe(3);
			foldingSections[0].FoldingStart.ShouldBe(0);
			foldingSections[0].FoldingEnd.ShouldBe(251);
			foldingSections[0].IsNested.ShouldBe(false);

			foldingSections[1].FoldingStart.ShouldBe(20);
			foldingSections[1].FoldingEnd.ShouldBe(80);
			foldingSections[1].IsNested.ShouldBe(true);

			foldingSections[2].FoldingStart.ShouldBe(189);
			foldingSections[2].FoldingEnd.ShouldBe(249);
			foldingSections[2].IsNested.ShouldBe(true);
		}
    }
}