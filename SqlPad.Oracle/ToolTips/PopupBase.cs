using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlPad.Oracle.ToolTips
{
	public class PopupBase : UserControl, IToolTip
	{
		public static readonly DependencyProperty IsPinnableProperty = DependencyProperty.Register(nameof(IsPinnable), typeof (bool), typeof (PopupBase), new FrameworkPropertyMetadata(true));
		public static readonly DependencyProperty IsExtractDdlVisibleProperty = DependencyProperty.Register(nameof(IsExtractDdlVisible), typeof (bool), typeof (PopupBase), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty IsExtractingProperty = DependencyProperty.Register(nameof(IsExtracting), typeof (bool), typeof (PopupBase), new FrameworkPropertyMetadata());

		[Bindable(true)]
		public bool IsPinnable
		{
			get { return (bool)GetValue(IsPinnableProperty); }
			set { SetValue(IsPinnableProperty, value); }
		}

		[Bindable(true)]
		public bool IsExtractDdlVisible
		{
			get { return (bool)GetValue(IsExtractDdlVisibleProperty); }
			set { SetValue(IsExtractDdlVisibleProperty, value); }
		}

		[Bindable(true)]
		public bool IsExtracting
		{
			get { return (bool)GetValue(IsExtractingProperty); }
			private set { SetValue(IsExtractingProperty, value); }
		}

		public static readonly RoutedCommand PinPopupCommand = new RoutedCommand();
		public static readonly RoutedCommand ExtractDdlCommand = new RoutedCommand();

		static PopupBase()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof (PopupBase), new FrameworkPropertyMetadata(typeof (PopupBase)));
		}

		public PopupBase()
		{
			CommandBindings.Add(new CommandBinding(PinPopupCommand, PinHandler));
			CommandBindings.Add(new CommandBinding(ExtractDdlCommand, ExtractDdlHandler));
			MaxHeight = SystemParameters.WorkArea.Height;
		}

		public event EventHandler Pin;

		public Control Control => this;

		public FrameworkElement InnerContent => (FrameworkElement)Content;

		protected virtual Task<string> ExtractDdlAsync(CancellationToken cancellationToken)
		{
			if (IsExtractDdlVisible)
			{
				throw new InvalidOperationException("ExtractDdlAsync must be overriden when IsExtractDdlVisible is enabled. ");
			}

			return Task.FromResult(String.Empty);
		}

		private void PinHandler(object sender, RoutedEventArgs args)
		{
			Pin?.Invoke(this, EventArgs.Empty);
		}

		private async void ExtractDdlHandler(object sender, RoutedEventArgs args)
		{
			try
			{
				IsExtracting = true;

				var ddl = await ExtractDdlAsync(CancellationToken.None);
				if (!String.IsNullOrEmpty(ddl))
				{
					Clipboard.SetText(ddl);
				}

				IsExtracting = false;
			}
			catch (Exception exception)
			{
				IsExtracting = false;
				App.LogErrorAndShowMessage(exception);
			}
		}
	}
}
