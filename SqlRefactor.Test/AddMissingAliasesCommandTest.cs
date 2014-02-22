using NUnit.Framework;
using Shouldly;
using SqlRefactor.Commands;

namespace SqlRefactor.Test
{
	[TestFixture]
	public class AddMissingAliasesCommandTest
	{
		private readonly AddMissingAliasesCommand _command = new AddMissingAliasesCommand();

		[Test(Description = @"")]
		public void Test1()
		{
			const string testQuery = "SELECT 1, 1 + 1 MYCOLUMN, DUMMY || '3' FROM DUAL";
			var result = _command.Execute(0, testQuery);

			const string expectedResult = "SELECT 1 COLUMN1, 1 + 1 MYCOLUMN, DUMMY || '3' COLUMN3 FROM DUAL";
			result.ShouldBe(expectedResult);
		}
	}
}