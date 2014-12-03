using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections;
using System.Windows.Documents;

namespace SqlPad.DragDrop
{
	public class DragDropHelper
	{
		private readonly DataFormat _format = DataFormats.GetDataFormat("DragDropItemsControl");
		private Point _initialMousePosition;
		private Vector _initialMouseOffset;
		private object _draggedData;
		private DraggedAdorner _draggedAdorner;
		private InsertionAdorner _insertionAdorner;
		private Window _topWindow;
		private ItemsControl _sourceItemsControl;
		private FrameworkElement _sourceItemContainer;
		private ItemsControl _targetItemsControl;
		private FrameworkElement _targetItemContainer;
		private int _insertionIndex;
		private bool _isInFirstHalf;

		private static readonly DragDropHelper Instance = new DragDropHelper();

		public static bool GetIsDragSource(DependencyObject obj)
		{
			return (bool) obj.GetValue(IsDragSourceProperty);
		}

		public static void SetIsDragSource(DependencyObject obj, bool value)
		{
			obj.SetValue(IsDragSourceProperty, value);
		}

		public static readonly DependencyProperty IsDragSourceProperty = DependencyProperty.RegisterAttached("IsDragSource", typeof (bool), typeof (DragDropHelper), new UIPropertyMetadata(false, IsDragSourceChanged));

		public static bool GetIsDropTarget(DependencyObject obj)
		{
			return (bool) obj.GetValue(IsDropTargetProperty);
		}

		public static void SetIsDropTarget(DependencyObject obj, bool value)
		{
			obj.SetValue(IsDropTargetProperty, value);
		}

		public static readonly DependencyProperty IsDropTargetProperty = DependencyProperty.RegisterAttached("IsDropTarget", typeof (bool), typeof (DragDropHelper), new UIPropertyMetadata(false, IsDropTargetChanged));

		public static DataTemplate GetDragDropTemplate(DependencyObject obj)
		{
			return (DataTemplate) obj.GetValue(DragDropTemplateProperty);
		}

		public static void SetDragDropTemplate(DependencyObject obj, DataTemplate value)
		{
			obj.SetValue(DragDropTemplateProperty, value);
		}

		public static readonly DependencyProperty DragDropTemplateProperty = DependencyProperty.RegisterAttached("DragDropTemplate", typeof (DataTemplate), typeof (DragDropHelper), new UIPropertyMetadata(null));

		private static void IsDragSourceChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
		{
			var dragSource = obj as ItemsControl;
			if (dragSource == null)
			{
				return;
			}
			
			if (Equals(e.NewValue, true))
			{
				dragSource.PreviewMouseLeftButtonDown += Instance.DragSource_PreviewMouseLeftButtonDown;
				dragSource.PreviewMouseLeftButtonUp += Instance.DragSource_PreviewMouseLeftButtonUp;
				dragSource.PreviewMouseMove += Instance.DragSource_PreviewMouseMove;
			}
			else
			{
				dragSource.PreviewMouseLeftButtonDown -= Instance.DragSource_PreviewMouseLeftButtonDown;
				dragSource.PreviewMouseLeftButtonUp -= Instance.DragSource_PreviewMouseLeftButtonUp;
				dragSource.PreviewMouseMove -= Instance.DragSource_PreviewMouseMove;
			}
		}

		private static void IsDropTargetChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
		{
			var dropTarget = obj as ItemsControl;
			if (dropTarget == null)
			{
				return;
			}
			
			if (Equals(e.NewValue, true))
			{
				dropTarget.AllowDrop = true;
				dropTarget.PreviewDrop += Instance.DropTarget_PreviewDrop;
				dropTarget.PreviewDragEnter += Instance.DropTarget_PreviewDragEnter;
				dropTarget.PreviewDragOver += Instance.DropTarget_PreviewDragOver;
				dropTarget.PreviewDragLeave += Instance.DropTarget_PreviewDragLeave;
			}
			else
			{
				dropTarget.AllowDrop = false;
				dropTarget.PreviewDrop -= Instance.DropTarget_PreviewDrop;
				dropTarget.PreviewDragEnter -= Instance.DropTarget_PreviewDragEnter;
				dropTarget.PreviewDragOver -= Instance.DropTarget_PreviewDragOver;
				dropTarget.PreviewDragLeave -= Instance.DropTarget_PreviewDragLeave;
			}
		}

		private void DragSource_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var visual = e.OriginalSource as Visual;
			if (visual == null)
			{
				return;
			}

			_sourceItemsControl = (ItemsControl)sender;
			_topWindow = Window.GetWindow(_sourceItemsControl);
			_initialMousePosition = e.GetPosition(_topWindow);

			_sourceItemContainer = _sourceItemsControl.ContainerFromElement(visual) as FrameworkElement;
			if (_sourceItemContainer != null)
			{
				_draggedData = _sourceItemContainer.DataContext;
			}
		}

