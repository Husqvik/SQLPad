using System.Threading;
using System.Threading.Tasks;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipDatabaseLink
	{
		public ToolTipDatabaseLink(OracleDatabaseLink databaseLink)
		{
			InitializeComponent();

			DataContext = databaseLink;
		}

		protected override Task<string> ExtractDdlAsync(CancellationToken cancellationToken)
		{
			return ScriptExtractor.ExtractSchemaObjectScriptAsync((OracleDatabaseLink)DataContext, cancellationToken);
		}
	}
}
