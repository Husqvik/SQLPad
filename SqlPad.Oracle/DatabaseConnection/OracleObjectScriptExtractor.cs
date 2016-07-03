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

		Task<string> ExtractViewTextAsync(OracleObjectIdentifier viewIdentifier, CancellationToken cancellationToken);
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
			await UpdateDataModel(cancellationToken, scriptDataProvider);
			return scriptDataProvider.ScriptText;
		}

		public async Task<string> ExtractNonSchemaObjectScriptAsync(string objectName, string objectType, CancellationToken cancellationToken)
		{
			var scriptDataProvider = new ObjectScriptDataProvider(objectName, objectType);
			await UpdateDataModel(cancellationToken, scriptDataProvider);
			return scriptDataProvider.ScriptText;
		}

		public async Task<string> ExtractViewTextAsync(OracleObjectIdentifier viewIdentifier, CancellationToken cancellationToken)
		{
			var dataModel = new ViewDetailModel();
			var viewDetailsDataProvider = new ViewDetailDataProvider(dataModel, viewIdentifier, _databaseModel.Version);
			await UpdateDataModel(cancellationToken, viewDetailsDataProvider);
			return dataModel.Text;
		}

		private async Task UpdateDataModel(CancellationToken cancellationToken, IModelDataProvider scriptDataProvider)
		{
			var connectionString = OracleConnectionStringRepository.GetBackgroundConnectionString(_databaseModel.ConnectionString.ConnectionString);
			await OracleDatabaseModel.UpdateModelAsync(connectionString, _databaseModel.CurrentSchema, false, cancellationToken, scriptDataProvider);
		}
	}
}