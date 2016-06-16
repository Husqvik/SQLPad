namespace SqlPad.DataExport
{
	public class TsvDataExporter : CsvDataExporter
	{
		public override string Name { get; } = "Tab separated value";

		public override string FileNameFilter { get; } = "TSV files (*.tsv)|*.tsv|All files (*.*)|*";

		public override string FileExtension { get; } = "tsv";

		protected override string Separator { get; } = "\t";
	}
}
