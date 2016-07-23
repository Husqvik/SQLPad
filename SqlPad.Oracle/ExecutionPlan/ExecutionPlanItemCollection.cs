using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle.ExecutionPlan
{
	[DebuggerDisplay("ExecutionPlanItemCollection (Items={AllItems.Count}; HasInactiveItems={HasInactiveItems})")]
	public class ExecutionPlanItemCollection : ExecutionPlanItemCollectionBase<ExecutionPlanItem> { }

	public interface IExecutionPlanItemCollection
	{
		ExecutionPlanItem RootItem { get; }

		bool HasInactiveItems { get; }

		void SetAllItems();

		void SetActiveItems();
	}

	public abstract class ExecutionPlanItemCollectionBase<T> : ModelBase, IExecutionPlanItemCollection where T : ExecutionPlanItem
	{
		private readonly Dictionary<int, T> _allItems = new Dictionary<int, T>();

		private int _currentExecutionStep;
		private DisplayFilter _filter = DisplayFilter.NotSet;

		public T RootItem { get; private set; }

		public bool HasInactiveItems { get; private set; }

		public IReadOnlyDictionary<int, T> AllItems => _allItems;

		ExecutionPlanItem IExecutionPlanItemCollection.RootItem => RootItem;

		public void Add(T item)
		{
			if (_currentExecutionStep > 0)
			{
				throw new InvalidOperationException("Item cannot be added because the collection is frozen. ");
			}

			if (item.ParentId == null)
			{
				if (_allItems.Count > 0)
				{
					throw new InvalidOperationException("Root item can be added only as the first item. ");
				}

				RootItem = item;
			}

			HasInactiveItems |= item.IsInactive;

			var costRatio = item.Cost.HasValue && RootItem.Cost > 0
				? Math.Round(item.Cost.Value / (decimal)RootItem.Cost, 2)
				: (decimal?)null;

			item.CostRatio = costRatio;

			_allItems.Add(item.Id, item);
		}

		public void SetAllItems()
		{
			SetItems(DisplayFilter.None);
		}

		public void SetActiveItems()
		{
			SetItems(DisplayFilter.ActiveNodesOnly);
		}

		private ExecutionPlanItem FindParentItem(int parentId, DisplayFilter filter)
		{
			do
			{
				var item = _allItems[parentId];
				if (filter == DisplayFilter.None || !item.IsInactive)
				{
					return item;
				}

				parentId = item.ParentId.Value;
			}
			while (true);
		}

		private void SetItems(DisplayFilter filter)
		{
			if (filter == _filter || _allItems.Count == 0)
			{
				return;
			}

			RootItem.ClearAllChildItems();

			foreach (var item in _allItems.Values)
			{
				item.ExecutionOrder = 0;

				if (item.ParentId.HasValue)
				{
					var parentItem = FindParentItem(item.ParentId.Value, filter);
					if (filter == DisplayFilter.None || !item.IsInactive)
					{
						parentItem.AddChildItem(item);
					}
				}
			}

			var leafItemSource = _allItems.Values.Where(i => i.IsLeaf);
			if (filter == DisplayFilter.ActiveNodesOnly)
			{
				leafItemSource = leafItemSource.Where(i => !i.IsInactive);
			}

			ResolveExecutionOrder(leafItemSource.ToList());

			_filter = filter;
		}

		private void ResolveExecutionOrder(List<T> leafItems)
		{
			_currentExecutionStep = 0;

			var startNode = leafItems[0];
			leafItems.RemoveAt(0);

			ResolveExecutionOrder(startNode, null, leafItems);
		}

		private void ResolveExecutionOrder(ExecutionPlanItem nextItem, ExecutionPlanItem breakAtItem, List<T> remainingLeafItems)
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

				var otherBranchItemIndex = remainingLeafItems.FindIndex(i => i.IsChildFrom(nextItem));
				if (otherBranchItemIndex == -1)
				{
					return;
				}

				var otherBranchLeafItem = remainingLeafItems[otherBranchItemIndex];
				remainingLeafItems.RemoveAt(otherBranchItemIndex);

				ResolveExecutionOrder(otherBranchLeafItem, nextItem, remainingLeafItems);
			} while (true);
		}

		private enum DisplayFilter
		{
			NotSet,
			None,
			ActiveNodesOnly
		}
	}
}
