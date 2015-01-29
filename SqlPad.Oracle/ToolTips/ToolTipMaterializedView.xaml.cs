using System;
using System.Globalization;
using System.Windows.Controls;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipMaterializedView : IToolTip
	{
		public ToolTipMaterializedView()
		{
			InitializeComponent();
		}

		public UserControl Control { get { return this; } }
	}

	public class MaterializedViewDetailsModel : TableDetailsModel
	{
		public OracleMaterializedView MaterializedView { get; set; }

		public string MaterializedViewTitle { get; set; }
	}

	internal class MaterializedViewPropertyConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (value is MaterializedViewRefreshMode)
			{
				return (MaterializedViewRefreshMode)value == MaterializedViewRefreshMode.OnDemand ? "On demand" : "On commit";
			}

			var stringValue = (string)value;
			return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(stringValue.ToLowerInvariant());
		}
	}
}
