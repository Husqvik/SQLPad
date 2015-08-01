using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SqlPad
{
	public partial class ComplexTypeViewer
	{
		public static readonly DependencyProperty ComplexTypeProperty = DependencyProperty.Register(nameof(ComplexType), typeof(IComplexType), typeof(ComplexTypeViewer), new FrameworkPropertyMetadata());

		internal EventHandler<DataGridBeginningEditEventArgs> ResultGridBeginningEditCancelTextInputHandler => App.ResultGridBeginningEditCancelTextInputHandlerImplementation;

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

		private void ComplexTypeViewerGridSortingHandler(object sender, DataGridSortingEventArgs e)
		{
			if (!Equals(e.Column, AttributeNameColumn))
			{
				e.Handled = true;
			}
		}
	}

	/*internal class CustomTypeDataTemplateSelector : DataTemplateSelector
	{
		public bool IsEditing { get; set; }

		public override DataTemplate SelectTemplate(object item, DependencyObject container)
		{
			var collectionValue = item as ICollectionValue;
			var complexType = item as IComplexType;

			var complexTypeViewer = WpfExtensions.FindParentVisual<ComplexTypeViewer>(container);
			var templateName = String.Format("PrimitiveValueType{0}Template", IsEditing ? "Editing" : null);

			return (DataTemplate)complexTypeViewer.Resources[templateName];
		}
	}*/
}
