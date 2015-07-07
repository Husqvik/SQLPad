using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SqlPad
{
	public static class WpfExtensions
	{
		private static readonly Brush PopupBackgroundBrush = new SolidColorBrush(Color.FromRgb(241, 242, 247));

		private static readonly Brush PopupBorderBrush = new SolidColorBrush(Color.FromRgb(118, 118, 118));

		public static T FindParent<T>(DependencyObject child) where T : DependencyObject
		{
			var parent = VisualTreeHelper.GetParent(child);
			if (parent == null)
			{
				return null;
			}

			var typedParent = parent as T;
			return typedParent ?? FindParent<T>(parent);
		}

		public static T AsPopupChild<T>(this T control) where T : Control
		{
			control.Background = PopupBackgroundBrush;
			control.BorderThickness = new Thickness(1);
			control.BorderBrush = PopupBorderBrush;
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
						var run = new Run(item) { Background = Brushes.DarkGray };
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
