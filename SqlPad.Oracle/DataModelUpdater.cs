using System;
using System.Collections.Generic;
using System.Text;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.ToolTips;

namespace SqlPad.Oracle
{
	internal interface IDataModelUpdater
	{
		void InitializeCommand(OracleCommand command);
		
		bool CanContinue { get; }
		
		void MapReaderData(OracleDataReader reader);

		void MapScalarData(object value);

		bool HasScalarResult { get; }
	}

	internal abstract class DataModelUpdater<TModel> : IDataModelUpdater where TModel: ModelBase
	{
		protected TModel DataModel { get; private set; }

		protected DataModelUpdater(TModel dataModel)
		{
			DataModel = dataModel;
		}

		public abstract void InitializeCommand(OracleCommand command);

		public virtual void MapReaderData(OracleDataReader reader)
		{
			throw new NotImplementedException();
		}

		public abstract bool CanContinue { get; }

		public virtual bool HasScalarResult { get { return false; } }

		public virtual void MapScalarData(object value)
		{
			throw new NotImplementedException();
		}
	}

	internal class ColumnDetailsModelUpdater : DataModelUpdater<ColumnDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;

		public ColumnDetailsModelUpdater(ColumnDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.GetColumnStatisticsCommand;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName);
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}
			
			DataModel.DistinctValueCount = Convert.ToInt32(reader["NUM_DISTINCT"]);
			DataModel.LastAnalyzed = (DateTime)reader["LAST_ANALYZED"];
			DataModel.NullValueCount = Convert.ToInt32(reader["NUM_NULLS"]);
			DataModel.SampleSize = OracleReaderValueConvert.ToInt32(reader["SAMPLE_SIZE"]);
			DataModel.AverageValueSize = Convert.ToInt32(reader["AVG_COL_LEN"]);
			DataModel.HistogramBucketCount = Convert.ToInt32(reader["NUM_BUCKETS"]);
			DataModel.HistogramType = (string)reader["HISTOGRAM"];
		}

		public override bool CanContinue
		{
			get { return DataModel.HistogramType != null && DataModel.HistogramType != "None"; }
		}
	}

	internal class ColumnDetailsHistogramUpdater : DataModelUpdater<ColumnDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		private readonly string _columnName;

		public ColumnDetailsHistogramUpdater(ColumnDetailsModel dataModel, OracleObjectIdentifier objectIdentifier, string columnName)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
			_columnName = columnName;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.GetColumnHistogramCommand;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName);
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			var histogramValues = new List<double>();

			while (reader.Read())
			{
				histogramValues.Add(Convert.ToInt32(reader["ENDPOINT_NUMBER"]));
			}

			DataModel.HistogramValues = histogramValues;
		}

		public override bool CanContinue
		{
			get { return false; }
		}
	}

	internal class TableDetailsModelUpdater : DataModelUpdater<TableDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;

		public TableDetailsModelUpdater(TableDetailsModel dataModel, OracleObjectIdentifier objectIdentifier)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.GetTableDetailsCommand;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override void MapReaderData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}

			DataModel.RowCount = OracleReaderValueConvert.ToInt32(reader["NUM_ROWS"]);
			DataModel.LastAnalyzed = OracleReaderValueConvert.ToDateTime(reader["LAST_ANALYZED"]);
			DataModel.AverageRowSize = OracleReaderValueConvert.ToInt32(reader["AVG_ROW_LEN"]);
			DataModel.BlockCount = OracleReaderValueConvert.ToInt32(reader["BLOCKS"]);
			DataModel.Compression = OracleReaderValueConvert.ToString(reader["COMPRESSION"]);
			DataModel.Organization = OracleReaderValueConvert.ToString(reader["ORGANIZATION"]);
			DataModel.ClusterName = OracleReaderValueConvert.ToString(reader["CLUSTER_NAME"]);
			DataModel.IsTemporary = (string)reader["TEMPORARY"] == "Y";
			DataModel.IsPartitioned = (string)reader["PARTITIONED"] == "YES";
		}

		public override bool CanContinue
		{
			get { return true; }
		}
	}

	internal class TableSpaceAllocationModelUpdater : DataModelUpdater<TableDetailsModel>
	{
		private readonly OracleObjectIdentifier _objectIdentifier;
		
		public TableSpaceAllocationModelUpdater(TableDetailsModel dataModel,  OracleObjectIdentifier objectIdentifier)
			: base(dataModel)
		{
			_objectIdentifier = objectIdentifier;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.GetTableAllocatedBytesCommand;
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override void MapScalarData(object value)
		{
			DataModel.AllocatedBytes = OracleReaderValueConvert.ToInt64(value);
		}

		public override bool CanContinue
		{
			get { return false; }
		}

		public override bool HasScalarResult
		{
			get { return true; }
		}
	}

	internal class ObjectScriptUpdater : IDataModelUpdater
	{
		private readonly OracleSchemaObject _schemaObject;

		public string ScriptText { get; private set; }

		public ObjectScriptUpdater(OracleSchemaObject schemaObject)
		{
			_schemaObject = schemaObject;
		}

		public void InitializeCommand(OracleCommand command)
		{
			command.CommandText = DatabaseCommands.GetObjectScriptCommand;
			command.AddSimpleParameter("OBJECT_TYPE", _schemaObject.Type.ToUpperInvariant());
			command.AddSimpleParameter("NAME", _schemaObject.FullyQualifiedName.Name.Trim('"'));
			command.AddSimpleParameter("SCHEMA", _schemaObject.FullyQualifiedName.Owner.Trim('"'));
		}

		public void MapReaderData(OracleDataReader reader)
		{
			throw new NotSupportedException();
		}

		public void MapScalarData(object value)
		{
			ScriptText = (string)value;
		}

		public bool CanContinue
		{
			get { return false; }
		}

		public bool HasScalarResult { get { return true; } }
	}

	internal class DisplayCursorUpdater
	{
		private readonly CursorModel _cursorModel;
		
		public DisplayCursorUpdater(int sessionId)
		{
			_cursorModel = new CursorModel(sessionId);
			ActiveCommandIdentifierUpdater = new ActiveCommandIdentifierUpdaterInternal(_cursorModel);
			DisplayCursorOutputUpdater = new DisplayCursorUpdaterInternal(_cursorModel);
		}

		public IDataModelUpdater ActiveCommandIdentifierUpdater { get; private set; }

		public IDataModelUpdater DisplayCursorOutputUpdater { get; private set; }

		public string PlanText { get { return _cursorModel.PlanText; } }

		private class CursorModel
		{
			public CursorModel(int sessionId)
			{
				SessionId = sessionId;
			}

			public int SessionId { get; private set; }

			public string PlanText { get; set; }

			public string SqlId { get; set; }

			public int ChildNumber { get; set; }
		}

		private class ActiveCommandIdentifierUpdaterInternal : IDataModelUpdater
		{
			private readonly CursorModel _cursorModel;

			public ActiveCommandIdentifierUpdaterInternal(CursorModel cursorModel)
			{
				_cursorModel = cursorModel;
			}

			public void InitializeCommand(OracleCommand command)
			{
				command.CommandText = DatabaseCommands.GetExecutionPlanIdentifiers;
				command.AddSimpleParameter("SID", _cursorModel.SessionId);
			}

			public void MapReaderData(OracleDataReader reader)
			{
				if (!reader.Read())
				{
					return;
				}

				_cursorModel.SqlId = OracleReaderValueConvert.ToString(reader["SQL_ID"]);
				if (_cursorModel.SqlId == null)
				{
					return;
				}

				_cursorModel.ChildNumber = Convert.ToInt32(reader["SQL_CHILD_NUMBER"]);
			}

			public void MapScalarData(object value)
			{
				throw new NotSupportedException();
			}

			public bool CanContinue
			{
				get { return _cursorModel.SqlId != null; }
			}

			public bool HasScalarResult { get { return false; } }
		}

		private class DisplayCursorUpdaterInternal : IDataModelUpdater
		{
			private readonly CursorModel _cursorModel;

			public DisplayCursorUpdaterInternal(CursorModel cursorModel)
			{
				_cursorModel = cursorModel;
			}

			public void InitializeCommand(OracleCommand command)
			{
				command.CommandText = DatabaseCommands.GetExecutionPlanText;
				command.AddSimpleParameter("SQL_ID", _cursorModel.SqlId);
				command.AddSimpleParameter("CHILD_NUMBER", _cursorModel.ChildNumber);
			}

			public void MapReaderData(OracleDataReader reader)
			{
				var builder = new StringBuilder();

				while (reader.Read())
				{
					builder.AppendLine(Convert.ToString(reader["PLAN_TABLE_OUTPUT"]));
				}

				_cursorModel.PlanText = builder.ToString();
			}

			public void MapScalarData(object value)
			{
				throw new NotSupportedException();
			}

			public bool CanContinue
			{
				get { return false; }
			}

			public bool HasScalarResult { get { return false; } }
		}
	}

	internal class ExplainPlanUpdater : IDataModelUpdater
	{
		private readonly string _statementText;
		private readonly string _planKey;
		private readonly OracleObjectIdentifier _targetTableIdentifier;

		public ExplainPlanUpdater(string statementText, string planKey, OracleObjectIdentifier targetTableIdentifier)
		{
			_statementText = statementText;
			_planKey = planKey;
			_targetTableIdentifier = targetTableIdentifier;
		}

		public void InitializeCommand(OracleCommand command)
		{
			var targetTable = _targetTableIdentifier.ToString();
			command.CommandText = String.Format("EXPLAIN PLAN SET STATEMENT_ID = '{0}' INTO {1} FOR {2}", _planKey, targetTable, _statementText);
		}

		public void MapReaderData(OracleDataReader reader) { }

		public void MapScalarData(object value)
		{
			throw new NotSupportedException();
		}

		public bool CanContinue { get { return false; } }

		public bool HasScalarResult { get { return false; } }
	}

	internal class SessionExecutionStatisticsUpdater
	{
		private readonly SessionExecutionStatisticsModel _statisticsModel;

		public IDataModelUpdater SessionBeginExecutionStatisticsUpdater { get; private set; }

		public IDataModelUpdater SessionEndExecutionStatisticsUpdater { get; private set; }

		public ICollection<SessionExecutionStatisticsRecord> ExecutionStatistics { get { return _statisticsModel.StatisticsRecords.Values; } }

		public SessionExecutionStatisticsUpdater(IDictionary<int, string> statisticsKeys, int sessionId)
		{
			_statisticsModel = new SessionExecutionStatisticsModel(statisticsKeys, sessionId);
			SessionBeginExecutionStatisticsUpdater = new SessionExecutionStatisticsUpdaterInternal(_statisticsModel, true);
			SessionEndExecutionStatisticsUpdater = new SessionExecutionStatisticsUpdaterInternal(_statisticsModel, false);
		}

		private class SessionExecutionStatisticsUpdaterInternal : DataModelUpdater<SessionExecutionStatisticsModel>
		{
			private readonly bool _executionStart;

			public SessionExecutionStatisticsUpdaterInternal(SessionExecutionStatisticsModel dataModel, bool executionStart)
				: base(dataModel)
			{
				_executionStart = executionStart;
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = DatabaseCommands.GetSessionsStatistics;
				command.AddSimpleParameter("SID", DataModel.SessionId);

				DataModel.StatisticsRecords.Clear();
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				if (DataModel.StatisticsKeys.Count == 0)
				{
					return;
				}

				while (reader.Read())
				{
					var statisticsRecord =
						new SessionExecutionStatisticsRecord
						{
							Name = DataModel.StatisticsKeys[Convert.ToInt32(reader["STATISTIC#"])],
							Value = Convert.ToDecimal(reader["VALUE"])
						};

					if (_executionStart)
					{
						DataModel.ExecutionStartRecords[statisticsRecord.Name] = statisticsRecord;
					}
					else
					{
						SessionExecutionStatisticsRecord executionStartRecord;
						var executionStartValue = 0m;
						if (DataModel.ExecutionStartRecords.TryGetValue(statisticsRecord.Name, out executionStartRecord))
						{
							executionStartValue = executionStartRecord.Value;
						}

						statisticsRecord.Value = executionStartValue;
						DataModel.StatisticsRecords[statisticsRecord.Name] = statisticsRecord;
					}
				}
			}

			public override bool CanContinue
			{
				get { return DataModel.StatisticsKeys.Count > 0; }
			}
		}

		private class SessionExecutionStatisticsModel : ModelBase
		{
			public readonly Dictionary<string, SessionExecutionStatisticsRecord> ExecutionStartRecords = new Dictionary<string, SessionExecutionStatisticsRecord>();
			public Dictionary<string, SessionExecutionStatisticsRecord> StatisticsRecords = new Dictionary<string, SessionExecutionStatisticsRecord>();

			public int SessionId { get; private set; }

			public IDictionary<int, string> StatisticsKeys { get; private set; }

			public SessionExecutionStatisticsModel(IDictionary<int, string> statisticsKeys, int sessionId)
			{
				StatisticsKeys = statisticsKeys;
				SessionId = sessionId;
			}
		}
	}
}
