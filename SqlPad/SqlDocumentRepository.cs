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
		private IDictionary<StatementBase, IValidationModel> _validationModels = new Dictionary<StatementBase, IValidationModel>();
		//private IDictionary<StatementBase, IValidationModel> _precedingValidationModels = new Dictionary<StatementBase, IValidationModel>();
		private readonly ISqlParser _parser;
		private readonly IStatementValidator _validator;
		private readonly IDatabaseModel _databaseModel;
		private StatementCollection _statements = new StatementCollection(new StatementBase[0], new IToken[0], new StatementCommentNode[0]);

		public StatementCollection Statements
		{
			get { return _statements; }
		}

		public string StatementText { get; private set; }

		public IDictionary<StatementBase, IValidationModel> ValidationModels { get { return _validationModels; } }

		public SqlDocumentRepository(ISqlParser parser, IStatementValidator validator, IDatabaseModel databaseModel, string statementText = null)
		{
			if (parser == null)
				throw new ArgumentNullException("parser");

			if (validator == null)
				throw new ArgumentNullException("validator");

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
			UpdateStatementsInternal(statementText, () => _parser.Parse(statementText), BuildValidationModels);
		}

		public async Task UpdateStatementsAsync(string statementText, CancellationToken cancellationToken)
		{
			try
			{
				await _parser.ParseAsync(statementText, cancellationToken)
					.ContinueWith(t => new { Statements = t.Result, ValidationModels = BuildValidationModels(t.Result, statementText) }, cancellationToken)
					.ContinueWith(t => UpdateStatementsInternal(statementText, () => t.Result.Statements, (c, text) => t.Result.ValidationModels), cancellationToken);
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

		private IDictionary<StatementBase, IValidationModel> BuildValidationModels(StatementCollection statements, string statementText)
		{
			return new ReadOnlyDictionary<StatementBase, IValidationModel>(statements.ToDictionary(s => s, s => _validator.BuildValidationModel(_validator.BuildSemanticModel(statementText, s, _databaseModel))));
		}

		private void UpdateStatementsInternal(string statementText, Func<StatementCollection> parseFunction, Func<StatementCollection, string, IDictionary<StatementBase, IValidationModel>> buildValidationModelFunction)
		{
			var statements = parseFunction();
			var validationModels = buildValidationModelFunction(statements, statementText);

			lock (_lockObject)
			{
				_statements = statements;
				//_precedingValidationModels = _validationModels;
				_validationModels = validationModels;
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
	}
}
