using System;
using System.Collections.Generic;
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
				var treeItemDictionary = new Dictionary<int, ExecutionPlanItem>();
				ExecutionPlanItem startItem = null;
				
				while (reader.Read())
				{
					var time = OracleReaderValueConvert.ToInt32(reader["TIME"]);
					var otherData = OracleReaderValueConvert.ToString(reader["OTHER_XML"]);
					var parentId = OracleReaderValueConvert.ToInt32(reader["PARENT_ID"]);
					var item =
						new ExecutionPlanItem
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

					if (parentId.HasValue)
					{
						treeItemDictionary[parentId.Value].AddChildItem(item);
					}

					if (startItem == null || item.Depth > startItem.Depth)
					{
						startItem = item;
					}

					treeItemDictionary.Add(item.Id, item);
				}

				if (treeItemDictionary.Count > 0)
				{
					DataModel.RootItem = treeItemDictionary[0];

					var leafItems = treeItemDictionary.Values.Where(v => v.IsLeaf).ToList();
					var startNode = leafItems[0];
					leafItems.RemoveAt(0);

					var executionOrder = 0;
					ResolveExecutionOrder(leafItems, startNode, null, ref executionOrder);
				}
			}
		}

		private static void ResolveExecutionOrder(List<ExecutionPlanItem> leafItems, ExecutionPlanItem nextItem, ExecutionPlanItem breakAtItem, ref int executionOrder)
		{
			do
			{
				if (nextItem == breakAtItem)
				{
					return;
				}

				nextItem.ExecutionOrder = ++executionOrder;
				if (nextItem.Parent == null)
				{
					return;
				}

				nextItem = nextItem.Parent;
				var allChildrenExecuted = nextItem.ChildItems.Count(i => i.ExecutionOrder == 0) == 0;
				if (!allChildrenExecuted)
				{
					var otherBranchItemIndex = leafItems.FindIndex(i => i.IsChildFrom(nextItem));
					if (otherBranchItemIndex == -1)
					{
						return;
					}

					var otherBranchLeafItem = leafItems[otherBranchItemIndex];
					leafItems.RemoveAt(otherBranchItemIndex);

					ResolveExecutionOrder(leafItems, otherBranchLeafItem, nextItem, ref executionOrder);
				}
			} while (true);
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
}
