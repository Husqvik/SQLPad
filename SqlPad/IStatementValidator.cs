namespace SqlPad
{
	public interface IStatementValidator
	{
		IValidationModel ResolveReferences(string sqlText, IStatement statement, IDatabaseModel databaseModel);
	}
}