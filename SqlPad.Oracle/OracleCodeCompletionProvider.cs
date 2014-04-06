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

		private static readonly OracleCodeCompletionItem[] JoinClauses =
		{
			new OracleCodeCompletionItem { Name = "JOIN", Priority = 0, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = "LEFT JOIN", Priority = 1, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = "RIGHT JOIN", Priority = 2, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = "FULL JOIN", Priority = 3, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = "CROSS JOIN", Priority = 4, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
		};

		public ICollection<ICodeCompletionItem> ResolveItems(string statementText, int cursorPosition)
		{
			//Trace.WriteLine("OracleCodeCompletionProvider.ResolveItems called. Cursor position: "+ cursorPosition);

			var databaseModel = DatabaseModelFake.Instance;
			StatementDescriptionNode currentNode;

			var completionItems = Enumerable.Empty<ICodeCompletionItem>();
			var statements = _oracleParser.Parse(statementText);
			var statement = (OracleStatement)statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			var isCursorAtTerminal = true;
			if (statement == null)
			{
				isCursorAtTerminal = false;

				statement = (OracleStatement)statements.LastOrDefault(s => s.GetNearestTerminalToPosition(cursorPosition) != null);
				if (statement == null)
				{
					return EmptyCollection;
				}

				currentNode = statement.GetNearestTerminalToPosition(cursorPosition);

				var substring = statementText.Substring(currentNode.SourcePosition.IndexEnd + 1, cursorPosition - currentNode.SourcePosition.IndexEnd - 1).Trim();
				if (!String.IsNullOrEmpty(substring))
				{
					return EmptyCollection;
				}
			}
			else
			{
				currentNode = statement.GetNodeAtPosition(cursorPosition);
				if (currentNode.Type == NodeType.NonTerminal)
				{
					isCursorAtTerminal = false;
					currentNode = statement.GetNearestTerminalToPosition(cursorPosition);
				}
			}

			if (currentNode == null)
			{
				return EmptyCollection;
			}

			var semanticModel = new OracleStatementSemanticModel(null, statement, databaseModel);
			var terminalCandidates = new HashSet<string>(_oracleParser.GetTerminalCandidates(currentNode));

			var cursorAtLastTerminal = cursorPosition <= currentNode.SourcePosition.IndexEnd + 1;
			var terminalToReplace = cursorAtLastTerminal ? currentNode : null;
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			var extraOffset = currentNode.SourcePosition.IndexStart + currentNode.SourcePosition.Length == cursorPosition ? 1 : 0;

			if (currentNode.Id == Terminals.From ||
				currentNode.Id == Terminals.ObjectIdentifier)
			{
				var schemaName = databaseModel.CurrentSchema;
				var schemaFound = false;
				if (currentNode.Id == Terminals.ObjectIdentifier && currentNode.ParentNode.Id == NonTerminals.QueryTableExpression &&
					currentNode.ParentNode.FirstTerminalNode.Id == Terminals.SchemaIdentifier)
				{
					schemaFound = true;
					schemaName = currentNode.ParentNode.FirstTerminalNode.Token.Value;
				}

				var currentName = currentNode.Id == Terminals.From ? null : statementText.Substring(currentNode.SourcePosition.IndexStart, cursorPosition - currentNode.SourcePosition.IndexStart);
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(schemaName, currentName, terminalToReplace, extraOffset));

				if (!schemaFound)
				{
					completionItems = completionItems.Concat(GenerateSchemaItems(currentName, terminalToReplace, extraOffset));
				}

				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, currentName, terminalToReplace, extraOffset));
			}

			if (currentNode.Id == Terminals.Dot &&
				currentNode.ParentNode.Id == NonTerminals.SchemaPrefix &&
				!currentNode.IsWithinSelectClauseOrCondition())
			{
				var ownerName = currentNode.ParentNode.ChildNodes.Single(n => n.Id == Terminals.SchemaIdentifier).Token.Value;
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(ownerName, null, null, 0));
			}

			var joinClauseNode = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
			if (currentNode.Id == Terminals.ObjectIdentifier ||
				currentNode.Id == Terminals.Alias ||
				currentNode.Id == Terminals.RightParenthesis ||
				currentNode.Id == Terminals.On)
			{
				if (joinClauseNode != null)
				{
					var isInnerJoin = joinClauseNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.InnerJoinClause) != null;
					if (!isInnerJoin || (joinClauseNode.FirstTerminalNode.Id != Terminals.Cross && joinClauseNode.FirstTerminalNode.Id != Terminals.Natural))
					{
						var joinedTableReferenceNode = joinClauseNode.GetPathFilterDescendants(n => n.Id != NonTerminals.JoinClause, NonTerminals.TableReference).SingleOrDefault();
						if (joinedTableReferenceNode != null)
						{
							var joinedTableReference = queryBlock.TableReferences.SingleOrDefault(t => t.TableReferenceNode == joinedTableReferenceNode);

							foreach (var parentTableReference in queryBlock.TableReferences
								.Where(t => t.TableReferenceNode.SourcePosition.IndexStart < joinedTableReference.TableReferenceNode.SourcePosition.IndexStart))
							{
								var joinSuggestions = GenerateJoinConditionSuggestionItems(parentTableReference, joinedTableReference, currentNode.Id == Terminals.On, extraOffset);
								completionItems = completionItems.Concat(joinSuggestions);
							}
						}
					}
				}
			}

			if (((currentNode.Id == Terminals.ObjectIdentifier || currentNode.Id == Terminals.Alias) && !cursorAtLastTerminal) ||
			    (joinClauseNode != null && joinClauseNode.IsGrammarValid))
			{
				var tableReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.TableReference);
				if ((tableReference != null && currentNode == tableReference.LastTerminalNode && tableReference.ParentNode.Id == NonTerminals.FromClause && tableReference == tableReference.ParentNode.ChildNodes.First()) ||
					(joinClauseNode != null && joinClauseNode.IsGrammarValid))
				{
					completionItems = completionItems.Concat(
						//JoinClauses.Where(j => alias == null || j.Name.Contains(alias.Token.Value.ToUpperInvariant()))
						JoinClauses
							.Select(j => new OracleCodeCompletionItem
							             {
								             Name = j.Name,
								             Category = j.Category,
								             CategoryPriority = j.CategoryPriority,
								             Priority = j.Priority,
								             StatementNode = terminalToReplace,
								             Offset = extraOffset
							             }));
				}
			}

			if (currentNode.Id == Terminals.Join ||
				(currentNode.Id == Terminals.Alias && currentNode.Token.Value.ToUpperInvariant() == Terminals.Join.ToUpperInvariant()))
			{
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(databaseModel.CurrentSchema, null, null, extraOffset));
				completionItems = completionItems.Concat(GenerateSchemaItems(null, null, extraOffset));
				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, null, null, extraOffset));
			}

			if (!isCursorAtTerminal && joinClauseNode == null && !currentNode.IsWithinHavingClause() &&
				(terminalCandidates.Contains(Terminals.ObjectIdentifier)))
			{
				var whereTableReferences = queryBlock.TableReferences
					.Select(t => new OracleCodeCompletionItem
					             {
									 Name = t.FullyQualifiedName.ToString(),
									 Category = t.Type.ToCategoryLabel(),
									 Offset = extraOffset
					             });

				completionItems = completionItems.Concat(whereTableReferences);
			}

			if (currentNode.IsWithinSelectClauseOrCondition() &&
				(isCursorAtTerminal || terminalCandidates.Contains(Terminals.Identifier)) &&
				(currentNode.Id == Terminals.ObjectIdentifier || currentNode.Id == Terminals.Identifier || currentNode.Id == Terminals.Comma || currentNode.Id == Terminals.Dot))
			{
				completionItems = completionItems.Concat(GenerateColumnItems(currentNode, semanticModel, cursorPosition));
			}

			return completionItems.OrderItems().ToArray();

			/*if (currentNode.Id == Terminals.ObjectIdentifier &&
			    !currentNode.IsWithinSelectClauseOrCondition())
			{
				// TODO: Add option to search all/current/public schemas
				var schemaIdentifier = currentNode.ParentNode.GetSingleDescendant(Terminals.SchemaIdentifier);

				var schemaName = schemaIdentifier != null
					? schemaIdentifier.Token.Value
					: databaseModel.CurrentSchema;

				var currentName = statementText.Substring(currentNode.SourcePosition.IndexStart, cursorPosition - currentNode.SourcePosition.IndexStart);
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(schemaName, currentName, currentNode, 0).OrderBy(i => i.CategoryPriority).ThenBy(i => i.Priority).ThenBy(i => i.Name));
			}

			return completionItems.OrderItems().ToArray();*/
		}

		private IEnumerable<ICodeCompletionItem> GenerateColumnItems(StatementDescriptionNode currentNode, OracleStatementSemanticModel semanticModel, int cursorPosition)
		{
			if (!currentNode.IsWithinSelectClauseOrCondition())
			{
				return EmptyCollection;
			}
			
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			var objectIdentifier = currentNode.ParentNode.Id == NonTerminals.ObjectPrefix ? currentNode.ParentNode.GetSingleDescendant(Terminals.ObjectIdentifier) : null;
			if (objectIdentifier == null)
			{
				var prefixedColumnReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
				if (prefixedColumnReference != null)
				{
					objectIdentifier = prefixedColumnReference.GetSingleDescendant(Terminals.ObjectIdentifier);
				}
			}

			var tableReferences = queryBlock.TableReferences.AsEnumerable();
			if (objectIdentifier != null)
			{
				var schemaIdentifier = currentNode.ParentNode.GetSingleDescendant(Terminals.SchemaIdentifier);
				var schemaName = schemaIdentifier == null ? null : schemaIdentifier.Token.Value;
				var fullyQualifiedName = OracleObjectIdentifier.Create(schemaName, objectIdentifier.Token.Value);
				tableReferences = tableReferences.Where(t => t.FullyQualifiedName == fullyQualifiedName || (String.IsNullOrEmpty(fullyQualifiedName.Owner) && fullyQualifiedName.NormalizedName == t.FullyQualifiedName.NormalizedName));
			}

			var currentName = currentNode.Id == Terminals.Identifier && cursorPosition <= currentNode.SourcePosition.IndexEnd + 1 ? currentNode.Token.Value.Substring(0, cursorPosition - currentNode.SourcePosition.IndexStart) : null;

			return tableReferences
				.SelectMany(t => t.Columns
					.Where(c => objectIdentifier == null || String.IsNullOrEmpty(currentName) || (c.Name != currentName.ToQuotedIdentifier() && c.Name != currentNode.Token.Value.ToQuotedIdentifier() && c.Name.Contains(currentName.ToUpperInvariant())))
					.Select(c => new { TableReference = t, Column = c }))
				.Select(t => CreateColumnCodeCompletionItem(t.Column, objectIdentifier == null ? t.TableReference : null, currentNode));
		}

		private OracleCodeCompletionItem CreateColumnCodeCompletionItem(OracleColumn column, OracleTableReference tableReference, StatementDescriptionNode currentNode)
		{
			var tablePrefix = tableReference == null ? null : tableReference.FullyQualifiedName + ".";

			return new OracleCodeCompletionItem
			       {
					   Name = tablePrefix + column.Name.ToSimpleIdentifier(),
				       StatementNode = currentNode.Id == Terminals.Identifier ? currentNode : null,
				       Category = OracleCodeCompletionCategory.Column
			       };
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaItems(string schemaNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return DatabaseModelFake.Instance.Schemas
				.Where(s => schemaNamePart.ToQuotedIdentifier() != s && (String.IsNullOrEmpty(schemaNamePart) || s.Contains(schemaNamePart.ToUpperInvariant())))
				.Select(s => new OracleCodeCompletionItem
				             {
								 Name = s.ToSimpleIdentifier(),
								 StatementNode = node,
								 Category = OracleCodeCompletionCategory.DatabaseSchema,
								 Offset = insertOffset,
								 CategoryPriority = 1
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaObjectItems(string schemaName, string objectNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return DatabaseModelFake.Instance.AllObjects.Values
						.Where(o => o.Owner == schemaName.ToQuotedIdentifier() && objectNamePart.ToQuotedIdentifier() != o.Name && (String.IsNullOrEmpty(objectNamePart) || o.Name.Contains(objectNamePart.ToUpperInvariant())))
						.Select(o => new OracleCodeCompletionItem
						{
							Name = o.Name.ToSimpleIdentifier(),
							StatementNode = node,
							Category = OracleCodeCompletionCategory.SchemaObject,
							Offset = insertOffset
						});
		}

		private IEnumerable<ICodeCompletionItem> GenerateCommonTableExpressionReferenceItems(OracleStatementSemanticModel model, string referenceNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return model.QueryBlocks
						.Where(qb => qb.Type == QueryBlockType.CommonTableExpression && referenceNamePart.ToQuotedIdentifier() != qb.Alias && (String.IsNullOrEmpty(referenceNamePart) || qb.Alias.ToUpperInvariant().Contains(referenceNamePart.ToUpperInvariant())))
						.Select(qb => new OracleCodeCompletionItem
						{
							Name = qb.Alias.ToSimpleIdentifier(),
							StatementNode = node,
							Category = OracleCodeCompletionCategory.CommonTableExpression,
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

	public static class OracleCodeCompletionCategory
	{
		public const string DatabaseSchema = "Database Schema";
		public const string SchemaObject = "Schema Object";
		public const string Subquery = "Subquery";
		public const string CommonTableExpression = "Common Table Expression";
		public const string Column = "Column";
		public const string JoinMethod = "Join Method";
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
