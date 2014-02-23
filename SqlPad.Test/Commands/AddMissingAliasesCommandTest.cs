using NUnit.Framework;
using Shouldly;
using SqlPad.Commands;

namespace SqlPad.Test.Commands
{
	[TestFixture]
	public class AddMissingAliasesCommandTest
	{
		private readonly AddMissingAliasesCommand _command = new AddMissingAliasesCommand();

		[Test(Description = @"")]
		public void Test1()
		{
			const string testQuery = "SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' FROM DUAL";
			var result = _command.Execute(testQuery, 0);

			const string expectedResult = "SELECT 1 COLUMN1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";
			result.ShouldBe(expectedResult);
		}

		[Test(Description = @"")]
		public void Test2()
		{
			const string testQuery = "SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' FROM DUAL";
			var result = new WrapAsCommonTableExpressionCommand().Execute(testQuery, 0, "MYQUERY");

			const string expectedResult = "WITH MYQUERY AS (SELECT 1 COLUMN1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT COLUMN1, MYCOLUMN, COLUMN3 FROM MYQUERY";
			result.ShouldBe(expectedResult);
		}

		[Test(Description = @"")]
		public void Test3()
		{
			const string testQuery = "WITH OLDQUERY AS (SELECT OLD FROM OLD) SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' FROM DUAL";
			var result = new WrapAsCommonTableExpressionCommand().Execute(testQuery, 44, "NEWQUERY");

			const string expectedResult = "WITH OLDQUERY AS (SELECT OLD FROM OLD), NEWQUERY AS (SELECT 1 COLUMN1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL) SELECT COLUMN1, MYCOLUMN, COLUMN3 FROM NEWQUERY";
			result.ShouldBe(expectedResult);
		}
	}
}