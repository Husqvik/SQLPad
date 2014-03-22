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
				if (selectList != null)
				{
					var prefixedColumnReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
					if (prefixedColumnReference != null)
					{
						var objectIdentifier = prefixedColumnReference.GetSingleDescendant(Terminals.ObjectIdentifier);
						if (objectIdentifier != null)
						{
							var queryBlock = semanticModel.GetQueryBlock(selectList);
							var columnReferences = queryBlock.Columns.SelectMany(c => c.ColumnReferences).Where(c => c.TableNode == objectIdentifier).ToArray();
							if (columnReferences.Length == 1 && columnReferences[0].TableNode != null)
							{
								if (columnReferences[0].TableNodeReferences.Count == 1)
								{
									return columnReferences[0].TableNodeReferences.Single().Columns.Select(c => new OracleCodeCompletionItem { Name = c.Name.ToSimpleIdentifier() }).ToArray();
								}
							}
						}
					}
				}
			}

			return EmptyCollection;
		}
	}

	public class OracleCodeCompletionItem : ICodeCompletionItem
	{
		public string Name { get; set; }
	}
}
