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
