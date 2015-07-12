using System;
using System.Windows;
using System.Windows.Controls;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipSchema : IToolTip
	{
		public event EventHandler Pin;

		public ToolTipSchema(OracleSchema schema)
		{
			InitializeComponent();

			DataContext = schema;
			LabelTitle.Text = String.Format("{0} (User/schema)", schema.Name.ToSimpleIdentifier());
		}

		public Control Control { get { return this; } }

		public FrameworkElement InnerContent { get { return this; } }
	}
}
