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

		private readonly object _lockObject = new object();
		private readonly OracleConnectionStringBuilder _oracleConnectionString;
		private readonly Timer _timer = new Timer(RefreshInterval * 60000);
		private static readonly DataContractSerializer Serializer = new DataContractSerializer(typeof(OracleFunctionMetadataCollection));
		private bool _isRefreshing;
		private bool _cacheLoaded;
		private bool _isExecuting;
		private Task _backgroundTask;
		private Task _statementExecutionTask;
		private OracleFunctionMetadataCollection _allFunctionMetadata = new OracleFunctionMetadataCollection(Enumerable.Empty<OracleFunctionMetadata>());
		private OracleFunctionMetadataCollection _builtInFunctionMetadata = new OracleFunctionMetadataCollection(Enumerable.Empty<OracleFunctionMetadata>());
		private ILookup<string, OracleFunctionMetadata> _nonSchemaBuiltInFunctionMetadata = Enumerable.Empty<OracleFunctionMetadata>().ToLookup(m => m.Identifier.Name, m => m);
		private readonly ConnectionStringSettings _connectionString;
		private HashSet<string> _schemas = new HashSet<string>();
		private HashSet<string> _allSchemas = new HashSet<string>();
		private string _currentSchema;
		private OracleDataDictionary _dataDictionary = new OracleDataDictionary(Enumerable.Empty<KeyValuePair<OracleObjectIdentifier, OracleSchemaObject>>(), DateTime.MinValue);
		private readonly OracleConnection _userConnection;
		private OracleDataReader _userDataReader;
		private OracleCommand _userCommand;

		private static readonly Dictionary<string, OracleDataDictionary> CachedDataDictionaries = new Dictionary<string, OracleDataDictionary>();
		internal static readonly Dictionary<string, OracleDatabaseModel> DatabaseModels = new Dictionary<string, OracleDatabaseModel>();

		private OracleDatabaseModel(ConnectionStringSettings connectionString)
		{
			_connectionString = connectionString;
			_oracleConnectionString = new OracleConnectionStringBuilder(connectionString.ConnectionString);
			_currentSchema = _oracleConnectionString.UserID;

			string metadata;
			if (MetadataCache.TryLoadMetadata(SqlFuntionMetadataFileName, out metadata))
			{
				using (var reader = XmlReader.Create(new StringReader(metadata)))
				{
					BuiltInFunctionMetadata = (OracleFunctionMetadataCollection)Serializer.ReadObject(reader);
				}
			}
			else
			{
				ExecuteActionAsync(GenerateBuiltInFunctionMetadata);
			}

			LoadSchemaNames();

			_userConnection = new OracleConnection(connectionString.ConnectionString);

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

			lock (_lockObject)
			{
				if (_isRefreshing)
					return;

				_backgroundTask = Task.Factory.StartNew(action);
			}
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

			return _backgroundTask;
		}

		public async override Task<string> GetObjectScriptAsync(OracleSchemaObject schemaObject, CancellationToken cancellationToken)
		{
			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = DatabaseCommands.GetObjectScriptCommand;
					command.BindByName = true;

					command.AddSimpleParameter("OBJECT_TYPE", schemaObject.Type.ToUpperInvariant())
						.AddSimpleParameter("NAME", schemaObject.FullyQualifiedName.Name.Trim('"'))
						.AddSimpleParameter("SCHEMA", schemaObject.FullyQualifiedName.Owner == SchemaPublic ? null : schemaObject.FullyQualifiedName.Owner.Trim('"'));

					connection.Open();

					return (string)await command.ExecuteScalarAsynchronous(cancellationToken);
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

			DisposeCommandAndReader();

			if (_statementExecutionTask != null)
				_statementExecutionTask.Dispose();

			if (_backgroundTask != null)
			{
				if (_isRefreshing)
				{
					_isRefreshing = false;
					RaiseEvent(RefreshFinished);
				}

				if (_backgroundTask.Status == TaskStatus.Running)
				{
					_backgroundTask.ContinueWith(t => t.Dispose());
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

		private void DisposeCommandAndReader()
		{
			if (_userDataReader != null)
			{
				_userDataReader.Dispose();
			}

			if (_userCommand != null)
			{
				_userCommand.Dispose();
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

		private OracleFunctionMetadataCollection GetUserFunctionMetadata()
		{
			return GetFunctionMetadataCollection(DatabaseCommands.UserFunctionMetadataCommandText, DatabaseCommands.UserFunctionParameterMetadataCommandText, false);
		}

		private void GenerateBuiltInFunctionMetadata()
		{
			BuiltInFunctionMetadata = GetFunctionMetadataCollection(DatabaseCommands.BuiltInFunctionMetadataCommandText, DatabaseCommands.BuiltInFunctionParameterMetadataCommandText, true);

			using (var writer = XmlWriter.Create(MetadataCache.GetFullFileName(SqlFuntionMetadataFileName)))
			{
				Serializer.WriteObject(writer, BuiltInFunctionMetadata);
			}

			/*var allFunctionMetadata = GetUserFunctionMetadata();

			var test = new OracleFunctionMetadataCollection(allFunctionMetadata.SqlFunctions.Where(f => f.Identifier.Owner == "husqvik".ToQuotedIdentifier()).ToArray());
			using (var writer = XmlWriter.Create(@"D:\TestFunctionCollection.xml"))
			{
				Serializer.WriteObject(writer, test);
			}*/

			_isRefreshing = false;
		}

		private OracleFunctionMetadataCollection GetFunctionMetadataCollection(string getFunctionMetadataCommandText, string getParameterMetadataCommandText, bool isBuiltIn)
		{
			var functionMetadataDictionary = new Dictionary<OracleFunctionIdentifier, OracleFunctionMetadata>();

			using (var connection = new OracleConnection(_oracleConnectionString.ConnectionString))
			{
				using (var command = connection.CreateCommand())
				{
					command.CommandText = getFunctionMetadataCommandText;

					connection.Open();

					using (var reader = command.ExecuteReader())
					{
						while (reader.Read())
						{
							var identifier = CreateFunctionIdentifierFromReaderValues(reader["OWNER"], reader["PACKAGE_NAME"], reader["FUNCTION_NAME"], reader["OVERLOAD"]);
							var isAnalytic = (string)reader["ANALYTIC"] == "YES";
							var isAggregate = (string)reader["AGGREGATE"] == "YES";
							var isPipelined = (string)reader["PIPELINED"] == "YES";
							var isOffloadable = (string)reader["OFFLOADABLE"] == "YES";
							var parallelSupport = (string)reader["PARALLEL"] == "YES";
							var isDeterministic = (string)reader["DETERMINISTIC"] == "YES";
							var minimumArgumentsRaw = reader["MINARGS"];
							var metadataMinimumArguments = minimumArgumentsRaw == DBNull.Value ? null : (int?)Convert.ToInt32(minimumArgumentsRaw);
							var maximumArgumentsRaw = reader["MAXARGS"];
							var metadataMaximumArguments = maximumArgumentsRaw == DBNull.Value ? null : (int?)Convert.ToInt32(maximumArgumentsRaw);
							var authId = (string)reader["AUTHID"] == "CURRENT_USER" ? AuthId.CurrentUser : AuthId.Definer;
							var displayType = (string)reader["DISP_TYPE"];

							var functionMetadata = new OracleFunctionMetadata(identifier, isAnalytic, isAggregate, isPipelined, isOffloadable, parallelSupport, isDeterministic, metadataMinimumArguments, metadataMaximumArguments, authId, displayType, isBuiltIn);
							functionMetadataDictionary.Add(functionMetadata.Identifier, functionMetadata);
						}
					}

					command.CommandText = getParameterMetadataCommandText;

					using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
					{
						while (reader.Read())
						{
							var identifier = CreateFunctionIdentifierFromReaderValues(reader[0], reader[1], reader[2], reader[3]);

							if (!functionMetadataDictionary.ContainsKey(identifier))
								continue;

							var metadata = functionMetadataDictionary[identifier];

							var parameterNameRaw = reader[4];
							var parameterName = parameterNameRaw == DBNull.Value ? null : (string)parameterNameRaw;
							var position = Convert.ToInt32(reader[5]);
							var dataTypeRaw = reader[6];
							var dataType = dataTypeRaw == DBNull.Value ? null : (string)dataTypeRaw;
							var isOptional = (string)reader[7] == "Y";
							var directionRaw = (string)reader[8];
							ParameterDirection direction;
							switch (directionRaw)
							{
								case "IN":
									direction = ParameterDirection.Input;
									break;
								case "OUT":
									direction = String.IsNullOrEmpty(parameterName) ? ParameterDirection.ReturnValue : ParameterDirection.Output;
									break;
								case "IN/OUT":
									direction = ParameterDirection.InputOutput;
									break;
								default:
									throw new NotSupportedException(String.Format("Parameter direction '{0}' is not supported. ", directionRaw));
							}

							var parameterMetadata = new OracleFunctionParameterMetadata(parameterName, position, direction, dataType, isOptional);
							metadata.Parameters.Add(parameterMetadata);
						}
					}
				}
			}

			return new OracleFunctionMetadataCollection(functionMetadataDictionary.Values);
		}

		internal static OracleFunctionIdentifier CreateFunctionIdentifierFromReaderValues(object owner, object package, object name, object overload)
		{
			return OracleFunctionIdentifier.CreateFromValues(owner == DBNull.Value ? null : QualifyStringObject(owner), package == DBNull.Value ? null : QualifyStringObject(package), QualifyStringObject(name), Convert.ToInt32(overload));
		}

		private int ExecuteUserNonQuery(string commandText)
		{
			return ExecuteUserStatement(commandText, c => c.ExecuteNonQuery(), true);
		}

		private T ExecuteUserStatement<T>(string commandText, Func<OracleCommand, T> executeFunction, bool closeConnection = false)
		{
			_userCommand = _userConnection.CreateCommand();
			_userCommand.CommandText = String.Format("ALTER SESSION SET CURRENT_SCHEMA = {0}", _currentSchema);

			lock (_lockObject)
			{
				try
				{
					_isExecuting = true;
					_userConnection.Open();

					_userCommand.ExecuteNonQuery();

					_userCommand.CommandText = commandText;

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
					_userDataReader = await ExecuteUserStatement(statementText, c => c.ExecuteReaderAsynchronous(CommandBehavior.CloseConnection, cancellationToken));
				}
				else
				{
					affectedRowCount = await ExecuteUserStatement(statementText, c => c.ExecuteNonQueryAsynchronous(cancellationToken));
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

			DisposeCommandAndReader();
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

			for (var i = 0; i < _userDataReader.FieldCount; i++)
			{
				var fieldType = _userDataReader.GetDataTypeName(i);
				Trace.Write(i + ". " + fieldType + "; ");
			}

			Trace.WriteLine(String.Empty);

			for (var i = 0; i < rowCount; i++)
			{
				if (_userDataReader.Read())
				{
					var columnData = new object[_userDataReader.FieldCount];
					_userDataReader.GetValues(columnData);
					yield return columnData;
				}
				else
				{
					_userDataReader.Close();
					break;
				}
			}
		}

		private void CheckCanFetch()
		{
			if (_userDataReader == null || _userDataReader.IsClosed)
				throw new InvalidOperationException("No data reader available. ");
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

					using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
					{
						while (reader.Read())
						{
							yield return formatFunction(reader);
						}
					}
				}
			}
		}

		private void LoadSchemaObjectMetadata(bool force)
		{
			TryLoadSchemaObjectMetadataFromCache();

			//var otmp = _dataDictionary.AllObjects.Where(o => o.Key.NormalizedName.Contains("XMLTYPE")).ToArray();
			//var customer = _dataDictionary.AllObjects.Where(o => o.Key.NormalizedName.Contains("CUSTOMER\"")).ToArray();

			if (!IsRefreshNeeded && !force)
			{
				return;
			}

			var reason = force ? "has been forced to refresh" : (_dataDictionary.Timestamp > DateTime.MinValue ? "has expired" : "does not exist");
			Trace.WriteLine(String.Format("{0} - Cache for '{1}' {2}. Cache refresh started. ", DateTime.Now, CachedConnectionStringName, reason));

			RaiseEvent(RefreshStarted);
			var lastRefresh = DateTime.Now;
			_isRefreshing = true;

			var allObjects = new Dictionary<OracleObjectIdentifier, OracleSchemaObject>();

			var schemaTypeMetadataSource = ExecuteReader(
				DatabaseCommands.SelectTypesCommandText, null,
				r =>
				{
					OracleTypeBase schemaType = null;
					var typeType = (string)r["TYPECODE"];
					switch (typeType)
					{
						case OracleTypeBase.XmlType:
						case OracleTypeBase.ObjectType:
							schemaType =
								new OracleObjectType
								{

								};
							break;
						case OracleTypeBase.CollectionType:
							schemaType =
								new OracleCollectionType
								{

								};
							break;
					}

					schemaType.FullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TYPE_NAME"]));

					return schemaType;
				});

			foreach (var schemaType in schemaTypeMetadataSource)
			{
				AddSchemaObjectToDictionary(allObjects, schemaType);
			}

			ExecuteReader(
				DatabaseCommands.SelectAllObjectsCommandText, null,
				r =>
				{
					var objectTypeIdentifer = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["OBJECT_NAME"]));
					var objectType = (string)r["OBJECT_TYPE"];
					var created = (DateTime)r["CREATED"];
					var isValid = (string)r["STATUS"] == "VALID";
					var lastDdl = (DateTime)r["LAST_DDL_TIME"];
					var isTemporary = (string)r["TEMPORARY"] == "Y";
					
					OracleSchemaObject schemaObject;
					if (objectType == OracleSchemaObjectType.Type)
					{
						if (allObjects.TryGetValue(objectTypeIdentifer, out schemaObject))
						{
							schemaObject.Created = created;
							schemaObject.IsTemporary = isTemporary;
							schemaObject.IsValid = isValid;
							schemaObject.LastDdl = lastDdl;
						}
					}
					else
					{
						schemaObject = OracleObjectFactory.CreateSchemaObjectMetadata(objectType, objectTypeIdentifer.NormalizedOwner, objectTypeIdentifer.NormalizedName, isValid, created, lastDdl, isTemporary);
						AddSchemaObjectToDictionary(allObjects, schemaObject);
					}

					return schemaObject;
				})
				.ToArray();

			ExecuteReader(
				DatabaseCommands.SelectTablesCommandText, null,
				r =>
				{
					var tableFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject schemaObject;
					if (!allObjects.TryGetValue(tableFullyQualifiedName, out schemaObject))
					{
						return null;
					}

					var table = (OracleTable)schemaObject;
					table.Organization = (OrganizationType)Enum.Parse(typeof(OrganizationType), (string)r["ORGANIZATION"]);
					return table;
				})
				.ToArray();

			ExecuteReader(
				DatabaseCommands.SelectSynonymTargetsCommandText, null,
				r =>
				{
					var synonymFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["SYNONYM_NAME"]));
					OracleSchemaObject synonymObject;
					if (!allObjects.TryGetValue(synonymFullyQualifiedName, out synonymObject))
					{
						return null;
					}

					var objectFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["TABLE_OWNER"]), QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject schemaObject;
					if (!allObjects.TryGetValue(objectFullyQualifiedName, out schemaObject))
					{
						return null;
					}

					var synonym = (OracleSynonym)synonymObject;
					synonym.SchemaObject = schemaObject;
					schemaObject.Synonym = synonym;

					return synonymObject;
				}
				).ToArray();

			var columnMetadataSource = ExecuteReader(
				DatabaseCommands.SelectTableColumnsCommandText, null,
				r =>
				{
					var dataTypeOwnerRaw = r["DATA_TYPE_OWNER"];
					var dataTypeOwner = dataTypeOwnerRaw == DBNull.Value ? null : String.Format("{0}.", dataTypeOwnerRaw);
					var type = String.Format("{0}{1}", dataTypeOwner, r["DATA_TYPE"]);
					var precisionRaw = r["DATA_PRECISION"];
					var scaleRaw = r["DATA_SCALE"];
					return new KeyValuePair<OracleObjectIdentifier, OracleColumn>(
						OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TABLE_NAME"])),
						new OracleColumn
						{
							Name = QualifyStringObject(r["COLUMN_NAME"]),
							Nullable = (string)r["NULLABLE"] == "Y",
							Type = type,
							Size = Convert.ToInt32(r["DATA_LENGTH"]),
							CharacterSize = Convert.ToInt32(r["CHAR_LENGTH"]),
							Precision = precisionRaw == DBNull.Value ? null : (int?)Convert.ToInt32(precisionRaw),
							Scale = scaleRaw == DBNull.Value ? null : (int?)Convert.ToInt32(scaleRaw),
							Unit = type.In("VARCHAR", "VARCHAR2")
								? (string)r["CHAR_USED"] == "C" ? DataUnit.Character : DataUnit.Byte
								: DataUnit.NotApplicable
						});
				});

			foreach (var columnMetadata in columnMetadataSource)
			{
				OracleSchemaObject schemaObject;
				if (!allObjects.TryGetValue(columnMetadata.Key, out schemaObject))
					continue;

				var dataObject = (OracleDataObject)schemaObject;
				dataObject.Columns.Add(columnMetadata.Value.Name, columnMetadata.Value);
			}

			var constraintSource = ExecuteReader(
				DatabaseCommands.SelectConstraintsCommandText, null,
				r =>
				{
					var remoteConstraintIdentifier = OracleObjectIdentifier.Empty;
					var owner = QualifyStringObject(r["OWNER"]);
					var ownerObjectFullyQualifiedName = OracleObjectIdentifier.Create(owner, QualifyStringObject(r["TABLE_NAME"]));
					OracleSchemaObject ownerObject;
					if (!allObjects.TryGetValue(ownerObjectFullyQualifiedName, out ownerObject))
						return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(null, remoteConstraintIdentifier); ;

					var relyRaw = r["RELY"];
					var constraint = OracleObjectFactory.CreateConstraint((string)r["CONSTRAINT_TYPE"], owner, QualifyStringObject(r["CONSTRAINT_NAME"]), (string)r["STATUS"] == "ENABLED", (string)r["VALIDATED"] == "VALIDATED", (string)r["DEFERRABLE"] == "DEFERRABLE", relyRaw != DBNull.Value && (string)relyRaw == "RELY");
					constraint.Owner = ownerObject;
					((OracleDataObject)ownerObject).Constraints.Add(constraint);

					var foreignKeyConstraint = constraint as OracleForeignKeyConstraint;
					if (foreignKeyConstraint != null)
					{
						var cascadeAction = DeleteRule.None;
						switch ((string)r["DELETE_RULE"])
						{
							case "CASCADE":
								cascadeAction = DeleteRule.Cascade;
								break;
							case "SET NULL":
								cascadeAction = DeleteRule.SetNull;
								break;
							case "NO ACTION":
								break;
						}

						foreignKeyConstraint.DeleteRule = cascadeAction;
						remoteConstraintIdentifier = OracleObjectIdentifier.Create(QualifyStringObject(r["R_OWNER"]), QualifyStringObject(r["R_CONSTRAINT_NAME"]));
					}
					
					return new KeyValuePair<OracleConstraint, OracleObjectIdentifier>(constraint, remoteConstraintIdentifier);
				})
				.Where(c => c.Key != null)
				.ToArray();

			var constraints = new Dictionary<OracleObjectIdentifier, OracleConstraint>();
			foreach (var constraintPair in constraintSource)
			{
				constraints[constraintPair.Key.FullyQualifiedName] = constraintPair.Key;
			}

			var constraintColumns = ExecuteReader(
				DatabaseCommands.SelectConstraintColumnsCommandText, null,
				r =>
				{
					var column = (string)r["COLUMN_NAME"];
					return new KeyValuePair<OracleObjectIdentifier, string>(OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["CONSTRAINT_NAME"])), column[0] == '"' ? column : QualifyStringObject(column));
				})
				.GroupBy(c => c.Key)
				.ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Value).ToList());

			foreach (var constraintPair in constraintSource)
			{
				OracleConstraint constraint;
				if (!constraints.TryGetValue(constraintPair.Key.FullyQualifiedName, out constraint))
					continue;

				List<string> columns;
				if (constraintColumns.TryGetValue(constraintPair.Key.FullyQualifiedName, out columns))
				{
					constraint.Columns = columns.AsReadOnly();
				}

				var foreignKeyConstraint = constraintPair.Key as OracleForeignKeyConstraint;
				if (foreignKeyConstraint == null)
					continue;

				var referenceConstraint = (OracleUniqueConstraint)constraints[constraintPair.Value];
				foreignKeyConstraint.TargetObject = referenceConstraint.Owner;
				foreignKeyConstraint.ReferenceConstraint = referenceConstraint;
			}

			ExecuteReader(
				DatabaseCommands.SelectSequencesCommandText, null,
				r =>
				{
					var sequenceFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["SEQUENCE_OWNER"]), QualifyStringObject(r["SEQUENCE_NAME"]));
					OracleSchemaObject sequenceObject;
					if (!AllObjects.TryGetValue(sequenceFullyQualifiedName, out sequenceObject))
						return null;

					var sequence = (OracleSequence) sequenceObject;
					sequence.CurrentValue = Convert.ToDecimal(r["LAST_NUMBER"]);
					sequence.MinimumValue = Convert.ToDecimal(r["MIN_VALUE"]);
					sequence.MaximumValue = Convert.ToDecimal(r["MAX_VALUE"]);
					sequence.Increment = Convert.ToDecimal(r["INCREMENT_BY"]);
					sequence.CacheSize = Convert.ToDecimal(r["CACHE_SIZE"]);
					sequence.CanCycle = (string)r["CYCLE_FLAG"] == "Y";
					sequence.IsOrdered = (string)r["ORDER_FLAG"] == "Y";

					return sequence;
				})
				.ToArray();

			ExecuteReader(
				DatabaseCommands.SelectTypeAttributesCommandText, null,
				r =>
				{
					var typeFullyQualifiedName = OracleObjectIdentifier.Create(QualifyStringObject(r["OWNER"]), QualifyStringObject(r["TYPE_NAME"]));
					OracleSchemaObject typeObject;
					if (!AllObjects.TryGetValue(typeFullyQualifiedName, out typeObject))
						return null;

					var type = (OracleTypeBase)typeObject;
					// TODO:
					return type;
				})
				.ToArray();

			_allFunctionMetadata = new OracleFunctionMetadataCollection(BuiltInFunctionMetadata.SqlFunctions.Concat(GetUserFunctionMetadata().SqlFunctions));

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

			_dataDictionary = new OracleDataDictionary(allObjects, lastRefresh);
			CachedDataDictionaries[CachedConnectionStringName] = _dataDictionary;

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

			var functionMetadata = _dataDictionary.AllObjects.Values
				.OfType<IFunctionCollection>()
				.Where(o => o.FullyQualifiedName != BuiltInFunctionPackageIdentifier)
				.SelectMany(o => o.Functions);

			_allFunctionMetadata = new OracleFunctionMetadataCollection(BuiltInFunctionMetadata.SqlFunctions.Concat(functionMetadata));

			_cacheLoaded = true;
		}

		private void RaiseEvent(EventHandler eventHandler)
		{
			if (eventHandler != null)
			{
				eventHandler(this, EventArgs.Empty);
			}
		}

		private static void AddSchemaObjectToDictionary(IDictionary<OracleObjectIdentifier, OracleSchemaObject> allObjects, OracleSchemaObject schemaObject)
		{
			if (allObjects.ContainsKey(schemaObject.FullyQualifiedName))
			{
				Trace.WriteLine(string.Format("Object '{0}' ({1}) is already in the dictionary. ", schemaObject.FullyQualifiedName, schemaObject.Type));
			}
			else
			{
				allObjects.Add(schemaObject.FullyQualifiedName, schemaObject);
			}
		}

		private static string QualifyStringObject(object stringValue)
		{
			return String.Format("{0}{1}{0}", "\"", stringValue);
		}

		private void LoadSchemaNames()
		{
			var schemaSource = ExecuteReader(
				DatabaseCommands.SelectAllSchemasCommandText, null,
				r => ((string)r["USERNAME"]))
				.ToArray();

			_schemas = new HashSet<string>(schemaSource);
			_allSchemas = new HashSet<string>(schemaSource.Select(QualifyStringObject)) { SchemaPublic };
		}
	}
}
