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

		private const string JoinTypeJoin = "JOIN";
		private const string JoinTypeInnerJoin = "INNER JOIN";
		private const string JoinTypeLeftJoin = "LEFT JOIN";
		private const string JoinTypeRightJoin = "RIGHT JOIN";
		private const string JoinTypeFullJoin = "FULL JOIN";
		private const string JoinTypeCrossJoin = "CROSS JOIN";

		private static readonly ICodeCompletionItem[] EmptyCollection = new ICodeCompletionItem[0];

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
			var queryBlock = semanticModel.GetQueryBlock(cursorPosition);

			var referenceContainers = GetReferenceContainers(semanticModel.MainObjectReferenceContainer, queryBlock);
			var functionOverloadSource = ResolveFunctionOverloads(referenceContainers, node, cursorPosition);

			var functionOverloads = functionOverloadSource.Select(
				fo =>
				{
					var metadata = fo.ProgramMetadata;
					var returnParameter = metadata.Parameters.FirstOrDefault();
					return
						new FunctionOverloadDescription
						{
							Name = metadata.Identifier.FullyQualifiedIdentifier,
							Parameters = metadata.Parameters
								.Where(p => p.Direction != ParameterDirection.ReturnValue)
								.Select(p => String.Format("{0}{1}", p.Name, String.IsNullOrEmpty(p.FullDataTypeName) ? null : String.Format(": {0}", p.FullDataTypeName)))
								.ToArray(),
							CurrentParameterIndex = fo.CurrentParameterIndex,
							ReturnedDatatype = returnParameter == null ? null : returnParameter.FullDataTypeName,
							IsParameterMetadataAvailable = !String.IsNullOrEmpty(metadata.Identifier.Owner)
						};
				});
			
			return functionOverloads.ToArray();
		}

		private ICollection<OracleReferenceContainer> GetReferenceContainers(OracleReferenceContainer mainContainer, OracleQueryBlock currentQueryBlock)
		{
			var referenceContainers = new List<OracleReferenceContainer> { mainContainer };
			if (currentQueryBlock != null)
			{
				referenceContainers.Add(currentQueryBlock);
				referenceContainers.AddRange(currentQueryBlock.Columns);
			}

			return referenceContainers;
		}

		private IEnumerable<OracleCodeCompletionFunctionOverload> ResolveFunctionOverloads(IEnumerable<OracleReferenceContainer> referenceContainers, StatementGrammarNode node, int cursorPosition)
		{
			var programReferenceBase = referenceContainers.SelectMany(c => ((IEnumerable<OracleProgramReferenceBase>)c.ProgramReferences).Concat(c.TypeReferences)).Where(f => node.HasAncestor(f.ParameterListNode))
				.OrderByDescending(r => r.RootNode.Level)
				.FirstOrDefault();

			if (programReferenceBase == null || programReferenceBase.Metadata == null)
			{
				return Enumerable.Empty<OracleCodeCompletionFunctionOverload>();
			}

			var currentParameterIndex = -1;
			if (programReferenceBase.ParameterNodes != null)
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
					var parameterNode = programReferenceBase.ParameterNodes.FirstOrDefault(f => lookupNode.HasAncestor(f));
					currentParameterIndex = parameterNode == null
						? programReferenceBase.ParameterNodes.Count
						: programReferenceBase.ParameterNodes.ToList().IndexOf(parameterNode);
				}
			}

			IEnumerable<OracleProgramMetadata> matchedMetadata;
			var typeReference = programReferenceBase as OracleTypeReference;
			if (typeReference == null)
			{
				var collectionType = programReferenceBase.SchemaObject.GetTargetSchemaObject() as OracleTypeCollection;
				if (collectionType == null)
				{
					matchedMetadata = programReferenceBase.Container.SemanticModel.DatabaseModel.AllFunctionMetadata[programReferenceBase.Metadata.Identifier]
						.Where(m => m.Parameters.Count == 0 || currentParameterIndex < m.Parameters.Count - 1);
				}
				else
				{
					matchedMetadata = Enumerable.Repeat(collectionType.GetConstructorMetadata(), 1);
					currentParameterIndex = 0;
				}
			}
			else
			{
				matchedMetadata = Enumerable.Repeat(typeReference.Metadata, 1);
			}

			return matchedMetadata
				.Select(m =>
					new OracleCodeCompletionFunctionOverload
					{
						ProgramReference = programReferenceBase,
						ProgramMetadata = m,
						CurrentParameterIndex = currentParameterIndex
					});
		}

		internal ICollection<ICodeCompletionItem> ResolveItems(IDatabaseModel databaseModel, string statementText, int cursorPosition, bool forcedInvokation = true, params string[] categories)
		{
			var documentStore = new SqlDocumentRepository(_parser, new OracleStatementValidator(), databaseModel, statementText);
			var sourceItems = ResolveItems(documentStore, databaseModel, statementText, cursorPosition, forcedInvokation);
			return sourceItems.Where(i => categories.Length == 0 || categories.Contains(i.Category)).ToArray();
		}

		public ICollection<ICodeCompletionItem> ResolveItems(SqlDocumentRepository sqlDocumentRepository, IDatabaseModel databaseModel, string statementText, int cursorPosition, bool forcedInvokation)
		{
			if (sqlDocumentRepository == null || sqlDocumentRepository.Statements == null)
				return EmptyCollection;

			var completionType = new OracleCodeCompletionType(sqlDocumentRepository, statementText, cursorPosition);
			//completionType.PrintResults();

			if (completionType.InComment)
				return EmptyCollection;
			
			if (!forcedInvokation && !completionType.JoinCondition && String.IsNullOrEmpty(completionType.TerminalValuePartUntilCaret) && !completionType.IsCursorTouchingIdentifier)
				return EmptyCollection;

			StatementGrammarNode currentTerminal;

			var completionItems = Enumerable.Empty<ICodeCompletionItem>();
			var statement = (OracleStatement)sqlDocumentRepository.Statements.SingleOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);

			if (statement == null)
			{
				statement = completionType.Statement;
				if (statement == null)
				{
					return EmptyCollection;
				}

				currentTerminal = statement.GetNearestTerminalToPosition(cursorPosition);

				if (completionType.InUnparsedData || currentTerminal == null)
					return EmptyCollection;
			}
			else
			{
				currentTerminal = statement.GetNodeAtPosition(cursorPosition);
				if (currentTerminal.Type == NodeType.NonTerminal)
				{
					currentTerminal = statement.GetNearestTerminalToPosition(cursorPosition);
				}
				else if (currentTerminal.Id.In(Terminals.RightParenthesis, Terminals.Comma, Terminals.Semicolon))
				{
					var precedingNode = statement.GetNearestTerminalToPosition(cursorPosition - 1);
					if (precedingNode != null)
					{
						currentTerminal = precedingNode;
					}
				}
			}

			var oracleDatabaseModel = (OracleDatabaseModelBase)databaseModel;
			var semanticModel = (OracleStatementSemanticModel)sqlDocumentRepository.ValidationModels[statement].SemanticModel;

			var cursorAtLastTerminal = cursorPosition <= currentTerminal.SourcePosition.IndexEnd + 1;
			var terminalToReplace = completionType.ReferenceIdentifier.IdentifierUnderCursor;

			var referenceContainers = GetReferenceContainers(semanticModel.MainObjectReferenceContainer, completionType.CurrentQueryBlock);

			var extraOffset = currentTerminal.SourcePosition.IndexStart + currentTerminal.SourcePosition.Length == cursorPosition && currentTerminal.Id != Terminals.LeftParenthesis ? 1 : 0;

			if (completionType.SchemaDataObject)
			{
				var schemaName = completionType.ReferenceIdentifier.HasSchemaIdentifier
					? currentTerminal.ParentNode.FirstTerminalNode.Token.Value
					: databaseModel.CurrentSchema.ToQuotedIdentifier();

				var currentName = completionType.TerminalValuePartUntilCaret;
				completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, schemaName, currentName, terminalToReplace, insertOffset: extraOffset));

				if (!completionType.ReferenceIdentifier.HasSchemaIdentifier)
				{
					completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, OracleDatabaseModelBase.SchemaPublic, currentName, terminalToReplace, insertOffset: extraOffset));
					completionItems = completionItems.Concat(GenerateSchemaItems(currentName, terminalToReplace, extraOffset, oracleDatabaseModel));
				}

				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, currentName, terminalToReplace, extraOffset));
			}

			var joinClauseNode = currentTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
			if (joinClauseNode != null && !cursorAtLastTerminal && currentTerminal.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias, Terminals.On))
			{
				var isNotInnerJoin = joinClauseNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.InnerJoinClause) == null;
				if (isNotInnerJoin || (!joinClauseNode.FirstTerminalNode.Id.In(Terminals.Cross, Terminals.Natural)))
				{
					var joinedTableReferenceNodes = joinClauseNode.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.JoinClause, NonTerminals.NestedQuery), NonTerminals.TableReference).ToArray();
					if (joinedTableReferenceNodes.Length == 1)
					{
						var joinedTableReference = completionType.CurrentQueryBlock.ObjectReferences.SingleOrDefault(t => t.RootNode == joinedTableReferenceNodes[0]);
						if (joinedTableReference != null)
						{
							foreach (var parentTableReference in completionType.CurrentQueryBlock.ObjectReferences
								.Where(t => t.RootNode.SourcePosition.IndexStart < joinedTableReference.RootNode.SourcePosition.IndexStart &&
								            (t.Type != ReferenceType.InlineView || t.AliasNode != null)))
							{
								var joinSuggestions = GenerateJoinConditionSuggestionItems(parentTableReference, joinedTableReference, completionType, extraOffset);
								completionItems = completionItems.Concat(joinSuggestions);
							}
						}
					}
				}
			}

			if (completionType.JoinType)
			{
				completionItems = completionItems.Concat(CreateJoinTypeCompletionItems(completionType));
			}

			if (currentTerminal.Id == Terminals.Join ||
				(currentTerminal.Id == Terminals.ObjectAlias && currentTerminal.Token.Value.ToUpperInvariant() == Terminals.Join.ToUpperInvariant()))
			{
				completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, databaseModel.CurrentSchema.ToQuotedIdentifier(), null, null, insertOffset: extraOffset));
				completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, OracleDatabaseModelBase.SchemaPublic, null, null, insertOffset: extraOffset));
				completionItems = completionItems.Concat(GenerateSchemaItems(null, null, extraOffset, oracleDatabaseModel));
				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, null, null, extraOffset));
			}

			if (completionType.Column || completionType.SpecialFunctionParameter)
			{
				if (completionType.Column)
				{
					completionItems = completionItems.Concat(GenerateSelectListItems(referenceContainers, cursorPosition, oracleDatabaseModel, completionType, forcedInvokation));
				}

				var functionOverloads = ResolveFunctionOverloads(referenceContainers, currentTerminal, cursorPosition);
				var specificFunctionParameterCodeCompletionItems = CodeCompletionSearchHelper.ResolveSpecificFunctionParameterCodeCompletionItems(currentTerminal, functionOverloads, oracleDatabaseModel);
				completionItems = completionItems.Concat(specificFunctionParameterCodeCompletionItems);
			}

			if (completionType.ColumnAlias)
			{
				completionItems = completionItems.Concat(GenerateColumnAliases(terminalToReplace, completionType));
			}

			if (completionType.UpdateSetColumn && semanticModel.MainObjectReferenceContainer.MainObjectReference != null)
			{
				completionItems = completionItems.Concat(GenerateUpdateSetColumnItems(currentTerminal, semanticModel.MainObjectReferenceContainer.MainObjectReference, completionType));
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

			completionItems = completionItems.Concat(GenerateKeywordItems(completionType));

			return completionItems.OrderItems().ToArray();

			// TODO: Add option to search all/current/public schemas
		}

		private IEnumerable<ICodeCompletionItem> GenerateKeywordItems(OracleCodeCompletionType completionType)
		{
			return completionType.KeywordsClauses
				.Where(t => !String.Equals(t.TerminalId, completionType.TerminalValueUnderCursor, StringComparison.InvariantCultureIgnoreCase))
				.Where(t => CodeCompletionSearchHelper.IsMatch(t.Text, completionType.TerminalValuePartUntilCaret))
				.Select(t =>
					new OracleCodeCompletionItem
					{
						Name = t.Text,
						Text = t.Text,
						Category = OracleCodeCompletionCategory.Keyword,
						StatementNode = completionType.CurrentTerminal,
						CategoryPriority = 1
					});
		}

		private IEnumerable<ICodeCompletionItem> GenerateColumnAliases(StatementGrammarNode currentTerminal, OracleCodeCompletionType completionType)
		{
			return completionType.CurrentQueryBlock.Columns
				.Where(c => c.HasExplicitAlias)
				.Select(c =>
					new OracleCodeCompletionItem
					{
						Name = c.NormalizedName.ToSimpleIdentifier(),
						Text = c.NormalizedName.ToSimpleIdentifier(),
						Category = OracleCodeCompletionCategory.Column,
						StatementNode = currentTerminal
					});
		}

		private IEnumerable<ICodeCompletionItem> GenerateUpdateSetColumnItems(StatementGrammarNode currentTerminal, OracleDataObjectReference targetDataObject, OracleCodeCompletionType completionType)
		{
			return targetDataObject.Columns
				.Where(c => completionType.TerminalValueUnderCursor.ToQuotedIdentifier() != c.Name && CodeCompletionSearchHelper.IsMatch(c.Name, completionType.TerminalValuePartUntilCaret))
				.Select(c => new OracleCodeCompletionItem
				{
					Name = c.Name.ToSimpleIdentifier(),
					Text = c.Name.ToSimpleIdentifier(),
					Category = OracleCodeCompletionCategory.Column,
					StatementNode = currentTerminal
				});
		}

		private IEnumerable<ICodeCompletionItem> GenerateSelectListItems(IEnumerable<OracleReferenceContainer> referenceContainers, int cursorPosition, OracleDatabaseModelBase databaseModel, OracleCodeCompletionType completionType, bool forcedInvokation)
		{
			var currentNode = completionType.EffectiveTerminal;
			
			var prefixedColumnReference = currentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);
			var columnIdentifierFollowing = !completionType.IsNewExpressionWithInvalidGrammar && currentNode.Id != Terminals.Identifier && prefixedColumnReference != null && prefixedColumnReference.GetDescendants(Terminals.Identifier).FirstOrDefault() != null;
			if (columnIdentifierFollowing || currentNode.Id.IsLiteral())
			{
				return EmptyCollection;
			}

			var programReferences = referenceContainers.SelectMany(c => c.ProgramReferences);

			var objectIdentifierNode = completionType.ReferenceIdentifier.ObjectIdentifier;
			var partialName = completionType.ReferenceIdentifier.IdentifierEffectiveValue;
			var currentName = completionType.ReferenceIdentifier.IdentifierOriginalValue;
			var nodeToReplace = completionType.ReferenceIdentifier.IdentifierUnderCursor;
			var schemaName = completionType.ReferenceIdentifier.SchemaIdentifierOriginalValue;

			var functionReference = programReferences.SingleOrDefault(f => f.FunctionIdentifierNode == currentNode);
			var addParameterList = functionReference == null || functionReference.ParameterListNode == null;

			var tableReferences = (ICollection<OracleDataObjectReference>)referenceContainers.SelectMany(c => c.ObjectReferences).ToArray();
			var suggestedFunctions = Enumerable.Empty<ICodeCompletionItem>();
			if (objectIdentifierNode != null)
			{
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
							new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectPackage().AsResultValue(),
							null);

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.Package, nodeToReplace, 0, addParameterList, databaseModel, packageMatcher);

						var packageFunctionMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(databaseModel.CurrentSchema).SelectOwner(), 
							new FunctionMatchElement(objectName).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue());

						var publicSynonymPackageFunctionMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(OracleDatabaseModelBase.SchemaPublic).SelectSynonymOwner(),
							new FunctionMatchElement(objectName).SelectSynonymPackage(),
							new FunctionMatchElement(partialName) {AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName}.SelectSynonymName().AsResultValue());

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.PackageFunction, nodeToReplace, 0, addParameterList, databaseModel, packageFunctionMatcher, publicSynonymPackageFunctionMatcher)
							.Concat(suggestedFunctions);

						var schemaFunctionMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(objectName).SelectOwner(),
							new FunctionMatchElement(null).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue());

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.SchemaFunction, nodeToReplace, 0, addParameterList, databaseModel, schemaFunctionMatcher)
							.Concat(suggestedFunctions);
					}
					else
					{
						var packageFunctionMatcher = new OracleFunctionMatcher(
							new FunctionMatchElement(schemaName).SelectOwner(),
							new FunctionMatchElement(objectName).SelectPackage(),
							new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue());

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.PackageFunction, nodeToReplace, 0, addParameterList, databaseModel, packageFunctionMatcher);
					}
				}
			}
			else
			{
				var builtInPackageFunctionMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(OracleDatabaseModelBase.SchemaSys).SelectOwner(),
						new FunctionMatchElement(OracleDatabaseModelBase.PackageBuiltInFunction).SelectPackage(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue());

				var builtInNonSchemaFunctionMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(null).SelectOwner(),
						new FunctionMatchElement(null).SelectPackage(),
						new FunctionMatchElement(partialName) {AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName}.SelectName().AsResultValue());

				var currentSchema = databaseModel.CurrentSchema.ToQuotedIdentifier();
				var localSchemaFunctionMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(currentSchema).SelectOwner(),
						new FunctionMatchElement(null).SelectPackage(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue());

				var localSynonymFunctionMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(currentSchema).SelectSynonymOwner(),
						new FunctionMatchElement(null).SelectPackage(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymName().AsResultValue());

				var publicSynonymFunctionMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(OracleDatabaseModelBase.SchemaPublic).SelectSynonymOwner(),
						new FunctionMatchElement(null).SelectPackage(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymName().AsResultValue());

				var localSchemaPackageMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(currentSchema).SelectOwner(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectPackage().AsResultValue(),
						null);

				var localSynonymPackageMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(currentSchema).SelectSynonymOwner(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymPackage().AsResultValue(),
						null);

				var publicSynonymPackageMatcher =
					new OracleFunctionMatcher(
						new FunctionMatchElement(OracleDatabaseModelBase.SchemaPublic).SelectSynonymOwner(),
						new FunctionMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymPackage().AsResultValue(),
						null);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.PackageFunction, nodeToReplace, 0, addParameterList, databaseModel, builtInPackageFunctionMatcher);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.BuiltInFunction, nodeToReplace, 0, addParameterList, databaseModel, builtInNonSchemaFunctionMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.SchemaFunction, nodeToReplace, 0, addParameterList, databaseModel, localSchemaFunctionMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.SchemaFunction, nodeToReplace, 0, addParameterList, databaseModel, localSynonymFunctionMatcher, publicSynonymFunctionMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.Package, nodeToReplace, 0, addParameterList, databaseModel, localSchemaPackageMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.Package, nodeToReplace, 0, addParameterList, databaseModel, localSynonymPackageMatcher, publicSynonymPackageMatcher)
					.Concat(suggestedFunctions);
			}

			var columnCandidates = tableReferences
				.SelectMany(t => t.Columns
					.Where(c =>
						(currentNode.Id != Terminals.Identifier || c.Name != currentNode.Token.Value.ToQuotedIdentifier()) &&
						(objectIdentifierNode == null && String.IsNullOrEmpty(partialName) ||
						(c.Name != partialName.ToQuotedIdentifier() && CodeCompletionSearchHelper.IsMatch(c.Name, partialName))))
					.Select(c => new { TableReference = t, Column = c }))
					.GroupBy(c => c.Column.Name)
					.ToDictionary(g => g.Key ?? String.Empty, g => g.Select(o => o.TableReference).ToArray());

			var suggestedColumns = new List<Tuple<string, OracleObjectIdentifier>>();
			foreach (var columnCandidate in columnCandidates)
			{
				suggestedColumns.AddRange(OracleObjectIdentifier.GetUniqueReferences(columnCandidate.Value.Select(t => t.FullyQualifiedObjectName).ToArray())
					.Select(objectIdentifier => new Tuple<string, OracleObjectIdentifier>(columnCandidate.Key, objectIdentifier)));
			}

			var suggestedItems = suggestedColumns.Select(t => CreateColumnCodeCompletionItem(t.Item1, objectIdentifierNode == null ? t.Item2.ToString() : null, nodeToReplace));

			if (CodeCompletionSearchHelper.IsMatch(OracleColumn.RowId, partialName))
			{
				var rowIdItems = tableReferences.Select(r => new { r.FullyQualifiedObjectName, DataObject = r.SchemaObject.GetTargetSchemaObject() as OracleTable })
					.Where(t => t.DataObject != null && t.DataObject.Organization.In(OrganizationType.Heap, OrganizationType.Index))
					.Distinct()
					.Select(t => CreateColumnCodeCompletionItem(OracleColumn.RowId, objectIdentifierNode == null ? t.FullyQualifiedObjectName.ToString() : null, nodeToReplace, OracleCodeCompletionCategory.PseudoColumn));
				suggestedItems = suggestedItems.Concat(rowIdItems);
			}

			if (partialName == null && currentNode.IsWithinSelectClause() && currentNode.GetParentExpression().GetParentExpression() == null)
			{
				suggestedItems = suggestedItems.Concat(CreateAsteriskColumnCompletionItems(tableReferences, objectIdentifierNode != null, currentNode));
			}

			if (objectIdentifierNode == null)
			{
				var queryBlockReferencedObjects = tableReferences.Where(r => !String.IsNullOrEmpty(r.FullyQualifiedObjectName.ToString())).ToArray();
				var referencedObjectCompletionData = queryBlockReferencedObjects
					.Select(r =>
						new ObjectReferenceCompletionData
						{
							Identifier1 = r.FullyQualifiedObjectName.Owner,
							Identifier2 = r.FullyQualifiedObjectName.NormalizedName,
							SchemaObject = r.SchemaObject,
							Category = r.Type.ToCategoryLabel()
						});

				suggestedItems = suggestedItems.Concat(CreateObjectItems(referencedObjectCompletionData, partialName, nodeToReplace));
				suggestedItems = suggestedItems.Concat(GenerateSchemaItems(partialName, nodeToReplace, 0, databaseModel, 2));

				var otherSchemaObjectItems = GenerateSchemaObjectItems(databaseModel, null, partialName, nodeToReplace, o => FilterOtherSchemaObject(o, completionType.Sequence), categoryOffset: 1);
				suggestedItems = suggestedItems.Concat(otherSchemaObjectItems);

				if (partialName != null && currentNode.IsWithinSelectClause() && currentNode.GetParentExpression().GetParentExpression() == null)
				{
					var matchedqueryBlockReferencedObjects = queryBlockReferencedObjects.Where(r => CodeCompletionSearchHelper.IsMatch(r.FullyQualifiedObjectName.Name, partialName));
					suggestedItems = suggestedItems.Concat(CreateAsteriskColumnCompletionItems(matchedqueryBlockReferencedObjects, false, currentNode));
				}
			}
			else if (String.IsNullOrEmpty(schemaName))
			{
				var objectName = objectIdentifierNode.Token.Value;
				var otherSchemaObjectItems = GenerateSchemaObjectItems(databaseModel, objectName, partialName, nodeToReplace, o => FilterOtherSchemaObject(o, completionType.Sequence), categoryOffset: 1);
				suggestedItems = suggestedItems.Concat(otherSchemaObjectItems);
			}

			return suggestedItems.Concat(suggestedFunctions);
		}

		private bool FilterOtherSchemaObject(OracleSchemaObject schemaObject, bool sequencesAllowed)
		{
			var targetObject = schemaObject.GetTargetSchemaObject();
			return targetObject != null && (targetObject.Type == OracleSchemaObjectType.Type || (sequencesAllowed && targetObject.Type == OracleSchemaObjectType.Sequence));
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

		private IEnumerable<OracleCodeCompletionItem> CreateAsteriskColumnCompletionItems(IEnumerable<OracleDataObjectReference> tables, bool skipFirstObjectIdentifier, StatementGrammarNode nodeToReplace)
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
								 StatementNode = nodeToReplace,
								 CategoryPriority = -2,
								 Category = OracleCodeCompletionCategory.AllColumns
				             };
			}
		}

		private ICodeCompletionItem CreateColumnCodeCompletionItem(string columnName, string objectPrefix, StatementGrammarNode nodeToReplace, string category = OracleCodeCompletionCategory.Column)
		{
			if (!String.IsNullOrEmpty(objectPrefix))
				objectPrefix += ".";

			var text = objectPrefix + columnName.ToSimpleIdentifier();

			return new OracleCodeCompletionItem
			       {
					   Name = text,
					   Text = text,
				       StatementNode = nodeToReplace,
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
								 InsertOffset = insertOffset,
								 CategoryPriority = 1 + priorityOffset
				             });
		}

		private IEnumerable<ICodeCompletionItem> GenerateCodeItems(string category, StatementGrammarNode node, int insertOffset, bool addParameterList, OracleDatabaseModelBase databaseModel, params OracleFunctionMatcher[] matchers)
		{
			string parameterList = null;
			var parameterListCaretOffset = 0;
			if (addParameterList)
			{
				parameterList = "()";
				parameterListCaretOffset = -1;
			}

			var quotedSchemaName = databaseModel.CurrentSchema.ToQuotedIdentifier();

			return databaseModel.AllFunctionMetadata
				.SelectMany(g => g)
				.SelectMany(f => matchers.Select(m => m.GetMatchResult(f, quotedSchemaName)))
				.Where(r => r.IsMatched)
				.SelectMany(r => r.Matches.Where(v => !String.IsNullOrEmpty(v)).Select(v => new { Name = v.ToSimpleIdentifier(), r.Metadata }))
				.GroupBy(r => r.Name)
				.Select(g => new { Name = g.Key, g.First().Metadata })
				.Select(i =>
				{
					var hasReservedWordName = i.Metadata.IsBuiltIn && i.Name.CollidesWithReservedWord();
					var functionName = hasReservedWordName
						? i.Name.Trim('"')
						: i.Name;

					var postFix = parameterList;
					if (category == OracleCodeCompletionCategory.Package)
					{
						postFix = ".";
					}
					else if (i.Metadata.DisplayType == OracleProgramMetadata.DisplayTypeNoParenthesis || hasReservedWordName)
					{
						postFix = null;
					}
					
					var analyticClause = addParameterList
						? GetAdditionalFunctionClause(i.Metadata)
						: String.Empty;

					return
						new OracleCodeCompletionItem
						{
							Name = functionName,
							Text = String.Format("{0}{1}{2}", functionName, postFix, analyticClause),
							StatementNode = node,
							Category = category,
							InsertOffset = insertOffset,
							CaretOffset = hasReservedWordName || category == OracleCodeCompletionCategory.Package || i.Metadata.DisplayType == OracleProgramMetadata.DisplayTypeNoParenthesis
								? 0
								: (parameterListCaretOffset - analyticClause.Length),
							CategoryPriority = 2
						};
				});
		}

		private string GetAdditionalFunctionClause(OracleProgramMetadata metadata)
		{
			var orderByClause = metadata.IsBuiltIn && metadata.Identifier.Name.In("\"NTILE\"", "\"ROW_NUMBER\"", "\"RANK\"", "\"DENSE_RANK\"", "\"LEAD\"", "\"LAG\"")
				? "ORDER BY NULL"
				: String.Empty;

			if (metadata.IsBuiltIn && metadata.Identifier.Name == "\"LISTAGG\"")
			{
				return " WITHIN GROUP (ORDER BY NULL)";
			}

			if (!metadata.IsAggregate && metadata.IsAnalytic)
			{
				return String.Format(" OVER ({0})", orderByClause);
			}

			return String.Empty;
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaDataObjectItems(OracleDatabaseModelBase databaseModel, string schemaName, string objectNamePart, StatementGrammarNode node, int categoryOffset = 0, int insertOffset = 0)
		{
			return GenerateSchemaObjectItems(databaseModel, schemaName, objectNamePart, node, IsDataObject, categoryOffset, insertOffset);
		}

		private IEnumerable<ICodeCompletionItem> GenerateSchemaObjectItems(OracleDatabaseModelBase databaseModel, string schemaName, string objectNamePart, StatementGrammarNode node, Func<OracleSchemaObject, bool> filter, int categoryOffset = 0, int insertOffset = 0)
		{
			var activeSchema = databaseModel.CurrentSchema.ToQuotedIdentifier();
			var dataObjects = databaseModel.AllObjects.Values
				.Where(o => (filter == null || filter(o)) && FilterSchema(o, activeSchema, schemaName))
				.Select(o => new ObjectReferenceCompletionData { Identifier2 = o.Name, SchemaObject = o, Category = OracleCodeCompletionCategory.SchemaObject });
			return CreateObjectItems(dataObjects, objectNamePart, node, categoryOffset, insertOffset);
		}

		private bool FilterSchema(OracleSchemaObject schemaObject, string activeSchema, string schemaName)
		{
			var safeIdentifier = MakeSaveQuotedIdentifier(schemaName);
			return String.IsNullOrEmpty(safeIdentifier)
				? schemaObject.Owner.In(activeSchema, OracleDatabaseModelBase.SchemaPublic)
				: schemaObject.Owner == safeIdentifier;
		}

		private IEnumerable<ICodeCompletionItem> CreateObjectItems(IEnumerable<ObjectReferenceCompletionData> objects, string objectNamePart, StatementGrammarNode node, int categoryOffset = 0, int insertOffset = 0)
		{
			return objects
				.Where(o => MakeSaveQuotedIdentifier(objectNamePart) != o.Identifier2 &&
							(node == null || node.Token.Value.ToQuotedIdentifier() != o.Identifier2) && CodeCompletionSearchHelper.IsMatch(o.Identifier2, objectNamePart))
				.Select(o =>
					new OracleCodeCompletionItem
					{
						Name = o.CompletionText,
						Text = o.CompletionText + o.TextPostFix,
						Priority = String.IsNullOrEmpty(objectNamePart) || o.CompletionText.TrimStart('"').ToUpperInvariant().StartsWith(objectNamePart.ToUpperInvariant()) ? 0 : 1,
						StatementNode = node,
						Category = o.Category,
						InsertOffset = insertOffset,
						CaretOffset = o.CaretOffset,
						CategoryPriority = categoryOffset
					});
		}

		private class ObjectReferenceCompletionData
		{
			public OracleSchemaObject SchemaObject { get; set; }

			public string Identifier1 { get; set; }
			
			public string Identifier2 { get; set; }

			public string CompletionText { get { return OracleObjectIdentifier.MergeIdentifiersIntoSimpleString(Identifier1, Identifier2); } }
			
			public string Category { get; set; }

			public int CaretOffset
			{
				get { return IsSchemaType() ? -1 : 0; }
			}

			public string TextPostFix
			{
				get { return IsSchemaType() ? "()" : null; }
			}

			private bool IsSchemaType()
			{
				var targetObject = SchemaObject.GetTargetSchemaObject();
				return targetObject != null && targetObject.Type == OracleSchemaObjectType.Type;
			}
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
							InsertOffset = insertOffset,
							CategoryPriority = -1
						});
		}

		private IEnumerable<ICodeCompletionItem> GenerateJoinConditionSuggestionItems(OracleDataObjectReference parentSchemaObject, OracleDataObjectReference joinedSchemaObject, OracleCodeCompletionType completionType, int insertOffset)
		{
			var codeItems = Enumerable.Empty<ICodeCompletionItem>();
			var skipOnTerminal = completionType.EffectiveTerminal.Id == Terminals.On;

			var parentObject = parentSchemaObject.SchemaObject.GetTargetSchemaObject() as OracleDataObject;
			var joinedObject = joinedSchemaObject.SchemaObject.GetTargetSchemaObject() as OracleDataObject;

			var effectiveJoinedObjectIdentifier = GetJoinedObjectIdentifier(joinedSchemaObject, completionType.CursorPosition);
			if (parentObject != null && joinedObject != null && (parentObject.ForeignKeys.Any() || joinedObject.ForeignKeys.Any()))
			{
				var joinedToParentKeys = parentObject.ForeignKeys.Where(k => k.TargetObject == joinedObject)
					.Select(k => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedObjectName, effectiveJoinedObjectIdentifier, k.Columns, k.ReferenceConstraint.Columns, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(joinedToParentKeys);

				var parentToJoinedKeys = joinedObject.ForeignKeys.Where(k => k.TargetObject == parentObject)
					.Select(k => GenerateJoinConditionSuggestionItem(effectiveJoinedObjectIdentifier, parentSchemaObject.FullyQualifiedObjectName, k.Columns, k.ReferenceConstraint.Columns, true, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(parentToJoinedKeys);
			}
			else
			{
				var columnNameJoinConditions = parentSchemaObject.Columns
					.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name)
					.Intersect(
						joinedSchemaObject.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name))
					.Select(c => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedObjectName, effectiveJoinedObjectIdentifier, new[] { c }, new[] { c }, false, skipOnTerminal, insertOffset));

				codeItems = codeItems.Concat(columnNameJoinConditions);
			}

			return codeItems;
		}

		private OracleObjectIdentifier GetJoinedObjectIdentifier(OracleDataObjectReference objectReference, int cursorPosition)
		{
			return objectReference.AliasNode == null || objectReference.AliasNode.SourcePosition.IndexEnd < cursorPosition
				? objectReference.FullyQualifiedObjectName
				: OracleObjectIdentifier.Create(objectReference.OwnerNode, objectReference.ObjectNode, null);
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

			return
				new OracleCodeCompletionItem
				{
					Name = builder.ToString(),
					Text = builder.ToString(),
					InsertOffset = insertOffset,
					Category = OracleCodeCompletionCategory.JoinCondition
				};
		}
	}

	internal class OracleCodeCompletionFunctionOverload
	{
		public OracleProgramReferenceBase ProgramReference { get; set; }

		public OracleProgramMetadata ProgramMetadata { get; set; }

		public int CurrentParameterIndex { get; set; }
	}
}