		private static bool IsMovementBigEnough(Point initialMousePosition, Point currentPosition)
		{
			return (Math.Abs(currentPosition.X - initialMousePosition.X) >= SystemParameters.MinimumHorizontalDragDistance ||
					Math.Abs(currentPosition.Y - initialMousePosition.Y) >= SystemParameters.MinimumVerticalDragDistance);
		}

		private void DragSource_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (_draggedData == null)
			{
				return;
			}
			
			if (!IsMovementBigEnough(_initialMousePosition, e.GetPosition(_topWindow)))
			{
				return;
			}
			
			_initialMouseOffset = _initialMousePosition - _sourceItemContainer.TranslatePoint(new Point(0, 0), _topWindow);

			var data = new DataObject(_format.Name, _draggedData);

			var previousAllowDrop = _topWindow.AllowDrop;
			_topWindow.AllowDrop = true;
			_topWindow.DragEnter += TopWindow_DragEnter;
			_topWindow.DragOver += TopWindow_DragOver;
			_topWindow.DragLeave += TopWindow_DragLeave;

			System.Windows.DragDrop.DoDragDrop((DependencyObject) sender, data, DragDropEffects.Move);

			// Without this call, there would be a bug in the following scenario: Click on a data item, and drag
			// the mouse very fast outside of the window. When doing this really fast, for some reason I don't get 
			// the Window leave event, and the dragged adorner is left behind.
			// With this call, the dragged adorner will disappear when we release the mouse outside of the window,
			// which is when the DoDragDrop synchronous method returns.
			RemoveDraggedAdorner();

			_topWindow.AllowDrop = previousAllowDrop;
			_topWindow.DragEnter -= TopWindow_DragEnter;
			_topWindow.DragOver -= TopWindow_DragOver;
			_topWindow.DragLeave -= TopWindow_DragLeave;

