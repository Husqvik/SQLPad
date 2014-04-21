using System.Configuration;

namespace SqlPad.Oracle.Test
{
	public class TestFixture
	{
		public static readonly OracleDatabaseModel DatabaseModel = new OracleDatabaseModel(new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=oracle;USER ID=HUSQVIK", "Oracle.DataAccess.Client")); 
	}
}