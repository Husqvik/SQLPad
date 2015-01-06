using System;
using System.Collections.Generic;
using Oracle.DataAccess.Client;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class SessionExecutionStatisticsDataProvider
	{
		private readonly SessionExecutionStatisticsModel _statisticsModel;

		public IModelDataProvider SessionBeginExecutionStatisticsDataProvider { get; private set; }

		public IModelDataProvider SessionEndExecutionStatisticsDataProvider { get; private set; }

		public ICollection<SessionExecutionStatisticsRecord> ExecutionStatistics { get { return _statisticsModel.StatisticsRecords.Values; } }

		public SessionExecutionStatisticsDataProvider(IDictionary<int, string> statisticsKeys, int sessionId)
		{
			_statisticsModel = new SessionExecutionStatisticsModel(statisticsKeys, sessionId);
			SessionBeginExecutionStatisticsDataProvider = new SessionExecutionStatisticsDataProviderInternal(_statisticsModel, true);
			SessionEndExecutionStatisticsDataProvider = new SessionExecutionStatisticsDataProviderInternal(_statisticsModel, false);
		}

		private class SessionExecutionStatisticsDataProviderInternal : ModelDataProvider<SessionExecutionStatisticsModel>
		{
			private readonly bool _executionStart;

			public SessionExecutionStatisticsDataProviderInternal(SessionExecutionStatisticsModel dataModel, bool executionStart)
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

			public override bool IsValid
			{
				get { return _executionStart || DataModel.StatisticsKeys.Count > 0; }
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				if (DataModel.StatisticsKeys.Count == 0)
				{
					return;
				}

				if (!_executionStart && !DataModel.IsInitialized)
				{
					throw new InvalidOperationException("Execution start statistics have not been set. ");
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

						statisticsRecord.Value = statisticsRecord.Value - executionStartValue;
						DataModel.StatisticsRecords[statisticsRecord.Name] = statisticsRecord;
					}
				}

				if (_executionStart)
				{
					DataModel.ExecutionStartRecordsSet();
				}
			}
		}

		private class SessionExecutionStatisticsModel : ModelBase
		{
			public readonly Dictionary<string, SessionExecutionStatisticsRecord> ExecutionStartRecords = new Dictionary<string, SessionExecutionStatisticsRecord>();
			public readonly Dictionary<string, SessionExecutionStatisticsRecord> StatisticsRecords = new Dictionary<string, SessionExecutionStatisticsRecord>();

			public int SessionId { get; private set; }

			public bool IsInitialized { get; private set; }

			public IDictionary<int, string> StatisticsKeys { get; private set; }

			public void ExecutionStartRecordsSet()
			{
				if (IsInitialized)
				{
					throw new InvalidOperationException("Execution start statistics have been set already. ");
				}

				IsInitialized = true;
			}

			public SessionExecutionStatisticsModel(IDictionary<int, string> statisticsKeys, int sessionId)
			{
				StatisticsKeys = statisticsKeys;
				SessionId = sessionId;
			}
		}
	}
}
