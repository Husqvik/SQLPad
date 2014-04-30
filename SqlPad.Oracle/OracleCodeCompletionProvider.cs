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

		private const string JoinTypeJoin = "JOIN";
		private const string JoinTypeLeftJoin = "LEFT JOIN";
		private const string JoinTypeRightJoin = "RIGHT JOIN";
		private const string JoinTypeFullJoin = "FULL JOIN";
		private const string JoinTypeCrossJoin = "CROSS JOIN";

		private static readonly OracleCodeCompletionItem[] JoinClauses =
		{
			new OracleCodeCompletionItem { Name = JoinTypeJoin, Text = JoinTypeJoin, Priority = 0, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeLeftJoin, Text = JoinTypeLeftJoin, Priority = 1, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeRightJoin, Text = JoinTypeRightJoin, Priority = 2, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeFullJoin, Text = JoinTypeFullJoin, Priority = 3, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeCrossJoin, Text = JoinTypeCrossJoin, Priority = 4, Category = OracleCodeCompletionCategory.JoinMethod, CategoryPriority = 1 },
		};

		public ICollection<ICodeCompletionItem> ResolveItems(IDatabaseModel databaseModel, string statementText, int cursorPosition)
		{
			return ResolveItems(databaseModel, statementText, _oracleParser.Parse(statementText), cursorPosition);
		}

		public ICollection<ICodeCompletionItem> ResolveItems(IDatabaseModel databaseModel, string statementText, StatementCollection statements, int cursorPosition)
		{
			//Trace.WriteLine("OracleCodeCompletionProvider.ResolveItems called. Cursor position: "+ cursorPosition);

			if (statements == null)
				return EmptyCollection;

			StatementDescriptionNode currentNode;

			var completionItems = Enumerable.Empty<ICodeCompletionItem>();
			var statement = (OracleStatement)statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);
			//
			/*currentNode = statements.GetTerminalAtPosition(cursorPosition);
			var isCursorAtTerminal = true;
			if (currentNode == null)
			{
				var statement = (OracleStatement)statements.LastOrDefault(s => s.GetNearestTerminalToPosition(cursorPosition) != null);
				if (statement != null)
				{
					currentNode = statement.GetNearestTerminalToPosition(cursorPosition);
					isCursorAtTerminal = false;
				}
			}

			if (currentNode == null)
				return EmptyCollection;*/

			//

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

				var extraLength = cursorPosition - currentNode.SourcePosition.IndexEnd - 1;
				if (extraLength > 0)
				{
					var substring = statementText.Substring(currentNode.SourcePosition.IndexEnd + 1, extraLength).Trim();
					if (!String.IsNullOrEmpty(substring))
					{
						return EmptyCollection;
					}
				}
			}
			else
			{
				currentNode = statement.GetNodeAtPosition(cursorPosition);
				if (currentNode.Type == NodeType.NonTerminal)
				{
					currentNode = statement.GetNearestTerminalToPosition(cursorPosition);
					isCursorAtTerminal = currentNode.SourcePosition.IndexEnd + 1 == cursorPosition;
				}
				else if (currentNode.Id.In(Terminals.RightParenthesis, Terminals.Comma, Terminals.Semicolon))
				{
					var previousNode = statement.GetNearestTerminalToPosition(cursorPosition - 1);
					if (previousNode != null)
					{
						currentNode = previousNode;
						isCursorAtTerminal = false;
					}
				}
			}

			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)currentNode.Statement, (OracleDatabaseModel)databaseModel);
			var terminalCandidates = new HashSet<string>(_oracleParser.GetTerminalCandidates(isCursorAtTerminal && !currentNode.Id.IsSingleCharacterTerminal() ? currentNode.PreviousTerminal : currentNode));

			var cursorAtLastTerminal = cursorPosition <= currentNode.SourcePosition.IndexEnd + 1;
			var terminalToReplace = cursorAtLastTerminal ? currentNode : null;
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			var extraOffset = currentNode.SourcePosition.IndexStart + currentNode.SourcePosition.Length == cursorPosition ? 1 : 0;

			var fromClause = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.FromClause);
			if ((currentNode.Id == Terminals.From && !cursorAtLastTerminal) ||
				(currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.Comma) && fromClause != null))
			{
				var schemaName = databaseModel.CurrentSchema;
				var schemaFound = false;
				if (currentNode.Id == Terminals.ObjectIdentifier && currentNode.ParentNode.Id == NonTerminals.QueryTableExpression &&
				    currentNode.ParentNode.FirstTerminalNode.Id == Terminals.SchemaIdentifier)
				{
					schemaFound = true;
					schemaName = currentNode.ParentNode.FirstTerminalNode.Token.Value;
				}

				var currentName = currentNode.Id.In(Terminals.From, Terminals.Comma) ? null : statementText.Substring(currentNode.SourcePosition.IndexStart, cursorPosition - currentNode.SourcePosition.IndexStart);

				completionItems = completionItems.Concat(GenerateSchemaObjectItems(schemaName, currentName, terminalToReplace, extraOffset));

				if (!schemaFound)
				{
					completionItems = completionItems.Concat(GenerateSchemaItems(currentName, terminalToReplace, extraOffset));
				}

				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, currentName, terminalToReplace, extraOffset));
			}

			if (currentNode.Id == Terminals.Dot &&
				currentNode.ParentNode.Id == NonTerminals.SchemaPrefix &&
				!currentNode.IsWithinSelectClauseOrExpression())
			{
				var ownerName = currentNode.ParentNode.ChildNodes.Single(n => n.Id == Terminals.SchemaIdentifier).Token.Value;
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(ownerName, null, null, 0));
			}

			var joinClauseNode = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
			if (currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias, Terminals.On))
			{
				if (joinClauseNode != null && !cursorAtLastTerminal)
				{
					var isInnerJoin = joinClauseNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.InnerJoinClause) != null;
					if (!isInnerJoin || (!joinClauseNode.FirstTerminalNode.Id.In(Terminals.Cross, Terminals.Natural)))
					{
						var joinedTableReferenceNodes = joinClauseNode.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.JoinClause, NonTerminals.NestedQuery), NonTerminals.TableReference).ToArray();
						if (joinedTableReferenceNodes.Length == 1)
						{
							var joinedTableReference = queryBlock.ObjectReferences.SingleOrDefault(t => t.TableReferenceNode == joinedTableReferenceNodes[0]);

							foreach (var parentTableReference in queryBlock.ObjectReferences
								.Where(t => t.TableReferenceNode.SourcePosition.IndexStart < joinedTableReference.TableReferenceNode.SourcePosition.IndexStart))
							{
								var joinSuggestions = GenerateJoinConditionSuggestionItems(parentTableReference, joinedTableReference, currentNode.Id == Terminals.On, extraOffset);
								completionItems = completionItems.Concat(joinSuggestions);
							}
						}
					}
				}
			}

			if ((currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias) ||
			    (joinClauseNode != null && joinClauseNode.IsGrammarValid)) &&
				!cursorAtLastTerminal)
			{
				var tableReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.TableReference);
				if ((tableReference != null && currentNode == tableReference.LastTerminalNode && tableReference.ParentNode.Id == NonTerminals.FromClause && tableReference == tableReference.ParentNode.ChildNodes.First()) ||
					(joinClauseNode != null && joinClauseNode.IsGrammarValid))
				{
					completionItems = completionItems.Concat(
						//JoinClauses.Where(j => alias == null || j.Name.Contains(alias.Token.Value.ToUpperInvariant()))
						JoinClauses);
				}
			}

			if (currentNode.Id == Terminals.Join ||
				(currentNode.Id == Terminals.ObjectAlias && currentNode.Token.Value.ToUpperInvariant() == Terminals.Join.ToUpperInvariant()))
			{
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(databaseModel.CurrentSchema, null, null, extraOffset));
				completionItems = completionItems.Concat(GenerateSchemaItems(null, null, extraOffset));
				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, null, null, extraOffset));
			}

			if (queryBlock != null && !isCursorAtTerminal && joinClauseNode == null && fromClause == null && !currentNode.IsWithinHavingClause() &&
				terminalCandidates.Contains(Terminals.ObjectIdentifier))
			{
				var whereTableReferences = queryBlock.ObjectReferences
					.Where(o => !String.IsNullOrEmpty(o.FullyQualifiedName.ToString()))
					.Select(o => new OracleCodeCompletionItem
					             {
									 Name = o.FullyQualifiedName.ToString(),
									 Category = o.Type.ToCategoryLabel(),
									 Offset = extraOffset,
									 Text = o.FullyQualifiedName.ToString()
					             });

				completionItems = completionItems.Concat(whereTableReferences);
			}

			if (currentNode.IsWithinSelectClauseOrExpression() &&
				terminalCandidates.Contains(Terminals.Identifier) &&
				currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.Identifier, Terminals.Comma, Terminals.Dot, Terminals.Select))
			{
				completionItems = completionItems.Concat(GenerateColumnItems(currentNode, semanticModel, cursorPosition));
			}

			return completionItems.OrderItems().ToArray();

			/*if (currentNode.Id == Terminals.ObjectIdentifier &&
			    !currentNode.IsWithinSelectClauseOrExpression())
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
			var prefixedColumnReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
			var columnIdentifierFollowing = currentNode.Id != Terminals.Identifier && prefixedColumnReference != null && prefixedColumnReference.GetDescendants(Terminals.Identifier).FirstOrDefault() != null;
			if (!currentNode.IsWithinSelectClauseOrExpression() || columnIdentifierFollowing)
			{
				return EmptyCollection;
			}
			
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			var objectIdentifierNode = currentNode.ParentNode.Id == NonTerminals.ObjectPrefix ? currentNode.ParentNode.GetSingleDescendant(Terminals.ObjectIdentifier) : null;
			if (objectIdentifierNode == null && prefixedColumnReference != null)
			{
				objectIdentifierNode = prefixedColumnReference.GetSingleDescendant(Terminals.ObjectIdentifier);
			}

			var tableReferences = queryBlock.ObjectReferences.AsEnumerable();
			if (objectIdentifierNode != null)
			{
				var schemaIdentifier = currentNode.ParentNode.GetSingleDescendant(Terminals.SchemaIdentifier);
				var schemaName = schemaIdentifier == null ? null : schemaIdentifier.Token.Value;
				var fullyQualifiedName = OracleObjectIdentifier.Create(schemaName, objectIdentifierNode.Token.Value);
				tableReferences = tableReferences.Where(t => t.FullyQualifiedName == fullyQualifiedName || (String.IsNullOrEmpty(fullyQualifiedName.Owner) && fullyQualifiedName.NormalizedName == t.FullyQualifiedName.NormalizedName));
			}

			var currentName = currentNode.Id == Terminals.Identifier && cursorPosition <= currentNode.SourcePosition.IndexEnd + 1
				? currentNode.Token.Value.Substring(0, cursorPosition - currentNode.SourcePosition.IndexStart)
				: null;

			var columnCandidates = tableReferences
				.SelectMany(t => t.Columns
					.Where(c =>
						(currentNode.Id != Terminals.Identifier || c.Name != currentNode.Token.Value.ToQuotedIdentifier()) &&
						(objectIdentifierNode == null || String.IsNullOrEmpty(currentName) || (c.Name != currentName.ToQuotedIdentifier() && c.Name.Contains(currentName.ToUpperInvariant()))))
					.Select(c => new { TableReference = t, Column = c }))
					.GroupBy(c => c.Column.Name).ToDictionary(g => g.Key ?? String.Empty, g => g.Select(o => o.TableReference.FullyQualifiedName).ToArray());

			var suggestedColumns = new List<Tuple<string, OracleObjectIdentifier>>();
			foreach (var columnCandidate in columnCandidates)
			{
				suggestedColumns.AddRange(OracleObjectIdentifier.GetUniqueReferences(columnCandidate.Value)
					.Select(objectIdentifier => new Tuple<string, OracleObjectIdentifier>(columnCandidate.Key, objectIdentifier)));
			}

			var specificColumns = suggestedColumns.Select(t => CreateColumnCodeCompletionItem(t.Item1, objectIdentifierNode == null ? t.Item2.ToString() : null, currentNode));

			if (currentName == null && currentNode.IsWithinSelectClause() && currentNode.GetParentExpression().GetParentExpression() == null)
			{
				specificColumns = specificColumns.Concat(CreateAsteriskColumnCompletionItems(tableReferences, objectIdentifierNode != null, currentNode));
			}

			return specificColumns;
		}

		private IEnumerable<OracleCodeCompletionItem> CreateAsteriskColumnCompletionItems(IEnumerable<OracleObjectReference> tables, bool skipFirstObjectIdentifier, StatementDescriptionNode currentNode)
		{
			var builder = new StringBuilder();
			
			foreach (var table in tables)
			{
				if (table.Columns.Count <= 1)
					continue;

				builder.Clear();
				var isFirstColumn = true;
				var skipTablePrefix = skipFirstObjectIdentifier;

				foreach (var column in table.Columns)
				{
					if (!isFirstColumn)
					{
						builder.Append(", ");
					}

					if (!skipTablePrefix && !String.IsNullOrEmpty(table.FullyQualifiedName.Name))
					{
						builder.Append(table.FullyQualifiedName);
						builder.Append(".");
					}
					
					builder.Append(column.Name.ToSimpleIdentifier());

					isFirstColumn = false;
					skipTablePrefix = false;
				}

				yield return new OracleCodeCompletionItem
				             {
								 Name = (skipFirstObjectIdentifier || String.IsNullOrEmpty(table.FullyQualifiedName.Name) ? String.Empty : table.FullyQualifiedName + ".") + "*",
								 Text = builder.ToString(),
								 StatementNode = currentNode.Id == Terminals.Identifier ? currentNode : null,
								 CategoryPriority = -2,
								 Category = OracleCodeCompletionCategory.AllColumns
				             };
			}
		}

		private OracleCodeCompletionItem CreateColumnCodeCompletionItem(string columnName, string objectPrefix, StatementDescriptionNode currentNode)
		{
			if (!String.IsNullOrEmpty(objectPrefix))
				objectPrefix += ".";

			var text = objectPrefix + columnName.ToSimpleIdentifier();

			return new OracleCodeCompletionItem
			       {
					   Name = text,
					   Text = text,
				       StatementNode = currentNode.Id == Terminals.Identifier ? currentNode : null,
				       Category = OracleCodeCompletionCategory.Column,
					   CategoryPriority = -1
			       };
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaItems(string schemaNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return DatabaseModelFake.Instance.Schemas
				.Where(s => schemaNamePart.ToQuotedIdentifier() != s && (String.IsNullOrEmpty(schemaNamePart) || s.Contains(schemaNamePart.ToUpperInvariant())))
				.Select(s => new OracleCodeCompletionItem
				             {
								 Name = s.ToSimpleIdentifier(),
								 Text = s.ToSimpleIdentifier(),
								 StatementNode = node,
								 Category = OracleCodeCompletionCategory.DatabaseSchema,
								 Offset = insertOffset,
								 CategoryPriority = 1
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaObjectItems(string schemaName, string objectNamePart, StatementDescriptionNode node, int insertOffset)
		{
			return DatabaseModelFake.Instance.AllObjects.Values
						.Where(o => o.Owner == schemaName.ToQuotedIdentifier() && objectNamePart.ToQuotedIdentifier() != o.Name &&
							(node == null || node.Token.Value.ToQuotedIdentifier() != o.Name) &&
							(String.IsNullOrEmpty(objectNamePart) || o.Name.Contains(objectNamePart.ToUpperInvariant())))
						.Select(o => new OracleCodeCompletionItem
						{
							Name = o.Name.ToSimpleIdentifier(),
							Text = o.Name.ToSimpleIdentifier(),
							StatementNode = node,
							Category = OracleCodeCompletionCategory.SchemaObject,
							Offset = insertOffset
						});
		}

		private IEnumerable<ICodeCompletionItem> GenerateCommonTableExpressionReferenceItems(OracleStatementSemanticModel model, string referenceNamePart, StatementDescriptionNode node, int insertOffset)
		{
			// TODO: Make proper resolution of CTE accessibility
			return model.QueryBlocks
						.Where(qb => qb.Type == QueryBlockType.CommonTableExpression && referenceNamePart.ToQuotedIdentifier() != qb.NormalizedAlias && (String.IsNullOrEmpty(referenceNamePart) || qb.Alias.ToUpperInvariant().Contains(referenceNamePart.ToUpperInvariant())))
						.Select(qb => new OracleCodeCompletionItem
						{
							Name = qb.Alias,
							Text = qb.Alias,
							StatementNode = node,
							Category = OracleCodeCompletionCategory.CommonTableExpression,
							Offset = insertOffset,
							CategoryPriority = -1
						});
		}

		private IEnumerable<ICodeCompletionItem> GenerateJoinConditionSuggestionItems(OracleObjectReference parentSchemaObject, OracleObjectReference joinedSchemaObject, bool skipOnTerminal, int insertOffset)
		{
			var codeItems = Enumerable.Empty<ICodeCompletionItem>();

			if (parentSchemaObject.Type == TableReferenceType.PhysicalObject && joinedSchemaObject.Type == TableReferenceType.PhysicalObject)
			{
				if (parentSchemaObject.SearchResult.SchemaObject == null || joinedSchemaObject.SearchResult.SchemaObject == null)
					return EmptyCollection;

				var parentObject = parentSchemaObject.SearchResult.SchemaObject;
				var joinedObject = joinedSchemaObject.SearchResult.SchemaObject;

				var joinedToParentKeys = parentObject.ForeignKeys.Where(k => k.TargetObject == joinedObject.FullyQualifiedName)
					.Select(k => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedName, joinedSchemaObject.FullyQualifiedName, k.SourceColumns, k.TargetColumns, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(joinedToParentKeys);

				var parentToJoinedKeys = joinedObject.ForeignKeys.Where(k => k.TargetObject == parentObject.FullyQualifiedName)
					.Select(k => GenerateJoinConditionSuggestionItem(joinedSchemaObject.FullyQualifiedName, parentSchemaObject.FullyQualifiedName, k.SourceColumns, k.TargetColumns, true, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(parentToJoinedKeys);
			}
			else
			{
				var columnNameJoinConditions = parentSchemaObject.Columns
					.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name)
					.Intersect(
						joinedSchemaObject.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name))
					.Select(c => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedName, joinedSchemaObject.FullyQualifiedName, new[] { c }, new[] { c }, false, skipOnTerminal, insertOffset));

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

			return new OracleCodeCompletionItem { Name = builder.ToString(), Text = builder.ToString(), Offset = insertOffset };
		}
	}

	public static class OracleCodeCompletionCategory
	{
		public const string DatabaseSchema = "Database Schema";
		public const string SchemaObject = "Schema Object";
		public const string Subquery = "Subquery";
		public const string CommonTableExpression = "Common Table Expression";
		public const string Column = "Column";
		public const string AllColumns = "All Columns";
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

		public string Text { get; set; }
	}
}
