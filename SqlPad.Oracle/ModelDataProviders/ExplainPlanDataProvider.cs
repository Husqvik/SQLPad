using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml.Linq;
using Oracle.DataAccess.Client;
using SqlPad.Oracle.ExecutionPlan;

namespace SqlPad.Oracle.ModelDataProviders
{
	internal class ExplainPlanDataProvider
	{
		private readonly ExplainPlanModelInternal _dataMmodel;
		
		public IModelDataProvider CreateExplainPlanUpdater { get; private set; }
		
		public IModelDataProvider LoadExplainPlanUpdater { get; private set; }

		public ExecutionPlanItem RootItem { get { return _dataMmodel.RootItem; } }

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
				command.CommandText = String.Format(DatabaseCommands.ExplainPlanBase, DataModel.TargetTableName);
				command.AddSimpleParameter("STATEMENT_ID", DataModel.ExecutionPlanKey);
			}

			public override void MapReaderData(OracleDataReader reader)
			{
				DataModel.RootItem = _planBuilder.Build(reader);
			}
		}

		private class ExplainPlanModelInternal : ModelBase
		{
			public string StatementText { get; private set; }
			
			public string ExecutionPlanKey { get; private set; }
			
			public string TargetTableName { get; private set; }

			public ExecutionPlanItem RootItem { get; set; }
			
			public ExplainPlanModelInternal(string statementText, string executionPlanKey, OracleObjectIdentifier targetTableIdentifier)
			{
				StatementText = statementText;
				ExecutionPlanKey = executionPlanKey;
				TargetTableName = targetTableIdentifier.ToString();
			}
		}
	}

	internal class ExecutionPlanBuilder : ExecutionPlanBuilderBase<ExecutionPlanItem> { }

	internal class ExecutionPlanBuilderBase<TItem> where TItem : ExecutionPlanItem, new()
	{
		private readonly Dictionary<int, TItem> _treeItemDictionary = new Dictionary<int, TItem>();
		private readonly List<ExecutionPlanItem> _leafItems = new List<ExecutionPlanItem>();
		private int _currentExecutionStep;

		public TItem Build(OracleDataReader reader)
		{
			Initialize();

			while (reader.Read())
			{
				var parentId = OracleReaderValueConvert.ToInt32(reader["PARENT_ID"]);
				var item = CreatePlanItem(reader);

				FillData(reader, item);

				if (parentId.HasValue)
				{
					_treeItemDictionary[parentId.Value].AddChildItem(item);
				}

				_treeItemDictionary.Add(item.Id, item);
			}

			if (_treeItemDictionary.Count == 0)
			{
				return null;
			}

			_leafItems.AddRange(_treeItemDictionary.Values.Where(v => v.IsLeaf));
			var startNode = _leafItems[0];
			_leafItems.RemoveAt(0);

			ResolveExecutionOrder(startNode, null);

			return _treeItemDictionary[0];
		}

		private void Initialize()
		{
			_currentExecutionStep = 0;
			_treeItemDictionary.Clear();
			_leafItems.Clear();
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

		private void ResolveExecutionOrder(ExecutionPlanItem nextItem, ExecutionPlanItem breakAtItem)
		{
			do
			{
				if (nextItem == breakAtItem)
				{
					return;
				}

				nextItem.ExecutionOrder = ++_currentExecutionStep;
				if (nextItem.Parent == null)
				{
					return;
				}

				nextItem = nextItem.Parent;
				var allChildrenExecuted = nextItem.ChildItems.Count(i => i.ExecutionOrder == 0) == 0;
				if (!allChildrenExecuted)
				{
					var otherBranchItemIndex = _leafItems.FindIndex(i => i.IsChildFrom(nextItem));
					if (otherBranchItemIndex == -1)
					{
						return;
					}

					var otherBranchLeafItem = _leafItems[otherBranchItemIndex];
					_leafItems.RemoveAt(otherBranchItemIndex);

					ResolveExecutionOrder(otherBranchLeafItem, nextItem);
				}
			} while (true);
		}
	}
}
