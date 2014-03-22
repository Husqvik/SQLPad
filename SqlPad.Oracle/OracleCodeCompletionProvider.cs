using System.Collections.Generic;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleCodeCompletionProvider : ICodeCompletionProvider
	{
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();
		private static readonly ICodeCompletionItem[] EmptyCollection = new ICodeCompletionItem[0];

		public ICollection<ICodeCompletionItem> ResolveItems(string statementText, int cursorPosition)
		{
			var statements = _oracleParser.Parse(statementText);
			var statement = (OracleStatement)statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			if (statement == null)
				return EmptyCollection;

			var currentNode = statement.GetNodeAtPosition(cursorPosition);
			var semanticModel = new OracleStatementSemanticModel(statementText, statement, DatabaseModelFake.Instance);

			if (currentNode.Id == Terminals.Identifier)
			{
				var selectList = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList);
				var condition = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.Condition);
				var rootNode = selectList ?? condition;
				if (selectList != null || condition != null)
				{
					var prefixedColumnReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
					if (prefixedColumnReference != null)
					{
						var objectIdentifier = prefixedColumnReference.GetSingleDescendant(Terminals.ObjectIdentifier);
						if (objectIdentifier != null)
						{
							var queryBlock = semanticModel.GetQueryBlock(rootNode);
							var columnReferences = queryBlock.Columns.SelectMany(c => c.ColumnReferences).Where(c => c.TableNode == objectIdentifier).ToArray();
							if (columnReferences.Length == 1 && columnReferences[0].TableNode != null)
							{
								if (columnReferences[0].TableNodeReferences.Count == 1)
								{
									return columnReferences[0].TableNodeReferences.Single().Columns
										.Select(c => new OracleCodeCompletionItem
										             {
											             Name = c.Name.ToSimpleIdentifier(),
														 StatementNode = currentNode
										             }).ToArray();
								}
							}
						}
					}
				}
			}

			if (currentNode.Id == Terminals.ObjectIdentifier)
			{
				var selectList = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SelectList);
				var condition = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.Condition);
				if (selectList == null && condition == null)
				{
					// TODO: Add option to search all/current/public schemas
					var schemaIdentifier = currentNode.ParentNode.GetSingleDescendant(Terminals.SchemaIdentifier);

					var schemaName = schemaIdentifier != null
						? schemaIdentifier.Token.Value.ToOracleIdentifier()
						: DatabaseModelFake.Instance.CurrentSchema;

					return DatabaseModelFake.Instance.AllObjects.Values
						.Where(o => o.Owner == schemaName)
						.Select(o => new OracleCodeCompletionItem
						             {
							             Name = o.Name.ToSimpleIdentifier(),
							             StatementNode = currentNode
						             }).ToArray();
				}
			}

			return EmptyCollection;
		}
	}

	public class OracleCodeCompletionItem : ICodeCompletionItem
	{
		public string Name { get; set; }
		
		public StatementDescriptionNode StatementNode { get; set; }
	}
}
