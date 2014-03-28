using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleCodeCompletionProvider : ICodeCompletionProvider
	{
		private readonly OracleSqlParser _oracleParser = new OracleSqlParser();
		private static readonly ICodeCompletionItem[] EmptyCollection = new ICodeCompletionItem[0];
		private const string CategoryJoinType = "Join Type";

		private static readonly OracleCodeCompletionItem[] JoinClauses =
		{
			new OracleCodeCompletionItem { Name = "JOIN", Priority = 0, Category = CategoryJoinType, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = "LEFT JOIN", Priority = 1, Category = CategoryJoinType, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = "RIGHT JOIN", Priority = 2, Category = CategoryJoinType, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = "FULL JOIN", Priority = 3, Category = CategoryJoinType, CategoryPriority = 1 },
		};

		public ICollection<ICodeCompletionItem> ResolveItems(string statementText, int cursorPosition)
		{
			//Trace.WriteLine("OracleCodeCompletionProvider.ResolveItems called. Cursor position: "+ cursorPosition);

			OracleStatementSemanticModel semanticModel;
			var databaseModel = DatabaseModelFake.Instance;

			var statements = _oracleParser.Parse(statementText);
			var statement = (OracleStatement)statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			if (statement == null)
			{
				if (statements.Count > 0)
				{
					var lastStatement = (OracleStatement)statements.Last(s => s.GetNearestTerminalToPosition(cursorPosition) != null);
					semanticModel = new OracleStatementSemanticModel(null, lastStatement, databaseModel);

					var lastPreviousTerminal = lastStatement.GetNearestTerminalToPosition(cursorPosition);
					if (lastPreviousTerminal != null)
					{
						var queryBlock = semanticModel.GetQueryBlock(lastPreviousTerminal);
						var extraOffset = lastPreviousTerminal.SourcePosition.IndexStart + lastPreviousTerminal.SourcePosition.Length == cursorPosition ? 1 : 0;

						var completionItems = Enumerable.Empty<ICodeCompletionItem>();
						if (lastPreviousTerminal.Id == Terminals.From ||
						    lastPreviousTerminal.Id == Terminals.ObjectIdentifier)
						{
							var currentName = lastPreviousTerminal.Id == Terminals.From ? null : statementText.Substring(lastPreviousTerminal.SourcePosition.IndexStart, cursorPosition - lastPreviousTerminal.SourcePosition.IndexStart);
							completionItems = completionItems.Concat(GenerateSchemaObjectItems(databaseModel.CurrentSchema, currentName, lastPreviousTerminal, extraOffset));
							completionItems = completionItems.Concat(GenerateSchemaItems(currentName, lastPreviousTerminal, extraOffset));
							completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, currentName, lastPreviousTerminal, extraOffset));
						}

						if (lastPreviousTerminal.Id == Terminals.Dot &&
							lastPreviousTerminal.ParentNode.Id == NonTerminals.SchemaPrefix &&
							!lastPreviousTerminal.IsWithinSelectClauseOrCondition())
						{
							var ownerName = lastPreviousTerminal.ParentNode.ChildNodes.Single(n => n.Id == Terminals.SchemaIdentifier).Token.Value;
							completionItems = completionItems.Concat(GenerateSchemaObjectItems(ownerName, null, null, 0));
						}

						if (lastPreviousTerminal.Id == Terminals.ObjectIdentifier ||
						    lastPreviousTerminal.Id == Terminals.Alias ||
							lastPreviousTerminal.Id == Terminals.On)
						{
							var joinClause = lastPreviousTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
							if (joinClause != null)
							{
								var fromClause = joinClause.GetPathFilterAncestor(n => n.Id != NonTerminals.QueryBlock, NonTerminals.FromClause);
								if (fromClause != null)
								{
									var joinedTableReferenceNode = joinClause.GetPathFilterDescendants(n => n.Id != NonTerminals.JoinClause, NonTerminals.TableReference).SingleOrDefault();
									if (joinedTableReferenceNode != null)
									{
										var joinedTableReference = queryBlock.TableReferences.SingleOrDefault(t => t.TableReferenceNode == joinedTableReferenceNode);

										foreach (var parentTableReference in queryBlock.TableReferences
											.Where(t => t.TableReferenceNode.SourcePosition.IndexStart < joinedTableReference.TableReferenceNode.SourcePosition.IndexStart))
										{
											var joinSuggestions = GenerateJoinConditionSuggestionItems(parentTableReference, joinedTableReference, lastPreviousTerminal.Id == Terminals.On, extraOffset);
											completionItems = completionItems.Concat(joinSuggestions);
										}
									}
								}
							}
						}

						if (lastPreviousTerminal.Id == Terminals.ObjectIdentifier || lastPreviousTerminal.Id == Terminals.Alias)
						{
							var tableReference = lastPreviousTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.TableReference);
							if (tableReference != null && tableReference.ParentNode.Id == NonTerminals.FromClause && tableReference == tableReference.ParentNode.ChildNodes.First())
							{
								var alias = tableReference.GetDescendantsWithinSameQuery(Terminals.Alias).SingleOrDefault();
								if (alias == null || !String.Equals(alias.Token.Value, Terminals.Join, StringComparison.InvariantCultureIgnoreCase))
								{
									completionItems = completionItems.Concat(
										JoinClauses.Where(j => alias == null || j.Name.Contains(alias.Token.Value.ToUpperInvariant()))
											.Select(j => new OracleCodeCompletionItem
											             {
												             Name = j.Name,
															 Category = j.Category,
															 CategoryPriority = j.CategoryPriority,
															 Priority = j.Priority,
												             StatementNode = lastPreviousTerminal,
															 Offset = extraOffset
											             }));
								}
							}
						}

						if (lastPreviousTerminal.Id == Terminals.Join || (lastPreviousTerminal.Id == Terminals.Alias && lastPreviousTerminal.Token.Value.ToUpperInvariant() == Terminals.Join.ToUpperInvariant()))
						{
							completionItems = completionItems.Concat(GenerateSchemaObjectItems(databaseModel.CurrentSchema, null, null, extraOffset));
							completionItems = completionItems.Concat(GenerateSchemaItems(null, null, extraOffset));
							completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, null, null, extraOffset));
						}

						return completionItems.OrderBy(i => i.CategoryPriority).ThenBy(i => i.Priority).ThenBy(i => i.Name).ToArray();
					}
				}
				
				return EmptyCollection;
			}

			var currentNode = statement.GetNodeAtPosition(cursorPosition);
			semanticModel = new OracleStatementSemanticModel(statementText, statement, databaseModel);

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
														 StatementNode = currentNode,
														 Category = "Column"
										             }).ToArray();
								}
							}
						}
					}
				}
			}

			if (currentNode.Id == Terminals.ObjectIdentifier &&
				!currentNode.IsWithinSelectClauseOrCondition())
			{
				// TODO: Add option to search all/current/public schemas
				var schemaIdentifier = currentNode.ParentNode.GetSingleDescendant(Terminals.SchemaIdentifier);

				var schemaName = schemaIdentifier != null
					? schemaIdentifier.Token.Value
					: databaseModel.CurrentSchema;

				var currentName = statementText.Substring(currentNode.SourcePosition.IndexStart, cursorPosition - currentNode.SourcePosition.IndexStart);
				return GenerateSchemaObjectItems(schemaName, currentName, currentNode, 0).OrderBy(i => i.CategoryPriority).ThenBy(i => i.Priority).ThenBy(i => i.Name).ToArray();
			}

			return EmptyCollection;
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaItems(string schemaNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return DatabaseModelFake.Instance.Schemas
				.Where(s => String.IsNullOrEmpty(schemaNamePart) || s.Contains(schemaNamePart.ToUpperInvariant()))
				.Select(s => new OracleCodeCompletionItem
				             {
								 Name = s.ToSimpleIdentifier(),
								 StatementNode = node,
								 Category = "Database Schema",
								 Offset = insertOffset,
								 CategoryPriority = 1
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaObjectItems(string schemaName, string objectNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return DatabaseModelFake.Instance.AllObjects.Values
						.Where(o => o.Owner == schemaName.ToQuotedIdentifier() && (String.IsNullOrEmpty(objectNamePart) || o.Name.Contains(objectNamePart.ToUpperInvariant())))
						.Select(o => new OracleCodeCompletionItem
						{
							Name = o.Name.ToSimpleIdentifier(),
							StatementNode = node,
							Category = "Schema Object",
							Offset = insertOffset
						});
		}

		private IEnumerable<ICodeCompletionItem> GenerateCommonTableExpressionReferenceItems(OracleStatementSemanticModel model, string referenceNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return model.QueryBlocks
						.Where(qb => qb.Type == QueryBlockType.CommonTableExpression && (String.IsNullOrEmpty(referenceNamePart) || qb.Alias.ToUpperInvariant().Contains(referenceNamePart.ToUpperInvariant())))
						.Select(qb => new OracleCodeCompletionItem
						{
							Name = qb.Alias.ToSimpleIdentifier(),
							StatementNode = node,
							Category = "Common Table Expression",
							Offset = insertOffset,
							CategoryPriority = -1
						});
		}

		private IEnumerable<ICodeCompletionItem> GenerateJoinConditionSuggestionItems(OracleTableReference parentTable, OracleTableReference joinedTable, bool skipOnTerminal, int insertOffset)
		{
			var codeItems = Enumerable.Empty<ICodeCompletionItem>();

			if (parentTable.Type == TableReferenceType.PhysicalObject && joinedTable.Type == TableReferenceType.PhysicalObject)
			{
				if (parentTable.SearchResult.SchemaObject == null || joinedTable.SearchResult.SchemaObject == null)
					return EmptyCollection;

				var parentObject = parentTable.SearchResult.SchemaObject;
				var joinedObject = joinedTable.SearchResult.SchemaObject;

				var joinedToParentKeys = parentObject.ForeignKeys.Where(k => k.TargetObject == joinedObject.FullyQualifiedName)
					.Select(k => GenerateJoinConditionSuggestionItem(parentTable.FullyQualifiedName, joinedTable.FullyQualifiedName, k.SourceColumns, k.TargetColumns, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(joinedToParentKeys);

				var parentToJoinedKeys = joinedObject.ForeignKeys.Where(k => k.TargetObject == parentObject.FullyQualifiedName)
					.Select(k => GenerateJoinConditionSuggestionItem(joinedTable.FullyQualifiedName, parentTable.FullyQualifiedName, k.SourceColumns, k.TargetColumns, true, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(parentToJoinedKeys);
			}
			else
			{
				var columnNameJoinConditions = parentTable.Columns.Select(c => c.Name).Intersect(joinedTable.Columns.Select(c => c.Name))
					.Select(c => GenerateJoinConditionSuggestionItem(parentTable.FullyQualifiedName, joinedTable.FullyQualifiedName, new[] { c }, new[] { c }, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(columnNameJoinConditions);
			}

			return codeItems;
		}

		private OracleCodeCompletionItem GenerateJoinConditionSuggestionItem(OracleObjectIdentifier sourceObject, OracleObjectIdentifier targetObject, IList<string> keySourceColumns, IList<string> keyTargetColumns, bool swapSides, bool skipOnTerminal, int insertOffset)
		{
			var builder = new StringBuilder();
			if (!skipOnTerminal)
			{
				builder.Append(Terminals.On.ToUpperInvariant());
				builder.Append(" ");
			}

			var logicalOperator = String.Empty;

			for (var i = 0; i < keySourceColumns.Count; i++)
			{
				builder.Append(logicalOperator);
				builder.Append(swapSides ? targetObject : sourceObject);
				builder.Append('.');
				builder.Append((swapSides ? keyTargetColumns[i] : keySourceColumns[i]).ToSimpleIdentifier());
				builder.Append(" = ");
				builder.Append(swapSides ? sourceObject : targetObject);
				builder.Append('.');
				builder.Append((swapSides ? keySourceColumns[i] : keyTargetColumns[i]).ToSimpleIdentifier());

				logicalOperator = " AND ";
			}

			return new OracleCodeCompletionItem { Name = builder.ToString(), Offset = insertOffset };
		}
	}

	[DebuggerDisplay("OracleCodeCompletionItem (Name={Name}; Category={Category}; Priority={Priority})")]
	public class OracleCodeCompletionItem : ICodeCompletionItem
	{
		public string Category { get; set; }
		
		public string Name { get; set; }
		
		public StatementDescriptionNode StatementNode { get; set; }

		public int Priority { get; set; }

		public int CategoryPriority { get; set; }
		
		public int Offset { get; set; }
	}
}
