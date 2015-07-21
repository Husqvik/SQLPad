using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SqlPad
{
	public static class WpfExtensions
	{
		public static T FindParent<T>(this Visual child) where T : Visual
        {
			var parent = (Visual)VisualTreeHelper.GetParent(child);
			if (parent == null)
			{
				return null;
			}

			var typedParent = parent as T;
			return typedParent ?? FindParent<T>(parent);
		}

		public static T FindVisualChild<T>(this Visual parent) where T : Visual
		{
			var child = default(T);
			var numVisuals = VisualTreeHelper.GetChildrenCount(parent);
			for (var i = 0; i < numVisuals; i++)
			{
				var v = (Visual)VisualTreeHelper.GetChild(parent, i);
				child = v as T ?? FindVisualChild<T>(v);
				
				if (child != null)
				{
					break;
				}
			}

			return child;
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
				var text = textBlock.Text;
				if (regexPattern.Length == 0)
				{
					textBlock.Inlines.Clear();
					textBlock.Inlines.Add(text);
					return;
				}

				var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
				var substrings = regex.Split(text);
				textBlock.Inlines.Clear();

				foreach (var item in substrings)
				{
					if (regex.Match(item).Success)
					{
						var run = new Run(item) { Background = Brushes.Yellow };
						textBlock.Inlines.Add(run);
					}
					else
					{
						textBlock.Inlines.Add(item);
					}
				}
			}
		}
	}
}
