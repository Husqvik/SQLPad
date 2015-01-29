using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipAsterisk : IToolTip
	{
		public ToolTipAsterisk()
		{
			InitializeComponent();
		}

		public IEnumerable<OracleColumn> Columns
		{
			get { return (IEnumerable<OracleColumn>)ItemsControl.ItemsSource; }
			set { ItemsControl.ItemsSource = value; }
		}

		public UserControl Control
		{
			get { return this; }
		}
	}

	internal class SimpleColumnNameConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return ((string)value).ToSimpleIdentifier();
		}
	}
}
