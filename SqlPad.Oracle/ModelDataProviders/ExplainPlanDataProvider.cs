using System;
using System.Data;
using System.Xml.Linq;
using SqlPad.Oracle.DataDictionary;
#if ORACLE_MANAGED_DATA_ACCESS_CLIENT
using Oracle.ManagedDataAccess.Client;
#else
using Oracle.DataAccess.Client;
#endif
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class ExplainPlanDataProvider
	{
		private readonly ExplainPlanModelInternal _dataMmodel;
		
		public IModelDataProvider CreateExplainPlanUpdater { get; private set; }
		
		public IModelDataProvider LoadExplainPlanUpdater { get; private set; }

		public ExecutionPlanItemCollection ItemCollection { get { return _dataMmodel.ItemCollection; } }

		public ExplainPlanDataProvider(string statementText, string planKey, OracleObjectIdentifier targetTableIdentifier)
		{
			_dataMmodel = new ExplainPlanModelInternal(statementText, planKey, targetTableIdentifier);
			CreateExplainPlanUpdater = new CreateExplainPlanDataProviderInternal(_dataMmodel);
			LoadExplainPlanUpdater = new LoadExplainPlanDataProviderInternal(_dataMmodel);
		}

		private class CreateExplainPlanDataProviderInternal : ModelDataProvider<ExplainPlanModelInternal>
		{
			public CreateExplainPlanDataProviderInternal(ExplainPlanModelInternal model) : base(model)
			{
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = String.Format("EXPLAIN PLAN SET STATEMENT_ID = '{0}' INTO {1} FOR\n{2}", DataModel.ExecutionPlanKey, DataModel.TargetTableName, DataModel.StatementText);
			}

			public override void MapReaderData(OracleDataReader reader) { }
		}

		private class LoadExplainPlanDataProviderInternal : ModelDataProvider<ExplainPlanModelInternal>
		{
			private readonly ExecutionPlanBuilder _planBuilder = new ExecutionPlanBuilder();

			public LoadExplainPlanDataProviderInternal(ExplainPlanModelInternal model) : base(model)
			{
			}

			public override void InitializeCommand(OracleCommand command)
			{
				command.CommandText = String.Format(DatabaseCommands.SelectExplainPlanCommandText, DataModel.TargetTableName);
				command.AddSimpleParameter("STATEMENT_ID", DataModel.ExecutionPlanKey);
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				DataModel.ItemCollection = _planBuilder.Build(reader);
			}
		}

		private class ExplainPlanModelInternal : ModelBase
		{
			public string StatementText { get; private set; }
			
			public string ExecutionPlanKey { get; private set; }
			
			public string TargetTableName { get; private set; }

			public ExecutionPlanItemCollection ItemCollection { get; set; }
			
			public ExplainPlanModelInternal(string statementText, string executionPlanKey, OracleObjectIdentifier targetTableIdentifier)
			{
				StatementText = statementText;
				ExecutionPlanKey = executionPlanKey;
				TargetTableName = targetTableIdentifier.ToString();
			}
		}
	}

	internal class ExecutionPlanBuilder : ExecutionPlanBuilderBase<ExecutionPlanItemCollection, ExecutionPlanItem> { }

	internal abstract class ExecutionPlanBuilderBase<TCollection, TItem> where TCollection : ExecutionPlanItemCollectionBase<TItem>, new() where TItem : ExecutionPlanItem, new()
	{
		public TCollection Build(OracleDataReader reader)
		{
			var planItemCollection = new TCollection();

			while (reader.Read())
			{
				var item = CreatePlanItem(reader);

				FillData(reader, item);

				planItemCollection.Add(item);
			}

			planItemCollection.Freeze();

			return planItemCollection;
		}

		protected virtual void FillData(IDataRecord reader, TItem item) { }

		private static TItem CreatePlanItem(IDataRecord reader)
		{
			var time = OracleReaderValueConvert.ToInt32(reader["TIME"]);
			var otherData = OracleReaderValueConvert.ToString(reader["OTHER_XML"]);

			return
				new TItem
				{
					Id = Convert.ToInt32(reader["ID"]),
					ParentId = OracleReaderValueConvert.ToInt32(reader["PARENT_ID"]),
					Depth = Convert.ToInt32(reader["DEPTH"]),
					Operation = (string)reader["OPERATION"],
					Options = OracleReaderValueConvert.ToString(reader["OPTIONS"]),
					Optimizer = OracleReaderValueConvert.ToString(reader["OPTIMIZER"]),
					ObjectOwner = OracleReaderValueConvert.ToString(reader["OBJECT_OWNER"]),
					ObjectName = OracleReaderValueConvert.ToString(reader["OBJECT_NAME"]),
					ObjectAlias = OracleReaderValueConvert.ToString(reader["OBJECT_ALIAS"]),
					ObjectType = OracleReaderValueConvert.ToString(reader["OBJECT_TYPE"]),
					Cost = OracleReaderValueConvert.ToInt64(reader["COST"]),
					Cardinality = OracleReaderValueConvert.ToInt64(reader["CARDINALITY"]),
					Bytes = OracleReaderValueConvert.ToInt64(reader["BYTES"]),
					PartitionStart = OracleReaderValueConvert.ToString(reader["PARTITION_START"]),
					PartitionStop = OracleReaderValueConvert.ToString(reader["PARTITION_STOP"]),
					Distribution = OracleReaderValueConvert.ToString(reader["DISTRIBUTION"]),
					CpuCost = OracleReaderValueConvert.ToInt64(reader["CPU_COST"]),
					IoCost = OracleReaderValueConvert.ToInt64(reader["IO_COST"]),
					TempSpace = OracleReaderValueConvert.ToInt64(reader["TEMP_SPACE"]),
					AccessPredicates = OracleReaderValueConvert.ToString(reader["ACCESS_PREDICATES"]),
					FilterPredicates = OracleReaderValueConvert.ToString(reader["FILTER_PREDICATES"]),
					Time = time.HasValue ? TimeSpan.FromSeconds(time.Value) : (TimeSpan?)null,
					QueryBlockName = OracleReaderValueConvert.ToString(reader["QBLOCK_NAME"]),
					Other = String.IsNullOrEmpty(otherData) ? null : XElement.Parse(otherData)
				};
		}
	}
}
