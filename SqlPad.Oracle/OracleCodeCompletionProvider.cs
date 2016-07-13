using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	public class OracleCodeCompletionProvider : ICodeCompletionProvider
	{
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

		private static OracleConfigurationFormatterFormatOptions FormatOptions => OracleConfiguration.Configuration.Formatter.FormatOptions;

		public IReadOnlyCollection<ProgramOverloadDescription> ResolveProgramOverloads(SqlDocumentRepository documentRepository, int cursorPosition)
		{
			var emptyCollection = new ProgramOverloadDescription[0];
			var node = documentRepository.Statements.GetNodeAtPosition(cursorPosition);
			if (node == null)
			{
				return emptyCollection;
			}

			var semanticModel = (OracleStatementSemanticModel)documentRepository.ValidationModels[node.Statement].SemanticModel;
			var programOverloadSource = ResolveProgramOverloads(semanticModel.AllReferenceContainers, node, cursorPosition);
			var programOverloads = programOverloadSource
				.Select(
					o =>
					{
						var metadata = o.ProgramMetadata;
						var returnParameter = metadata.ReturnParameter;
						var displayParameterDirection = !metadata.IsBuiltIn || !String.IsNullOrEmpty(metadata.Identifier.Package);
						var parameters = metadata.Parameters
							.Where(p => p.Direction != ParameterDirection.ReturnValue && p.DataLevel == 0)
							.Select(p => BuildParameterLabel(p, displayParameterDirection))
							.ToArray();

						return
							new ProgramOverloadDescription
							{
								Name = metadata.Identifier.FullyQualifiedIdentifier,
								Parameters = parameters,
								CurrentParameterIndex = o.CurrentParameterIndex,
								ReturnedDatatype = returnParameter?.FullDataTypeName
							};
					})
				.ToArray();

			return FindProgramReference(semanticModel.AllReferenceContainers, node) != null
				? programOverloads
				: ResolveInsertValuesDataTypes(semanticModel, node, cursorPosition).ToArray();
		}

		private static IEnumerable<ProgramOverloadDescription> ResolveInsertValuesDataTypes(OracleStatementSemanticModel semanticModel, StatementGrammarNode node, int cursorPosition)
		{
			var insertTarget = semanticModel.InsertTargets.SingleOrDefault(t => t.ValueList != null && node.HasAncestor(t.ValueList));
			if (insertTarget == null || insertTarget.Columns.Count == 0 || node == insertTarget.ValueList.FirstTerminalNode || insertTarget.ValueList.LastTerminalNode.SourcePosition.IndexStart < cursorPosition)
			{
				yield break;
			}

			var columnLookup = insertTarget.DataObjectReference.Columns.ToLookup(c => c.Name);
			var currentParameterIndex = insertTarget.ValueExpressions.TakeWhile(n => !node.HasAncestor(n) && n.FollowingTerminal.SourcePosition.IndexStart < cursorPosition).Count();

			yield return
				new ProgramOverloadDescription
				{
					CurrentParameterIndex = currentParameterIndex,
					Name = insertTarget.DataObjectReference.FullyQualifiedObjectName.ToLabel(),
					Parameters = insertTarget.Columns.Values.Select(name => BuildInsertColumnLabel(name, columnLookup[name].SingleOrDefault())).ToArray()
				};
		}

		private static string BuildInsertColumnLabel(string name, OracleColumn column)
		{
			var columnLabel = BuildNameDataTypeLabel(name, null, column?.FullTypeName);
			var nullability = column == null || column.Nullable ? "NULL" : "NOT NULL";
			return $"{columnLabel} {nullability}";
		}

		private static string BuildNameDataTypeLabel(string name, ParameterDirection? parameterDirection, string dataTypeLabel)
		{
			string parameterName;
			string dataType;
			if (String.IsNullOrEmpty(name))
			{
				parameterName = dataTypeLabel;
				dataType = null;
			}
			else
			{
				parameterName = name.ToSimpleIdentifier();
				dataType = dataTypeLabel;
			}

			var isPartialMetadata = String.IsNullOrEmpty(dataTypeLabel) || dataType == null;
			var parameterLabel = $"{parameterName}{GetParameterDirectionPostfix(parameterDirection)}{(isPartialMetadata ? null : ": ")}{dataType}";

			return parameterLabel;
		}

		private static string GetParameterDirectionPostfix(ParameterDirection? parameterDirection)
		{
			switch (parameterDirection)
			{
				case ParameterDirection.Output:
					return " (OUT)";
				case ParameterDirection.InputOutput:
					return " (IN/OUT)";
				default:
					return null;
			}
		}

		private static string BuildParameterLabel(OracleProgramParameterMetadata parameterMetadata, bool displayParameterDirection)
		{
			var parameterLabel = BuildNameDataTypeLabel(parameterMetadata.Name, displayParameterDirection ? parameterMetadata.Direction : (ParameterDirection?)null, parameterMetadata.FullDataTypeName);
			if (parameterMetadata.IsOptional)
			{
				parameterLabel = $"[{parameterLabel}]";
			}

			return parameterLabel;
		}

		private static ICollection<OracleReferenceContainer> GetReferenceContainers(OracleReferenceContainer mainContainer, OracleQueryBlock currentQueryBlock)
		{
			var referenceContainers = new List<OracleReferenceContainer> { mainContainer };
			if (currentQueryBlock != null)
			{
				referenceContainers.Add(currentQueryBlock);
				referenceContainers.AddRange(currentQueryBlock.Columns);

				if (currentQueryBlock.OuterCorrelatedQueryBlock != null)
				{
					referenceContainers.Add(currentQueryBlock.OuterCorrelatedQueryBlock);
				}
			}

			return referenceContainers;
		}

		private static IEnumerable<OracleCodeCompletionFunctionOverload> ResolveProgramOverloads(IEnumerable<OracleReferenceContainer> referenceContainers, StatementGrammarNode node, int cursorPosition)
		{
			var programReferenceBase = FindProgramReference(referenceContainers, node);
			if (programReferenceBase?.Metadata == null)
			{
				return Enumerable.Empty<OracleCodeCompletionFunctionOverload>();
			}

			var currentParameterIndex = -1;
			if (programReferenceBase.ParameterReferences != null)
			{
				var lookupNode = node.Type == NodeType.Terminal ? node : node.GetNearestTerminalToPosition(cursorPosition);
				if (String.Equals(lookupNode.Id, Terminals.Comma))
				{
					lookupNode = cursorPosition == lookupNode.SourcePosition.IndexStart ? lookupNode.PrecedingTerminal : lookupNode.FollowingTerminal;
				}
				else if (String.Equals(lookupNode.Id, Terminals.LeftParenthesis) && cursorPosition > lookupNode.SourcePosition.IndexStart)
				{
					lookupNode = lookupNode.FollowingTerminal;
				}
				else if (String.Equals(lookupNode.Id, Terminals.RightParenthesis) && cursorPosition == lookupNode.SourcePosition.IndexStart)
				{
					lookupNode = lookupNode.PrecedingTerminal;
				}

				if (lookupNode != null)
				{
					var parameterReference = programReferenceBase.ParameterReferences.FirstOrDefault(f => lookupNode.HasAncestor(f.ParameterNode, true));
					currentParameterIndex = parameterReference.ParameterNode == null
						? programReferenceBase.ParameterReferences.Count
						: programReferenceBase.ParameterReferences.ToList().IndexOf(parameterReference);
				}
			}

			var matchedMetadata = new List<OracleProgramMetadata>();
			var typeReference = programReferenceBase as OracleTypeReference;
			if (typeReference == null)
			{
				var programReference = (OracleProgramReference)programReferenceBase;
				var metadataSource = programReference.Container.SemanticModel.DatabaseModel.AllProgramMetadata[programReference.Metadata.Identifier].ToList();
				if (metadataSource.Count == 0 && programReference.Owner != null && programReference.ObjectNode == null && programReference.OwnerNode == null)
				{
					metadataSource.AddRange(programReference.Owner.AccessibleAttachedFunctions.Where(m => String.Equals(m.Identifier.Name, programReference.Metadata.Identifier.Name)));
				}

				var plSqlProgram = programReference.Container as OraclePlSqlProgram;
				if (plSqlProgram != null)
				{
					metadataSource.AddRange(plSqlProgram.SubPrograms.Where(p => String.Equals(p.Name, programReference.Metadata.Identifier.Name)).Select(p => p.Metadata));
				}

				matchedMetadata.AddRange(metadataSource.Where(m => IsMetadataMatched(m, programReference, currentParameterIndex)).OrderBy(m => m.Parameters.Count));
			}
			else
			{
				matchedMetadata.Add(typeReference.Metadata);
				var collectionType = typeReference.SchemaObject.GetTargetSchemaObject() as OracleTypeCollection;
				if (collectionType != null)
				{
					currentParameterIndex = 0;
				}
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

		private static OracleProgramReferenceBase FindProgramReference(IEnumerable<OracleReferenceContainer> referenceContainers, StatementGrammarNode node)
		{
			return referenceContainers
				.SelectMany(c => ((IEnumerable<OracleProgramReferenceBase>)c.ProgramReferences).Concat(c.TypeReferences))
				.Where(f => node.HasAncestor(f.ParameterListNode))
				.OrderByDescending(r => r.RootNode.Level)
				.FirstOrDefault();
		}

		private static bool IsMetadataMatched(OracleProgramMetadata metadata, OracleProgramReference programReference, int currentParameterIndex)
		{
			var isParameterlessCompatible = currentParameterIndex == 0 && metadata.NamedParameters.Count == 0;
			if (!isParameterlessCompatible && metadata.Parameters.Count > 0 && metadata.Parameters[0].Direction == ParameterDirection.ReturnValue && currentParameterIndex >= metadata.NamedParameters.Count)
			{
				return false;
			}

			var isNotAnalyticCompatible = !metadata.IsAnalytic || !String.IsNullOrEmpty(metadata.Identifier.Owner);
			return (programReference.AnalyticClauseNode == null && isNotAnalyticCompatible) ||
			       (programReference.AnalyticClauseNode != null && metadata.IsAnalytic);
		}

		internal IReadOnlyCollection<ICodeCompletionItem> ResolveItems(IDatabaseModel databaseModel, string statementText, int cursorPosition, bool forcedInvokation = true, params string[] categories)
		{
			var documentStore = new SqlDocumentRepository(OracleSqlParser.Instance, new OracleStatementValidator(), databaseModel, statementText);
			var sourceItems = ResolveItems(documentStore, databaseModel, cursorPosition, forcedInvokation);
			return sourceItems.Where(i => categories.Length == 0 || categories.Contains(i.Category)).ToArray();
		}

		public IReadOnlyCollection<ICodeCompletionItem> ResolveItems(SqlDocumentRepository sqlDocumentRepository, IDatabaseModel databaseModel, int cursorPosition, bool forcedInvokation)
		{
			if (sqlDocumentRepository?.Statements == null)
			{
				return EmptyCollection;
			}

			var completionType = new OracleCodeCompletionType(sqlDocumentRepository, sqlDocumentRepository.StatementText, cursorPosition);
			//completionType.PrintResults();

			if (completionType.InComment)
			{
				return EmptyCollection;
			}

			if (!forcedInvokation && !completionType.JoinCondition && String.IsNullOrEmpty(completionType.TerminalValuePartUntilCaret) && !completionType.IsCursorTouchingIdentifier)
			{
				return EmptyCollection;
			}

			StatementGrammarNode currentTerminal;

			var completionItems = Enumerable.Empty<ICodeCompletionItem>();
			var statement = (OracleStatement)sqlDocumentRepository.Statements.LastOrDefault(s => s.GetNodeAtPosition(cursorPosition) != null);

			if (statement == null)
			{
				statement = completionType.Statement;
				if (statement == null)
				{
					return EmptyCollection;
				}

				currentTerminal = statement.GetNearestTerminalToPosition(cursorPosition);

				if (completionType.InUnparsedData || currentTerminal == null)
				{
					return EmptyCollection;
				}
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

			var extraOffset = currentTerminal.SourcePosition.ContainsIndex(cursorPosition) && !currentTerminal.Id.In(Terminals.LeftParenthesis, Terminals.Dot) ? 1 : 0;

			if (completionType.SchemaDataObject)
			{
				var schemaName = completionType.ReferenceIdentifier.HasSchemaIdentifier
					? currentTerminal.ParentNode.FirstTerminalNode.Token.Value
					: databaseModel.CurrentSchema.ToQuotedIdentifier();

				completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, schemaName, completionType.TerminalValuePartUntilCaret, terminalToReplace, insertOffset: extraOffset));

				if (!completionType.ReferenceIdentifier.HasSchemaIdentifier)
				{
					completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, OracleObjectIdentifier.SchemaPublic, completionType.TerminalValuePartUntilCaret, terminalToReplace, insertOffset: extraOffset));
				}

				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, completionType.TerminalValuePartUntilCaret, terminalToReplace, extraOffset));
			}

			var joinClauseNode = currentTerminal.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.FromClause), NonTerminals.JoinClause);
			if (joinClauseNode != null && !cursorAtLastTerminal && currentTerminal.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias, Terminals.On))
			{
				var isNotInnerJoin = joinClauseNode.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.InnerJoinClause)) == null;
				if (isNotInnerJoin || (!joinClauseNode.FirstTerminalNode.Id.In(Terminals.Cross, Terminals.Natural)))
				{
					var joinedTableReferenceNodes = joinClauseNode.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.JoinClause, NonTerminals.NestedQuery), NonTerminals.TableReference).ToArray();
					if (joinedTableReferenceNodes.Length == 1)
					{
						var joinedTableReference = completionType.CurrentQueryBlock.ObjectReferences.SingleOrDefault(t => t.RootNode == joinedTableReferenceNodes[0]);
						if (joinedTableReference != null && (joinedTableReference.Type != ReferenceType.InlineView || joinedTableReference.AliasNode != null))
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

			if (String.Equals(currentTerminal.Id, Terminals.Join) ||
				(String.Equals(currentTerminal.Id, Terminals.ObjectAlias) && String.Equals(((OracleToken)currentTerminal.Token).UpperInvariantValue, TerminalValues.Join)))
			{
				completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, databaseModel.CurrentSchema.ToQuotedIdentifier(), null, null, insertOffset: extraOffset));
				completionItems = completionItems.Concat(GenerateSchemaDataObjectItems(oracleDatabaseModel, OracleObjectIdentifier.SchemaPublic, null, null, insertOffset: extraOffset));
				completionItems = completionItems.Concat(GenerateCommonTableExpressionReferenceItems(semanticModel, null, null, extraOffset));
			}

			if (completionType.Column || completionType.PlSqlCompletion != PlSqlCompletion.None)
			{
				completionItems = completionItems.Concat(GenerateSelectListItems(referenceContainers, cursorPosition, oracleDatabaseModel, completionType, forcedInvokation));
			}

			if (completionType.Column || completionType.SpecialFunctionParameter)
			{
				var programOverloads = ResolveProgramOverloads(referenceContainers, currentTerminal, cursorPosition);
				var specificFunctionParameterCodeCompletionItems = CodeCompletionSearchHelper.ResolveSpecificFunctionParameterCodeCompletionItems(currentTerminal, programOverloads, oracleDatabaseModel);
				completionItems = completionItems.Concat(specificFunctionParameterCodeCompletionItems);
			}

			if (completionType.ColumnAlias)
			{
				completionItems = completionItems.Concat(GenerateColumnAliases(terminalToReplace, completionType));
			}

			if (completionType.UpdateSetColumn && semanticModel.MainObjectReferenceContainer.MainObjectReference != null)
			{
				completionItems = completionItems.Concat(GenerateSimpleColumnItems(semanticModel.MainObjectReferenceContainer.MainObjectReference, completionType));
			}

			if (completionType.InsertIntoColumn)
			{
				var columnList = currentTerminal.GetAncestor(NonTerminals.ParenthesisEnclosedPrefixedIdentifierList);
				var insertTarget = semanticModel.InsertTargets.SingleOrDefault(t => t.ColumnListNode == columnList && t.DataObjectReference != null);
				if (insertTarget != null)
				{
					completionItems = completionItems.Concat(GenerateSimpleColumnItems(insertTarget.DataObjectReference, completionType));
				}
			}

			if (completionType.DatabaseLink)
			{
				var databaseLinkItems = oracleDatabaseModel.DatabaseLinks.Values
					.Where(
						l =>
							l.FullyQualifiedName.NormalizedOwner.In(OracleObjectIdentifier.SchemaPublic, oracleDatabaseModel.CurrentSchema.ToQuotedIdentifier()) &&
							(String.IsNullOrEmpty(completionType.TerminalValueUnderCursor) || !String.Equals(completionType.TerminalValueUnderCursor.ToQuotedIdentifier(), l.FullyQualifiedName.NormalizedName)) &&
							CodeCompletionSearchHelper.IsMatch(l.FullyQualifiedName.Name, completionType.TerminalValuePartUntilCaret))
					.Select(
						l =>
							new OracleCodeCompletionItem
							{
								Name = l.FullyQualifiedName.Name.ToSimpleIdentifier(),
								Text = l.FullyQualifiedName.Name.ToSimpleIdentifier(),
								Category = OracleCodeCompletionCategory.DatabaseLink,
								StatementNode = completionType.CurrentTerminal
							});

				completionItems = completionItems.Concat(databaseLinkItems);
			}

			if (completionType.ExplicitPartition || completionType.ExplicitSubPartition)
			{
				var tableReferenceNode = completionType.EffectiveTerminal.GetAncestor(NonTerminals.TableReference);
				var tableReference = referenceContainers.SelectMany(c => c.ObjectReferences).SingleOrDefault(o => o.RootNode == tableReferenceNode && o.SchemaObject != null);
				if (tableReference != null)
				{
					completionItems = completionItems.Concat(GenerateTablePartitionItems(tableReference, completionType, completionType.ExplicitSubPartition));
				}
			}

			if (completionType.DataType)
			{
				completionItems = completionItems.Concat(GenerateDataTypeItems(completionType, oracleDatabaseModel));
			}

			if (completionType.Schema && !completionType.UpdateSetColumn &&
				(!completionType.ReferenceIdentifier.HasSchemaIdentifier || String.Equals(completionType.EffectiveTerminal.Id, Terminals.SchemaIdentifier)))
			{
				completionItems = completionItems.Concat(GenerateSchemaItems(completionType, terminalToReplace, extraOffset, oracleDatabaseModel, 2));
			}

			if (completionType.BindVariable)
			{
				var providerConfiguration = WorkDocumentCollection.GetProviderConfiguration(oracleDatabaseModel.ConnectionString.ProviderName);
				var currentNormalizedValue = completionType.TerminalValuePartUntilCaret.ToQuotedIdentifier();
				var bindVariables =
					providerConfiguration.BindVariables.Where(bv => bv.Value != null && !Equals(bv.Value, String.Empty) && !Equals(bv.Value, DateTime.MinValue) && !String.Equals(bv.Name.ToQuotedIdentifier(), currentNormalizedValue) && CodeCompletionSearchHelper.IsMatch(bv.Name.ToQuotedIdentifier(), completionType.TerminalValuePartUntilCaret))
						.Select(
							bv =>
								new OracleCodeCompletionItem
								{
									Name = bv.Name,
									Text = bv.Name,
									Category = OracleCodeCompletionCategory.BindVariable,
									StatementNode = completionType.ReferenceIdentifier.IdentifierUnderCursor
								});

				completionItems = completionItems.Concat(bindVariables);
			}

			completionItems = completionItems.Concat(GenerateKeywordItems(completionType));

			return completionItems.OrderItems().ToArray();

			// TODO: Add option to search all/current/public schemas
		}

		private IEnumerable<ICodeCompletionItem> GenerateDataTypeItems(OracleCodeCompletionType completionType, OracleDatabaseModelBase databaseModel)
		{
			var dateTypePart = MakeSaveQuotedIdentifier(completionType.ReferenceIdentifier.ObjectIdentifierEffectiveValue);
			var owner = completionType.ReferenceIdentifier.HasSchemaIdentifier
				? completionType.ReferenceIdentifier.SchemaIdentifierOriginalValue.ToQuotedIdentifier()
				: null;

			var currentSchema = databaseModel.CurrentSchema.ToQuotedIdentifier();
			var node = completionType.ReferenceIdentifier.IdentifierUnderCursor;
			var safeTokenValueQuotedIdentifier = node == null ? null : MakeSaveQuotedIdentifier(node.Token.Value);

			var dataTypeSource = databaseModel.AllObjects.Values
				.Where(
					o =>
						o.GetTargetSchemaObject() is OracleTypeBase &&
						!String.Equals(dateTypePart, o.Name) &&
						FilterSchema(o, currentSchema, owner))
				.Select(o => new { o.Owner, o.Name });

			if (String.IsNullOrEmpty(owner))
			{
				dataTypeSource = dataTypeSource.Concat(OracleDatabaseModelBase.BuiltInDataTypes.Select(d => new { Owner = (string)null, Name = d }));
			}

			if (node != null)
			{
				dataTypeSource = dataTypeSource.Where(o => !String.Equals(safeTokenValueQuotedIdentifier, o.Name) && CodeCompletionSearchHelper.IsMatch(o.Name, dateTypePart));
			}

			return dataTypeSource
				.Select(t =>
				{
					var name = t.Name.ToSimpleIdentifier();
					var text = !String.IsNullOrEmpty(owner) || String.Equals(t.Owner, OracleObjectIdentifier.SchemaPublic) || String.IsNullOrEmpty(t.Owner)
						? name
						: $"{t.Owner.ToSimpleIdentifier()}.{name}";

					var addSizeParentheses = text.In(TerminalValues.Varchar2, TerminalValues.NVarchar2, TerminalValues.Char, TerminalValues.NChar, TerminalValues.Raw);

					return
						new OracleCodeCompletionItem
						{
							Name = text,
							Text = addSizeParentheses ? $"{text}()" : text,
							Category = OracleCodeCompletionCategory.DataType,
							StatementNode = completionType.ReferenceIdentifier.IdentifierUnderCursor,
							CaretOffset = addSizeParentheses ? -1 : 0
						};
				});
		}

		private static IEnumerable<OracleCodeCompletionItem> GenerateTablePartitionItems(OracleDataObjectReference tableReference, OracleCodeCompletionType completionType, bool subPartitions)
		{
			var table = tableReference.SchemaObject.GetTargetSchemaObject() as OracleTable;
			if (table == null)
			{
				return Enumerable.Empty<OracleCodeCompletionItem>();
			}

			var sourcePartitions = subPartitions
				? table.Partitions.Values.SelectMany(p => p.SubPartitions.Values)
				: (IEnumerable<OraclePartitionBase>)table.Partitions.Values;

			var quotedTerminalValueUnderCursor = completionType.TerminalValueUnderCursor.ToQuotedIdentifier();
			var partitions = sourcePartitions
				.Where(p => (String.IsNullOrEmpty(completionType.TerminalValueUnderCursor) || !String.Equals(quotedTerminalValueUnderCursor, p.Name)) &&
				            CodeCompletionSearchHelper.IsMatch(p.Name, completionType.TerminalValuePartUntilCaret))
				.Select(l =>
					new OracleCodeCompletionItem
					{
						Name = l.Name.ToSimpleIdentifier(),
						Text = l.Name.ToSimpleIdentifier(),
						Category = subPartitions ? OracleCodeCompletionCategory.Subpartition : OracleCodeCompletionCategory.Partition,
						StatementNode = completionType.ReferenceIdentifier.IdentifierUnderCursor
					});
			
			return partitions;
		}

		private IEnumerable<ICodeCompletionItem> GenerateKeywordItems(OracleCodeCompletionType completionType)
		{
			var alternativeTerminalToReplace = completionType.CurrentTerminal != null && !completionType.CurrentTerminal.Id.In(Terminals.RightParenthesis, Terminals.Comma)
				? completionType.CurrentTerminal
				: null;
			
			return completionType.KeywordsClauses
				.Where(t => !String.Equals(t.TerminalId, completionType.TerminalValueUnderCursor, StringComparison.InvariantCultureIgnoreCase))
				.Where(t => CodeCompletionSearchHelper.IsMatch(t.Text, completionType.TerminalValuePartUntilCaret))
				.Select(t =>
					new OracleCodeCompletionItem
					{
						Name = t.Text,
						Text = t.Text,
						Category = OracleCodeCompletionCategory.Keyword,
						StatementNode = completionType.ReferenceIdentifier.IdentifierUnderCursor ?? alternativeTerminalToReplace,
						CategoryPriority = 1
					});
		}

		private static IEnumerable<ICodeCompletionItem> GenerateColumnAliases(StatementGrammarNode currentTerminal, OracleCodeCompletionType completionType)
		{
			var formatOption = FormatOptions.Identifier;
			return completionType.CurrentQueryBlock.Columns
				.Where(c => c.HasExplicitAlias)
				.Select(c =>
				{
					var identifier = OracleStatementFormatter.FormatTerminalValue(c.NormalizedName.ToSimpleIdentifier(), formatOption);
					return
						new OracleCodeCompletionItem
						{
							Name = identifier,
							Text = identifier,
							Category = OracleCodeCompletionCategory.Column,
							StatementNode = currentTerminal
						};
				});
		}

		private static IEnumerable<ICodeCompletionItem> GenerateSimpleColumnItems(OracleObjectWithColumnsReference targetDataObject, OracleCodeCompletionType completionType)
		{
			return targetDataObject.Columns
				.Where(c => !String.Equals(completionType.TerminalValueUnderCursor.ToQuotedIdentifier(), c.Name) && CodeCompletionSearchHelper.IsMatch(c.Name, completionType.TerminalValuePartUntilCaret))
				.Select(c =>
					new OracleCodeCompletionItem
					{
						Name = c.Name.ToSimpleIdentifier(),
						Text = c.Name.ToSimpleIdentifier(),
						Category = OracleCodeCompletionCategory.Column,
						StatementNode = completionType.ReferenceIdentifier.IdentifierUnderCursor
					});
		}

		private IEnumerable<ICodeCompletionItem> GenerateSelectListItems(ICollection<OracleReferenceContainer> referenceContainers, int cursorPosition, OracleDatabaseModelBase databaseModel, OracleCodeCompletionType completionType, bool forcedInvokation)
		{
			var currentNode = completionType.EffectiveTerminal;
			
			var objectOrSchemaIdentifierFollowing = !completionType.IsNewExpressionWithInvalidGrammar && !String.Equals(currentNode.Id, Terminals.Identifier) && currentNode.Id.In(Terminals.ObjectIdentifier, Terminals.SchemaIdentifier);
			if (objectOrSchemaIdentifierFollowing || currentNode.Id.IsLiteral())
			{
				return EmptyCollection;
			}

			var programReferences = referenceContainers.SelectMany(c => c.ProgramReferences);

			var objectIdentifierNode = completionType.ReferenceIdentifier.ObjectIdentifier;
			var partialName = completionType.ReferenceIdentifier.IdentifierEffectiveValue;
			var currentName = MakeSaveQuotedIdentifier(completionType.ReferenceIdentifier.IdentifierOriginalValue);
			var nodeToReplace = completionType.ReferenceIdentifier.IdentifierUnderCursor;
			var schemaName = completionType.ReferenceIdentifier.SchemaIdentifierOriginalValue;

			var programReference = programReferences.SingleOrDefault(f => f.ProgramIdentifierNode == currentNode);
			var addParameterList = programReference?.ParameterListNode == null;

			var tableReferenceSource = (ICollection<OracleObjectWithColumnsReference>)referenceContainers
				.SelectMany(c => c.ObjectReferences)
				.Where(o => !completionType.InQueryBlockFromClause || completionType.CursorPosition > o.RootNode.SourcePosition.IndexEnd)
				.Distinct(r => r.FullyQualifiedObjectName)
				.ToArray();

			var suggestedFunctions = Enumerable.Empty<ICodeCompletionItem>();
			var suggestedItems = Enumerable.Empty<ICodeCompletionItem>();
			if (objectIdentifierNode != null)
			{
				var objectName = objectIdentifierNode.Token.Value;
				var fullyQualifiedName = OracleObjectIdentifier.Create(schemaName, objectName);
				tableReferenceSource = tableReferenceSource
					.Where(t => t.FullyQualifiedObjectName == fullyQualifiedName || (String.IsNullOrEmpty(fullyQualifiedName.Owner) && fullyQualifiedName.NormalizedName == t.FullyQualifiedObjectName.NormalizedName))
					.ToArray();

				OracleSchemaObject schemaObject;
				if (tableReferenceSource.Count == 0 && databaseModel.AllObjects.TryGetFirstValue(out schemaObject, databaseModel.GetPotentialSchemaObjectIdentifiers(fullyQualifiedName)))
				{
					var sequence = schemaObject.GetTargetSchemaObject() as OracleSequence;
					if (sequence != null)
					{
						suggestedItems = sequence.Columns
							.Where(c => !String.Equals(c.Name, currentName) && CodeCompletionSearchHelper.IsMatch(c.Name, partialName))
							.Select(c => CreateColumnCodeCompletionItem(c.Name.ToSimpleIdentifier(), null, nodeToReplace, OracleCodeCompletionCategory.Pseudocolumn));
					}
				}

				if (tableReferenceSource.Count == 0 && (partialName != null || currentNode.SourcePosition.IndexEnd < cursorPosition))
				{
					if (String.IsNullOrEmpty(schemaName))
					{
						var packageMatcher = new OracleProgramMatcher(
							new ProgramMatchElement(objectName).SelectOwner(),
							new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectPackage().AsResultValue(),
							null)
							{ PlSqlCompletion = completionType.PlSqlCompletion };

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.Package, nodeToReplace, 0, addParameterList, databaseModel, packageMatcher);

						var packageFunctionMatcher = new OracleProgramMatcher(
							new ProgramMatchElement(databaseModel.CurrentSchema).SelectOwner(), 
							new ProgramMatchElement(objectName).SelectPackage(),
							new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue())
							{ PlSqlCompletion = completionType.PlSqlCompletion };

						var publicSynonymPackageFunctionMatcher = new OracleProgramMatcher(
							new ProgramMatchElement(OracleObjectIdentifier.SchemaPublic).SelectSynonymOwner(),
							new ProgramMatchElement(objectName).SelectSynonymPackage(),
							new ProgramMatchElement(partialName) {AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName}.SelectSynonymName().AsResultValue())
							{ PlSqlCompletion = completionType.PlSqlCompletion };

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.PackageFunction, nodeToReplace, 0, addParameterList, databaseModel, packageFunctionMatcher, publicSynonymPackageFunctionMatcher)
							.Concat(suggestedFunctions);

						var schemaFunctionMatcher = new OracleProgramMatcher(
							new ProgramMatchElement(objectName).SelectOwner(),
							new ProgramMatchElement(null).SelectPackage(),
							new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue())
							{ PlSqlCompletion = completionType.PlSqlCompletion };

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.SchemaFunction, nodeToReplace, 0, addParameterList, databaseModel, schemaFunctionMatcher)
							.Concat(suggestedFunctions);
					}
					else
					{
						var packageFunctionMatcher = new OracleProgramMatcher(
							new ProgramMatchElement(schemaName).SelectOwner(),
							new ProgramMatchElement(objectName).SelectPackage(),
							new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue())
							{ PlSqlCompletion = completionType.PlSqlCompletion };

						suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.PackageFunction, nodeToReplace, 0, addParameterList, databaseModel, packageFunctionMatcher);
					}
				}
			}
			else
			{
				var builtInPackageFunctionMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(OracleObjectIdentifier.SchemaSys).SelectOwner(),
						new ProgramMatchElement(OracleObjectIdentifier.PackageBuiltInFunction).SelectPackage(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue())
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				var builtInNonSchemaFunctionMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(null).SelectOwner(),
						new ProgramMatchElement(null).SelectPackage(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue())
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				var currentSchema = databaseModel.CurrentSchema.ToQuotedIdentifier();
				var localSchemaProgramMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(currentSchema).SelectOwner(),
						new ProgramMatchElement(null).SelectPackage(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectName().AsResultValue())
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				var localSynonymProgramMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(currentSchema).SelectSynonymOwner(),
						new ProgramMatchElement(null).SelectPackage(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymName().AsResultValue())
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				var publicSynonymProgramMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(OracleObjectIdentifier.SchemaPublic).SelectSynonymOwner(),
						new ProgramMatchElement(null).SelectPackage(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymName().AsResultValue())
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				var localSchemaPackageMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(currentSchema).SelectOwner(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectPackage().AsResultValue(),
						null)
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				var localSynonymPackageMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(currentSchema).SelectSynonymOwner(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymPackage().AsResultValue(),
						null)
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				var publicSynonymPackageMatcher =
					new OracleProgramMatcher(
						new ProgramMatchElement(OracleObjectIdentifier.SchemaPublic).SelectSynonymOwner(),
						new ProgramMatchElement(partialName) { AllowStartWithMatch = forcedInvokation, AllowPartialMatch = !forcedInvokation, DeniedValue = currentName }.SelectSynonymPackage().AsResultValue(),
						null)
					{ PlSqlCompletion = completionType.PlSqlCompletion };

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.PackageFunction, nodeToReplace, 0, addParameterList, databaseModel, builtInPackageFunctionMatcher);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.BuiltInFunction, nodeToReplace, 0, addParameterList, databaseModel, builtInNonSchemaFunctionMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.SchemaFunction, nodeToReplace, 0, addParameterList, databaseModel, localSchemaProgramMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.SchemaFunction, nodeToReplace, 0, addParameterList, databaseModel, localSynonymProgramMatcher, publicSynonymProgramMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.Package, nodeToReplace, 0, addParameterList, databaseModel, localSchemaPackageMatcher)
					.Concat(suggestedFunctions);

				suggestedFunctions = GenerateCodeItems(OracleCodeCompletionCategory.Package, nodeToReplace, 0, addParameterList, databaseModel, localSynonymPackageMatcher, publicSynonymPackageMatcher)
					.Concat(suggestedFunctions);
			}

			if (completionType.PlSqlCompletion == PlSqlCompletion.Procedure)
			{
				return suggestedFunctions;
			}

			var columnCandidates = tableReferenceSource
				.SelectMany(t => t.Columns
					.Where(c =>
						(!String.Equals(currentNode.Id, Terminals.Identifier) || !String.Equals(c.Name, currentName)) &&
						(objectIdentifierNode == null && String.IsNullOrEmpty(partialName) ||
						(!String.Equals(c.Name, partialName.ToQuotedIdentifier()) && CodeCompletionSearchHelper.IsMatch(c.Name, partialName))))
					.Select(c => new { ObjectReference = t, Column = c }))
					.GroupBy(c => c.Column.Name)
					.ToDictionary(g => g.Key ?? String.Empty, g => g.Select(o => o.ObjectReference).ToArray());

			var suggestedColumns = new List<Tuple<string, OracleObjectIdentifier>>();
			foreach (var columnCandidate in columnCandidates)
			{
				suggestedColumns.AddRange(OracleObjectIdentifier.GetUniqueReferences(columnCandidate.Value.Select(t => t.FullyQualifiedObjectName).ToArray())
					.Select(objectIdentifier => new Tuple<string, OracleObjectIdentifier>(columnCandidate.Key, objectIdentifier)));
			}

			var rowSourceColumnItems = suggestedColumns.Select(t => CreateColumnCodeCompletionItem(t.Item1, objectIdentifierNode == null ? t.Item2.ToString() : null, nodeToReplace));
			suggestedItems = suggestedItems.Concat(rowSourceColumnItems);

			var flashbackColumns = tableReferenceSource
				.SelectMany(r => r.Pseudocolumns.Select(c => new { r.FullyQualifiedObjectName, Pseudocolumn = c }))
				.Where(c => CodeCompletionSearchHelper.IsMatch(c.Pseudocolumn.Name, partialName))
				.Select(c =>
					CreateColumnCodeCompletionItem(GetPrettyColumnName(c.Pseudocolumn.Name), objectIdentifierNode == null ? c.FullyQualifiedObjectName.ToString() : null, nodeToReplace, OracleCodeCompletionCategory.Pseudocolumn));

			suggestedItems = suggestedItems.Concat(flashbackColumns);

			if (partialName == null && currentNode.IsWithinSelectClause() && currentNode.GetParentExpression().GetParentExpression() == null)
			{
				suggestedItems = suggestedItems.Concat(CreateAsteriskColumnCompletionItems(tableReferenceSource, objectIdentifierNode != null, nodeToReplace));
			}

			if (objectIdentifierNode == null)
			{
				var queryBlockReferencedObjects = tableReferenceSource.Where(r => CodeCompletionSearchHelper.IsMatch(r.FullyQualifiedObjectName.Name, partialName)).ToArray();
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

		internal static string GetPrettyColumnName(string normalizedColumnName)
		{
			return String.Equals(normalizedColumnName, OracleDataObjectReference.RowIdNormalizedName)
				? TerminalValues.RowIdPseudocolumn
				: normalizedColumnName.ToSimpleIdentifier();
		}

		private static bool FilterOtherSchemaObject(OracleSchemaObject schemaObject, bool sequencesAllowed)
		{
			var targetObject = schemaObject.GetTargetSchemaObject();
			return targetObject != null && (String.Equals(targetObject.Type, OracleObjectType.Type) || (sequencesAllowed && String.Equals(targetObject.Type, OracleObjectType.Sequence)));
		}

		private static IEnumerable<OracleCodeCompletionItem> CreateJoinTypeCompletionItems(OracleCodeCompletionType completionType)
		{
			var formatOption = FormatOptions.Keyword;

			return JoinClauseTemplates
				.Where(c => !completionType.ExistsTerminalValue || c.Name.StartsWith(completionType.TerminalValueUnderCursor.ToUpperInvariant()))
				.Select(j =>
				{
					var joinType = OracleStatementFormatter.FormatTerminalValue(j.Name, formatOption);
					return
						new OracleCodeCompletionItem
						{
							Name = joinType,
							Text = joinType,
							Priority = j.Priority,
							Category = OracleCodeCompletionCategory.JoinMethod,
							CategoryPriority = 1,
							StatementNode = completionType.EffectiveTerminal
						};
				});
		}

		private static IEnumerable<OracleCodeCompletionItem> CreateAsteriskColumnCompletionItems(IEnumerable<OracleObjectWithColumnsReference> tables, bool skipFirstObjectIdentifier, StatementGrammarNode nodeToReplace)
		{
			var formatOption = FormatOptions.Identifier;
			var builder = new StringBuilder();
			
			foreach (var table in tables)
			{
				if (table.Columns.Count <= 1)
				{
					continue;
				}

				builder.Clear();
				var skipTablePrefix = skipFirstObjectIdentifier;
				var separator = String.Empty;
				var rowSourceFullName = String.Empty;

				if (table.FullyQualifiedObjectName.HasOwner)
				{
					var rowSourceOwner = table.FullyQualifiedObjectName.Owner.ToSimpleIdentifier();
					rowSourceFullName = $"{OracleStatementFormatter.FormatTerminalValue(rowSourceOwner, formatOption)}.";
				}

				var rowSourceName = table.FullyQualifiedObjectName.Name.ToSimpleIdentifier();
				rowSourceFullName = $"{rowSourceFullName}{OracleStatementFormatter.FormatTerminalValue(rowSourceName, formatOption)}";

				foreach (var column in table.Columns)
				{
					if (column.Hidden)
					{
						continue;
					}

					builder.Append(separator);

					if (!skipTablePrefix && !String.IsNullOrEmpty(table.FullyQualifiedObjectName.Name))
					{
						builder.Append(rowSourceFullName);
						builder.Append(".");
					}
					
					builder.Append(OracleStatementFormatter.FormatTerminalValue(column.Name.ToSimpleIdentifier(), formatOption));

					skipTablePrefix = false;
					separator = ", ";
				}

				yield return
					new OracleCodeCompletionItem
					{
						Name = (skipFirstObjectIdentifier || String.IsNullOrEmpty(table.FullyQualifiedObjectName.Name) ? String.Empty : $"{rowSourceFullName}.") + "*",
						Text = builder.ToString(),
						StatementNode = nodeToReplace,
						CategoryPriority = -2,
						Category = OracleCodeCompletionCategory.AllColumns
					};
			}
		}

		private static ICodeCompletionItem CreateColumnCodeCompletionItem(string columnName, string objectPrefix, StatementGrammarNode nodeToReplace, string category = OracleCodeCompletionCategory.Column)
		{
			var formatOption = FormatOptions.Identifier;
			columnName = OracleStatementFormatter.FormatTerminalValue(columnName.ToSimpleIdentifier(), formatOption);
			var text = String.IsNullOrEmpty(objectPrefix)
				? columnName
				: $"{OracleStatementFormatter.FormatTerminalValue(objectPrefix, formatOption)}.{columnName}";

			return
				new OracleCodeCompletionItem
				{
					Name = text,
					Text = text,
					StatementNode = nodeToReplace,
					Category = category,
					CategoryPriority = -1
				};
		}

		private static IEnumerable<ICodeCompletionItem> GenerateSchemaItems(OracleCodeCompletionType completionType, StatementGrammarNode node, int insertOffset, OracleDatabaseModelBase databaseModel, int priorityOffset = 0)
		{
			var formatOption = FormatOptions.Identifier;
			var schemaNamePart = completionType.TerminalValuePartUntilCaret;
			var currentSchema = MakeSaveQuotedIdentifier(completionType.TerminalValueUnderCursor);

			return databaseModel.AllSchemas.Values
				.Where(s =>
					!String.Equals(s.Name, OracleObjectIdentifier.SchemaPublic) &&
					!String.Equals(currentSchema, s.Name) &&
					CodeCompletionSearchHelper.IsMatch(s.Name, schemaNamePart))
				.Select(
					s =>
					{
						var schemaItem = OracleStatementFormatter.FormatTerminalValue(s.Name.ToSimpleIdentifier(), formatOption); 
						return new OracleCodeCompletionItem
						{
							Name = schemaItem,
							Text = schemaItem,
							StatementNode = node,
							Category = OracleCodeCompletionCategory.DatabaseSchema,
							InsertOffset = insertOffset,
							CategoryPriority = 1 + priorityOffset
						};
					});
		}

		private static IEnumerable<ICodeCompletionItem> GenerateCodeItems(string category, StatementGrammarNode node, int insertOffset, bool addParameterList, OracleDatabaseModelBase databaseModel, params OracleProgramMatcher[] matchers)
		{
			string parameterList = null;
			var parameterListCaretOffset = 0;
			if (addParameterList)
			{
				parameterList = "()";
				parameterListCaretOffset = -1;
			}

			var quotedSchemaName = databaseModel.CurrentSchema.ToQuotedIdentifier();
			var formatOptionIdentifier = FormatOptions.Identifier;
			var formatOptionReservedWord = FormatOptions.ReservedWord;

			return databaseModel.AllProgramMetadata
				.SelectMany(g => g)
				.SelectMany(f => matchers.Select(m => m.GetMatchResult(f, quotedSchemaName)))
				.Where(r => r.IsMatched)
				.SelectMany(r => r.Matches.Where(v => !String.IsNullOrEmpty(v)).Select(v => new { Name = v.ToSimpleIdentifier(), r.Metadata }))
				.GroupBy(r => r.Name)
				.Select(g => new { Name = g.Key, MetadataCollection = g.Select(i => i.Metadata).ToArray() })
				.Select(i =>
				{
					var metadata = i.MetadataCollection[0];
					var hasReservedWordName = metadata.IsBuiltIn && i.Name.CollidesWithReservedWord();

					var programName = i.Name;
					var formatOption = formatOptionIdentifier;
					if (hasReservedWordName)
					{
						programName = i.Name.Trim('"');
						formatOption = formatOptionReservedWord;
					}
					
					programName = OracleStatementFormatter.FormatTerminalValue(programName, formatOption);

					var postFix = parameterList;
					var isPackage = String.Equals(category, OracleCodeCompletionCategory.Package);
					if (isPackage)
					{
						postFix = ".";
					}
					else if (hasReservedWordName || String.Equals(metadata.DisplayType, OracleProgramMetadata.DisplayTypeNoParenthesis))
					{
						postFix = null;
					}
					else if (addParameterList && metadata.IsBuiltIn && Equals(metadata.Identifier, OracleProgramIdentifier.IdentifierBuiltInProgramExtract))
					{
						postFix = "(DAY FROM )";
					}
					
					var analyticClause = addParameterList
						? GetAdditionalFunctionClause(i.MetadataCollection)
						: String.Empty;

					string description = null;
					if (metadata.Owner != null || metadata.IsBuiltIn)
					{
						description = isPackage
							? metadata.Owner?.Documentation
							: metadata.Documentation;
					}

					return
						new OracleCodeCompletionItem
						{
							Name = programName,
							Text = $"{programName}{postFix}{analyticClause}",
							StatementNode = node,
							Category = category,
							InsertOffset = insertOffset,
							CaretOffset = hasReservedWordName || isPackage || String.Equals(metadata.DisplayType, OracleProgramMetadata.DisplayTypeNoParenthesis)
								? 0
								: parameterListCaretOffset - analyticClause.Length,
							CategoryPriority = 2,
							Description = description
						};
				});
		}

		private static string GetAdditionalFunctionClause(OracleProgramMetadata[] metadataCollection)
		{
			var metadata = metadataCollection[0];
			var orderByClause = metadata.IsBuiltIn && metadata.Identifier.Name.In("\"NTILE\"", "\"ROW_NUMBER\"", "\"RANK\"", "\"DENSE_RANK\"", "\"LEAD\"", "\"LAG\"")
				? "ORDER BY NULL"
				: String.Empty;

			if (metadata.IsBuiltIn && String.Equals(metadata.Identifier.Name, "\"LISTAGG\""))
			{
				return " WITHIN GROUP (ORDER BY NULL)";
			}

			if (!metadataCollection.Any(m => m.IsAggregate) && metadata.IsAnalytic)
			{
				return $" OVER ({orderByClause})";
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
			var safeSchemaName = MakeSaveQuotedIdentifier(schemaName);
			var dataObjects = databaseModel.AllObjects.Values
				.Where(o => (filter == null || filter(o)) && FilterSchema(o, activeSchema, safeSchemaName))
				.Select(o => new ObjectReferenceCompletionData { Identifier2 = o.Name, SchemaObject = o, Category = OracleCodeCompletionCategory.SchemaObject });
			return CreateObjectItems(dataObjects, objectNamePart, node, categoryOffset, insertOffset);
		}

		private bool FilterSchema(OracleSchemaObject schemaObject, string activeSchema, string schemaName)
		{
			return String.IsNullOrEmpty(schemaName)
				? String.Equals(schemaObject.Owner, activeSchema) || String.Equals(schemaObject.Owner, OracleObjectIdentifier.SchemaPublic)
				: String.Equals(schemaObject.Owner, schemaName);
		}

		private static IEnumerable<ICodeCompletionItem> CreateObjectItems(IEnumerable<ObjectReferenceCompletionData> objects, string objectNamePart, StatementGrammarNode node, int categoryOffset = 0, int insertOffset = 0)
		{
			var safeObjectPartQuotedIdentifier = MakeSaveQuotedIdentifier(objectNamePart);
			var safeTokenValueQuotedIdentifier = node == null ? null : MakeSaveQuotedIdentifier(node.Token.Value);
			var objectNamePartUpperInvariant = objectNamePart?.ToUpperInvariant() ?? String.Empty;
			return objects
				.Where(o => !String.Equals(safeObjectPartQuotedIdentifier, o.Identifier2) &&
				            (node == null || !String.Equals(safeTokenValueQuotedIdentifier, o.Identifier2)) && CodeCompletionSearchHelper.IsMatch(o.Identifier2, objectNamePart))
				.Select(o =>
				{
					var schemaObject = o.SchemaObject.GetTargetSchemaObject();
					DocumentationDataDictionaryObject documentation = null;
					if (String.Equals(schemaObject?.Owner, OracleObjectIdentifier.SchemaSys))
					{
						OracleHelpProvider.DataDictionaryObjectDocumentation.TryGetValue(schemaObject.FullyQualifiedName, out documentation);
					}

					var completionText = o.CompletionText;
					return
						new OracleCodeCompletionItem
						{
							Name = completionText,
							Text = $"{completionText}{o.TextPostFix}",
							Priority = String.IsNullOrEmpty(objectNamePart) || completionText.TrimStart('"').ToUpperInvariant().StartsWith(objectNamePartUpperInvariant) ? 0 : 1,
							StatementNode = node,
							Category = o.Category,
							InsertOffset = insertOffset,
							CaretOffset = o.CaretOffset,
							CategoryPriority = categoryOffset,
							Description = documentation?.Value
						};
				});
		}

		private class ObjectReferenceCompletionData
		{
			public OracleSchemaObject SchemaObject { get; set; }

			public string Identifier1 { get; set; }
			
			public string Identifier2 { get; set; }

			public string CompletionText => MergeIdentifiersIntoSimpleString(Identifier1, Identifier2);

			public string Category { get; set; }

			public int CaretOffset => IsSchemaType() ? -1 : 0;

			public string TextPostFix => IsSchemaType() ? "()" : null;

			private bool IsSchemaType()
			{
				var targetObject = SchemaObject.GetTargetSchemaObject();
				return targetObject != null && String.Equals(targetObject.Type, OracleObjectType.Type);
			}
		}

		private static string MergeIdentifiersIntoSimpleString(string identifier1, string identifier2)
		{
			var formatOption = FormatOptions.Identifier;
			var ownerPrefix = String.IsNullOrEmpty(identifier1) ? null : $"{OracleStatementFormatter.FormatTerminalValue(identifier1.ToSimpleIdentifier(), formatOption)}.";
			return $"{ownerPrefix}{OracleStatementFormatter.FormatTerminalValue(identifier2.ToSimpleIdentifier(), formatOption)}";
		}

		private static string MakeSaveQuotedIdentifier(string identifierPart)
		{
			if (String.IsNullOrEmpty(identifierPart) || identifierPart.All(c => c == '"'))
			{
				return null;
			}

			var preFix = identifierPart[0] != '"' && identifierPart[identifierPart.Length - 1] == '"' ? "\"" : null;
			var postFix = identifierPart[0] == '"' && identifierPart[identifierPart.Length - 1] != '"' ? "\"" : null;
			return $"{preFix}{identifierPart}{postFix}".ToQuotedIdentifier();
		}

		private static bool IsDataObject(OracleSchemaObject schemaObject)
		{
			return schemaObject.GetTargetSchemaObject() is OracleDataObject;
		}

		private static IEnumerable<ICodeCompletionItem> GenerateCommonTableExpressionReferenceItems(OracleStatementSemanticModel model, string referenceNamePart, StatementGrammarNode node, int insertOffset)
		{
			var formatOption = FormatOptions.Alias;
			// TODO: Make proper resolution of CTE accessibility
			return model.QueryBlocks
				.Where(qb => qb.Type == QueryBlockType.CommonTableExpression && qb.PrecedingConcatenatedQueryBlock == null && referenceNamePart.ToQuotedIdentifier() != qb.NormalizedAlias && CodeCompletionSearchHelper.IsMatch(qb.Alias, referenceNamePart))
				.Select(qb =>
				{
					var alias = OracleStatementFormatter.FormatTerminalValue(qb.Alias, formatOption);
					return new OracleCodeCompletionItem
					{
						Name = alias,
						Text = alias,
						StatementNode = node,
						Category = OracleCodeCompletionCategory.CommonTableExpression,
						InsertOffset = insertOffset,
						CategoryPriority = -1
					};
				});
		}

		private static IEnumerable<ICodeCompletionItem> GenerateJoinConditionSuggestionItems(OracleDataObjectReference parentSchemaObject, OracleDataObjectReference joinedSchemaObject, OracleCodeCompletionType completionType, int insertOffset)
		{
			var suggestionItems = new List<ICodeCompletionItem>();
			var skipOnTerminal = String.Equals(completionType.EffectiveTerminal.Id, Terminals.On);

			var parentObject = parentSchemaObject.SchemaObject.GetTargetSchemaObject() as OracleDataObject;
			var joinedObject = joinedSchemaObject.SchemaObject.GetTargetSchemaObject() as OracleDataObject;

			var effectiveJoinedObjectIdentifier = GetJoinedObjectIdentifier(joinedSchemaObject, completionType.CursorPosition);
			var referenceConstraintJoinConditionFound = false;
			if (parentObject != null && joinedObject != null && (parentObject.ReferenceConstraints.Any() || joinedObject.ReferenceConstraints.Any()))
			{
				var joinedToParentKeys = parentObject.ReferenceConstraints.Where(k => k.TargetObject == joinedObject)
					.Select(k => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedObjectName, effectiveJoinedObjectIdentifier, k.Columns, k.ReferenceConstraint.Columns, OracleCodeCompletionCategory.JoinConditionByReferenceConstraint, false, skipOnTerminal, insertOffset, 0));

				suggestionItems.AddRange(joinedToParentKeys);

				var parentToJoinedKeys = joinedObject.ReferenceConstraints.Where(k => k.TargetObject == parentObject)
					.Select(k => GenerateJoinConditionSuggestionItem(effectiveJoinedObjectIdentifier, parentSchemaObject.FullyQualifiedObjectName, k.Columns, k.ReferenceConstraint.Columns, OracleCodeCompletionCategory.JoinConditionByReferenceConstraint, true, skipOnTerminal, insertOffset, 0));

				suggestionItems.AddRange(parentToJoinedKeys);
				referenceConstraintJoinConditionFound = suggestionItems.Any();
			}
			
			if (!referenceConstraintJoinConditionFound)
			{
				var columnNameJoinConditions = parentSchemaObject.Columns
					.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name)
					.Intersect(
						joinedSchemaObject.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name)).Select(c => c.Name))
					.Select(c => GenerateJoinConditionSuggestionItem(parentSchemaObject.FullyQualifiedObjectName, effectiveJoinedObjectIdentifier, new[] { c }, new[] { c }, OracleCodeCompletionCategory.JoinConditionByName, false, skipOnTerminal, insertOffset, 1));

				suggestionItems.AddRange(columnNameJoinConditions);
			}

			return suggestionItems;
		}

		private static OracleObjectIdentifier GetJoinedObjectIdentifier(OracleDataObjectReference objectReference, int cursorPosition)
		{
			return objectReference.AliasNode == null || objectReference.AliasNode.SourcePosition.IndexEnd < cursorPosition
				? objectReference.FullyQualifiedObjectName
				: OracleObjectIdentifier.Create(objectReference.OwnerNode, objectReference.ObjectNode, null);
		}

		private static OracleCodeCompletionItem GenerateJoinConditionSuggestionItem(OracleObjectIdentifier sourceObject, OracleObjectIdentifier targetObject, IList<string> keySourceColumns, IList<string> keyTargetColumns, string itemCategory, bool swapSides, bool skipOnTerminal, int insertOffset, int priority)
		{
			var builder = new StringBuilder();
			if (!skipOnTerminal)
			{
				builder.Append(OracleStatementFormatter.FormatTerminalValue(TerminalValues.On, FormatOptions.ReservedWord));
				builder.Append(" ");
			}

			var logicalOperator = String.Empty;
			var formatOption = FormatOptions.Identifier;

			for (var i = 0; i < keySourceColumns.Count; i++)
			{
				var sourceObjectName = MergeIdentifiersIntoSimpleString(OracleStatementFormatter.FormatTerminalValue(sourceObject.Owner.ToSimpleIdentifier(), formatOption), OracleStatementFormatter.FormatTerminalValue(sourceObject.Name.ToSimpleIdentifier(), formatOption));
				var targetObjectName = MergeIdentifiersIntoSimpleString(OracleStatementFormatter.FormatTerminalValue(targetObject.Owner.ToSimpleIdentifier(), formatOption), OracleStatementFormatter.FormatTerminalValue(targetObject.Name.ToSimpleIdentifier(), formatOption));
				builder.Append(logicalOperator);
				builder.Append(swapSides ? targetObjectName : sourceObjectName);
				builder.Append('.');
				builder.Append(OracleStatementFormatter.FormatTerminalValue((swapSides ? keyTargetColumns[i] : keySourceColumns[i]).ToSimpleIdentifier(), formatOption));
				builder.Append(" = ");
				builder.Append(swapSides ? sourceObjectName : targetObjectName);
				builder.Append('.');
				builder.Append(OracleStatementFormatter.FormatTerminalValue((swapSides ? keySourceColumns[i] : keyTargetColumns[i]).ToSimpleIdentifier(), formatOption));

				logicalOperator = " AND ";
			}

			return
				new OracleCodeCompletionItem
				{
					Name = builder.ToString(),
					Text = builder.ToString(),
					InsertOffset = insertOffset,
					Category = itemCategory,
					Priority = priority
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
