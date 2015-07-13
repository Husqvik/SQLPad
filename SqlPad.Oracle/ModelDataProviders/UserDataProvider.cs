using System;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.ToolTips;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class UserDataProvider : ModelDataProvider<OracleSchemaModel>
	{
		public UserDataProvider(OracleSchemaModel dataModel)
			: base(dataModel)
		{
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = OracleDatabaseCommands.SelectUserAdditionalData;
			command.AddSimpleParameter("USERNAME", DataModel.Schema.Name.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}

			DataModel.AccountStatus = (string)reader["ACCOUNT_STATUS"];
			DataModel.LockDate = OracleReaderValueConvert.ToDateTime(reader["LOCK_DATE"]);
			DataModel.ExpiryDate = OracleReaderValueConvert.ToDateTime(reader["EXPIRY_DATE"]);
			DataModel.DefaultTablespace = (string)reader["DEFAULT_TABLESPACE"];
			DataModel.TemporaryTablespace = (string)reader["TEMPORARY_TABLESPACE"];
			DataModel.Profile = (string)reader["PROFILE"];
			DataModel.EditionsEnabled = String.Equals((string)reader["EDITIONS_ENABLED"], "Y");
			DataModel.AuthenticationType = (string)reader["AUTHENTICATION_TYPE"];
			DataModel.LastLogin = OracleReaderValueConvert.ToDateTime(reader["LAST_LOGIN"]);
		}
	}
}
