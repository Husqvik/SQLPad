using System.Configuration;

namespace SqlPad.Oracle.Test
{
	public class OracleTestInfrastructureFactory : OracleInfrastructureFactory, IInfrastructureFactory
	{
		public new IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString, string identifier)
		{
			return OracleTestDatabaseModel.Instance;
		}
	}
}