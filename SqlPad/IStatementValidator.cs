namespace SqlPad
{
	public interface IStatementValidator
	{
		IValidationModel BuildValidationModel(string sqlText, StatementBase statement, IDatabaseModel databaseModel);
	}
}