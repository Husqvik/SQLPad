using System.Windows;
using System.Windows.Media;

namespace SqlPad
{
	public static class WpfExtensions
	{
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
	}
}
