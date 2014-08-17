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
		private readonly ISqlParser _parser;
		private readonly IStatementValidator _validator;
		private readonly IDatabaseModel _databaseModel;
		private StatementCollection _statements = new StatementCollection(new StatementBase[0], new StatementCommentNode[0]);

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

		public async Task UpdateStatementsAsync(string statementText)
		{
			var statements = await _parser.ParseAsync(statementText, CancellationToken.None);
			var validationModels = await Task.Factory.StartNew(() => BuildValidationModels(statements, statementText));
			
			UpdateStatementsInternal(statementText, () => statements, (c, t) => validationModels);
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
				_validationModels = validationModels;
				StatementText = statementText;
			}
		}

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
