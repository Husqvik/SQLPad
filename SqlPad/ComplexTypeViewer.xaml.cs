using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SqlPad
{
	public partial class ComplexTypeViewer
	{
		public static readonly DependencyProperty ComplexTypeProperty = DependencyProperty.Register("ComplexType", typeof(IComplexType), typeof(ComplexTypeViewer), new FrameworkPropertyMetadata());
		public static readonly DependencyProperty GridTitleProperty = DependencyProperty.Register("GridTitle", typeof(string), typeof(ComplexTypeViewer), new FrameworkPropertyMetadata(String.Empty));

		public ComplexTypeViewer()
		{
			InitializeComponent();
		}

		[Bindable(true)]
		public IComplexType ComplexType
		{
			get { return (IComplexType)GetValue(ComplexTypeProperty); }
			set { SetValue(ComplexTypeProperty, value); }
		}

		[Bindable(true)]
		public string GridTitle
		{
			get { return (string)GetValue(GridTitleProperty); }
			set { SetValue(GridTitleProperty, value); }
		}

		public string RowTitle
		{
			get { return (string)AttributeNameColumn.Header; }
			set { AttributeNameColumn.Header = value; }
		}

		private void ComplexTypeViewerGridBeginningEditHandler(object sender, DataGridBeginningEditEventArgs e)
		{
			var textCompositionArgs = e.EditingEventArgs as TextCompositionEventArgs;
			if (textCompositionArgs != null)
			{
				e.Cancel = true;
			}
		}

		private void ComplexTypeViewerGridSortingHandler(object sender, DataGridSortingEventArgs e)
		{
			if (!Equals(e.Column, AttributeNameColumn))
			{
				e.Handled = true;
			}
		}

		private void ColumnHeaderClickHandler(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
		}
	}

	/*internal class CustomTypeDataTemplateSelector : DataTemplateSelector
	{
		public bool IsEditing { get; set; }

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var collectionValue = item as ICollectionValue;
			var complexType = item as IComplexType;

			var complexTypeViewer = WpfExtensions.FindParent<ComplexTypeViewer>(container);
			var templateName = String.Format("PrimitiveValueType{0}Template", IsEditing ? "Editing" : null);

			return (DataTemplate)complexTypeViewer.Resources[templateName];
		}
	}*/
}
