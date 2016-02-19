#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
#else
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using SqlPad.Oracle.DatabaseConnection;

#endif

namespace SqlPad.Oracle
{
	public class OracleValueAggregator : IValueAggregator
	{
		private long count;
		private OracleDecimal _oracleSum = OracleDecimal.Zero;

		public void AddValue(object value)
		{
			count++;

			var oracleNumber = value as OracleNumber;
			if (oracleNumber != null)
			{
				
			}


		}

		public string Minimum { get; }

		public string Maximum { get; }

		public string Average { get; }

		public string Sum { get; }

		public long Count { get; }
	}
}