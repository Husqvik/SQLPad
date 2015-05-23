using System.Windows;
using System.Windows.Controls;
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
	}
}
