using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipMaterializedView
	{
		public ToolTipMaterializedView()
		{
			InitializeComponent();
		}

		protected override async Task<string> ExtractDdlAsync(CancellationToken cancellationToken)
		{
			return await ScriptExtractor.ExtractSchemaObjectScriptAsync(((MaterializedViewDetailsModel)DataContext).MaterializedView, cancellationToken);
		}
	}

	public class MaterializedViewDetailsModel : TableDetailsModel
	{
		public OracleMaterializedView MaterializedView
		{
			get { return (OracleMaterializedView)Table; }
			set { Table = value; }
		}

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
