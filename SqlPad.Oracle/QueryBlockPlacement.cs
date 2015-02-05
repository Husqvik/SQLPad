namespace SqlPad.Oracle
{
	public enum QueryBlockPlacement
	{
		None,
		SelectList,
		TableReference,
		Where,
		GroupBy,
		Having,
		Join,
		OrderBy,
		Model,
		ConnectBy
	}
}
