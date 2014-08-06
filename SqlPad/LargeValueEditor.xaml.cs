using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;

namespace SqlPad
{
	public partial class LargeValueEditor
	{
		public LargeValueEditor(ILargeValue largeValue)
		{
			InitializeComponent();

			var largeTextValue = largeValue as ILargeTextValue;
			var largeBinaryValue = largeValue as ILargeBinaryValue;
			if (largeTextValue != null)
			{
				var isXml = true;
				using (var reader = new XmlTextReader(new StringReader(largeTextValue.Value)))
				{
					try
					{
						while (reader.Read())
						{
						}
					}
					catch
					{
						isXml = false;
					}
				}

				if (isXml)
				{
					TextEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("XML");
				}

				TextEditor.Text = largeTextValue.Value;
			}
			else if (largeBinaryValue != null)
			{
				TabControl.SelectedItem = TabRaw;
			}
		}
	}
}
