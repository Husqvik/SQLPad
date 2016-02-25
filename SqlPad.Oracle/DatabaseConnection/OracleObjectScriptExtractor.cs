using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.ModelDataProviders;

namespace SqlPad.Oracle.DatabaseConnection
{
	public interface IOracleObjectScriptExtractor
	{
		Task<string> ExtractSchemaObjectScriptAsync(OracleObject schemaObject, CancellationToken cancellationToken);

		Task<string> ExtractNonSchemaObjectScriptAsync(string objectName, string objectType, CancellationToken cancellationToken);
	}

	public class OracleObjectScriptExtractor : IOracleObjectScriptExtractor
	{
		private readonly OracleDatabaseModelBase _databaseModel;

		public OracleObjectScriptExtractor(OracleDatabaseModelBase databaseModel)
		{
			_databaseModel = databaseModel;
		}

		public async Task<string> ExtractSchemaObjectScriptAsync(OracleObject schemaObject, CancellationToken cancellationToken)
		{
			var scriptDataProvider = new ObjectScriptDataProvider(schemaObject);
			return await ExtractScriptInternal(cancellationToken, scriptDataProvider);
		}

		public async Task<string> ExtractNonSchemaObjectScriptAsync(string objectName, string objectType, CancellationToken cancellationToken)
		{
			var scriptDataProvider = new ObjectScriptDataProvider(objectName, objectType);
			return await ExtractScriptInternal(cancellationToken, scriptDataProvider);
		}

		private async Task<string> ExtractScriptInternal(CancellationToken cancellationToken, ObjectScriptDataProvider scriptDataProvider)
		{
			var connectionString = OracleConnectionStringRepository.GetBackgroundConnectionString(_databaseModel.ConnectionString.ConnectionString);
			await OracleDatabaseModel.UpdateModelAsync(connectionString, _databaseModel.CurrentSchema, cancellationToken, false, scriptDataProvider);
			return scriptDataProvider.ScriptText;
		}
	}
}