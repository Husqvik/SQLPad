using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for ComplexTypeViewer.xaml
	/// </summary>
	public partial class ComplexTypeViewer
	{
		public ComplexTypeViewer()
		{
			InitializeComponent();
		}

		public IComplexType ComplexType
		{
			get { return (IComplexType)RootPanel.DataContext; }
			set { RootPanel.DataContext = value; }
		}
	}

	internal class CustomTypeAttributeCellValueConverter : IMultiValueConverter
	{
		private static readonly CellValueConverter CellValueConverter = new CellValueConverter();

		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			return CellValueConverter.Convert(values[0], targetType, values[1], culture);
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
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
