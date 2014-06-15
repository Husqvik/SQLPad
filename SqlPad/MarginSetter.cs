using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad
{
	public class MarginSetter
	{
		public static Thickness GetMargin(DependencyObject obj)
		{
			return (Thickness)obj.GetValue(MarginProperty);
		}

		public static void SetMargin(DependencyObject obj, Thickness value)
		{
			obj.SetValue(MarginProperty, value);
		}

		// Using a DependencyProperty as the backing store for Margin.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty MarginProperty =
			DependencyProperty.RegisterAttached("Margin", typeof(Thickness), typeof(MarginSetter), new UIPropertyMetadata(new Thickness(), MarginChangedCallback));

		public static void MarginChangedCallback(object sender, DependencyPropertyChangedEventArgs e)
		{
			// Make sure this is put on a panel
			var panel = sender as Panel;

			if (panel == null)
				return;

			panel.Loaded += PanelLoadedHandler;
		}

		private static void PanelLoadedHandler(object sender, RoutedEventArgs e)
		{
			var panel = (Panel)sender;

			// Go over the children and set margin for them:
			foreach (var frameworkElement in panel.Children.OfType<FrameworkElement>())
			{
				frameworkElement.Margin = GetMargin(panel);
			}
		}
	}
}
