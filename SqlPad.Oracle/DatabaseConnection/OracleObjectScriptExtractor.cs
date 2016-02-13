using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle.DatabaseConnection
{
	public interface IOracleObjectScriptExtractor
	{
		Task<string> ExtractSchemaObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = false);
	}

	public class OracleObjectScriptExtractor : IOracleObjectScriptExtractor
	{
		private readonly OracleDatabaseModelBase _databaseModel;

		public OracleObjectScriptExtractor(OracleDatabaseModelBase databaseModel)
		{
			_databaseModel = databaseModel;
		}

		public async Task<string> ExtractSchemaObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException)
		{
			var scriptDataProvider = new ObjectScriptDataProvider(schemaObject);
			var connectionString = OracleConnectionStringRepository.GetBackgroundConnectionString(_databaseModel.ConnectionString.ConnectionString);
			await OracleDatabaseModel.UpdateModelAsync(connectionString, _databaseModel.CurrentSchema, cancellationToken, false, scriptDataProvider);
			return scriptDataProvider.ScriptText;
		}
	}
}