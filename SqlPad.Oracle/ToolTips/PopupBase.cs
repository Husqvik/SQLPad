using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlPad.Oracle.ToolTips
{
	public class PopupBase : UserControl, IToolTip
	{
		public static readonly DependencyProperty IsPinnableProperty = DependencyProperty.Register("IsPinnable", typeof(bool), typeof(PopupBase), new FrameworkPropertyMetadata(true));

		public static readonly RoutedCommand PinPopupCommand = new RoutedCommand();

		static PopupBase()
        {
			DefaultStyleKeyProperty.OverrideMetadata(typeof(PopupBase), new FrameworkPropertyMetadata(typeof(PopupBase)));
        }

		public PopupBase()
		{
			CommandBindings.Add(new CommandBinding(PinPopupCommand, PinHandler));
		}

		[Bindable(true)]
		public bool IsPinnable
		{
			get { return (bool)GetValue(IsPinnableProperty); }
			set { SetValue(IsPinnableProperty, value); }
		}

		public event EventHandler Pin;

		public Control Control { get { return this; } }

		public FrameworkElement InnerContent { get { return (FrameworkElement)Content; } }

		private void PinHandler(object sender, RoutedEventArgs e)
		{
			if (Pin != null)
			{
				Pin(this, EventArgs.Empty);
			}
		}
	}
}
