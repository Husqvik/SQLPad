namespace SqlPad
{
	public class TsvDataExporter : CsvDataExporter
	{
		public override string FileNameFilter
		{
			get { return "TSV files (*.tsv)|*.tsv|All files (*.*)|*"; }
		}

		protected override string Separator
		{
			get { return "\t"; }
		}
	}
}
