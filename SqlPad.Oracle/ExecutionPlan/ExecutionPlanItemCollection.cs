using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace SqlPad.Oracle.ExecutionPlan
{
	public class ExecutionPlanItemCollection : ExecutionPlanItemCollectionBase<ExecutionPlanItem> { }

	public abstract class ExecutionPlanItemCollectionBase<T> : IEnumerable<T> where T : ExecutionPlanItem
	{
		private readonly Dictionary<int, T> _allItems = new Dictionary<int, T>();
		private readonly List<ExecutionPlanItem> _leafItems = new List<ExecutionPlanItem>();

		private int _currentExecutionStep;

		public T RootItem { get; private set; }

		public IReadOnlyDictionary<int, T> AllItems { get { return _allItems; } }

		public void Add(T item)
		{
			if (_currentExecutionStep > 0)
			{
				throw new InvalidOperationException("Item cannot be added because the collection is frozen. ");
			}

			if (item.ParentId.HasValue)
			{
				_allItems[item.ParentId.Value].AddChildItem(item);
			}
			else
			{
				if (_allItems.Count > 0)
				{
					throw new InvalidOperationException("Root item can be added only as the first item. ");
				}

				RootItem = item;
			}

			_allItems.Add(item.Id, item);
		}

		public void Freeze()
		{
			if (_allItems.Count == 0)
			{
				return;
			}

			_leafItems.AddRange(_allItems.Values.Where(v => v.IsLeaf));
			var startNode = _leafItems[0];
			_leafItems.RemoveAt(0);

			ResolveExecutionOrder(startNode, null);
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

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerator<T> GetEnumerator()
		{
			return _allItems.Values.GetEnumerator();
		}
	}


	[DebuggerDisplay("ExecutionPlanItem (Id={Id}; Operation={Operation}; Depth={Depth}; IsLeaf={IsLeaf}; ExecutionOrder={ExecutionOrder})")]
	public class ExecutionPlanItem
	{
		private readonly List<ExecutionPlanItem> _childItems = new List<ExecutionPlanItem>();

		public int ExecutionOrder { get; set; }

		public int Id { get; set; }

		public int? ParentId { get; set; }

		public int Depth { get; set; }

		public bool IsLeaf { get { return _childItems.Count == 0; } }

		public string Operation { get; set; }

		public string Options { get; set; }

		public string Optimizer { get; set; }

		public string ObjectOwner { get; set; }

		public string ObjectName { get; set; }

		public string ObjectAlias { get; set; }

		public string ObjectType { get; set; }

		public long? Cost { get; set; }
		
		public long? Cardinality { get; set; }

		public long? Bytes { get; set; }

		public string PartitionStart { get; set; }

		public string PartitionStop { get; set; }

		public string Distribution { get; set; }

		public long? CpuCost { get; set; }

		public long? IoCost { get; set; }

		public long? TempSpace { get; set; }

		public string AccessPredicates { get; set; }

		public string FilterPredicates { get; set; }

		public TimeSpan? Time { get; set; }

		public string QueryBlockName { get; set; }

		public XElement Other { get; set; }

		public ExecutionPlanItem Parent { get; private set; }

		public IReadOnlyList<ExecutionPlanItem> ChildItems
		{
			get { return _childItems.AsReadOnly(); }
		}

		public IEnumerable<ExecutionPlanItem> AllChildItems
		{
			get { return _childItems.Concat(_childItems.SelectMany(i => i.AllChildItems)); }
		}

		public void AddChildItem(ExecutionPlanItem childItem)
		{
			_childItems.Add(childItem);
			childItem.Parent = this;
		}

		public bool IsChildFrom(ExecutionPlanItem parent)
		{
			var item = this;
			do
			{
				item = item.Parent;
			} while (item != null && item != parent);

			return item != null;
		}
	}
}