			_draggedData = null;
		}

		private void DragSource_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			_draggedData = null;
		}

		private void DropTarget_PreviewDragEnter(object sender, DragEventArgs e)
		{
			var draggedItem = e.Data.GetData(_format.Name);
			if (draggedItem == null)
			{
				return;
			}

			_targetItemsControl = (ItemsControl) sender;

			DecideDropTarget(e);

			ShowDraggedAdorner(e.GetPosition(_topWindow));
			CreateInsertionAdorner();

			e.Handled = true;
		}

		private void DropTarget_PreviewDragOver(object sender, DragEventArgs e)
		{
			var draggedItem = e.Data.GetData(_format.Name);
			if (draggedItem == null)
			{
				return;
			}

			DecideDropTarget(e);

			ShowDraggedAdorner(e.GetPosition(_topWindow));
			UpdateInsertionAdornerPosition();
			
			e.Handled = true;
		}

		private void DropTarget_PreviewDrop(object sender, DragEventArgs e)
		{
			var draggedItem = e.Data.GetData(_format.Name);
			if (draggedItem == null)
			{
				return;
			}

			var indexRemoved = -1;

			if ((e.Effects & DragDropEffects.Move) != 0)
			{
				indexRemoved = RemoveItemFromItemsControl(_sourceItemsControl, draggedItem);
			}

			var isItemDropAfterItsOriginalPosition = indexRemoved != -1 && Equals(_sourceItemsControl, _targetItemsControl) && indexRemoved < _insertionIndex;
			if (isItemDropAfterItsOriginalPosition)
			{
				_insertionIndex--;
			}

			InsertItemInItemsControl(_targetItemsControl, draggedItem, _insertionIndex);

			RemoveDraggedAdorner();
			RemoveInsertionAdorner();

			e.Handled = true;
		}

		private void DropTarget_PreviewDragLeave(object sender, DragEventArgs e)
		{
			var draggedItem = e.Data.GetData(_format.Name);
			if (draggedItem == null)
			{
				return;
			}

			RemoveInsertionAdorner();			
			e.Handled = true;
		}

		private void DecideDropTarget(DragEventArgs e)
		{
			var draggedItem = e.Data.GetData(_format.Name);

			if (IsDropDataTypeAllowed(draggedItem))
			{
				if (_targetItemsControl.Items.Count > 0)
				{
					_targetItemContainer = _targetItemsControl.ContainerFromElement((DependencyObject) e.OriginalSource) as FrameworkElement;

					if (_targetItemContainer != null)
					{
						var positionRelativeToItemContainer = e.GetPosition(_targetItemContainer);
						_isInFirstHalf = positionRelativeToItemContainer.X < _targetItemContainer.ActualWidth / 2;
						_insertionIndex = _targetItemsControl.ItemContainerGenerator.IndexFromContainer(_targetItemContainer);

						var canBeDroppedIntoContainer = _insertionIndex < _targetItemsControl.Items.Count - 1;
						if (!canBeDroppedIntoContainer)
						{
							_isInFirstHalf = true;
							_targetItemContainer = null;
						}
						else if (!_isInFirstHalf)
						{
							_insertionIndex++;
						}
					}
					else
					{
						_targetItemContainer = _targetItemsControl.ItemContainerGenerator.ContainerFromIndex(_targetItemsControl.Items.Count - 2) as FrameworkElement;
						_isInFirstHalf = false;
						_insertionIndex = _targetItemsControl.Items.Count - 1;
					}
				}
				else
				{
					_targetItemContainer = null;
					_insertionIndex = 0;
				}
			}
			else
			{
				_targetItemContainer = null;
				_insertionIndex = -1;
				e.Effects = DragDropEffects.None;
			}
		}

		private bool IsDropDataTypeAllowed(object draggedItem)
		{
			bool isDropDataTypeAllowed;
			var collectionSource = _targetItemsControl.ItemsSource;
			if (draggedItem != null)
			{
				if (collectionSource != null)
				{
					var draggedType = draggedItem.GetType();
					var collectionType = collectionSource.GetType();

					var genericIListType = collectionType.GetInterface("IList`1");
					if (genericIListType != null)
					{
						var genericArguments = genericIListType.GetGenericArguments();
						isDropDataTypeAllowed = genericArguments[0].IsAssignableFrom(draggedType);
					}
					else if (typeof (IList).IsAssignableFrom(collectionType))
					{
						isDropDataTypeAllowed = true;
					}
					else
					{
						isDropDataTypeAllowed = false;
					}
				}
				else
				{
					isDropDataTypeAllowed = true;
				}
			}
			else
			{
				isDropDataTypeAllowed = false;
			}
			
			return isDropDataTypeAllowed;
		}

		private void TopWindow_DragEnter(object sender, DragEventArgs e)
		{
			ShowDraggedAdorner(e.GetPosition(_topWindow));
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private void TopWindow_DragOver(object sender, DragEventArgs e)
		{
			ShowDraggedAdorner(e.GetPosition(_topWindow));
			e.Effects = DragDropEffects.None;
			e.Handled = true;
		}

		private void TopWindow_DragLeave(object sender, DragEventArgs e)
		{
			RemoveDraggedAdorner();
			e.Handled = true;
		}

		private void ShowDraggedAdorner(Point currentPosition)
		{
			if (_draggedAdorner == null)
			{
				var adornerLayer = AdornerLayer.GetAdornerLayer(_sourceItemsControl);
				_draggedAdorner = new DraggedAdorner(_draggedData, GetDragDropTemplate(_sourceItemsControl), _sourceItemContainer, adornerLayer);
			}
			
			_draggedAdorner.SetPosition(currentPosition.X - _initialMousePosition.X + _initialMouseOffset.X, currentPosition.Y - _initialMousePosition.Y + _initialMouseOffset.Y);
		}

		private void RemoveDraggedAdorner()
		{
			if (_draggedAdorner == null)
			{
				return;
			}
			
			_draggedAdorner.Detach();
			_draggedAdorner = null;
		}

		private void CreateInsertionAdorner()
		{
			if (_targetItemContainer == null)
			{
				return;
			}
			
			var adornerLayer = AdornerLayer.GetAdornerLayer(_targetItemContainer);
			_insertionAdorner = new InsertionAdorner(false, _isInFirstHalf, _targetItemContainer, adornerLayer);
		}

		private void UpdateInsertionAdornerPosition()
		{
			if (_insertionAdorner == null)
			{
				return;
			}

			_insertionAdorner.IsInFirstHalf = _isInFirstHalf;
			_insertionAdorner.InvalidateVisual();
		}

		private void RemoveInsertionAdorner()
		{
			if (_insertionAdorner == null)
			{
				return;
			}
			
			_insertionAdorner.Detach();
			_insertionAdorner = null;
		}

		private static void InsertItemInItemsControl(ItemsControl itemsControl, object itemToInsert, int insertionIndex)
		{
			if (itemToInsert == null)
			{
				return;
			}

			itemsControl.Items.Insert(insertionIndex, itemToInsert);

			TrySetSelectedIndex(itemsControl, insertionIndex);
		}

		private static void TrySetSelectedIndex(ItemsControl itemsControl, int index)
		{
			var selector = itemsControl as Selector;
			if (selector != null)
			{
				selector.SelectedIndex = index;
			}
		}

		private static int RemoveItemFromItemsControl(ItemsControl itemsControl, object itemToRemove)
		{
			var indexToBeRemoved = -1;
			if (itemToRemove == null)
			{
				return indexToBeRemoved;
			}

			indexToBeRemoved = itemsControl.Items.IndexOf(itemToRemove);

			if (indexToBeRemoved == -1)
			{
				return indexToBeRemoved;
			}

			if (indexToBeRemoved == itemsControl.Items.Count - 2)
			{
				TrySetSelectedIndex(itemsControl, itemsControl.Items.Count - 3);
			}

			itemsControl.Items.RemoveAt(indexToBeRemoved);

			return indexToBeRemoved;
		}
	}
}
