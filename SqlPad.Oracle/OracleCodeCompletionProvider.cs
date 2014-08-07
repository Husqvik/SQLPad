using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleCodeCompletionProvider : ICodeCompletionProvider
	{
		private readonly OracleSqlParser _parser = new OracleSqlParser();
		private static readonly ICodeCompletionItem[] EmptyCollection = new ICodeCompletionItem[0];

		private const string JoinTypeJoin = "JOIN";
		private const string JoinTypeInnerJoin = "INNER JOIN";
		private const string JoinTypeLeftJoin = "LEFT JOIN";
		private const string JoinTypeRightJoin = "RIGHT JOIN";
		private const string JoinTypeFullJoin = "FULL JOIN";
		private const string JoinTypeCrossJoin = "CROSS JOIN";

		private static readonly OracleCodeCompletionItem[] JoinClauseTemplates =
		{
			new OracleCodeCompletionItem { Name = JoinTypeJoin, Priority = 0 },
			new OracleCodeCompletionItem { Name = JoinTypeLeftJoin, Priority = 1 },
			new OracleCodeCompletionItem { Name = JoinTypeRightJoin, Priority = 2 },
			new OracleCodeCompletionItem { Name = JoinTypeFullJoin, Priority = 3 },
			new OracleCodeCompletionItem { Name = JoinTypeCrossJoin, Priority = 4 }
		};

		public ICollection<FunctionOverloadDescription> ResolveFunctionOverloads(SqlDocumentRepository sqlDocumentRepository, int cursorPosition)
		{
			var emptyCollection = new FunctionOverloadDescription[0];
			var node = sqlDocumentRepository.Statements.GetNodeAtPosition(cursorPosition);
			if (node == null)
				return emptyCollection;

			var semanticModel = (OracleStatementSemanticModel)sqlDocumentRepository.ValidationModels[node.Statement].SemanticModel;
			var oracleDatabaseModel = semanticModel.DatabaseModel;
			var queryBlock = semanticModel.GetQueryBlock(cursorPosition);
			var programReference = queryBlock.AllProgramReferences.Where(f => node.HasAncestor(f.ParameterListNode))
				.OrderByDescending(r => r.RootNode.Level)
				.FirstOrDefault();
			
			if (programReference == null || programReference.Metadata == null)
				return emptyCollection;

			var currentParameterIndex = -1;
			if (programReference.ParameterNodes != null)
			{
				var lookupNode = node.Type == NodeType.Terminal ? node : node.GetNearestTerminalToPosition(cursorPosition);

				if (lookupNode.Id == Terminals.Comma)
				{
					lookupNode = cursorPosition == lookupNode.SourcePosition.IndexStart ? lookupNode.PrecedingTerminal : lookupNode.FollowingTerminal;
				}
				else if (lookupNode.Id == Terminals.LeftParenthesis && cursorPosition > lookupNode.SourcePosition.IndexStart)
				{
					lookupNode = lookupNode.FollowingTerminal;
				}
				else if (lookupNode.Id == Terminals.RightParenthesis && cursorPosition == lookupNode.SourcePosition.IndexStart)
				{
					lookupNode = lookupNode.PrecedingTerminal;
				}

				if (lookupNode != null)
				{
					var parameterNode = programReference.ParameterNodes.FirstOrDefault(f => lookupNode.HasAncestor(f));
					currentParameterIndex = parameterNode == null
						? programReference.ParameterNodes.Count
						: programReference.ParameterNodes.ToList().IndexOf(parameterNode);
				}
			}

			var functionOverloads = oracleDatabaseModel.AllFunctionMetadata.SqlFunctions.Where(m => programReference.Metadata.Identifier.EqualsWithAnyOverload(m.Identifier) && (m.Parameters.Count == 0 || currentParameterIndex < m.Parameters.Count - 1));

			return functionOverloads.Select(
				o =>
				{
					var returnParameter = o.Parameters.FirstOrDefault();
					return new FunctionOverloadDescription
					       {
						       Name = programReference.Metadata.Identifier.FullyQualifiedIdentifier,
						       Parameters = o.Parameters.Skip(1).Select(p => p.Name + ": " + p.DataType).ToArray(),
						       CurrentParameterIndex = currentParameterIndex,
							   ReturnedDatatype = returnParameter == null ? null : returnParameter.DataType,
							   HasSchemaDefinition = !String.IsNullOrEmpty(o.Identifier.Owner)
					       };
				}).ToArray();
		}

		internal ICollection<ICodeCompletionItem> ResolveItems(IDatabaseModel databaseModel, string statementText, int cursorPosition, params string[] categories)
		{
			var documentStore = new SqlDocumentRepository(_parser, new OracleStatementValidator(), databaseModel, statementText);
			var sourceItems = ResolveItems(documentStore, databaseModel, statementText, cursorPosition, true);
			return sourceItems.Where(i => categories.Length == 0 || categories.Contains(i.Category)).ToArray();
		}

		public ICollection<ICodeCompletionItem> ResolveItems(SqlDocumentRepository sqlDocumentRepository, IDatabaseModel databaseModel, string statementText, int cursorPosition, bool forcedInvokation)
		{
			if (sqlDocumentRepository == null || sqlDocumentRepository.Statements == null)
				return EmptyCollection;

			var completionType = new OracleCodeCompletionType(sqlDocumentRepository, statementText, cursorPosition);
			completionType.PrintSupportedCompletions();
			if (!forcedInvokation && !completionType.JoinCondition && String.IsNullOrEmpty(completionType.TerminalValuePartUntilCaret) && !completionType.IsCursorTouchingIdentifier)
				return EmptyCollection;

			StatementGrammarNode currentNode;

			var completionItems = Enumerable.Empty<ICodeCompletionItem>();
			var statement = (OracleStatement)sqlDocumentRepository.Statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);

			var isCursorAtTerminal = true;
			if (statement == null)
			{
				isCursorAtTerminal = false;

				statement = completionType.Statement;
				if (statement == null)
				{
					return EmptyCollection;
				}

				currentNode = statement.GetNearestTerminalToPosition(cursorPosition);

				if (completionType.InUnparsedData || currentNode == null)
					return EmptyCollection;
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
					var precedingNode = statement.GetNearestTerminalToPosition(cursorPosition - 1);
					if (precedingNode != null)
					{
						currentNode = precedingNode;
						isCursorAtTerminal = false;
					}
				}
			}

			var oracleDatabaseModel = (OracleDatabaseModelBase)databaseModel;
			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)currentNode.Statement, oracleDatabaseModel);
			var terminalCandidates = new HashSet<string>(_parser.GetTerminalCandidates(isCursorAtTerminal && !currentNode.Id.IsSingleCharacterTerminal() ? currentNode.PrecedingTerminal : currentNode));

			var cursorAtLastTerminal = cursorPosition <= currentNode.SourcePosition.IndexEnd + 1;
			var terminalToReplace = cursorAtLastTerminal ? currentNode : null;
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			var extraOffset = currentNode.SourcePosition.IndexStart + currentNode.SourcePosition.Length == cursorPosition && currentNode.Id != Terminals.LeftParenthesis ? 1 : 0;

			var fromClause = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.FromClause);
			if (completionType.SchemaDataObject &&
				((currentNode.Id == Terminals.From && !cursorAtLastTerminal) ||
				 (currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.Comma) && fromClause != null)))
			{
				var schemaName = databaseModel.CurrentSchema.ToQuotedIdentifier();
				var schemaFound = false;
				if (currentNode.Id == Terminals.ObjectIdentifier && currentNode.ParentNode.Id == NonTerminals.QueryTableExpression &&
				    currentNode.ParentNode.FirstTerminalNode.Id == Terminals.SchemaIdentifier)
				{
					schemaFound = true;
					schemaName = currentNode.ParentNode.FirstTerminalNode.Token.Value;
				}

				var currentName = currentNode.Id.In(Terminals.From, Terminals.Comma) ? null : completionType.TerminalValuePartUntilCaret;
				if (String.IsNullOrEmpty(currentName) || currentName == currentName.Trim())
				{
					completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, schemaName, currentName, terminalToReplace, extraOffset, true));

					if (!schemaFound)
					{
						completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, OracleDatabaseModelBase.SchemaPublic, currentName, terminalToReplace, extraOffset, true));
						completionItems = completionItems.Concat(GenerateSchemaItems(currentName, terminalToReplace, extraOffset, oracleDatabaseModel));
					}

					completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, currentName, terminalToReplace, extraOffset));
				}
			}

			if (currentNode.Id == Terminals.Dot &&
				currentNode.ParentNode.Id == NonTerminals.SchemaPrefix &&
				!currentNode.IsWithinSelectClauseOrExpression())
			{
				var ownerName = currentNode.ParentNode.ChildNodes.Single(n => n.Id == Terminals.SchemaIdentifier).Token.Value;
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, ownerName, null, null, 0, true));
			}

			var joinClauseNode = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
			if (currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias, Terminals.On))
			{
				if (joinClauseNode != null && !cursorAtLastTerminal)
				{
					var isNotInnerJoin = joinClauseNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.InnerJoinClause) == null;
					if (isNotInnerJoin || (!joinClauseNode.FirstTerminalNode.Id.In(Terminals.Cross, Terminals.Natural)))
					{
						var joinedTableReferenceNodes = joinClauseNode.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.JoinClause, NonTerminals.NestedQuery), NonTerminals.TableReference).ToArray();
						if (joinedTableReferenceNodes.Length == 1)
						{
							var joinedTableReference = queryBlock.ObjectReferences.SingleOrDefault(t => t.RootNode == joinedTableReferenceNodes[0]);
							if (joinedTableReference != null)
							{
								foreach (var parentTableReference in queryBlock.ObjectReferences
									.Where(t => t.RootNode.SourcePosition.IndexStart < joinedTableReference.RootNode.SourcePosition.IndexStart &&
									            (t.Type != ReferenceType.InlineView || t.AliasNode != null)))
								{
									var joinSuggestions = GenerateJoinConditionSuggestionItems(parentTableReference, joinedTableReference, currentNode.Id == Terminals.On, extraOffset);
									completionItems = completionItems.Concat(joinSuggestions);
								}
							}
						}
					}
				}
			}

			if (completionType.JoinType)
			{
				completionItems = completionItems.Concat(CreateJoinTypeCompletionItems(completionType));
			}

			if (currentNode.Id == Terminals.Join ||
				(currentNode.Id == Terminals.ObjectAlias && currentNode.Token.Value.ToUpperInvariant() == Terminals.Join.ToUpperInvariant()))
			{
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, databaseModel.CurrentSchema.ToQuotedIdentifier(), null, null, extraOffset, true));
				completionItems = completionItems.Concat(GenerateSchemaObjectItems(oracleDatabaseModel, OracleDatabaseModelBase.SchemaPublic, null, null, extraOffset, true));
				completionItems = completionItems.Concat(GenerateSchemaItems(null, null, extraOffset, oracleDatabaseModel));
				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, null, null, extraOffset));
			}

			if (queryBlock != null && !isCursorAtTerminal && joinClauseNode == null && fromClause == null && !currentNode.IsWithinHavingClause() &&
				terminalCandidates.Contains(Terminals.ObjectIdentifier))
			{
				var whereTableReferences = queryBlock.ObjectReferences
					.Where(o => !String.IsNullOrEmpty(o.FullyQualifiedObjectName.ToString()))
					.Select(o => new OracleCodeCompletionItem
					             {
									 Name = o.FullyQualifiedObjectName.ToString(),
									 Category = o.Type.ToCategoryLabel(),
									 Offset = extraOffset,
									 Text = o.FullyQualifiedObjectName.ToString()
					             });

				completionItems = completionItems.Concat(whereTableReferences);
			}

			if (completionType.Column)
			{
				completionItems = completionItems.Concat(GenerateSelectListItems(currentNode, semanticModel, cursorPosition, oracleDatabaseModel));
			}

			if (completionType.DatabaseLink)
			{
				var databaseLinkItems = oracleDatabaseModel.DatabaseLinks.Values
					.Where(l => l.FullyQualifiedName.NormalizedOwner.In(OracleDatabaseModelBase.SchemaPublic, oracleDatabaseModel.CurrentSchema.ToQuotedIdentifier()) &&
					            (String.IsNullOrEmpty(completionType.TerminalValueUnderCursor) || completionType.TerminalValueUnderCursor.ToQuotedIdentifier() != l.FullyQualifiedName.NormalizedName) &&
								CodeCompletionSearchHelper.IsMatch(l.FullyQualifiedName.Name, completionType.TerminalValuePartUntilCaret))
					.Select(l => new OracleCodeCompletionItem
					             {
						             Name = l.FullyQualifiedName.Name.ToSimpleIdentifier(),
						             Text = l.FullyQualifiedName.Name.ToSimpleIdentifier(),
						             Category = OracleCodeCompletionCategory.DatabaseLink,
						             StatementNode = completionType.CurrentTerminal
					             });

				completionItems = completionItems.Concat(databaseLinkItems);
			}

			return completionItems.OrderItems().ToArray();

			// TODO: Add option to search all/current/public schemas
		}

		private IEnumerable<ICodeCompletionItem> GenerateSelectListItems(StatementGrammarNode currentNode, OracleStatementSemanticModel semanticModel, int cursorPosition, OracleDatabaseModelBase databaseModel)
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
				objectIdentifierNode = prefixedColumnReference.ChildNodes[0].GetSingleDescendant(Terminals.ObjectIdentifier);
			}

			var partialName = currentNode.Id == Terminals.Identifier && cursorPosition <= currentNode.SourcePosition.IndexEnd + 1
				? currentNode.Token.Value.Substring(0, cursorPosition - currentNode.SourcePosition.IndexStart).Trim('"')
				: null;

			var currentName = partialName == null ? null : currentNode.Token.Value;

			var functionReference = queryBlock.AllProgramReferences.SingleOrDefault(f => f.FunctionIdentifierNode == currentNode);
			var addParameterList = functionReference == null;

			var tableReferences = queryBlock.ObjectReferences;
			var suggestedFunctions = Enumerable.Empty<ICodeCompletionItem>();
			if (objectIdentifierNode != null)
			{
				var schemaIdentifier = currentNode.ParentNode.ParentNode.GetSingleDescendant(Terminals.SchemaIdentifier);
				var schemaName = schemaIdentifier == null ? null : schemaIdentifier.Token.Value;
				var objectName = objectIdentifierNode.Token.Value;
				var fullyQualifiedName = OracleObjectIdentifier.Create(schemaName, objectName);
				tableReferences = tableReferences
					.Where(t => t.FullyQualifiedObjectName == fullyQualifiedName || (String.IsNullOrEmpty(fullyQualifiedName.Owner) && fullyQualifiedName.NormalizedName == t.FullyQualifiedObjectName.NormalizedName))
					.ToArray();

				if (tableReferences.Count == 0 && (partialName != null || currentNode.SourcePosition.IndexEnd < cursorPosition))
				{
					if (String.IsNullOrEmpty(schemaName))
					{
						var packageMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(objectName).SelectOwner(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectPackage(),
							null);

						suggestedFunctions = GenerateCodeItems(m => m.Identifier.Package.ToSimpleIdentifier(), OracleCodeCompletionCategory.Package, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, packageMatcher);

						var packageFunctionMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(databaseModel.CurrentSchema).SelectOwner(), 
							new FunctionMatchElement(objectName).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());

						suggestedFunctions = GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.PackageFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, packageFunctionMatcher)
							.Concat(suggestedFunctions);

						var schemaFunctionMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(objectName).SelectOwner(),
							new FunctionMatchElement(null).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());

						suggestedFunctions = GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.SchemaFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, schemaFunctionMatcher)
							.Concat(suggestedFunctions);
					}
					else
					{
						var packageFunctionMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(schemaName).SelectOwner(),
							new FunctionMatchElement(objectName).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());

						suggestedFunctions = GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.PackageFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, packageFunctionMatcher);
					}
				}
			}
			else
			{
				var builtInFunctionMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(OracleDatabaseModelBase.SchemaSys).SelectOwner(),
						new FunctionMatchElement(OracleFunctionMetadataCollection.PackageBuiltInFunction).SelectPackage(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());

				var localSchemaFunctionMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(databaseModel.CurrentSchema.ToQuotedIdentifier()).SelectOwner(),
						new FunctionMatchElement(null).SelectPackage(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectName());

				var localSchemaPackageMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(databaseModel.CurrentSchema.ToQuotedIdentifier()).SelectOwner(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = true, DeniedValue = currentName }.SelectPackage(),
						null);

				suggestedFunctions = GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.PackageFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, builtInFunctionMatcher);

				suggestedFunctions = GenerateCodeItems(m => m.Identifier.Name.ToSimpleIdentifier(), OracleCodeCompletionCategory.SchemaFunction, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, localSchemaFunctionMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(m => m.Identifier.Package.ToSimpleIdentifier(), OracleCodeCompletionCategory.Package, partialName == null ? null : currentNode, 0, addParameterList, databaseModel, localSchemaPackageMatcher)
					.Concat(suggestedFunctions);
			}

			var columnCandidates = tableReferences
				.SelectMany(t => t.Columns
					.Where(c =>
						(currentNode.Id != Terminals.Identifier || c.Name != currentNode.Token.Value.ToQuotedIdentifier()) &&
						(objectIdentifierNode == null && String.IsNullOrEmpty(partialName) ||
						(c.Name != partialName.ToQuotedIdentifier() && c.Name.ToRawUpperInvariant().Contains(partialName.ToRawUpperInvariant()))))
					.Select(c => new { TableReference = t, Column = c }))
					.GroupBy(c => c.Column.Name).ToDictionary(g => g.Key ?? String.Empty, g => g.Select(o => o.TableReference).ToArray());

			var suggestedColumns = new List<Tuple<string, OracleObjectIdentifier>>();
			foreach (var columnCandidate in columnCandidates)
			{
				suggestedColumns.AddRange(OracleObjectIdentifier.GetUniqueReferences(columnCandidate.Value.Select(t => t.FullyQualifiedObjectName).ToArray())
					.Select(objectIdentifier => new Tuple<string, OracleObjectIdentifier>(columnCandidate.Key, objectIdentifier)));
			}

			var rowIdItems = columnCandidates.Values.SelectMany(v => v)
				.Select(o => new { o.FullyQualifiedObjectName, DataObject = o.SchemaObject.GetTargetSchemaObject() as OracleTable })
				.Distinct()
				.Where(o => o.DataObject != null && o.DataObject.Organization.In(OrganizationType.Heap, OrganizationType.Index) &&
							suggestedColumns.Select(t => t.Item2).Contains(o.FullyQualifiedObjectName))
				.Select(o => CreateColumnCodeCompletionItem(OracleColumn.RowId, objectIdentifierNode == null ? o.FullyQualifiedObjectName.ToString() : null, currentNode, OracleCodeCompletionCategory.PseudoColumn));

			var suggestedItems = rowIdItems.Concat(suggestedColumns.Select(t => CreateColumnCodeCompletionItem(t.Item1, objectIdentifierNode == null ? t.Item2.ToString() : null, currentNode)));

			if (partialName == null && currentNode.IsWithinSelectClause() && currentNode.GetParentExpression().GetParentExpression() == null)
			{
				suggestedItems = suggestedItems.Concat(CreateAsteriskColumnCompletionItems(tableReferences, objectIdentifierNode != null, currentNode));
			}

			if (objectIdentifierNode == null)
			{
				suggestedItems = suggestedItems.Concat(GenerateSchemaItems(partialName, currentNode.Id == Terminals.Select ? null : currentNode, 0, databaseModel, 1));
			}

			return suggestedItems.Concat(suggestedFunctions);
		}

		private IEnumerable<OracleCodeCompletionItem> CreateJoinTypeCompletionItems(OracleCodeCompletionType completionType)
		{
			return JoinClauseTemplates
				.Where(c => !completionType.ExistsTerminalValue || c.Name.StartsWith(completionType.TerminalValueUnderCursor.ToUpperInvariant()))
				.Select(j =>
					new OracleCodeCompletionItem
					{
						Name = j.Name,
						Text = j.Name,
						Priority = j.Priority,
						Category = OracleCodeCompletionCategory.JoinMethod,
						CategoryPriority = 1,
						StatementNode = completionType.CurrentTerminal
					});
		} 

		private IEnumerable<OracleCodeCompletionItem> CreateAsteriskColumnCompletionItems(IEnumerable<OracleDataObjectReference> tables, bool skipFirstObjectIdentifier, StatementGrammarNode currentNode)
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

					if (!skipTablePrefix && !String.IsNullOrEmpty(table.FullyQualifiedObjectName.Name))
					{
						builder.Append(table.FullyQualifiedObjectName);
						builder.Append(".");
					}
					
					builder.Append(column.Name.ToSimpleIdentifier());

					isFirstColumn = false;
					skipTablePrefix = false;
				}

				yield return new OracleCodeCompletionItem
				             {
								 Name = (skipFirstObjectIdentifier || String.IsNullOrEmpty(table.FullyQualifiedObjectName.Name) ? String.Empty : table.FullyQualifiedObjectName + ".") + "*",
								 Text = builder.ToString(),
								 StatementNode = currentNode.Id == Terminals.Identifier ? currentNode : null,
								 CategoryPriority = -2,
								 Category = OracleCodeCompletionCategory.AllColumns
				             };
			}
		}

		private ICodeCompletionItem CreateColumnCodeCompletionItem(string columnName, string objectPrefix, StatementGrammarNode currentNode, string category = OracleCodeCompletionCategory.Column)
		{
			if (!String.IsNullOrEmpty(objectPrefix))
				objectPrefix += ".";

			var text = objectPrefix + columnName.ToSimpleIdentifier();

			return new OracleCodeCompletionItem
			       {
					   Name = text,
					   Text = text,
				       StatementNode = currentNode.Id == Terminals.Identifier ? currentNode : null,
				       Category = category,
					   CategoryPriority = -1
			       };
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaItems(string schemaNamePart, StatementGrammarNode node, int insertOffset, OracleDatabaseModelBase databaseModel, int priorityOffset = 0)
		{
			return databaseModel.AllSchemas
				.Where(s => s != OracleDatabaseModelBase.SchemaPublic && (MakeSaveQuotedIdentifier(schemaNamePart) != s && CodeCompletionSearchHelper.IsMatch(s, schemaNamePart)))
				.Select(s => new OracleCodeCompletionItem
				             {
								 Name = s.ToSimpleIdentifier(),
								 Text = s.ToSimpleIdentifier(),
								 StatementNode = node,
								 Category = OracleCodeCompletionCategory.DatabaseSchema,
								 Offset = insertOffset,
								 CategoryPriority = 1 + priorityOffset
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateCodeItems(Func<OracleFunctionMetadata, string> identifierSelector, string category, StatementGrammarNode node, int insertOffset, bool addParameterList, OracleDatabaseModelBase databaseModel, params OracleFunctionMatcher[] matchers)
		{
			string parameterList = null;
			var parameterListCaretOffset = 0;
			if (addParameterList)
			{
				parameterList = "()";
				parameterListCaretOffset = -1;
			}
			
			return databaseModel.AllFunctionMetadata.SqlFunctions
				.Where(f => matchers.Any(m => m.IsMatch(f, databaseModel.CurrentSchema)) && !String.IsNullOrEmpty(identifierSelector(f)))
				.Select(f => identifierSelector(f).ToSimpleIdentifier())
				.Distinct()
				.Select(i => new OracleCodeCompletionItem
				             {
					             Name = i,
								 Text = i + (category == OracleCodeCompletionCategory.Package ? "." : parameterList),
					             StatementNode = node,
					             Category = category,
					             Offset = insertOffset,
								 CaretOffset = category == OracleCodeCompletionCategory.Package ? 0 : parameterListCaretOffset,
					             CategoryPriority = 2
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaObjectItems(OracleDatabaseModelBase databaseModel, string schemaName, string objectNamePart, StatementGrammarNode node, int insertOffset, bool dataObjectsOnly)
		{
			return databaseModel.AllObjects.Values
						.Where(o => (!dataObjectsOnly || IsDataObject(o)) &&
							o.Owner == MakeSaveQuotedIdentifier(schemaName) && MakeSaveQuotedIdentifier(objectNamePart) != o.Name &&
							(node == null || node.Token.Value.ToQuotedIdentifier() != o.Name) && CodeCompletionSearchHelper.IsMatch(o.Name, objectNamePart))
						.Select(o => new OracleCodeCompletionItem
						{
							Name = o.Name.ToSimpleIdentifier(),
							Text = o.Name.ToSimpleIdentifier(),
							Priority = String.IsNullOrEmpty(objectNamePart) || o.Name.Trim('"').ToUpperInvariant().StartsWith(objectNamePart.ToUpperInvariant()) ? 0 : 1,
							StatementNode = node,
							Category = OracleCodeCompletionCategory.SchemaObject,
							Offset = insertOffset
						});
		}

		private string MakeSaveQuotedIdentifier(string identifierPart)
		{
			if (String.IsNullOrEmpty(identifierPart) || identifierPart.All(c => c == '"'))
				return null;

			var preFix = identifierPart[0] != '"' && identifierPart[identifierPart.Length - 1] == '"' ? "\"" : null;
			var postFix = identifierPart[0] == '"' && identifierPart[identifierPart.Length - 1] != '"' ? "\"" : null;
			return String.Format("{0}{1}{2}", preFix, identifierPart, postFix).ToQuotedIdentifier();
		}

		private bool IsDataObject(OracleSchemaObject schemaObject)
		{
			return schemaObject.GetTargetSchemaObject() is OracleDataObject;
		}

		private IEnumerable<ICodeCompletionItem> GenerateCommonTableExpressionReferenceItems(OracleStatementSemanticModel model, string referenceNamePart, StatementGrammarNode node, int insertOffset)
		{
			// TODO: Make proper resolution of CTE accessibility
			return model.QueryBlocks
						.Where(qb => qb.Type == QueryBlockType.CommonTableExpression && referenceNamePart.ToQuotedIdentifier() != qb.NormalizedAlias && CodeCompletionSearchHelper.IsMatch(qb.Alias, referenceNamePart))
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

		private IEnumerable<ICodeCompletionItem> GenerateJoinConditionSuggestionItems(OracleDataObjectReference parentSchemaObject, OracleDataObjectReference joinedSchemaObject, bool skipOnTerminal, int insertOffset)
		{
			var codeItems = Enumerable.Empty<ICodeCompletionItem>();

			var parentObject = parentSchemaObject.SchemaObject.GetTargetSchemaObject() as OracleDataObject;
			var joinedObject = joinedSchemaObject.SchemaObject.GetTargetSchemaObject() as OracleDataObject;

			if (parentObject != null && joinedObject != null && (parentObject.ForeignKeys.Any() || joinedObject.ForeignKeys.Any()))
			{
				var joinedToParentKeys = parentObject.ForeignKeys.Where(k => k.TargetObject == joinedObject)
					.Select(k => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedObjectName, joinedSchemaObject.FullyQualifiedObjectName, k.Columns, k.ReferenceConstraint.Columns, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(joinedToParentKeys);

				var parentToJoinedKeys = joinedObject.ForeignKeys.Where(k => k.TargetObject == parentObject)
					.Select(k => GenerateJoinConditionSuggestionItem(joinedSchemaObject.FullyQualifiedObjectName, parentSchemaObject.FullyQualifiedObjectName, k.Columns, k.ReferenceConstraint.Columns, true, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(parentToJoinedKeys);
			}
			else
			{
				var columnNameJoinConditions = parentSchemaObject.Columns
					.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name)
					.Intersect(
						joinedSchemaObject.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name))
					.Select(c => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedObjectName, joinedSchemaObject.FullyQualifiedObjectName, new[] { c }, new[] { c }, false, skipOnTerminal, insertOffset));

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

	public static class CodeCompletionSearchHelper
	{
		public static bool IsMatch(string fullyQualifiedName, string inputPhrase)
		{
			inputPhrase = inputPhrase == null ? null : inputPhrase.Trim('"');
			return String.IsNullOrWhiteSpace(inputPhrase) ||
			       ResolveSearchPhrases(inputPhrase).All(p => fullyQualifiedName.ToUpperInvariant().Contains(p));
		}

		private static IEnumerable<string> ResolveSearchPhrases(string inputPhrase)
		{
			var builder = new StringBuilder();
			var containsSmallLetter = false;
			foreach (var character in inputPhrase)
			{
				if (containsSmallLetter && Char.IsUpper(character) && builder.Length > 0)
				{
					yield return builder.ToString().ToUpperInvariant();
					containsSmallLetter = false;
					builder.Clear();
				}
				else if (Char.IsLower(character))
				{
					containsSmallLetter = true;
				}

				builder.Append(character);
			}

			if (builder.Length > 0)
			{
				yield return builder.ToString().ToUpperInvariant();
			}
		}
	}
}
