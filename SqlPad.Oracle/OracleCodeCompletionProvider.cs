using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleCodeCompletionProvider : ICodeCompletionProvider
	{
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();
		private static readonly ICodeCompletionItem[] EmptyCollection = new ICodeCompletionItem[0];

		private static readonly string[] JoinClauses = { "JOIN", "LEFT JOIN", "RIGHT JOIN", "FULL JOIN" };

		public ICollection<ICodeCompletionItem> ResolveItems(string statementText, int cursorPosition)
		{
			//Trace.WriteLine("OracleCodeCompletionProvider.ResolveItems called. Cursor position: "+ cursorPosition);

			var statements = _oracleParser.Parse(statementText);
			var statement = (OracleStatement)statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			if (statement == null)
			{
				if (statements.Count > 0)
				{
					var lastPreviousTerminal = ((OracleStatement)statements.Last()).GetNearestTerminalToPosition(cursorPosition);
					if (lastPreviousTerminal != null)
					{
						if (lastPreviousTerminal.Id == Terminals.From ||
						    lastPreviousTerminal.Id == Terminals.ObjectIdentifier)
						{
							var currentName = lastPreviousTerminal.Id == Terminals.From ? null : statementText.Substring(lastPreviousTerminal.SourcePosition.IndexStart, cursorPosition - lastPreviousTerminal.SourcePosition.IndexStart);
							return GenerateSchemaObjectItems(DatabaseModelFake.Instance.CurrentSchema, currentName, null);
						}

						var tableReference = lastPreviousTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.TableReference);
						if (tableReference != null)
						{
							var alias = tableReference.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();
							var aliasValue = alias.Token.Value.ToUpperInvariant();

							return JoinClauses.Where(j => j.Contains(aliasValue))
								.Select(j => new OracleCodeCompletionItem
								             {
									             Name = j,
												 StatementNode = lastPreviousTerminal
								             }).ToArray();

							//if (tableReference.Terminals.Last().Token.Value.ToUpper() == )
						}
					}
				}
				
				return EmptyCollection;
			}

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
									var currentName = statementText.Substring(currentNode.SourcePosition.IndexStart, cursorPosition - currentNode.SourcePosition.IndexStart);
									return columnReferences[0].TableNodeReferences.Single().Columns
										.Where(c => String.IsNullOrEmpty(currentName) || c.Name.Contains(currentName.ToUpperInvariant()))
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
						? schemaIdentifier.Token.Value
						: DatabaseModelFake.Instance.CurrentSchema;

					var currentName = statementText.Substring(currentNode.SourcePosition.IndexStart, cursorPosition - currentNode.SourcePosition.IndexStart);
					return GenerateSchemaObjectItems(schemaName, currentName, currentNode);
				}
			}

			return EmptyCollection;
		}

		private ICollection<ICodeCompletionItem> GenerateSchemaObjectItems(string schemaName, string objectNamePart, StatementDescriptionNode node)
		{
			return DatabaseModelFake.Instance.AllObjects.Values
						.Where(o => o.Owner == schemaName.ToOracleIdentifier() && (String.IsNullOrEmpty(objectNamePart) || o.Name.Contains(objectNamePart.ToUpperInvariant())))
						.Select(o => new OracleCodeCompletionItem
						{
							Name = o.Name.ToSimpleIdentifier(),
							StatementNode = node
						}).ToArray();
		}
	}

	public class OracleCodeCompletionItem : ICodeCompletionItem
	{
		public string Name { get; set; }
		
		public StatementDescriptionNode StatementNode { get; set; }
	}
}
