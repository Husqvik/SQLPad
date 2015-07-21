namespace SqlPad
{
	public class TsvDataExporter : CsvDataExporter
	{
		public override string FileNameFilter => "TSV files (*.tsv)|*.tsv|All files (*.*)|*";

	    protected override string Separator => "\t";
	}
}
