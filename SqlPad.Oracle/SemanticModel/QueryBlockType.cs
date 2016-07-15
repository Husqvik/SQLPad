namespace SqlPad.Oracle.SemanticModel
{
	public enum QueryBlockType
	{
		Normal,
		CursorParameter,
		CommonTableExpression,
		ScalarSubquery
	}
}