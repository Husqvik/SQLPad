using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle.ExecutionPlan
{
	public class ExecutionPlanItemCollection : ExecutionPlanItemCollectionBase<ExecutionPlanItem> { }

	public abstract class ExecutionPlanItemCollectionBase<T> : ModelBase, IEnumerable<T> where T : ExecutionPlanItem
	{
		private readonly Dictionary<int, T> _allItems = new Dictionary<int, T>();
		private readonly List<ExecutionPlanItem> _leafItems = new List<ExecutionPlanItem>();

		private int _currentExecutionStep;

		public T RootItem { get; private set; }

		public IReadOnlyDictionary<int, T> AllItems => _allItems;

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

			foreach (var item in _allItems.Values)
			{
				if (item.IsLeaf)
				{
					_leafItems.Add(item);
				}

				var costRatio = item.Cost.HasValue && RootItem.Cost > 0
					? Math.Round(item.Cost.Value / (decimal)RootItem.Cost, 2)
					: (decimal?)null;

				item.CostRatio = costRatio;
			}

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
				var allChildrenExecuted = nextItem.ChildItems.All(i => i.ExecutionOrder != 0);
				if (allChildrenExecuted)
				{
					continue;
				}

				var otherBranchItemIndex = _leafItems.FindIndex(i => i.IsChildFrom(nextItem));
				if (otherBranchItemIndex == -1)
				{
					return;
				}

				var otherBranchLeafItem = _leafItems[otherBranchItemIndex];
				_leafItems.RemoveAt(otherBranchItemIndex);

				ResolveExecutionOrder(otherBranchLeafItem, nextItem);
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
}
