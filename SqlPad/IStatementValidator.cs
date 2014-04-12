namespace SqlPad
{
	public interface IStatementValidator
	{
		IValidationModel ResolveReferences(string sqlText, StatementBase statement, IDatabaseModel databaseModel);
	}
}