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
		
		void MapData(OracleDataReader reader);
	}

	internal abstract class DataModelUpdater<TModel> : IDataModelUpdater where TModel: ModelBase
	{
		protected TModel DataModel { get; private set; }

		protected DataModelUpdater(TModel dataModel)
		{
			DataModel = dataModel;
		}

		public abstract void InitializeCommand(OracleCommand command);

		public abstract void MapData(OracleDataReader reader);

		public abstract bool CanContinue { get; }
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
			command.Parameters.Clear();
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName);
		}

		public override void MapData(OracleDataReader reader)
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
			command.Parameters.Clear();
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
			command.AddSimpleParameter("COLUMN_NAME", _columnName);
		}

		public override void MapData(OracleDataReader reader)
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
			command.Parameters.Clear();
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override void MapData(OracleDataReader reader)
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
			command.Parameters.Clear();
			command.AddSimpleParameter("OWNER", _objectIdentifier.Owner.Trim('"'));
			command.AddSimpleParameter("TABLE_NAME", _objectIdentifier.Name.Trim('"'));
		}

		public override void MapData(OracleDataReader reader)
		{
			if (!reader.Read())
			{
				return;
			}

			DataModel.AllocatedBytes = OracleReaderValueConvert.ToInt64(reader["ALLOCATED_BYTES"]);
		}

		public override bool CanContinue
		{
			get { return false; }
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
			command.Parameters.Clear();
			command.AddSimpleParameter("OBJECT_TYPE", _schemaObject.Type.ToUpperInvariant());
			command.AddSimpleParameter("NAME", _schemaObject.FullyQualifiedName.Name.Trim('"'));
			command.AddSimpleParameter("SCHEMA", _schemaObject.FullyQualifiedName.Owner.Trim('"'));
		}

		public void MapData(OracleDataReader reader)
		{
			if (reader.Read())
			{
				ScriptText = (string)reader["SCRIPT"];
			}
		}

		public bool CanContinue
		{
			get { return false; }
		}
	}

	internal class DisplayCursorUpdater : DataModelUpdater<CursorModel>
	{
		private readonly int _sessionId;

		public DisplayCursorUpdater(int sessionId, CursorModel dataModel)
			: base(dataModel)
		{
			_sessionId = sessionId;
		}

		public override void InitializeCommand(OracleCommand command)
		{
			command.Parameters.Clear();

			if (DataModel.SqlId == null)
			{
				command.CommandText = DatabaseCommands.GetExecutionPlanIdentifiers;
				command.AddSimpleParameter("SID", _sessionId);
			}
			else
			{
				command.CommandText = DatabaseCommands.GetExecutionPlanText;
				command.AddSimpleParameter("SQL_ID", DataModel.SqlId);
				command.AddSimpleParameter("CHILD_NUMBER", DataModel.ChildNumber);
			}
		}

		public override void MapData(OracleDataReader reader)
		{
			var builder = new StringBuilder();

			while (reader.Read())
			{
				if (DataModel.SqlId == null)
				{
					DataModel.SqlId = OracleReaderValueConvert.ToString(reader["SQL_ID"]);
					if (DataModel.SqlId == null)
					{
						return;
					}

					DataModel.ChildNumber = Convert.ToInt32(reader["SQL_CHILD_NUMBER"]);
					return;
				}

				builder.AppendLine(Convert.ToString(reader["PLAN_TABLE_OUTPUT"]));
			}

			DataModel.PlanText = builder.ToString();
		}

		public override bool CanContinue
		{
			get { return DataModel.SqlId != null; }
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
			command.Parameters.Clear();
		}

		public void MapData(OracleDataReader reader) { }

		public bool CanContinue { get { return false; } }
	}
}
