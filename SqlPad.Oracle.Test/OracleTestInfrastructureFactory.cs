using System.Configuration;

namespace SqlPad.Oracle.Test
{
	public class OracleTestInfrastructureFactory : OracleInfrastructureFactory, IInfrastructureFactory
	{
		public new IDatabaseModel CreateDatabaseModel(ConnectionStringSettings connectionString)
		{
			return OracleTestDatabaseModel.Instance;
		}
	}
}