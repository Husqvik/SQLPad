using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace SqlPad.Test
{
	[TestFixture]
	public class OracleSemanticValidatorTest
	{
		private readonly OracleSqlParser _oracleSqlParser = new OracleSqlParser();
		private readonly OracleSemanticValidator _semanticValidator = new OracleSemanticValidator();
		private readonly DatabaseModelFake _databaseModel = new DatabaseModelFake();

		[Test(Description = @"")]
		public void Test1()
		{
			var statement = _oracleSqlParser.Parse("SELECT * FROM SYS.DUAL, HUSQVIK.COUNTRY, HUSQVIK.INVALID, INVALID.ORDERS, V$SESSION").Single();
			var validationModel = _semanticValidator.Validate(statement, _databaseModel);
		}
	}
}