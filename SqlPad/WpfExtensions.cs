using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace SqlPad
{
	public static class WpfExtensions
	{
		public static T FindParentVisual<T>(this Visual child) where T : Visual
		{
			var parent = (Visual)VisualTreeHelper.GetParent(child);
			if (parent == null)
			{
				return null;
			}

			var typedParent = parent as T;
			return typedParent ?? FindParentVisual<T>(parent);
		}

		public static T FindChildVisual<T>(this Visual parent) where T : Visual
		{
			var child = default(T);
			var numVisuals = VisualTreeHelper.GetChildrenCount(parent);
			for (var i = 0; i < numVisuals; i++)
			{
				var v = (Visual)VisualTreeHelper.GetChild(parent, i);
				child = v as T ?? FindChildVisual<T>(v);
				
				if (child != null)
				{
					break;
				}
			}

			return child;
		}

		public static void RemoveTabItemWithoutBindingError(this TabControl tabControl, TabItem item)
		{
			item.Template = null;
			tabControl.Items.Remove(item);
		}

		public static bool IsInViewport(this FrameworkElement container, FrameworkElement element)
		{
			if (!element.IsVisible)
			{
				return false;
			}

			var bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
			var rect = new Rect(0, 0, container.ActualWidth, container.ActualHeight);
			return rect.Contains(bounds.TopLeft) || rect.Contains(bounds.BottomRight);
		}

		public static void CancelOnEscape(this CancellationTokenSource cancellationTokenSource, Key key)
		{
			if (key != Key.Escape)
			{
				return;
			}

			Trace.WriteLine("Action is about to cancel. ");
			cancellationTokenSource.Cancel();
		}

		public static T AsPopupChild<T>(this T control) where T : Control
		{
			control.Background = (SolidColorBrush)Application.Current.Resources["PopupBackgroundBrush"];
			control.BorderThickness = new Thickness(1);
			control.BorderBrush = (SolidColorBrush)Application.Current.Resources["PopupBorderBrush"];
			return control;
		}

		public static void HighlightTextItems(this DependencyObject target, string regexPattern)
		{
			for (var i = 0; i < VisualTreeHelper.GetChildrenCount(target); i++)
			{
				if (target is ListViewItem || target is ListBoxItem || target is DataGridCell)
				{
					HighlightText(target, regexPattern);
				}

				HighlightTextItems(VisualTreeHelper.GetChild(target, i), regexPattern);
			}
		}

		private static void HighlightText(DependencyObject dependencyObject, string regexPattern)
		{
			if (dependencyObject == null)
			{
				return;
			}

			var textBlock = dependencyObject as TextBlock;
			if (textBlock == null)
			{
				for (var i = 0; i < VisualTreeHelper.GetChildrenCount(dependencyObject); i++)
				{
					HighlightText(VisualTreeHelper.GetChild(dependencyObject, i), regexPattern);
				}
			}
			else
			{
				var inlines = textBlock.Inlines;
				var inlineCount = inlines.Count;
				var firstRun = inlines.FirstInline as Run;
				if (firstRun == null)
				{
					return;
				}

				var text = textBlock.Text;

				if (regexPattern.Length == 0)
				{
					if (inlineCount == 1 && String.Equals(firstRun.Text, text))
					{
						return;
					}

					inlines.Clear();
					inlines.Add(text);
					return;
				}

				var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				var substrings = regex.Split(text);
				var index = 0;
				if (substrings.Length == inlineCount && inlines.All(i => (i as Run)?.Text.Length == substrings[index++].Length))
				{
					return;
				}

				inlines.Clear();
				var contentPresenter = (ContentPresenter)textBlock.TemplatedParent;

				object tag = null;
				foreach (var item in substrings)
				{
					if (regex.Match(item).Success)
					{
						var run = new Run(item) { Background = Brushes.Yellow };
						inlines.Add(run);
						tag = DataGridHelper.TagHighlight;
					}
					else
					{
						inlines.Add(item);
					}
				}

				if (contentPresenter != null)
				{
					contentPresenter.Tag = tag;
				}
			}
		}
	}
}
