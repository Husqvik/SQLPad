namespace SqlPad
{
	public interface IStatementValidator
	{
		IStatementSemanticModel BuildSemanticModel(string statementText, StatementBase statementBase, IDatabaseModel databaseModel);

		IValidationModel BuildValidationModel(IStatementSemanticModel semanticModel);
	}

	public interface IStatementSemanticModel
	{
		IDatabaseModel DatabaseModel { get; }
		StatementBase Statement { get; }
		string StatementText { get; }
		bool IsSimpleModel { get; }
	}
}