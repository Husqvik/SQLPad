using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Oracle.DataAccess.Client;
using Timer = System.Timers.Timer;

namespace SqlPad.Oracle
{
	public class OracleDatabaseModel : OracleDatabaseModelBase
	{
		private const int RefreshInterval = 10;
		private const string SqlFuntionMetadataFileName = "OracleSqlFunctionMetadataCollection_12_1_0_1_0.xml";
		internal const int OracleErrorCodeUserInvokedCancellation = 1013;
		internal const int InitialLongFetchSize = 131072;

		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private readonly Timer _timer = new Timer(RefreshInterval * 60000);
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));
		private bool _isRefreshing;
		private bool _cacheLoaded;
		private bool _isExecuting;
		private Task _backgroundTask;
		private readonly CancellationTokenSource _backgroundTaskCancellationTokenSource = new CancellationTokenSource();
		private OracleFunctionMetadataCollection _allFunctionMetadata = new OracleFunctionMetadataCollection(Enumerable.Empty<OracleFunctionMetadata>());
		private OracleFunctionMetadataCollection _builtInFunctionMetadata = new OracleFunctionMetadataCollection(Enumerable.Empty<OracleFunctionMetadata>());
		private ILookup<string, OracleFunctionMetadata> _nonSchemaBuiltInFunctionMetadata = Enumerable.Empty<OracleFunctionMetadata>().ToLookup(m => m.Identifier.Name, m => m);
		private readonly ConnectionStringSettings _connectionString;
		private HashSet<string> _schemas = new HashSet<string>();
		private HashSet<string> _allSchemas = new HashSet<string>();
		private string _currentSchema;
		private readonly DataDictionaryMapper _dataDictionaryMapper;
		private OracleDataDictionary _dataDictionary = new OracleDataDictionary(new Dictionary<OracleObjectIdentifier, OracleSchemaObject>(), new Dictionary<OracleObjectIdentifier, OracleDatabaseLink>(), DateTime.MinValue);
		private readonly OracleConnection _userConnection;
		private OracleDataReader _userDataReader;
		private OracleCommand _userCommand;

		private static readonly Dictionary<string, OracleDataDictionary> CachedDataDictionaries = new Dictionary<string, OracleDataDictionary>();
		private static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();
		private static readonly HashSet<string> ActiveDataModelRefresh = new HashSet<string>();
		private static readonly Dictionary<string, List<RefreshModel>> WaitingDataModelRefresh = new Dictionary<string, List<RefreshModel>>();

		private OracleDatabaseModel(ConnectionStringSettings connectionString)
		{
			_connectionString = connectionString;
			_oracleConnectionString = new OracleConnectionStringBuilder(connectionString.ConnectionString);
			_currentSchema = _oracleConnectionString.UserID;

			if (!WaitingDataModelRefresh.ContainsKey(CachedConnectionStringName))
			{
				WaitingDataModelRefresh[CachedConnectionStringName] = new List<RefreshModel>();
			}

			_dataDictionaryMapper = new DataDictionaryMapper(this);

			Stream metadataStream;
			if (MetadataCache.TryLoadMetadata(SqlFuntionMetadataFileName, out metadataStream))
			{
				try
				{
					using (var reader = XmlReader.Create(metadataStream))
					{
						BuiltInFunctionMetadata = (OracleFunctionMetadataCollection)Serializer.ReadObject(reader);
					}
				}
				finally
				{
					metadataStream.Dispose();
				}
			}
			else
			{
				ExecuteActionAsync(GenerateBuiltInFunctionMetadata);
			}

			_userConnection = new OracleConnection(connectionString.ConnectionString);

			LoadSchemaNames();

			_timer.Elapsed += (sender, args) => RefreshIfNeeded();
			_timer.Start();
		}

		public static OracleDatabaseModel GetDatabaseModel(ConnectionStringSettings connectionString)
		{
			OracleDatabaseModel databaseModel;
			if (DatabaseModels.TryGetValue(connectionString.ConnectionString, out databaseModel))
			{
				databaseModel = databaseModel.Clone();
			}
			else
			{
				DatabaseModels[connectionString.ConnectionString] = databaseModel = new OracleDatabaseModel(connectionString);
			}

			return databaseModel;
		}

		private void ExecuteActionAsync(Action action)
		{
			if (_isRefreshing)
				return;

			_backgroundTask = Task.Factory.StartNew(action);
		}

		public OracleFunctionMetadataCollection BuiltInFunctionMetadata
		{
			get { return _builtInFunctionMetadata; }
			private set
			{
				_builtInFunctionMetadata = value;
				_nonSchemaBuiltInFunctionMetadata = value.SqlFunctions.Where(f => String.IsNullOrEmpty(f.Identifier.Owner)).ToLookup(f => f.Identifier.Name, f => f);
			}
		}

		public override OracleFunctionMetadataCollection AllFunctionMetadata { get { return _allFunctionMetadata; } }

		protected override ILookup<string, OracleFunctionMetadata> NonSchemaBuiltInFunctionMetadata { get { return _nonSchemaBuiltInFunctionMetadata; } }

		public override ConnectionStringSettings ConnectionString { get { return _connectionString; } }

		public override string CurrentSchema
		{
			get { return _currentSchema; }
			set { _currentSchema = value; }
		}

		public override ICollection<string> Schemas { get { return _schemas; } }
		
		public override ICollection<string> AllSchemas { get { return _allSchemas; } }

		public override IDictionary<OracleObjectIdentifier, OracleSchemaObject> AllObjects { get { return _dataDictionary.AllObjects; } }

		public override IDictionary<OracleObjectIdentifier, OracleDatabaseLink> DatabaseLinks { get { return _dataDictionary.DatabaseLinks; } }

		public override void RefreshIfNeeded()
		{
			if (IsRefreshNeeded)
			{
				Refresh();
			}
		}

		public override bool IsModelFresh
		{
			get { return !IsRefreshNeeded; }
		}

		private bool IsRefreshNeeded
		{
			get { return _dataDictionary.Timestamp.AddMinutes(RefreshInterval) < DateTime.Now; }
		}

		private string CachedConnectionStringName
		{
			get { return _oracleConnectionString.DataSource + "_" + _oracleConnectionString.UserID; }
		}

		public override Task Refresh(bool force = false)
		{
			lock (ActiveDataModelRefresh)
			{
				if (ActiveDataModelRefresh.Contains(CachedConnectionStringName))
				{
					var taskCompletionSource = new TaskCompletionSource<OracleDataDictionary>();
					WaitingDataModelRefresh[CachedConnectionStringName].Add(new RefreshModel { DatabaseModel = this, TaskCompletionSource = taskCompletionSource });

					Trace.WriteLine(String.Format("{0} - Cache for '{1}' is being loaded by other requester. Waiting until operation finishes. ", DateTime.Now, CachedConnectionStringName));

					RaiseEvent(RefreshStarted);
					return taskCompletionSource.Task.ContinueWith(t => RefreshTaskFinishedHandler(t.Result));
				}

				ActiveDataModelRefresh.Add(CachedConnectionStringName);

				if (_backgroundTask == null)
				{
					ExecuteActionAsync(() => LoadSchemaObjectMetadata(force));
				}
				else
				{
					_backgroundTask = _backgroundTask.ContinueWith(
						t =>
						{
							t.Dispose();
							LoadSchemaObjectMetadata(force);
						});
				}
			}

			return _backgroundTask;
		}

		private void RefreshTaskFinishedHandler(OracleDataDictionary dataDictionary)
		{
			_dataDictionary = dataDictionary;
			BuildAllFunctionMetadata();

			RaiseEvent(RefreshFinished);

			Trace.WriteLine(String.Format("{0} - Cache for '{1}' has been retrieved from the cache. ", DateTime.Now, CachedConnectionStringName));
		}

		public async override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken, bool suppressUserCancellationException = true)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = DatabaseCommands.GetObjectScriptCommand;
					command.BindByName = true;

					command.AddSimpleParameter("OBJECT_TYPE", schemaObject.Type.ToUpperInvariant())
						.AddSimpleParameter("NAME", schemaObject.FullyQualifiedName.Name.Trim('"'))
						.AddSimpleParameter("SCHEMA", schemaObject.FullyQualifiedName.Owner.Trim('"'));

					connection.Open();

					try
					{
						return (string)await command.ExecuteScalarAsynchronous(cancellationToken);
					}
					catch (OracleException e)
					{
						if (suppressUserCancellationException && e.Number == OracleErrorCodeUserInvokedCancellation)
						{
							return null;
						}

						throw;
					}
				}
			}
		}

		public override event EventHandler RefreshStarted;

		public override event EventHandler RefreshFinished;

		public override bool IsExecuting { get { return _isExecuting; } }

		public override bool CanFetch
		{
			get { return _userDataReader != null && !_userDataReader.IsClosed; }
		}

		public override void Dispose()
		{
			_timer.Stop();
			_timer.Dispose();

			DisposeCommandAndReaderAndCloseConnection();

			if (_backgroundTask != null)
			{
				if (_isRefreshing)
				{
					_isRefreshing = false;
					RaiseEvent(RefreshFinished);
				}

				if (_backgroundTask.Status == TaskStatus.Running)
				{
					_backgroundTaskCancellationTokenSource.Cancel();
				}
				else
				{
					_backgroundTask.Dispose();
				}
			}

			RefreshStarted = null;
			RefreshFinished = null;
			
			_userConnection.Dispose();

			DatabaseModels.Remove(_connectionString.ConnectionString);
		}

		private void DisposeCommandAndReaderAndCloseConnection()
		{
			if (_userDataReader != null)
			{
				_userDataReader.Dispose();
			}

			if (_userCommand != null)
			{
				_userCommand.Dispose();
			}

			if (_userConnection.State != ConnectionState.Closed)
			{
				_userConnection.Close();
			}
		}

		public OracleDatabaseModel Clone()
		{
			var clone =
				new OracleDatabaseModel(ConnectionString)
				{
					_currentSchema = _currentSchema,
					_dataDictionary = _dataDictionary,
					_allFunctionMetadata = _allFunctionMetadata,
					_builtInFunctionMetadata = _builtInFunctionMetadata,
					_nonSchemaBuiltInFunctionMetadata = _nonSchemaBuiltInFunctionMetadata
				};

			return clone;
		}

		private void GenerateBuiltInFunctionMetadata()
		{
			BuiltInFunctionMetadata = _dataDictionaryMapper.GetBuiltInFunctionMetadata();

			lock (Serializer)
			{
				if (File.Exists(MetadataCache.GetFullFileName(SqlFuntionMetadataFileName)))
				{
					return;
				}

				using (var writer = XmlWriter.Create(MetadataCache.GetFullFileName(SqlFuntionMetadataFileName)))
				{
					Serializer.WriteObject(writer, BuiltInFunctionMetadata);
				}
			}

			/*var allFunctionMetadata = GetUserFunctionMetadata();

			var test = new OracleFunctionMetadataCollection(allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Owner == "husqvik".ToQuotedIdentifier()).ToArray());
			using (var writer = XmlWriter.Create(@"D:\TestFunctionCollection.xml"))
			{
				Serializer.WriteObject(writer, test);
			}*/

			_isRefreshing = false;
		}

		private int ExecuteUserNonQuery(string commandText)
		{
			return ExecuteUserStatement(commandText, c => c.ExecuteNonQuery(), true);
		}

		private T ExecuteUserStatement<T>(string commandText, Func<OracleCommand, T> executeFunction, bool closeConnection = false)
		{
			_userCommand = _userConnection.CreateCommand();
			_userCommand.CommandText = String.Format("ALTER SESSION SET CURRENT_SCHEMA = {0}", _currentSchema);

			try
			{
				_isExecuting = true;
				_userConnection.Open();

				_userCommand.ExecuteNonQuery();

				_userCommand.CommandText = commandText;
				_userCommand.InitialLONGFetchSize = InitialLongFetchSize;

				return executeFunction(_userCommand);
			}
			finally
			{
				if (closeConnection && _userConnection.State != ConnectionState.Closed)
				{
					_userConnection.Close();
				}
			}
		}

		public override int ExecuteStatement(string statementText, bool returnDataset)
		{
			var executionTask = ExecuteStatementAsync(statementText, returnDataset, CancellationToken.None);
			executionTask.Wait();
			
			return executionTask.Result;
		}

		public override async Task<int> ExecuteStatementAsync(string statementText, bool returnDataset, CancellationToken cancellationToken)
		{
			PreInitialize();

			var affectedRowCount = 0;

			try
			{
				if (returnDataset)
				{
					_userDataReader = await ExecuteUserStatement(statementText, c => c.ExecuteReaderAsynchronous(CommandBehavior.Default, cancellationToken));
				}
				else
				{
					affectedRowCount = await ExecuteUserStatement(statementText, c => c.ExecuteNonQueryAsynchronous(cancellationToken), true);
				}
			}
			catch (Exception exception)
			{
				SafeCloseUserConnection();

				var oracleException = exception as OracleException;
				if (oracleException == null || oracleException.Number != OracleErrorCodeUserInvokedCancellation)
				{
					throw;
				}
			}
			finally
			{
				_isExecuting = false;
			}

			return affectedRowCount;
		}

		private void SafeCloseUserConnection()
		{
			try
			{
				if (_userConnection.State != ConnectionState.Closed)
				{
					_userConnection.Close();
				}
			}
			catch
			{
			}
		}

		private void PreInitialize()
		{
			if (_isExecuting)
				throw new InvalidOperationException("Another statement is executing right now. ");

			DisposeCommandAndReaderAndCloseConnection();
		}

		public override ICollection<ColumnHeader> GetColumnHeaders()
		{
			CheckCanFetch();

			var columnTypes = new ColumnHeader[_userDataReader.FieldCount];
			for (var i = 0; i < _userDataReader.FieldCount; i++)
			{
				columnTypes[i] =
					new ColumnHeader
					{
						ColumnIndex = i,
						Name = _userDataReader.GetName(i),
						DataType = _userDataReader.GetFieldType(i),
						DatabaseDataType = _userDataReader.GetDataTypeName(i),
						ValueConverterFunction = ValueConverterFunction
					};
			}

			return columnTypes;
		}

		public override IEnumerable<object[]> FetchRecords(int rowCount)
		{
			CheckCanFetch();

			var fieldTypes = new string[_userDataReader.FieldCount];
			for (var i = 0; i < _userDataReader.FieldCount; i++)
			{
				var fieldType = _userDataReader.GetDataTypeName(i);
				fieldTypes[i] = fieldType;
				Trace.Write(i + ". " + fieldType + "; ");
			}

			Trace.WriteLine(String.Empty);

			for (var i = 0; i < rowCount; i++)
			{
				if (_userDataReader.Read())
				{
					yield return BuildValueArray(fieldTypes);
				}
				else
				{
					_userDataReader.Close();
					break;
				}
			}
		}

		private object[] BuildValueArray(IList<string> fieldTypes)
		{
			var columnData = new object[fieldTypes.Count];

			for (var i = 0; i < fieldTypes.Count; i++)
			{
				object value;
				switch (fieldTypes[i])
				{
					case "Blob":
						value = new OracleBlobValue(_userDataReader.GetOracleBlob(i));
						break;
					case "Clob":
						value = new OracleClobValue(_userDataReader.GetOracleClob(i));
						break;
					case "Long":
						var oracleString = _userDataReader.GetOracleString(i);
						value = oracleString.IsNull
							? String.Empty
							: String.Format("{0}{1}", oracleString.Value, oracleString.Value.Length == InitialLongFetchSize ? OracleClobValue.Ellipsis : null);
						break;
					case "LongRaw":
						value = new OracleLongRawValue(_userDataReader, i);
						break;
					default:
						value = _userDataReader.GetValue(i);
						break;
				}

				columnData[i] = value;
			}

			return columnData;
		}

		private void CheckCanFetch()
		{
			if (_userDataReader == null || _userDataReader.IsClosed)
				throw new InvalidOperationException("No data reader available. ");
		}

		internal IEnumerable<T> ExecuteReader<T>(string commandText, Func<OracleDataReader, T> formatFunction)
		{
			return ExecuteReader(commandText, null, formatFunction);
		}

		private IEnumerable<T> ExecuteReader<T>(string commandText, Action<OracleCommand> configureCommandFunction, Func<OracleDataReader, T> formatFunction)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = commandText;
					command.BindByName = true;

					if (configureCommandFunction != null)
					{
						configureCommandFunction(command);
					}

					connection.Open();

					using (var task = command.ExecuteReaderAsynchronous(CommandBehavior.CloseConnection, _backgroundTaskCancellationTokenSource.Token))
					{
						try
						{
							task.Wait();
						}
						catch (AggregateException)
						{
							if (task.IsCanceled)
							{
								yield break;
							}
							
							throw;
						}

						using (var reader = task.Result)
						{
							while (reader.Read())
							{
								yield return formatFunction(reader);
							}
						}
					}
				}
			}
		}

		private void RaiseRefreshEvents()
		{
			List<RefreshModel> refreshModels;
			if (!WaitingDataModelRefresh.TryGetValue(CachedConnectionStringName, out refreshModels))
				return;

			foreach (var refreshModel in refreshModels)
			{
				refreshModel.DatabaseModel._dataDictionary = _dataDictionary;
				refreshModel.DatabaseModel.BuildAllFunctionMetadata();
				refreshModel.DatabaseModel.RaiseEvent(refreshModel.DatabaseModel.RefreshFinished);
				refreshModel.DatabaseModel.RaiseEvent(refreshModel.DatabaseModel.RefreshStarted);
			}
		}

		private void SetRefreshTaskResults()
		{
			lock (ActiveDataModelRefresh)
			{
				List<RefreshModel> refreshModels;
				if (WaitingDataModelRefresh.TryGetValue(CachedConnectionStringName, out refreshModels))
				{
					foreach (var refreshModel in refreshModels)
					{
						refreshModel.TaskCompletionSource.SetResult(_dataDictionary);
					}
				}

				ActiveDataModelRefresh.Remove(CachedConnectionStringName);
			}
		}

		private void LoadSchemaObjectMetadata(bool force)
		{
			TryLoadSchemaObjectMetadataFromCache();

			//var otmp = _dataDictionary.AllObjects.Where(o => o.Key.NormalizedName.Contains("XMLTYPE")).ToArray();
			//var customer = _dataDictionary.AllObjects.Where(o => o.Key.NormalizedName.Contains("CUSTOMER\"")).ToArray();

			var isRefreshDone = !IsRefreshNeeded && !force;
			if (isRefreshDone)
			{
				SetRefreshTaskResults();
				return;
			}

			RaiseRefreshEvents();

			var reason = force ? "has been forced to refresh" : (_dataDictionary.Timestamp > DateTime.MinValue ? "has expired" : "does not exist");
			Trace.WriteLine(String.Format("{0} - Cache for '{1}' {2}. Cache refresh started. ", DateTime.Now, CachedConnectionStringName, reason));

			RaiseEvent(RefreshStarted);
			var lastRefresh = DateTime.Now;
			_isRefreshing = true;

			var allObjects = _dataDictionaryMapper.BuildDataDictionary();

			_allFunctionMetadata = new OracleFunctionMetadataCollection(BuiltInFunctionMetadata.SqlFunctions.Concat(_dataDictionaryMapper.GetUserFunctionMetadata().SqlFunctions));

			foreach (var functionMetadata in _allFunctionMetadata.SqlFunctions)
			{
				if (functionMetadata.IsPackageFunction)
				{
					OracleSchemaObject packageObject;
					if (allObjects.TryGetValue(OracleObjectIdentifier.Create(functionMetadata.Identifier.Owner, functionMetadata.Identifier.Package), out packageObject))
					{
						((OraclePackage)packageObject).Functions.Add(functionMetadata);
					}
				}
				else
				{
					OracleSchemaObject functionObject;
					if (allObjects.TryGetValue(OracleObjectIdentifier.Create(functionMetadata.Identifier.Owner, functionMetadata.Identifier.Name), out functionObject))
					{
						((OracleFunction)functionObject).Metadata = functionMetadata;
					}
				}
			}

			//var ftmp = _allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Owner.Contains("CA_DEV")).ToArray();
			//var ftmp = _allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Package.Contains("DBMS_RANDOM")).ToArray();
			//var otmp = dataObjectMetadata.Where(o => o.Key.NormalizedName.Contains("DBMS_RANDOM")).ToArray();

			var databaseLinks = _dataDictionaryMapper.GetDatabaseLinks();

			_dataDictionary = new OracleDataDictionary(allObjects, databaseLinks, lastRefresh);
			CachedDataDictionaries[CachedConnectionStringName] = _dataDictionary;

			SetRefreshTaskResults();

			MetadataCache.StoreDatabaseModelCache(CachedConnectionStringName, stream => _dataDictionary.Serialize(stream));

			_isRefreshing = false;
			RaiseEvent(RefreshFinished);
		}

		private void TryLoadSchemaObjectMetadataFromCache()
		{
			if (_cacheLoaded)
				return;

			Stream stream;
			OracleDataDictionary dataDictionary;
			if (CachedDataDictionaries.TryGetValue(CachedConnectionStringName, out dataDictionary))
			{
				_dataDictionary = dataDictionary;
			}
			else if (MetadataCache.TryLoadDatabaseModelCache(CachedConnectionStringName, out stream))
			{
				try
				{
					RaiseEvent(RefreshStarted);
					var stopwatch = Stopwatch.StartNew();
					_dataDictionary = CachedDataDictionaries[CachedConnectionStringName] = OracleDataDictionary.Deserialize(stream);
					Trace.WriteLine(String.Format("{0} - Cache for '{1}' loaded in {2}", DateTime.Now, CachedConnectionStringName, stopwatch.Elapsed));
				}
				finally
				{
					stream.Dispose();
					RaiseEvent(RefreshFinished);
				}
			}

			BuildAllFunctionMetadata();

			_cacheLoaded = true;
		}

		private void BuildAllFunctionMetadata()
		{
			var functionMetadata = _dataDictionary.AllObjects.Values
				.OfType<IFunctionCollection>()
				.Where(o => o.FullyQualifiedName != BuiltInFunctionPackageIdentifier)
				.SelectMany(o => o.Functions);

			_allFunctionMetadata = new OracleFunctionMetadataCollection(BuiltInFunctionMetadata.SqlFunctions.Concat(functionMetadata));
		}

		private void RaiseEvent(EventHandler eventHandler)
		{
			if (eventHandler != null)
			{
				eventHandler(this, EventArgs.Empty);
			}
		}

		private void LoadSchemaNames()
		{
			_schemas = new HashSet<string>(_dataDictionaryMapper.GetSchemaNames());
			_allSchemas = new HashSet<string>(_schemas.Select(DataDictionaryMapper.QualifyStringObject)) { SchemaPublic };
		}

		private struct RefreshModel
		{
			public TaskCompletionSource<OracleDataDictionary> TaskCompletionSource { get; set; }
			public OracleDatabaseModel DatabaseModel { get; set; }
		}
	}
}
