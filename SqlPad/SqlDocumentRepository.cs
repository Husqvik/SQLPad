using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SqlPad
{
	public class SqlDocumentRepository
	{
		private readonly object _lockObject = new object();
	    //private IDictionary<StatementBase, IValidationModel> _precedingValidationModels = new Dictionary<StatementBase, IValidationModel>();
		private readonly ISqlParser _parser;
		private readonly IStatementValidator _validator;
		private readonly IDatabaseModel _databaseModel;

	    public StatementCollection Statements { get; private set; } = new StatementCollection(new StatementBase[0], new IToken[0], new StatementCommentNode[0]);

	    public string StatementText { get; private set; }

		public IDictionary<StatementBase, IValidationModel> ValidationModels { get; private set; } = new Dictionary<StatementBase, IValidationModel>();

	    public SqlDocumentRepository(ISqlParser parser, IStatementValidator validator, IDatabaseModel databaseModel, string statementText = null)
		{
			if (parser == null)
				throw new ArgumentNullException(nameof(parser));

			if (validator == null)
				throw new ArgumentNullException(nameof(validator));

			_parser = parser;
			_validator = validator;
			_databaseModel = databaseModel;

			if (!String.IsNullOrEmpty(statementText))
			{
				UpdateStatements(statementText);
			}
		}

		public void UpdateStatements(string statementText)
		{
			var statements = _parser.Parse(statementText);
			var validationModels = statements.ToDictionary(s => s, s => _validator.BuildValidationModel(_validator.BuildSemanticModel(statementText, s, _databaseModel)));
			UpdateStatementsInternal(statementText, statements, validationModels);
		}

		public async Task UpdateStatementsAsync(string statementText, CancellationToken cancellationToken)
		{
			try
			{
				var statements = await _parser.ParseAsync(statementText, cancellationToken);
				var validationModels =  await BuildValidationModelsAsync(statements, statementText, cancellationToken);
				UpdateStatementsInternal(statementText, statements, validationModels);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception exception)
			{
				App.CreateErrorLog(exception);
				throw;
			}
		}

		private async Task<IDictionary<StatementBase, IValidationModel>> BuildValidationModelsAsync(StatementCollection statements, string statementText, CancellationToken cancellationToken)
		{
			var dictionary = new Dictionary<StatementBase, IValidationModel>();
			foreach (var statement in statements)
			{
				var semanticModel = await _validator.BuildSemanticModelAsync(statementText, statement, _databaseModel, cancellationToken);
				var validationModel = _validator.BuildValidationModel(semanticModel);
				dictionary.Add(statement, validationModel);
			}

			return new ReadOnlyDictionary<StatementBase, IValidationModel>(dictionary);
		}

		private void UpdateStatementsInternal(string statementText, StatementCollection statements, IDictionary<StatementBase, IValidationModel> validationModels)
		{
			lock (_lockObject)
			{
				Statements = statements;
				//_precedingValidationModels = _validationModels;
				ValidationModels = validationModels;
				StatementText = statementText;
			}
		}

		/*public IValidationModel GetPrecedingValidationModel(int cursorPosition)
		{
			if (_precedingValidationModels.Count != _validationModels.Count)
			{
				return null;
			}

			var currentModel = _validationModels.Values.SingleOrDefault(m => m.Statement.SourcePosition.ContainsIndex(cursorPosition));
			if (currentModel == null)
			{
				return null;
			}

			var precedingModel = _precedingValidationModels.Values.SingleOrDefault(
				m => m.Statement.SourcePosition.IndexStart == currentModel.Statement.SourcePosition.IndexStart &&
				     m.Statement.SourcePosition.Length < currentModel.Statement.SourcePosition.Length &&
				     m.Statement.RootNode.TerminalCount == currentModel.Statement.RootNode.TerminalCount);

			return precedingModel;
		}*/

		public void ExecuteStatementAction(Action<StatementCollection> action)
		{
			lock (_lockObject)
			{
				action(Statements);
			}
		}

		public T ExecuteStatementAction<T>(Func<StatementCollection, T> function)
		{
			lock (_lockObject)
			{
				return function(Statements);
			}
		}

		public bool CanAddPairCharacter(int caretOffset, char character)
		{
			var token = Statements.Tokens.FirstOrDefault(t => caretOffset >= t.Index && caretOffset <= t.Index + t.Value.Length);
			return token == null || _parser.CanAddPairCharacter(token.Value, character);
		}
	}
}
