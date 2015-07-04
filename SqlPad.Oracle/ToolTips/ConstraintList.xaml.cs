using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ConstraintList
	{
		public ConstraintList()
		{
			InitializeComponent();
		}

		public static readonly DependencyProperty ConstraintsProperty = DependencyProperty.Register("Constraints", typeof(IEnumerable<ConstraintDetailsModel>), typeof(ConstraintList), new FrameworkPropertyMetadata(PropertyChangedCallbackHandler));

		[Bindable(true)]
		public IEnumerable<ConstraintDetailsModel> Constraints
		{
			get { return (IEnumerable<ConstraintDetailsModel>)GetValue(ConstraintsProperty); }
			set { SetValue(ConstraintsProperty, value); }
		}

		private static void PropertyChangedCallbackHandler(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
		{
			var constraintList = (ConstraintList)dependencyObject;
			constraintList.DataGrid.ItemsSource = (IEnumerable<ConstraintDetailsModel>)args.NewValue;
		}
	}
}
