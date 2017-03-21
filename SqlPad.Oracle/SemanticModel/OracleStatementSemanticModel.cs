using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.SemanticModel
{
	public class OracleStatementSemanticModel : IStatementSemanticModel
	{
		private static readonly string[] StandardIdentifierIds =
		{
			Terminals.Identifier,
			Terminals.RowIdPseudocolumn,
			Terminals.Level,
			Terminals.User,
			Terminals.NegationOrNull,
			Terminals.Extract,
			Terminals.JsonExists,
			Terminals.JsonQuery,
			Terminals.JsonValue,
			//Terminals.XmlAggregate,
			Terminals.Cast,
			Terminals.XmlCast,
			Terminals.XmlElement,
			Terminals.XmlForest,
			Terminals.XmlParse,
			Terminals.XmlQuery,
			Terminals.XmlRoot,
			Terminals.XmlSerialize,
			Terminals.JsonQuery,
			Terminals.JsonValue,
			Terminals.Count,
			Terminals.Trim,
			Terminals.CharacterCode,
			Terminals.RowNumberPseudocolumn,
			Terminals.ListAggregation,
			NonTerminals.DataType,
			NonTerminals.AggregateFunction,
			NonTerminals.AnalyticFunction,
			NonTerminals.WithinGroupAggregationFunction,
			NonTerminals.ConversionFunction
		};

		private readonly List<OracleInsertTarget> _insertTargets = new List<OracleInsertTarget>();
		private readonly List<OracleLiteral> _literals = new List<OracleLiteral>();
		private readonly Dictionary<StatementGrammarNode, OracleJoinDescription> _joinTableReferenceNodes = new Dictionary<StatementGrammarNode, OracleJoinDescription>();
		private readonly Dictionary<StatementGrammarNode, OracleDataObjectReference> _rootNodeObjectReference = new Dictionary<StatementGrammarNode, OracleDataObjectReference>();
		private readonly HashSet<StatementGrammarNode> _redundantTerminals = new HashSet<StatementGrammarNode>();
		private readonly List<RedundantTerminalGroup> _redundantTerminalGroups = new List<RedundantTerminalGroup>();
		private readonly OracleDatabaseModelBase _databaseModel;
		private readonly Dictionary<OracleSelectListColumn, List<OracleDataObjectReference>> _asteriskTableReferences = new Dictionary<OracleSelectListColumn, List<OracleDataObjectReference>>();
		private readonly Dictionary<OracleQueryBlock, ICollection<StatementGrammarNode>> _accessibleQueryBlockRoot = new Dictionary<OracleQueryBlock, ICollection<StatementGrammarNode>>();
		private readonly Dictionary<OracleDataObjectReference, ICollection<KeyValuePair<StatementGrammarNode, string>>> _objectReferenceCteRootNodes = new Dictionary<OracleDataObjectReference, ICollection<KeyValuePair<StatementGrammarNode, string>>>();
		private readonly Dictionary<OracleReference, OracleTableCollectionReference> _rowSourceTableCollectionReferences = new Dictionary<OracleReference, OracleTableCollectionReference>();
		private readonly Dictionary<StatementGrammarNode, OracleDataObjectReference> _joinPartitionColumnTableReferenceRootNodes = new Dictionary<StatementGrammarNode, OracleDataObjectReference>();
		private readonly HashSet<OracleQueryBlock> _unreferencedQueryBlocks = new HashSet<OracleQueryBlock>();
		private readonly Dictionary<StatementGrammarNode, StatementGrammarNode> _oldOuterJoinColumnReferences = new Dictionary<StatementGrammarNode, StatementGrammarNode>();

		protected readonly Dictionary<StatementGrammarNode, OracleQueryBlock> QueryBlockNodes = new Dictionary<StatementGrammarNode, OracleQueryBlock>();

		private OracleQueryBlock _mainQueryBlock;

		protected CancellationToken CancellationToken = CancellationToken.None;

		private StatementGrammarNode DmlRootNode => String.Equals(Statement.RootNode.Id, NonTerminals.StandaloneStatement) ? Statement.RootNode[0, 0] : Statement.RootNode;

		public OracleDatabaseModelBase DatabaseModel
		{
			get
			{
				if (_databaseModel == null)
				{
					throw new InvalidOperationException("This model does not include database model reference. ");
				}

				return _databaseModel;
			}
		}

		IDatabaseModel IStatementSemanticModel.DatabaseModel => DatabaseModel;

		public OracleStatement Statement { get; }

		StatementBase IStatementSemanticModel.Statement => Statement;

		public string StatementText { get; private set; }
		
		public bool HasDatabaseModel => _databaseModel != null && _databaseModel.IsInitialized;

		public ICollection<OracleQueryBlock> QueryBlocks => QueryBlockNodes.Values;

		public ICollection<OracleInsertTarget> InsertTargets => _insertTargets;

		public virtual ICollection<RedundantTerminalGroup> RedundantSymbolGroups => _redundantTerminalGroups.AsReadOnly();

		public OracleMainObjectReferenceContainer MainObjectReferenceContainer { get; }

		public IReadOnlyList<StatementGrammarNode> NonQueryBlockTerminals { get; private set; }

		public virtual IEnumerable<OracleReferenceContainer> AllReferenceContainers
		{
			get
			{
				return QueryBlockNodes.Values
					.SelectMany(qb => Enumerable.Repeat(qb, 1).Concat(qb.ChildContainers))
					.Concat(_insertTargets)
					.Concat(Enumerable.Repeat(MainObjectReferenceContainer, 1));
			}
		}

		public virtual OracleQueryBlock MainQueryBlock
		{
			get
			{
				return
					_mainQueryBlock ??
					(_mainQueryBlock = QueryBlockNodes.Values
						.Where(qb => qb.Type == QueryBlockType.Normal)
						.OrderBy(qb => qb.RootNode.Level)
						.FirstOrDefault());
			}
		}

		public IEnumerable<OracleLiteral> Literals => _literals;

		protected internal OracleStatementSemanticModel(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel)
		{
			StatementText = statementText;
			Statement = statement ?? throw new ArgumentNullException(nameof(statement));
			_databaseModel = databaseModel ?? throw new ArgumentNullException(nameof(databaseModel));

			MainObjectReferenceContainer = new OracleMainObjectReferenceContainer(this);

			NonQueryBlockTerminals = StatementGrammarNode.EmptyArray;
		}

		protected void Initialize()
		{
			var queryBlockTerminalListQueue = new Stack<KeyValuePair<OracleQueryBlock, List<StatementGrammarNode>>>();
			var queryBlockTerminalList = new KeyValuePair<OracleQueryBlock, List<StatementGrammarNode>>();
			var statementDefinedFunctions = new Dictionary<StatementGrammarNode, IReadOnlyCollection<OracleProgramMetadata>>();
			var queryBlockNodes = new Dictionary<StatementGrammarNode, OracleQueryBlock>();
			var nonQueryBlockTerminals = new List<StatementGrammarNode>();

			foreach (var terminal in Statement.AllTerminals)
			{
				StatementGrammarNode plSqlIdentifier = null;
				if (String.Equals(terminal.Id, Terminals.Select) && String.Equals(terminal.ParentNode.Id, NonTerminals.QueryBlock))
				{
					var queryBlock = new OracleQueryBlock(Statement, terminal.ParentNode, this);
					queryBlockNodes.Add(queryBlock.RootNode, queryBlock);

					if (queryBlockTerminalList.Key != null)
					{
						queryBlockTerminalListQueue.Push(queryBlockTerminalList);
					}

					queryBlockTerminalList = new KeyValuePair<OracleQueryBlock, List<StatementGrammarNode>>(queryBlock, new List<StatementGrammarNode>());
					queryBlock.Terminals = queryBlockTerminalList.Value;
				}
				else if ((String.Equals(terminal.Id, Terminals.Date) && String.Equals(terminal.ParentNode.Id, NonTerminals.Expression)) ||
				         String.Equals(terminal.Id, Terminals.Timestamp) && String.Equals(terminal.ParentNode.Id, NonTerminals.TimestampOrTime) ||
				         String.Equals(terminal.Id, Terminals.Interval) && String.Equals(terminal.ParentNode.Id, NonTerminals.IntervalExpression))
				{
					var literal = CreateLiteral(terminal);
					if (literal.Terminal != null && String.Equals(literal.Terminal.Id, Terminals.StringLiteral))
					{
						_literals.Add(literal);
					}
				}
				else if (String.Equals(terminal.Id, Terminals.With) && String.Equals(terminal.ParentNode.Id, NonTerminals.SubqueryFactoringClause))
				{
					var nestedQueryNode = terminal.ParentNode.ParentNode;
					var queryBlockRoot = GetQueryBlockRootFromNestedQuery(nestedQueryNode);
					if (queryBlockRoot != null)
					{
						var subqueryDefinedFunctions = terminal.ParentNode.GetDescendants(NonTerminals.PlSqlDeclarations)
							.Select(d => d[NonTerminals.FunctionDefinition, NonTerminals.FunctionHeading, Terminals.Identifier])
							.Where(i => i != null)
							.Select(ResolveProgramMetadataFromProgramDefinition);

						statementDefinedFunctions.Add(queryBlockRoot, subqueryDefinedFunctions.ToArray());
					}
				}
				else if (String.Equals(terminal.Id, Terminals.MathPlus) && String.Equals(terminal.ParentNode.Id, NonTerminals.OuterJoinOld) &&
				         String.Equals(terminal.ParentNode.ParentNode.Id, NonTerminals.ColumnReference))
				{
					_oldOuterJoinColumnReferences.Add(terminal.ParentNode.ParentNode.ParentNode, terminal.ParentNode);
				}
				else if (String.Equals(terminal.Id, Terminals.PlSqlIdentifier) && String.Equals(terminal.ParentNode.Id, NonTerminals.PlSqlAssignmentTarget))
				{
					plSqlIdentifier = terminal;
				}

				OracleReferenceContainer targetReferenceContainer;
				if (queryBlockTerminalList.Key != null)
				{
					targetReferenceContainer = queryBlockTerminalList.Key;
					queryBlockTerminalList.Value.Add(terminal);

					if (String.Equals(terminal.Id, Terminals.Join) &&
					    (String.Equals(terminal.ParentNode.Id, NonTerminals.InnerJoinClause) || String.Equals(terminal.Id, NonTerminals.OuterJoinClause)))
					{
						queryBlockTerminalList.Key.ContainsAnsiJoin = true;
					}

					if (terminal == queryBlockTerminalList.Key.RootNode.LastTerminalNode && queryBlockTerminalListQueue.Count > 0)
					{
						queryBlockTerminalList = queryBlockTerminalListQueue.Pop();
					}
				}
				else
				{
					targetReferenceContainer = MainObjectReferenceContainer;
					nonQueryBlockTerminals.Add(terminal);
				}

				if (plSqlIdentifier != null)
				{
					var plSqlReference =
						new OraclePlSqlVariableReference
						{
							Container = targetReferenceContainer,
							RootNode = plSqlIdentifier.ParentNode,
							IdentifierNode = plSqlIdentifier,
							Owner = targetReferenceContainer as OracleQueryBlock
						};

					targetReferenceContainer.PlSqlVariableReferences.Add(plSqlReference);
				}
			}

			NonQueryBlockTerminals = nonQueryBlockTerminals.AsReadOnly();

			foreach (var queryBlock in queryBlockNodes.Values)
			{
				var queryBlockRoot = queryBlock.RootNode;

				var commonTableExpression = queryBlockRoot.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.CommonTableExpression);
				if (commonTableExpression != null)
				{
					queryBlock.AliasNode = commonTableExpression.ChildNodes[0];
					queryBlock.Type = QueryBlockType.CommonTableExpression;
					var nestedQuery = queryBlockRoot.GetAncestor(NonTerminals.NestedQuery);
					var ownerQueryBlockRoot = GetQueryBlockRootFromNestedQuery(nestedQuery);
					if (ownerQueryBlockRoot != null)
					{
						queryBlock.Parent = queryBlockNodes[ownerQueryBlockRoot];
						queryBlock.Parent.AddCommonTableExpressions(queryBlock);
					}
				}
				else
				{
					var selfTableReference = queryBlockRoot.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.Expression), NonTerminals.TableReference);
					if (selfTableReference != null)
					{
						queryBlock.Type = QueryBlockType.Normal;
						queryBlock.AliasNode = selfTableReference[Terminals.ObjectAlias];
						var parentQueryBlockRoot = selfTableReference.GetAncestor(NonTerminals.QueryBlock);
						if (parentQueryBlockRoot != null)
						{
							queryBlock.Parent = queryBlockNodes[parentQueryBlockRoot];
						}
					}
					else
					{
						var isNotWithinExpressionList = queryBlockRoot.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.TableReference), NonTerminals.ExpressionList) == null;
						var scalarSubqueryExpression = queryBlockRoot.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.TableReference), NonTerminals.Expression);
						if (isNotWithinExpressionList && scalarSubqueryExpression != null)
						{
							queryBlock.Type = scalarSubqueryExpression[Terminals.Cursor] == null
								? QueryBlockType.ScalarSubquery
								: QueryBlockType.CursorParameter;

							var parentQueryBlockNode = scalarSubqueryExpression.GetAncestor(NonTerminals.QueryBlock);
							if (parentQueryBlockNode != null)
							{
								queryBlock.Parent = queryBlockNodes[parentQueryBlockNode];
							}
						}
					}
				}
			}

			foreach (var kvp in statementDefinedFunctions)
			{
				queryBlockNodes[kvp.Key].AttachedFunctions.AddRange(kvp.Value);
			}

			var normalQueryBlocks = queryBlockNodes.Values
				.Where(qb => qb.Type != QueryBlockType.CommonTableExpression || qb.Parent == null)
				.OrderBy(qb => qb.Type)
				.ThenByDescending(qb => qb.RootNode.Level)
				.ToHashSet();

			var commonTableExpressions = normalQueryBlocks
				.SelectMany(qb => qb.CommonTableExpressions.OrderBy(cte => cte.RootNode.SourcePosition.IndexStart))
				.ToArray();

			foreach (var commonTableExpression in commonTableExpressions)
			{
				var childQueryBlocks = normalQueryBlocks
					.Where(qb => qb.RootNode.HasAncestor(commonTableExpression.RootNode))
					.ToList();

				foreach (var queryBlock in childQueryBlocks)
				{
					QueryBlockNodes.Add(queryBlock.RootNode, queryBlock);
					normalQueryBlocks.Remove(queryBlock);
				}

				QueryBlockNodes.Add(commonTableExpression.RootNode, commonTableExpression);
			}

			foreach (var queryBlock in normalQueryBlocks)
			{
				QueryBlockNodes.Add(queryBlock.RootNode, queryBlock);
			}
		}

		private static string ResolveParameterType(StatementGrammarNode sourceNode)
		{
			var returnParameterNode = sourceNode[NonTerminals.PlSqlDataTypeWithoutConstraint];
			return returnParameterNode == null
				? String.Empty
				: String.Concat(returnParameterNode.Terminals.Select(t => t.Token.Value));
		}

		private static OracleProgramMetadata ResolveProgramMetadataFromProgramDefinition(StatementGrammarNode identifier)
		{
			var metadata = new OracleProgramMetadata(ProgramType.StatementFunction, OracleProgramIdentifier.CreateFromValues(null, null, identifier.Token.Value), false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeNormal, false);

			var returnParameterType = ResolveParameterType(identifier.ParentNode);

			metadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, returnParameterType, OracleObjectIdentifier.Empty, false));

			var parameterDeckarations = identifier.ParentNode[NonTerminals.ParenthesisEnclosedParameterDeclarationList, NonTerminals.ParameterDeclarationList];
			if (parameterDeckarations == null)
			{
				return metadata;
			}

			var parameterIndex = 0;
			foreach (var parameterDeclaration in parameterDeckarations.GetDescendants(NonTerminals.ParameterDeclaration))
			{
				parameterIndex++;
				var parameterName = parameterDeclaration[Terminals.ParameterIdentifier].Token.Value.ToQuotedIdentifier();
				var parameterDirectionDeclaration = parameterDeclaration[NonTerminals.ParameterDirectionDeclaration];

				var direction = ParameterDirection.Input;
				var isOptional = false;
				var parameterType = String.Empty;

				if (parameterDirectionDeclaration != null)
				{
					if (parameterDirectionDeclaration[Terminals.Out] != null)
					{
						direction = ParameterDirection.Output;
					}

					if (direction == ParameterDirection.Output && parameterDeclaration[Terminals.In] != null)
					{
						direction = ParameterDirection.InputOutput;
					}

					isOptional = parameterDirectionDeclaration[NonTerminals.VariableDeclarationDefaultValue] != null;

					parameterType = ResolveParameterType(parameterDirectionDeclaration);
				}

				metadata.AddParameter(new OracleProgramParameterMetadata(parameterName, parameterIndex, parameterIndex, 0, direction, parameterType, OracleObjectIdentifier.Empty, isOptional));
			}

			return metadata;
		}

		private static StatementGrammarNode GetQueryBlockRootFromNestedQuery(StatementGrammarNode ownerQueryNode)
		{
			var subquery = ownerQueryNode?[NonTerminals.Subquery];
			return subquery?.GetDescendants(NonTerminals.QueryBlock).First();
		}

		internal OracleStatementSemanticModel Build(CancellationToken cancellationToken)
		{
			CancellationToken = cancellationToken;
			Build();
			return this;
		}

		protected virtual void Build()
		{
			Initialize();

			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				FindObjectReferences(queryBlock);

				FindSelectListReferences(queryBlock);

				FindWhereGroupByHavingReferences(queryBlock);

				FindJoinColumnReferences(queryBlock);

				FindHierarchicalClauseReferences(queryBlock);
			}

			ResolveInlineViewOrCommonTableExpressionRelations();

			FindRecursiveQueryReferences();

			ResolvePivotClauses();

			ResolveModelClause();

			ResolveReferences();

			HarmonizeConcatenatedQueryBlockColumnTypes();

			BuildDmlModel();
			
			ResolveRedundantTerminals();
		}

		private void HarmonizeConcatenatedQueryBlockColumnTypes()
		{
			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				if (queryBlock.PrecedingConcatenatedQueryBlock != null || queryBlock.FollowingConcatenatedQueryBlock == null)
				{
					continue;
				}

				var columnCount = queryBlock.Columns.Count - queryBlock.AsteriskColumns.Count - queryBlock.AttachedColumns.Count;
				var columns = queryBlock.Columns.Where(c => !c.IsAsterisk).Take(columnCount).ToArray();
				foreach (var concatenatedQueryBlock in queryBlock.AllFollowingConcatenatedQueryBlocks)
				{
					var index = 0;
					foreach (var column in concatenatedQueryBlock.Columns)
					{
						if (column.IsAsterisk || columns.Length == index)
						{
							continue;
						}

						var outputColumn = columns[index];
						if (outputColumn == null)
						{
							continue;
						}

						var columnDataType = column.ColumnDescription.DataType;
						var outputColumnDescription = outputColumn.ColumnDescription;
						outputColumnDescription.Nullable |= column.ColumnDescription.Nullable;

						MatchInterchangeableDataTypes(columnDataType, outputColumnDescription.DataType);

						if (columnDataType.FullyQualifiedName == outputColumnDescription.DataType.FullyQualifiedName)
						{
							if (columnDataType.Length > outputColumnDescription.DataType.Length)
							{
								outputColumnDescription.DataType.Length = columnDataType.Length;
							}
							else if (outputColumnDescription.DataType.Length > columnDataType.Length)
							{
								columnDataType.Length = outputColumnDescription.DataType.Length;
							}

							if (column.ColumnDescription.CharacterSize > outputColumnDescription.CharacterSize)
							{
								outputColumnDescription.CharacterSize = column.ColumnDescription.CharacterSize;
							}
							else if (outputColumnDescription.CharacterSize > column.ColumnDescription.CharacterSize)
							{
								column.ColumnDescription.CharacterSize = outputColumnDescription.CharacterSize;
							}
						}
						else if (String.IsNullOrEmpty(columnDataType.FullyQualifiedName.Name))
						{
							var expression = column.RootNode[0, 0];
							if (expression != null && expression.TerminalCount > 1)
							{
								outputColumnDescription.DataType = OracleDataType.Empty;
								break;
							}
						}
						else if (String.IsNullOrEmpty(outputColumnDescription.DataType.FullyQualifiedName.Name))
						{
							var expression = outputColumn.RootNode[0, 0];
							if (expression != null && expression.TerminalCount == 1 && String.Equals(expression.FirstTerminalNode.Id, Terminals.Null))
							{
								outputColumnDescription.DataType = columnDataType;
							}
						}

						if (++index == columnCount)
						{
							break;
						}
					}
				}
			}
		}

		private static void MatchInterchangeableDataTypes(OracleObject columnDataType, OracleObject outputDataType)
		{
			if (String.Equals(outputDataType.FullyQualifiedName.Name, TerminalValues.Char) &&
			    (String.Equals(columnDataType.FullyQualifiedName.Name, TerminalValues.Varchar) ||
			     String.Equals(columnDataType.FullyQualifiedName.Name, TerminalValues.Varchar2)))
			{
				outputDataType.FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Varchar2);
			}
		}

		private void FindObjectReferences(OracleQueryBlock queryBlock)
		{
			var queryBlockRoot = queryBlock.RootNode;
			var tableReferenceNonterminals =
				queryBlock.FromClause?.GetDescendantsWithinSameQueryBlock(NonTerminals.TableReference).Where(n => n[NonTerminals.TableReference] == null)
				?? StatementGrammarNode.EmptyArray;

			var cteReferences = ResolveAccessibleCommonTableExpressions(queryBlockRoot).ToDictionary(qb => qb.CteNode, qb => qb.CteAlias);
			_accessibleQueryBlockRoot.Add(queryBlock, cteReferences.Keys);

			foreach (var tableReferenceNonterminal in tableReferenceNonterminals)
			{
				var queryTableExpression = tableReferenceNonterminal.GetDescendantsWithinSameQueryBlock(NonTerminals.QueryTableExpression).SingleOrDefault();
				if (queryTableExpression == null)
				{
					var specialTableReference = ResolveXmlTableReference(queryBlock, tableReferenceNonterminal) ?? ResolveJsonTableReference(queryBlock, tableReferenceNonterminal);
					if (specialTableReference != null)
					{
						specialTableReference.RootNode = tableReferenceNonterminal;
						specialTableReference.AliasNode = tableReferenceNonterminal[NonTerminals.InnerSpecialTableReference].GetSingleDescendant(Terminals.ObjectAlias);
						_rootNodeObjectReference.Add(specialTableReference.RootNode, specialTableReference);
					}
				}
				else
				{
					var objectReferenceAlias = tableReferenceNonterminal[Terminals.ObjectAlias];
					var databaseLinkNode = GetDatabaseLinkFromQueryTableExpression(queryTableExpression);

					var tableCollection = queryTableExpression[NonTerminals.TableCollectionExpression];
					if (tableCollection != null)
					{
						var expression = tableCollection.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.Expression).FirstOrDefault();
						if (expression != null)
						{
							var identifierNodes = expression.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.User, Terminals.DataTypeIdentifier).ToList();
							var grammarSpecificProgramReferences = CreateGrammarSpecificFunctionReferences(GetGrammarSpecificFunctionNodes(expression), queryBlock, queryBlock, StatementPlacement.TableReference, null);
							if (identifierNodes.Count > 0 || grammarSpecificProgramReferences.Count > 0)
							{
								OracleReference tableCollectionReference;
								var identifierNode = identifierNodes.FirstOrDefault();
								if (grammarSpecificProgramReferences.Count == 0 ||
								    identifierNode.SourcePosition.IndexStart < (tableCollectionReference = grammarSpecificProgramReferences[0]).RootNode.SourcePosition.IndexStart)
								{
									var prefixNonTerminal = identifierNode.ParentNode.ParentNode[NonTerminals.Prefix];
									tableCollectionReference = ResolveColumnFunctionOrDataTypeReferenceFromIdentifier(queryBlock, queryBlock, identifierNode, StatementPlacement.TableReference, null, n => prefixNonTerminal, null);
									identifierNodes.RemoveAt(0);
								}

								var tableCollectionDataObjectReference =
									new OracleTableCollectionReference(queryBlock)
									{
										RowSourceReference = tableCollectionReference,
										AliasNode = objectReferenceAlias,
										DatabaseLinkNode = databaseLinkNode,
										RootNode = tableReferenceNonterminal
									};

								_rootNodeObjectReference.Add(tableCollectionDataObjectReference.RootNode, tableCollectionDataObjectReference);
								_rowSourceTableCollectionReferences.Add(tableCollectionReference, tableCollectionDataObjectReference);

								ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, identifierNodes, StatementPlacement.TableReference, null);
							}
						}

						continue;
					}

					var nestedQueryTableReference = queryTableExpression.GetPathFilterDescendants(f => !String.Equals(f.Id, NonTerminals.Subquery), NonTerminals.NestedQuery).SingleOrDefault();
					if (nestedQueryTableReference != null)
					{
						var nestedQueryTableReferenceQueryBlock = nestedQueryTableReference.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.NestedQuery) && !String.Equals(n.Id, NonTerminals.SubqueryFactoringClause), NonTerminals.QueryBlock).FirstOrDefault();
						if (nestedQueryTableReferenceQueryBlock != null)
						{
							var objectReference =
								new OracleDataObjectReference(ReferenceType.InlineView)
								{
									Container = queryBlock,
									Owner = queryBlock,
									RootNode = tableReferenceNonterminal,
									ObjectNode = nestedQueryTableReferenceQueryBlock,
									AliasNode = objectReferenceAlias,
									IsLateral = queryTableExpression[Terminals.Lateral] != null
								};

							queryBlock.ObjectReferences.Add(objectReference);
							_rootNodeObjectReference.Add(objectReference.RootNode, objectReference);
						}

						var identifiers = queryTableExpression.GetPathFilterDescendants(n => n != nestedQueryTableReference && !String.Equals(n.Id, NonTerminals.NestedQuery), Terminals.Identifier, Terminals.User, Terminals.Trim, Terminals.CharacterCode);
						ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, identifiers, StatementPlacement.TableReference, null);

						continue;
					}

					var objectIdentifierNode = queryTableExpression[NonTerminals.SchemaObject, Terminals.ObjectIdentifier];
					if (objectIdentifierNode != null)
					{
						ICollection<KeyValuePair<StatementGrammarNode, string>> commonTableExpressions = new Dictionary<StatementGrammarNode, string>();
						var schemaTerminal = objectIdentifierNode.ParentNode[NonTerminals.SchemaPrefix];
					    schemaTerminal = schemaTerminal?.ChildNodes[0];

					    var tableName = objectIdentifierNode.Token.Value.ToQuotedIdentifier();
						if (schemaTerminal == null)
						{
							commonTableExpressions.AddRange(cteReferences.Where(n => n.Value == tableName));
						}

						OracleSchemaObject localSchemaObject = null;
						var referenceType = ReferenceType.CommonTableExpression;
						if (commonTableExpressions.Count == 0)
						{
							referenceType = ReferenceType.SchemaObject;

							var objectName = objectIdentifierNode.Token.Value;
							var owner = schemaTerminal?.Token.Value;

							if (HasDatabaseModel)
							{
								localSchemaObject = _databaseModel.GetFirstSchemaObject<OracleDataObject>(_databaseModel.GetPotentialSchemaObjectIdentifiers(owner, objectName));
							}
						}

						var objectReference =
							new OracleDataObjectReference(referenceType)
							{
								Container = queryBlock,
								Owner = queryBlock,
								RootNode = tableReferenceNonterminal,
								OwnerNode = schemaTerminal,
								ObjectNode = objectIdentifierNode,
								DatabaseLinkNode = databaseLinkNode,
								AliasNode = objectReferenceAlias,
								SchemaObject = databaseLinkNode == null ? localSchemaObject : null
							};

						queryBlock.ObjectReferences.Add(objectReference);
						_rootNodeObjectReference.Add(objectReference.RootNode, objectReference);

						if (commonTableExpressions.Count > 0)
						{
							_objectReferenceCteRootNodes[objectReference] = commonTableExpressions;
						}

						objectReference.FlashbackClauseNode = objectReference.RootNode.GetSingleDescendant(NonTerminals.FlashbackQueryClause);

						FindExplicitPartitionReferences(queryTableExpression, objectReference);
					}
				}
			}
		}

		private void ResolvePivotClauses()
		{
			var objectReferences = QueryBlockNodes.Values.SelectMany(qb => qb.ObjectReferences).ToArray();
			foreach (var objectReference in objectReferences)
			{
				ResolvePivotClause(objectReference);
			}
		}

		private void ResolvePivotClause(OracleDataObjectReference objectReference)
		{
			var pivotClause = objectReference.RootNode.GetDescendantsWithinSameQueryBlock(NonTerminals.PivotClause, NonTerminals.UnpivotClause).SingleOrDefault();
			if (pivotClause == null)
			{
				return;
			}

			var pivotTableReference =
				new OraclePivotTableReference(this, objectReference, pivotClause)
				{
					AliasNode = pivotClause[Terminals.ObjectAlias]
				};

			var identifierSourceNode = String.Equals(pivotClause.Id, NonTerminals.PivotClause) ? pivotClause : pivotClause[NonTerminals.UnpivotInClause];
			if (identifierSourceNode != null)
			{
				var pivotClauseIdentifiers = GetIdentifiers(identifierSourceNode, Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.User, Terminals.Trim, Terminals.CharacterCode);
				ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(pivotTableReference.Owner, pivotTableReference.SourceReferenceContainer, pivotClauseIdentifiers, StatementPlacement.PivotClause, null);
			}

			var pivotExpressions = pivotClause[NonTerminals.PivotAliasedAggregationFunctionList];
			if (pivotExpressions != null)
			{
				var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(pivotExpressions);
				CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, pivotTableReference.SourceReferenceContainer, pivotTableReference.Owner, StatementPlacement.PivotClause, null);
			}

			foreach (var kvp in _asteriskTableReferences)
			{
				var index = kvp.Value.IndexOf(objectReference);
				if (kvp.Value.Count == 0)
				{
					if (kvp.Key.ColumnReferences[0].FullyQualifiedObjectName == pivotTableReference.FullyQualifiedObjectName)
					{
						kvp.Value.Add(pivotTableReference);
					}
				}
				else if (index != -1)
				{
					kvp.Value.RemoveAt(index);
					kvp.Value.Insert(index, pivotTableReference);
				}
			}
		}

		private static void FindExplicitPartitionReferences(StatementGrammarNode queryTableExpression, OracleDataObjectReference objectReference)
		{
			var explicitPartitionIdentifier = queryTableExpression[NonTerminals.PartitionOrDatabaseLink, NonTerminals.PartitionExtensionClause, NonTerminals.PartitionNameOrKeySet, Terminals.ObjectIdentifier];
			if (explicitPartitionIdentifier == null)
			{
				return;
			}

			var table = (OracleTable)objectReference.SchemaObject.GetTargetSchemaObject();

			objectReference.PartitionReference =
				new OraclePartitionReference
				{
					Container = objectReference.Container,
					RootNode = explicitPartitionIdentifier.ParentNode.ParentNode,
					ObjectNode = explicitPartitionIdentifier,
				};

			var partitionName = objectReference.PartitionReference.Name.ToQuotedIdentifier();
			if (table == null)
			{
				return;
			}

			var isSubPartition = String.Equals(objectReference.PartitionReference.RootNode.FirstTerminalNode.Id, Terminals.Subpartition);
			if (isSubPartition)
			{
				OracleSubPartition subPartition = null;
				table.Partitions.Values.FirstOrDefault(p => p.SubPartitions.TryGetValue(partitionName, out subPartition));
				objectReference.PartitionReference.Partition = subPartition;
			}
			else
			{
				table.Partitions.TryGetValue(partitionName, out OraclePartition partition);
				objectReference.PartitionReference.Partition = partition;
			}

			objectReference.PartitionReference.DataObjectReference = objectReference;
		}

		private void FindRecursiveQueryReferences()
		{
			foreach (var queryBlock in QueryBlockNodes.Values.Where(qb => qb.Type == QueryBlockType.CommonTableExpression && qb.AliasNode != null))
			{
				FindRecusiveSearchReferences(queryBlock);
				FindRecusiveCycleReferences(queryBlock);
			}
		}

		private void FindRecusiveCycleReferences(OracleQueryBlock queryBlock)
		{
			var subqueryComponentNode = queryBlock.RootNode.GetAncestor(NonTerminals.CommonTableExpression);
			queryBlock.RecursiveCycleClause = subqueryComponentNode[NonTerminals.SubqueryFactoringCycleClause];

			var identifierListNode = queryBlock.RecursiveCycleClause?[NonTerminals.IdentifierList];
			if (identifierListNode == null)
			{
				return;
			}

			var recursiveCycleClauseIdentifiers = identifierListNode.GetDescendantsWithinSameQueryBlock(Terminals.Identifier);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, recursiveCycleClauseIdentifiers, StatementPlacement.RecursiveSearchOrCycleClause, null);

			var herarchicalQueryClauseGrammarSpecificFunctions = GetGrammarSpecificFunctionNodes(identifierListNode);
			CreateGrammarSpecificFunctionReferences(herarchicalQueryClauseGrammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.RecursiveSearchOrCycleClause, null);

			var cycleColumnAlias = queryBlock.RecursiveCycleClause[Terminals.ColumnAlias];
			if (!queryBlock.IsRecursive || cycleColumnAlias == null)
			{
				return;
			}

			var recursiveSequenceColumn =
				new OracleSelectListColumn(this, null)
				{
					Owner = queryBlock,
					RootNode = cycleColumnAlias,
					AliasNode = cycleColumnAlias,
					ColumnDescription =
						new OracleColumn
						{
							Name = cycleColumnAlias.Token.Value.ToQuotedIdentifier(),
							DataType = new OracleDataType { Length = 1, FullyQualifiedName = new OracleObjectIdentifier(String.Empty, "VARCHAR2") }
						}
				};

			queryBlock.AddAttachedColumn(recursiveSequenceColumn);
		}

		private void FindRecusiveSearchReferences(OracleQueryBlock queryBlock)
		{
			var subqueryComponentNode = queryBlock.RootNode.GetAncestor(NonTerminals.CommonTableExpression);
			queryBlock.RecursiveSearchClause = subqueryComponentNode[NonTerminals.SubqueryFactoringSearchClause];

			var orderExpressionListNode = queryBlock.RecursiveSearchClause?[NonTerminals.OrderExpressionList];
			if (orderExpressionListNode == null)
			{
				return;
			}

			var recursiveSearchClauseIdentifiers = orderExpressionListNode.GetDescendantsWithinSameQueryBlock(Terminals.Identifier);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, recursiveSearchClauseIdentifiers, StatementPlacement.RecursiveSearchOrCycleClause, null);

			var herarchicalQueryClauseGrammarSpecificFunctions = GetGrammarSpecificFunctionNodes(orderExpressionListNode);
			CreateGrammarSpecificFunctionReferences(herarchicalQueryClauseGrammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.RecursiveSearchOrCycleClause, null);

			if (!queryBlock.IsRecursive || !String.Equals(queryBlock.RecursiveSearchClause.LastTerminalNode.Id, Terminals.ColumnAlias))
			{
				return;
			}

			var recursiveSequenceColumn =
				new OracleSelectListColumn(this, null)
				{
					Owner = queryBlock,
					RootNode = queryBlock.RecursiveSearchClause.LastTerminalNode,
					AliasNode = queryBlock.RecursiveSearchClause.LastTerminalNode,
					ColumnDescription =
						new OracleColumn
						{
							Name = queryBlock.RecursiveSearchClause.LastTerminalNode.Token.Value.ToQuotedIdentifier(),
							DataType = OracleDataType.NumberType
						}
				};

			queryBlock.AddAttachedColumn(recursiveSequenceColumn);
		}

		private OracleSpecialTableReference ResolveJsonTableReference(OracleQueryBlock queryBlock, StatementGrammarNode tableReferenceNonterminal)
		{
			var jsonTableClause = tableReferenceNonterminal.GetDescendantsWithinSameQueryBlock(NonTerminals.JsonTableClause).SingleOrDefault();
			if (jsonTableClause == null)
			{
				return null;
			}

			var columns = new List<OracleSelectListColumn>();
			foreach (var jsonTableColumn in jsonTableClause.GetDescendants(NonTerminals.JsonColumnDefinition).Where(n => n.TerminalCount >= 1 && String.Equals(n.FirstTerminalNode.Id, Terminals.ColumnAlias)))
			{
				var columnAlias = jsonTableColumn.FirstTerminalNode.Token.Value.ToQuotedIdentifier();
				var column =
					new OracleSelectListColumn(this, null)
					{
						RootNode = jsonTableColumn,
						AliasNode = jsonTableColumn.FirstTerminalNode,
						ColumnDescription =
							new OracleColumn
							{
								Name = columnAlias,
								Nullable = true
							}
					};

				columns.Add(column);

				var columnDescription = column.ColumnDescription;
				if (TryAssingnColumnForOrdinality(columnDescription, jsonTableColumn.ChildNodes.Skip(1)))
				{
					continue;
				}

				var jsonReturnTypeNode = jsonTableColumn[NonTerminals.JsonDataType];
				columnDescription.DataType = OracleReferenceBuilder.ResolveDataTypeFromJsonDataTypeNode(jsonReturnTypeNode);

				var dataTypeNode = jsonReturnTypeNode?[NonTerminals.DataType];
				if (dataTypeNode != null)
				{
					var dataTypeIdentifier =
						dataTypeNode[NonTerminals.SchemaDatatype, Terminals.DataTypeIdentifier]
						?? dataTypeNode[NonTerminals.BuiltInDataType]?[0];

					if (dataTypeIdentifier != null)
					{
						ResolveDataTypeReference(queryBlock, queryBlock, dataTypeIdentifier, StatementPlacement.TableReference, null);
					}
				}

				if (columnDescription.DataType.Length != null || !String.IsNullOrEmpty(columnDescription.DataType.FullyQualifiedName.Owner))
				{
					continue;
				}

				switch (columnDescription.DataType.FullyQualifiedName.Name)
				{
					case TerminalValues.Varchar2:
						columnDescription.DataType.Length = DatabaseModel.MaximumVarcharLength;
						break;
					case TerminalValues.NVarchar2:
						columnDescription.DataType.Length = DatabaseModel.MaximumNVarcharLength;
						break;
					case TerminalValues.Raw:
						columnDescription.DataType.Length = DatabaseModel.MaximumRawLength;
						break;
				}
			}

			var inputExpression = jsonTableClause[NonTerminals.Expression];
			if (inputExpression != null)
			{
				var identifiers = GetIdentifiers(inputExpression, Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.User, Terminals.Trim, Terminals.CharacterCode);
				ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, queryBlock, identifiers, StatementPlacement.TableReference, null);
			}

			return new OracleSpecialTableReference(queryBlock, ReferenceType.JsonTable, columns, jsonTableClause[NonTerminals.JsonColumnsClause]) { Owner = queryBlock };
		}

		private OracleSpecialTableReference ResolveXmlTableReference(OracleQueryBlock queryBlock, StatementGrammarNode tableReferenceNonterminal)
		{
			var xmlTableClause = tableReferenceNonterminal.GetDescendantsWithinSameQueryBlock(NonTerminals.XmlTableClause).SingleOrDefault();
		    var xmlTableOptions = xmlTableClause?[NonTerminals.XmlTableOptions];
			if (xmlTableOptions == null)
			{
				return null;
			}

			var columns = new List<OracleSelectListColumn>();

			var columnListClause = xmlTableOptions[NonTerminals.XmlTableColumnListClause];
			if (columnListClause == null)
			{
				var column =
					new OracleSelectListColumn(this, null)
					{
						ColumnDescription = OracleColumn.BuildColumnValueColumn(OracleDataType.XmlType)
					};

				columns.Add(column);
			}
			else
			{
				foreach (var xmlTableColumn in columnListClause.GetDescendants(NonTerminals.XmlTableColumn).Where(n => n.TerminalCount >= 1 && n.FirstTerminalNode.Id == Terminals.ColumnAlias))
				{
					var columnAlias = xmlTableColumn.ChildNodes[0];

					var column =
						new OracleSelectListColumn(this, null)
						{
							RootNode = xmlTableColumn,
							AliasNode = columnAlias,
							ColumnDescription =
								new OracleColumn
								{
									Name = columnAlias.Token.Value.ToQuotedIdentifier(),
									Nullable = true,
									DataType = OracleDataType.Empty
								}
						};

					columns.Add(column);

					var xmlTableColumnDefinition = xmlTableColumn[NonTerminals.XmlTableColumnDefinition];
					if (xmlTableColumnDefinition != null && !TryAssingnColumnForOrdinality(column.ColumnDescription, xmlTableColumnDefinition.ChildNodes))
					{
						var dataTypeOrXmlTypeNode = xmlTableColumnDefinition[NonTerminals.DataTypeOrXmlType];
						if (dataTypeOrXmlTypeNode != null)
						{
							var dataTypeNode = dataTypeOrXmlTypeNode.ChildNodes[0];
							switch (dataTypeNode.Id)
							{
								case Terminals.XmlType:
									column.ColumnDescription.DataType = OracleDataType.XmlType;
									break;
								case NonTerminals.DataType:
									column.ColumnDescription.DataType = OracleReferenceBuilder.ResolveDataTypeFromNode(dataTypeNode);
									break;
							}
						}
					}
				}
			}

			var xmlTablePassingClause = xmlTableOptions[NonTerminals.XmlPassingClause, NonTerminals.ExpressionAsXmlAliasWithMandatoryAsList];
			if (xmlTablePassingClause != null)
			{
				var identifiers = GetIdentifiers(xmlTablePassingClause, Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.User, Terminals.Trim, Terminals.CharacterCode);
				ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, queryBlock, identifiers, StatementPlacement.TableReference, null);
			}

			return new OracleSpecialTableReference(queryBlock, ReferenceType.XmlTable, columns, columnListClause) { Owner = queryBlock };
		}

		private static bool TryAssingnColumnForOrdinality(OracleColumn column, IEnumerable<StatementGrammarNode> forOrdinalityTerminals)
		{
			var enumerator = forOrdinalityTerminals.GetEnumerator();
			if (enumerator.MoveNext() && enumerator.Current.Id == Terminals.For &&
			    enumerator.MoveNext() && enumerator.Current.Id == Terminals.Ordinality &&
			    !enumerator.MoveNext())
			{
				column.DataType = OracleDataType.NumberType;
				return true;
			}

			return false;
		}

		private void FindHierarchicalClauseReferences(OracleQueryBlock queryBlock)
		{
			if (queryBlock.HierarchicalQueryClause == null)
			{
				return;
			}

			var herarchicalQueryClauseIdentifiers = queryBlock.HierarchicalQueryClause.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, herarchicalQueryClauseIdentifiers, StatementPlacement.ConnectBy, null);

			var herarchicalQueryClauseGrammarSpecificFunctions = GetGrammarSpecificFunctionNodes(queryBlock.HierarchicalQueryClause);
			CreateGrammarSpecificFunctionReferences(herarchicalQueryClauseGrammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.ConnectBy, null);
		}

		private void ResolveModelClause()
		{
			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				var modelClause = queryBlock.RootNode[NonTerminals.ModelClause];

			    var modelColumnClauses = modelClause?[NonTerminals.MainModel, NonTerminals.ModelColumnClauses];
				if (modelColumnClauses == null || modelColumnClauses.ChildNodes.Count < 5)
				{
					continue;
				}

				var partitionExpressions = modelColumnClauses[NonTerminals.ModelColumnClausesPartitionByExpressionList, NonTerminals.ParenthesisEnclosedAliasedExpressionList];
				var sqlModelColumns = new List<OracleSelectListColumn>();
				if (partitionExpressions != null)
				{
					sqlModelColumns.AddRange(GatherSqlModelColumns(queryBlock, partitionExpressions));
				}

				var dimensionExpressionList = modelColumnClauses.ChildNodes[modelColumnClauses.ChildNodes.Count - 3];
				var dimensionColumns = GatherSqlModelColumns(queryBlock, dimensionExpressionList);
				sqlModelColumns.AddRange(dimensionColumns);
				
				var measureParenthesisEnclosedAliasedExpressionList = modelColumnClauses.ChildNodes[modelColumnClauses.ChildNodes.Count - 1];
				var measureColumns = GatherSqlModelColumns(queryBlock, measureParenthesisEnclosedAliasedExpressionList);
				sqlModelColumns.AddRange(measureColumns);

				queryBlock.ModelReference =
					new OracleSqlModelReference(this, sqlModelColumns, queryBlock.ObjectReferences, measureParenthesisEnclosedAliasedExpressionList)
					{
						Owner = queryBlock,
						RootNode = modelClause
					};

				var ruleDimensionIdentifiers = new List<StatementGrammarNode>();
				var ruleMeasureIdentifiers = new List<StatementGrammarNode>();
				var modelRulesClauseAssignmentList = modelColumnClauses.ParentNode[NonTerminals.ModelRulesClause, NonTerminals.ModelRulesClauseAssignmentList];
				if (modelRulesClauseAssignmentList == null)
				{
					continue;
				}

				foreach (var modelRulesClauseAssignment in modelRulesClauseAssignmentList.GetDescendants(NonTerminals.ModelRulesClauseAssignment))
				{
					var cellAssignment = modelRulesClauseAssignment[NonTerminals.CellAssignment];
					var assignmentDimensionList = cellAssignment[NonTerminals.MultiColumnForLoopOrConditionOrExpressionOrSingleColumnForLoopList];
					if (assignmentDimensionList != null)
					{
						ruleDimensionIdentifiers.AddRange(GetIdentifiers(assignmentDimensionList));
					}
					
					ruleMeasureIdentifiers.Add(cellAssignment.FirstTerminalNode);

					var orderByClause = modelRulesClauseAssignment[NonTerminals.OrderByClause];
					if (orderByClause != null)
					{
						ruleDimensionIdentifiers.AddRange(GetIdentifiers(orderByClause));
					}

					var assignmentExpression = modelRulesClauseAssignment[NonTerminals.Expression];
					if (assignmentExpression == null)
					{
						continue;
					}

					foreach (var identifier in GetIdentifiers(assignmentExpression))
					{
						if (identifier.GetPathFilterAncestor(
							n => !String.Equals(n.Id, NonTerminals.ModelRulesClauseAssignment),
							n => String.Equals(n.Id, NonTerminals.ConditionOrExpressionOrSingleColumnForLoopList) || String.Equals(n.Id, NonTerminals.ConditionOrExpressionList)) == null)
						{
							ruleMeasureIdentifiers.Add(identifier);
						}
						else
						{
							ruleDimensionIdentifiers.Add(identifier);
						}
					}
				}

				new OracleSpecialTableReference(queryBlock.ModelReference.DimensionReferenceContainer, ReferenceType.SqlModel, dimensionColumns, null)
				{
					RootNode = modelClause[NonTerminals.MainModel]
				};

				ResolveSqlModelReferences(queryBlock, queryBlock.ModelReference.DimensionReferenceContainer, ruleDimensionIdentifiers);

				queryBlock.ModelReference.MeasuresReferenceContainer.ObjectReferences.Add(queryBlock.ModelReference); // makes duplicate object reference
				ResolveSqlModelReferences(queryBlock, queryBlock.ModelReference.MeasuresReferenceContainer, ruleMeasureIdentifiers);

				queryBlock.ObjectReferences.Clear();
				queryBlock.ObjectReferences.Add(queryBlock.ModelReference);

				foreach (var column in queryBlock.AsteriskColumns)
				{
					if (column.RootNode.TerminalCount == 1)
					{
						_asteriskTableReferences[column] = new List<OracleDataObjectReference> { queryBlock.ModelReference };
						break;
					}

					_asteriskTableReferences.Remove(column);
				}
			}
		}

		private void ResolveSqlModelReferences(OracleQueryBlock queryBlock, OracleReferenceContainer referenceContainer, ICollection<StatementGrammarNode> identifiers)
		{
			var selectListColumn = referenceContainer as OracleSelectListColumn;
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, referenceContainer, identifiers.Where(t => t.Id.In(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level)), StatementPlacement.Model, selectListColumn);
			var grammarSpecificFunctions = identifiers.Where(t => t.Id.In(Terminals.Count, Terminals.User, Terminals.Trim, Terminals.CharacterCode, NonTerminals.AggregateFunction));
			CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, referenceContainer, queryBlock, StatementPlacement.Model, selectListColumn);
		}

		private List<OracleSelectListColumn> GatherSqlModelColumns(OracleQueryBlock queryBlock, StatementGrammarNode parenthesisEnclosedAliasedExpressionList)
		{
			var columns = new List<OracleSelectListColumn>();

			foreach (var aliasedExpression in parenthesisEnclosedAliasedExpressionList.GetDescendants(NonTerminals.AliasedExpression))
			{
				var sqlModelColumn =
					new OracleSelectListColumn(this, null)
					{
						RootNode = aliasedExpression,
						Owner = queryBlock,
						AliasNode = aliasedExpression[NonTerminals.ColumnAsAlias, Terminals.ColumnAlias]
					};

				var lastnode = aliasedExpression.ChildNodes[aliasedExpression.ChildNodes.Count - 1];
				var aliasTerminalOffset = String.Equals(lastnode.Id, NonTerminals.ColumnAsAlias) ? lastnode.TerminalCount : 0;
				if (aliasedExpression.TerminalCount - aliasTerminalOffset == 1 && String.Equals(aliasedExpression.FirstTerminalNode.Id, Terminals.Identifier))
				{
					sqlModelColumn.IsDirectReference = true;

					if (aliasedExpression.TerminalCount == 1)
					{
						sqlModelColumn.AliasNode = aliasedExpression.FirstTerminalNode;
					}
				}

				sqlModelColumn.ObjectReferences.AddRange(queryBlock.ObjectReferences);
				ResolveSqlModelReferences(queryBlock, sqlModelColumn, GetIdentifiers(aliasedExpression).ToArray());
				columns.Add(sqlModelColumn);
			}

			return columns;
		}

		private static IEnumerable<StatementGrammarNode> GetIdentifiers(StatementGrammarNode nonTerminal, params string[] nodeIds)
		{
			if (nodeIds.Length == 0)
			{
				nodeIds = StandardIdentifierIds;
			}

			return nonTerminal.GetDescendantsWithinSameQueryBlock(nodeIds);
		}

		private void ResolveInlineViewOrCommonTableExpressionRelations()
		{
			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				ResolveConcatenatedQueryBlocks(queryBlock);

				ResolveCrossAndOuterAppliedTableReferences(queryBlock);

				ResolveParentCorrelatedQueryBlock(queryBlock);

				if (queryBlock.Type == QueryBlockType.CommonTableExpression && queryBlock.PrecedingConcatenatedQueryBlock == null)
				{
					_unreferencedQueryBlocks.Add(queryBlock);
				}
			}

			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				foreach (var nestedQueryReference in queryBlock.ObjectReferences.Where(t => t.Type != ReferenceType.SchemaObject))
				{
					switch (nestedQueryReference.Type)
					{
						case ReferenceType.InlineView:
							nestedQueryReference.QueryBlocks.Add(QueryBlockNodes[nestedQueryReference.ObjectNode]);
							break;
						
						case ReferenceType.PivotTable:
							var pivotTableReference = (OraclePivotTableReference)nestedQueryReference;
							if (pivotTableReference.SourceReference.Type == ReferenceType.InlineView)
							{
								pivotTableReference.SourceReference.QueryBlocks.Add(QueryBlockNodes[pivotTableReference.SourceReference.ObjectNode]);
							}
							
							break;
						
						default:
							if (_objectReferenceCteRootNodes.ContainsKey(nestedQueryReference))
							{
								var commonTableExpressionNode = _objectReferenceCteRootNodes[nestedQueryReference];
								var nestedTableFullyQualifiedName = OracleObjectIdentifier.Create(null, nestedQueryReference.ObjectNode.Token.Value);
								
								foreach (var referencedQueryBlock in commonTableExpressionNode
									.Where(nodeName => OracleObjectIdentifier.Create(null, nodeName.Value) == nestedTableFullyQualifiedName))
								{
									var cteQueryBlockNode = referencedQueryBlock.Key.GetDescendants(NonTerminals.QueryBlock).FirstOrDefault();
									if (cteQueryBlockNode != null)
									{
										var referredQueryBlock = QueryBlockNodes[cteQueryBlockNode];
										nestedQueryReference.QueryBlocks.Add(referredQueryBlock);

										_unreferencedQueryBlocks.Remove(referredQueryBlock);
									}
								}
							}
							
							break;
					}
				}

				foreach (var accessibleQueryBlock in _accessibleQueryBlockRoot[queryBlock])
				{
					var accessibleQueryBlockRoot = accessibleQueryBlock.GetDescendants(NonTerminals.QueryBlock).FirstOrDefault();
					if (accessibleQueryBlockRoot != null)
					{
						queryBlock.AccessibleQueryBlocks.Add(QueryBlockNodes[accessibleQueryBlockRoot]);
					}
				}
			}
		}

		private static void ApplyExplicitCommonTableExpressionColumnNames(OracleQueryBlock queryBlock)
		{
			if (queryBlock.Type != QueryBlockType.CommonTableExpression || queryBlock.ExplicitColumnNames == null)
			{
				return;
			}

			var columnEnumerator = queryBlock.Columns.Where(c => !c.IsAsterisk).GetEnumerator();
			foreach (var kvp in queryBlock.ExplicitColumnNames)
			{
				if (columnEnumerator.MoveNext())
				{
					columnEnumerator.Current.ExplicitAliasNode = kvp.Key;
					columnEnumerator.Current.ExplicitNormalizedName = kvp.Value;
				}
				else
				{
					break;
				}
			}
		}

		private void ResolveRedundantTerminals()
		{
			ResolveRedundantCommonTableExpressions();

			ResolveRedundantColumns();
			
			ResolveRedundantQualifiers();
			
			ResolveRedundantAliases();
		}

		private void ResolveRedundantCommonTableExpressions()
		{
			foreach (var queryBlockDependentQueryBlocks in _unreferencedQueryBlocks)
			{
				queryBlockDependentQueryBlocks.IsRedundant = true;

				var commonTableExpression = queryBlockDependentQueryBlocks.RootNode.GetAncestor(NonTerminals.CommonTableExpression);
				List<StatementGrammarNode> redundantTerminals = null;
				var precedingTerminal = commonTableExpression.PrecedingTerminal;
				var followingTerminal = commonTableExpression.FollowingTerminal;
				if (followingTerminal != null && String.Equals(followingTerminal.Id, Terminals.Comma))
				{
					redundantTerminals = new List<StatementGrammarNode>(commonTableExpression.Terminals) { followingTerminal };
				}
				else if (precedingTerminal != null &&
				         (String.Equals(precedingTerminal.Id, Terminals.Comma) || String.Equals(precedingTerminal.Id, Terminals.With)))
				{
					redundantTerminals = new List<StatementGrammarNode> { precedingTerminal };
					redundantTerminals.AddRange(commonTableExpression.Terminals);
				}

				if (redundantTerminals != null)
				{
					var terminalGroup = new RedundantTerminalGroup(redundantTerminals, RedundancyType.UnusedQueryBlock);
					_redundantTerminalGroups.Add(terminalGroup);
				}
			}
		}

		private void ResolveRedundantColumns()
		{
			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				var nestedQuery = queryBlock.RootNode.GetAncestor(NonTerminals.NestedQuery);
				var groupingExpressionOrNestedQuery =
					nestedQuery.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.ExpressionListOrNestedQuery)
					?? nestedQuery.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.GroupingExpressionListOrNestedQuery);

				if (groupingExpressionOrNestedQuery != null)
				{
					continue;
				}

				foreach (var objectReference in queryBlock.ObjectReferences.OfType<OracleSpecialTableReference>())
				{
					var hasReferencedColumn = false;
					var columnDefinitions = objectReference.ColumnDefinitions;
					for (var i = columnDefinitions.Count - 1; i >= 0 ; i--)
					{
						var column = columnDefinitions[i];
						hasReferencedColumn |= column.IsReferenced;

						if (!column.IsReferenced && column.RootNode != null && (hasReferencedColumn || i > 0))
						{
							var terminals = new List<StatementGrammarNode>(column.RootNode.Terminals);
							StatementGrammarNode commaTerminal;
							int index;
							if (hasReferencedColumn)
							{
								commaTerminal = column.RootNode.LastTerminalNode.FollowingTerminal;
								index = column.RootNode.TerminalCount;
							}
							else
							{
								commaTerminal = column.RootNode.FirstTerminalNode.PrecedingTerminal;
								index = 0;
							}

							if (String.Equals(commaTerminal.Id, Terminals.Comma))
							{
								terminals.Insert(index, commaTerminal);
							}

							var terminalGroup = new RedundantTerminalGroup(terminals, RedundancyType.UnusedColumn);
							_redundantTerminalGroups.Add(terminalGroup);
						}
					}
				}

				if (queryBlock.HasDistinctResultSet || queryBlock.Type == QueryBlockType.CursorParameter)
				{
					continue;
				}

				if (queryBlock.IsMainQueryBlock && (queryBlock.IsInSelectStatement || queryBlock.IsInInsertStatement))
				{
					continue;
				}

				var redundantColumns = 0;
				var explicitSelectListColumns = queryBlock.Columns.Where(c => c.HasExplicitDefinition).ToArray();
				foreach (var column in explicitSelectListColumns.Where(c => !c.IsReferenced))
				{
					if (++redundantColumns == explicitSelectListColumns.Length - queryBlock.AttachedColumns.Count)
					{
						break;
					}

					if (!String.IsNullOrEmpty(column.ExplicitNormalizedName))
					{
						continue;
					}

					var initialPrecedingQueryBlock = queryBlock.AllPrecedingConcatenatedQueryBlocks.LastOrDefault();
					var initialQueryBlockColumn = initialPrecedingQueryBlock?.Columns
						.Where(c => c.HasExplicitDefinition)
						.Skip(redundantColumns - 1)
						.FirstOrDefault();

					if (initialQueryBlockColumn != null && (initialQueryBlockColumn.IsReferenced || initialPrecedingQueryBlock == MainQueryBlock))
					{
						continue;
					}

					var terminalGroup = new List<StatementGrammarNode>(column.RootNode.Terminals);
					_redundantTerminals.UnionWith(terminalGroup);

					if (!TryMakeRedundantIfComma(column.RootNode.PrecedingTerminal, out StatementGrammarNode commaTerminal))
					{
						if (TryMakeRedundantIfComma(column.RootNode.FollowingTerminal, out commaTerminal))
						{
							terminalGroup.Add(commaTerminal);
						}
					}
					else
					{
						terminalGroup.Insert(0, commaTerminal);
					}

					_redundantTerminalGroups.Add(new RedundantTerminalGroup(terminalGroup, RedundancyType.UnusedColumn));
				}
			}
		}

		private bool TryMakeRedundantIfComma(StatementGrammarNode terminal, out StatementGrammarNode commaTerminal)
		{
			commaTerminal = terminal != null && String.Equals(terminal.Id, Terminals.Comma) && _redundantTerminals.Add(terminal) ? terminal : null;
			return commaTerminal != null;
		}

		private void ResolveRedundantQualifiers()
		{
			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				var ownerNameObjectReferences = queryBlock.ObjectReferences
					.Where(o => o.OwnerNode != null && o.SchemaObject != null)
					.ToLookup(o => o.SchemaObject.Name);

				var removedObjectReferenceOwners = new HashSet<OracleDataObjectReference>();
				if (HasDatabaseModel)
				{
					foreach (var ownerNameObjectReference in ownerNameObjectReferences)
					{
						var uniqueObjectReferenceCount = ownerNameObjectReference.Count();
						if (uniqueObjectReferenceCount != 1)
						{
							continue;
						}

						foreach (var objectReference in ownerNameObjectReference.Where(o => o.SchemaObject.Owner == DatabaseModel.CurrentSchema.ToQuotedIdentifier()))
						{
							var terminals = objectReference.RootNode.Terminals.TakeWhile(t => t != objectReference.ObjectNode);
							CreateRedundantTerminalGroup(terminals, RedundancyType.Qualifier);
							removedObjectReferenceOwners.Add(objectReference);
						}
					}
				}

				var otherRedundantOwnerReferences = ((IEnumerable<OracleReference>)queryBlock.AllProgramReferences).Concat(queryBlock.AllTypeReferences).Concat(queryBlock.AllSequenceReferences)
					.Where(o => o.OwnerNode != null && IsSchemaObjectInCurrentSchemaOrAccessibleByPublicSynonym(o.SchemaObject));
				foreach (var reference in otherRedundantOwnerReferences)
				{
					CreateRedundantTerminalGroup(new[] { reference.OwnerNode, reference.OwnerNode.FollowingTerminal }, RedundancyType.Qualifier);
				}

				Func<OracleColumnReference, bool> objectPrefixedColumnFilter = c => c.ObjectNode != null && c.RootNode != null;
				foreach (var columnReference in queryBlock.AllColumnReferences.Where(objectPrefixedColumnFilter))
				{
					var uniqueObjectReferenceCount = queryBlock.ObjectReferences
						.Where(o => o.Columns.Concat(o.Pseudocolumns).Any(c => String.Equals(c.Name, columnReference.NormalizedName)))
						.Distinct()
						.Count();

					if (uniqueObjectReferenceCount != 1)
					{
						if (columnReference.OwnerNode != null && removedObjectReferenceOwners.Contains(columnReference.ValidObjectReference))
						{
							var redundantSchemaPrefixTerminals = columnReference.RootNode.Terminals.TakeWhile(t => t != columnReference.ObjectNode);
							CreateRedundantTerminalGroup(redundantSchemaPrefixTerminals, RedundancyType.Qualifier);
						}

						continue;
					}

					if (columnReference.Placement == StatementPlacement.OrderBy &&
						columnReference.ValidObjectReference?.QueryBlocks.FirstOrDefault() != queryBlock &&
						queryBlock.NamedColumns[columnReference.NormalizedName].Any(c => !c.IsDirectReference))
					{
						continue;
					}

					if ((String.Equals(columnReference.NormalizedName, OracleHierarchicalClauseReference.ColumnNameConnectByIsLeaf) || String.Equals(columnReference.NormalizedName, OracleHierarchicalClauseReference.ColumnNameConnectByIsCycle)) &&
						!columnReference.Name.IsQuoted())
					{
						continue;
					}

					var requiredNode = columnReference.IsCorrelated ? columnReference.ObjectNode : columnReference.ColumnNode;
					var terminals = columnReference.RootNode.Terminals.TakeWhile(t => t != requiredNode);
					CreateRedundantTerminalGroup(terminals, RedundancyType.Qualifier);
				}

				var innerPivotColumnReferences = queryBlock.ObjectReferences
					.OfType<OraclePivotTableReference>()
					.SelectMany(pt => pt.SourceReferenceContainer.ColumnReferences)
					.Where(objectPrefixedColumnFilter);

				foreach (var columnReference in innerPivotColumnReferences)
				{
					CreateRedundantTerminalGroup(columnReference.RootNode.Terminals.TakeWhile(t => t != columnReference.ColumnNode), RedundancyType.Qualifier);
				}
			}
		}

		private void ResolveRedundantAliases()
		{
			var redundantColumnAliases = QueryBlockNodes.Values
				.SelectMany(qb => qb.Columns)
				.Where(c => c.IsDirectReference && c.HasExplicitAlias && String.Equals(c.NormalizedName, c.AliasNode.PrecedingTerminal.Token.Value.ToQuotedIdentifier()) && !_redundantTerminals.Contains(c.AliasNode))
				.Select(c => c.AliasNode);

			foreach (var aliasNode in redundantColumnAliases)
			{
				_redundantTerminalGroups.Add(new RedundantTerminalGroup(Enumerable.Repeat(aliasNode, 1), RedundancyType.RedundantColumnAlias));
			}

			var redundantObjectAlias = AllReferenceContainers
				.SelectMany(c => c.ObjectReferences)
				.Where(r => r.AliasNode != null && r.Type == ReferenceType.SchemaObject && String.Equals(r.ObjectNode.Token.Value.ToQuotedIdentifier(), r.AliasNode.Token.Value.ToQuotedIdentifier()))
				.Select(r => r.AliasNode);

			foreach (var aliasNode in redundantObjectAlias)
			{
				_redundantTerminalGroups.Add(new RedundantTerminalGroup(Enumerable.Repeat(aliasNode, 1), RedundancyType.RedundantObjectAlias));
			}
		}

		private void CreateRedundantTerminalGroup(IEnumerable<StatementGrammarNode> terminals, RedundancyType redundancyType)
		{
			var terminalGroup = new RedundantTerminalGroup(terminals, redundancyType);
			if (terminalGroup.All(t => _redundantTerminals.Add(t)))
			{
				_redundantTerminalGroups.Add(terminalGroup);	
			}
		}

		private bool IsSchemaObjectInCurrentSchemaOrAccessibleByPublicSynonym(OracleSchemaObject schemaObject)
		{
			if (schemaObject == null)
			{
				return false;
			}

			var isSchemaObjectInCurrentSchema = schemaObject.Owner == DatabaseModel.CurrentSchema.ToQuotedIdentifier();
			var isAccessibleByPublicSynonym = schemaObject.Synonyms.Any(s => s.Owner == OracleObjectIdentifier.SchemaPublic && s.Name == schemaObject.Name);
			return isSchemaObjectInCurrentSchema || isAccessibleByPublicSynonym;
		}

		private void BuildDmlModel()
		{
			ResolveMainObjectReferenceInsert();
			ResolveMainObjectReferenceUpdateOrDelete();
			ResolveMainObjectReferenceMerge();

			var rootNode = DmlRootNode;
			if (rootNode == null)
			{
				return;
			}

			var whereClauseRootNode = rootNode.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.WhereClause));
			if (whereClauseRootNode != null)
			{
				var whereClauseIdentifiers = whereClauseRootNode.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level, Terminals.User);
				ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, MainObjectReferenceContainer, whereClauseIdentifiers, StatementPlacement.Where, null);

				var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(whereClauseRootNode);
				CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, MainObjectReferenceContainer, null, StatementPlacement.Where, null);
			}

			if (MainObjectReferenceContainer.MainObjectReference != null)
			{
				ResolveProgramReferences(MainObjectReferenceContainer.ProgramReferences);
				ResolveColumnObjectReferences(MainObjectReferenceContainer.ColumnReferences, new[] { MainObjectReferenceContainer.MainObjectReference }, OracleDataObjectReference.EmptyArray);
			}

			ResolveErrorLoggingObjectReference(rootNode, MainObjectReferenceContainer);
		}

		private void ResolveErrorLoggingObjectReference(StatementGrammarNode parentNode, OracleReferenceContainer referenceContainer)
		{
			var loggingTableIdentifier = parentNode[NonTerminals.ErrorLoggingClause, NonTerminals.ErrorLoggingIntoObject, NonTerminals.SchemaObject, Terminals.ObjectIdentifier];
			if (loggingTableIdentifier == null)
			{
				return;
			}

			var loggingObjectReference = CreateDataObjectReference(referenceContainer, loggingTableIdentifier.ParentNode, loggingTableIdentifier, null);
			referenceContainer.ObjectReferences.Add(loggingObjectReference);
		}

		private void ResolveMainObjectReferenceMerge()
		{
			var rootNode = DmlRootNode;
			if (rootNode == null)
			{
				return;
			}

			var mergeTarget = rootNode[NonTerminals.MergeTarget];
			if (mergeTarget != null)
			{
				MainObjectReferenceContainer.MainObjectReference = CreateMergeDataObjectReference(mergeTarget);
			}

			var mergeCondition = rootNode[NonTerminals.Condition];
			if (mergeCondition == null)
			{
				return;
			}

			var mergeConditionIdentifiers = mergeCondition.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, MainObjectReferenceContainer, mergeConditionIdentifiers, StatementPlacement.None, null);

			var updateInsertClause = rootNode[8];
			if (updateInsertClause != null && String.Equals(updateInsertClause.Id, NonTerminals.MergeUpdateInsertClause))
			{
				var updateInsertClauseIdentifiers = updateInsertClause.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn);
				ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, MainObjectReferenceContainer, updateInsertClauseIdentifiers, StatementPlacement.None, null);
			}

			var mergeSource = rootNode[NonTerminals.UsingMergeSource, NonTerminals.MergeSource];
			if (mergeSource == null)
			{
				return;
			}

			var mergeSourceReference = CreateMergeDataObjectReference(mergeSource);
			if (mergeSourceReference == null)
			{
				return;
			}
			
			MainObjectReferenceContainer.ObjectReferences.Add(mergeSourceReference);

			var mergeSourceAccessibleReferences = MainObjectReferenceContainer.ColumnReferences
				.Where(c => c.ColumnNode.GetPathFilterAncestor(null, n => n.Id.In(NonTerminals.PrefixedIdentifier, NonTerminals.ParenthesisEnclosedPrefixedIdentifierList)) == null);

			ResolveColumnObjectReferences(mergeSourceAccessibleReferences, new[] { mergeSourceReference }, OracleDataObjectReference.EmptyArray);
		}

		private OracleDataObjectReference CreateMergeDataObjectReference(StatementGrammarNode mergeDataObjectReferenceRootNode)
		{
			var mergeSourceNestedQuery = mergeDataObjectReferenceRootNode[NonTerminals.QueryTableExpression, NonTerminals.NestedQuery];
			var sourceObjectIdentifier = mergeDataObjectReferenceRootNode[NonTerminals.QueryTableExpression, NonTerminals.SchemaObject, Terminals.ObjectIdentifier];
			var sourceObjectReferenceAlias = mergeDataObjectReferenceRootNode[Terminals.ObjectAlias];
			OracleDataObjectReference mergeSourceReference = null;
			if (sourceObjectIdentifier != null)
			{
				mergeSourceReference = CreateDataObjectReference(MainObjectReferenceContainer, mergeDataObjectReferenceRootNode, sourceObjectIdentifier, sourceObjectReferenceAlias);
			}
			else
			{
				var queryBlockRoot = mergeSourceNestedQuery?.GetDescendants(NonTerminals.QueryBlock).FirstOrDefault();
				if (queryBlockRoot != null)
				{
					var queryBlock = QueryBlockNodes[queryBlockRoot];

					mergeSourceReference = queryBlock.SelfObjectReference;
					mergeSourceReference.RootNode = mergeDataObjectReferenceRootNode;
					mergeSourceReference.AliasNode = queryBlock.AliasNode = sourceObjectReferenceAlias;
				}
			}

			return mergeSourceReference;
		}

		private void ResolveMainObjectReferenceUpdateOrDelete()
		{
			var rootNode = DmlRootNode;
			var tableReferenceNode = rootNode?[NonTerminals.TableReference];
			var queryTableExpression = tableReferenceNode?.GetDescendantsWithinSameQueryBlock(NonTerminals.QueryTableExpression).SingleOrDefault();
			if (queryTableExpression == null)
			{
				return;
			}

			var objectIdentifier = queryTableExpression[NonTerminals.SchemaObject, Terminals.ObjectIdentifier];
			if (objectIdentifier != null)
			{
				var objectReferenceAlias = tableReferenceNode[Terminals.ObjectAlias];
				MainObjectReferenceContainer.MainObjectReference = CreateDataObjectReference(MainObjectReferenceContainer, tableReferenceNode, objectIdentifier, objectReferenceAlias);
			}
			else if (MainQueryBlock != null)
			{
				MainObjectReferenceContainer.MainObjectReference = MainQueryBlock.SelfObjectReference;
				MainObjectReferenceContainer.MainObjectReference.RootNode = tableReferenceNode;
			}

			if (!String.Equals(rootNode.FirstTerminalNode.Id, Terminals.Update))
			{
				return;
			}

			var updateListNode = rootNode[NonTerminals.UpdateSetClause, NonTerminals.UpdateSetColumnsOrObjectValue];
			if (updateListNode == null)
			{
				return;
			}

			var identifiers = updateListNode.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level, Terminals.User);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, MainObjectReferenceContainer, identifiers, StatementPlacement.None, null, GetPrefixNonTerminalFromPrefixedIdentifier);

			var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(updateListNode);
			CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, MainObjectReferenceContainer, null, StatementPlacement.None, null);
		}

		private StatementGrammarNode GetPrefixNonTerminalFromPrefixedIdentifier(StatementGrammarNode identifier)
		{
			return String.Equals(identifier.ParentNode.Id, NonTerminals.PrefixedIdentifier)
				? identifier.ParentNode[NonTerminals.Prefix]
				: GetPrefixNodeFromPrefixedColumnReference(identifier);
		}

		private void ResolveMainObjectReferenceInsert()
		{
			var insertIntoClauses = Statement.RootNode.GetDescendantsWithinSameQueryBlock(NonTerminals.InsertIntoClause);
			foreach (var insertIntoClause in insertIntoClauses)
			{
				var dmlTableExpressionClause = insertIntoClause.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.DmlTableExpressionClause));
				StatementGrammarNode objectReferenceAlias = null;
				StatementGrammarNode objectIdentifier = null;
				if (dmlTableExpressionClause != null)
				{
					objectReferenceAlias = dmlTableExpressionClause.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, Terminals.ObjectAlias));
					objectIdentifier = dmlTableExpressionClause[NonTerminals.QueryTableExpression, NonTerminals.SchemaObject, Terminals.ObjectIdentifier];
				}

				if (objectIdentifier == null)
				{
					continue;
				}

				var sourceReferenceContainer = new OracleMainObjectReferenceContainer(this);
				if (MainQueryBlock != null)
				{
					sourceReferenceContainer.ObjectReferences.Add(MainQueryBlock.SelfObjectReference);
				}

				StatementGrammarNode condition;
				if (MainQueryBlock != null &&
					String.Equals(insertIntoClause.ParentNode.ParentNode.Id, NonTerminals.ConditionalInsertConditionBranch) &&
					(condition = insertIntoClause.ParentNode.ParentNode[1]) != null && String.Equals(condition.Id, NonTerminals.Condition))
				{
					var identifiers = condition.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.Level, Terminals.RowIdPseudocolumn, Terminals.User);
					ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, sourceReferenceContainer, identifiers, StatementPlacement.None, null);
					ResolveColumnObjectReferences(sourceReferenceContainer.ColumnReferences, sourceReferenceContainer.ObjectReferences, OracleDataObjectReference.EmptyArray);

					var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(condition);
					CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, sourceReferenceContainer, null, StatementPlacement.None, null);

					ResolveProgramReferences(sourceReferenceContainer.ProgramReferences);
				}

				var insertTarget =
					new OracleInsertTarget(this)
					{
						TargetNode = dmlTableExpressionClause,
						RootNode = insertIntoClause.ParentNode
					};

				var dataObjectReference = CreateDataObjectReference(insertTarget, dmlTableExpressionClause, objectIdentifier, objectReferenceAlias);

				var targetReferenceContainer = new OracleMainObjectReferenceContainer(this);
				insertTarget.ObjectReferences.Add(dataObjectReference);
				insertTarget.ColumnListNode = insertIntoClause[NonTerminals.ParenthesisEnclosedPrefixedIdentifierList];
				if (insertTarget.ColumnListNode != null)
				{
					var columnIdentiferNodes = insertTarget.ColumnListNode.GetDescendants(Terminals.Identifier);
					ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, targetReferenceContainer, columnIdentiferNodes, StatementPlacement.None, null);

					insertTarget.Columns = targetReferenceContainer.ColumnReferences.ToDictionary(c => c.ColumnNode, c => c.NormalizedName).AsReadOnly();

					ResolveColumnObjectReferences(targetReferenceContainer.ColumnReferences, insertTarget.ObjectReferences, OracleDataObjectReference.EmptyArray);
				}

				insertTarget.ValueList =
					insertIntoClause.ParentNode[NonTerminals.InsertValuesClause, NonTerminals.ParenthesisEnclosedExpressionOrDefaultValueListOrAssignmentTarget]
					?? insertIntoClause.ParentNode[NonTerminals.InsertValuesOrSubquery, NonTerminals.InsertValuesClause, NonTerminals.ParenthesisEnclosedExpressionOrDefaultValueListOrAssignmentTarget];

				if (insertTarget.ValueList == null)
				{
					var subquery = insertIntoClause.ParentNode[NonTerminals.InsertValuesOrSubquery, NonTerminals.NestedQuery, NonTerminals.Subquery];
					var queryBlock = subquery?.GetDescendantsWithinSameQueryBlock(NonTerminals.QueryBlock).FirstOrDefault();
					if (queryBlock != null)
					{
						insertTarget.RowSource = QueryBlockNodes[queryBlock];
					}
				}
				else
				{
					insertTarget.ValueExpressions = insertTarget.ValueList.GetDescendantsWithinSameQueryBlock(NonTerminals.ExpressionOrDefaultValue).ToArray();

					var identifiers = insertTarget.ValueList.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.Level, Terminals.RowIdPseudocolumn, Terminals.User);
					ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, insertTarget, identifiers, StatementPlacement.ValuesClause, null); // TODO: Fix root node is not set

					var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(insertTarget.ValueList);
					CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, insertTarget, null, StatementPlacement.ValuesClause, null);

					ResolveColumnObjectReferences(insertTarget.ColumnReferences, sourceReferenceContainer.ObjectReferences, OracleDataObjectReference.EmptyArray);
					ResolveProgramReferences(insertTarget.ProgramReferences);
				}

				insertTarget.ColumnReferences.AddRange(targetReferenceContainer.ColumnReferences);
				insertTarget.ColumnReferences.AddRange(sourceReferenceContainer.ColumnReferences);
				insertTarget.ProgramReferences.AddRange(sourceReferenceContainer.ProgramReferences);

				ResolveErrorLoggingObjectReference(insertIntoClause.ParentNode, insertTarget);

				_insertTargets.Add(insertTarget);
			}
		}

		private OracleDataObjectReference CreateDataObjectReference(OracleReferenceContainer referenceContainer, StatementGrammarNode rootNode, StatementGrammarNode objectIdentifier, StatementGrammarNode aliasNode)
		{
			var queryTableExpressionNode = objectIdentifier.ParentNode;
			var schemaPrefixNode = queryTableExpressionNode[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier];

			OracleSchemaObject schemaObject = null;
			if (HasDatabaseModel)
			{
				var owner = schemaPrefixNode?.Token.Value;
				schemaObject = _databaseModel.GetFirstSchemaObject<OracleDataObject>(_databaseModel.GetPotentialSchemaObjectIdentifiers(owner, objectIdentifier.Token.Value));
			}

			return
				new OracleDataObjectReference(ReferenceType.SchemaObject)
				{
					Container = referenceContainer,
					RootNode = rootNode,
					OwnerNode = schemaPrefixNode,
					ObjectNode = objectIdentifier,
					DatabaseLinkNode = GetDatabaseLinkFromQueryTableExpression(queryTableExpressionNode),
					AliasNode = aliasNode,
					SchemaObject = schemaObject
				};
		}

		private void ResolveCrossAndOuterAppliedTableReferences(OracleQueryBlock queryBlock)
		{
			var tableReferenceJoinClause = queryBlock.RootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.TableReferenceJoinClause);
			var crossOrOuterApplyClause = queryBlock.RootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryBlock), NonTerminals.CrossOrOuterApplyClause);
			if (tableReferenceJoinClause == null || crossOrOuterApplyClause == null)
			{
				return;
			}

			var parentTableReferenceNode = tableReferenceJoinClause[0];
			if (parentTableReferenceNode == null)
			{
				return;
			}

			var appliedTableReferenceNode = crossOrOuterApplyClause[NonTerminals.TableReference];
			if (appliedTableReferenceNode != null)
			{
				var parentCorrelatedQueryBlock = GetQueryBlock(tableReferenceJoinClause);
				var appliedDataObjectReference = parentCorrelatedQueryBlock.ObjectReferences.SingleOrDefault(o => o.RootNode == appliedTableReferenceNode);
				if (appliedDataObjectReference != null)
				{
					appliedDataObjectReference.IsOuterJoined = crossOrOuterApplyClause[NonTerminals.CrossOrOuter, Terminals.Outer] != null;
				}
			}
		}

		private void ResolveParentCorrelatedQueryBlock(OracleQueryBlock queryBlock, bool allowMoreThanOneLevel = false)
		{
			var nestedQueryRoot = queryBlock.RootNode.GetAncestor(NonTerminals.NestedQuery);
			
			foreach (var parentId in new[] { NonTerminals.Expression, NonTerminals.Condition })
			{
				var parentExpression = nestedQueryRoot.GetPathFilterAncestor(n => allowMoreThanOneLevel || !String.Equals(n.Id, NonTerminals.NestedQuery), parentId);
				if (parentExpression == null)
					continue;
				
				queryBlock.OuterCorrelatedQueryBlock = GetQueryBlock(parentExpression);
				foreach (var asteriskColumn in queryBlock.AsteriskColumns)
				{
					asteriskColumn.RegisterOuterReference();
				}

				return;
			}
		}

		private void ResolveConcatenatedQueryBlocks(OracleQueryBlock queryBlock)
		{
			var concatenatedSubquery = queryBlock.RootNode.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.ConcatenatedSubquery);
			var parentQueryBlockNode = concatenatedSubquery?.ParentNode.GetDescendants(NonTerminals.QueryBlock).FirstOrDefault();
			if (parentQueryBlockNode == null)
			{
				return;
			}
			
			var precedingQueryBlock = QueryBlockNodes[parentQueryBlockNode];
			precedingQueryBlock.FollowingConcatenatedQueryBlock = queryBlock;
			queryBlock.PrecedingConcatenatedQueryBlock = precedingQueryBlock;

			queryBlock.AliasNode = null;

			var setOperation = queryBlock.RootNode.ParentNode.ParentNode[0];
			if (precedingQueryBlock.Type != QueryBlockType.CommonTableExpression || setOperation == null || setOperation.TerminalCount != 2 || !String.Equals(setOperation[0].Id, Terminals.Union) || !String.Equals(setOperation[1].Id, Terminals.All))
			{
				return;
			}

			var anchorReferences = queryBlock.ObjectReferences
				.Where(r => r.Type == ReferenceType.SchemaObject && r.OwnerNode == null && r.DatabaseLinkNode == null && String.Equals(OracleObjectIdentifier.Create(null, r.ObjectNode, null).NormalizedName, precedingQueryBlock.NormalizedAlias))
				.ToArray();
			if (anchorReferences.Length == 0)
			{
				return;
			}

			foreach(var anchorReference in anchorReferences)
			{
				queryBlock.ObjectReferences.Remove(anchorReference);

				var newAnchorReference =
					new OracleDataObjectReference(ReferenceType.CommonTableExpression)
					{
						Container = queryBlock,
						Owner = queryBlock,
						RootNode = anchorReference.RootNode,
						ObjectNode = anchorReference.ObjectNode,
						AliasNode = anchorReference.AliasNode
					};

				newAnchorReference.QueryBlocks.Add(queryBlock);
				queryBlock.ObjectReferences.Add(newAnchorReference);
			}

			precedingQueryBlock.IsRecursive = true;
		}

		private void ResolveReferences()
		{
			foreach (var queryBlock in QueryBlockNodes.Values)
			{
				CancellationToken.ThrowIfCancellationRequested();

				ResolveOrderByReferences(queryBlock);

				ResolveProgramReferences(queryBlock.AllProgramReferences);

				ResolvePivotTableColumnReferences(queryBlock);

				ResolveModelColumnAndProgramReferences(queryBlock);

				ResolveNaturalJoinColumnsAndOuterJoinReferences(queryBlock);

				var tableReferenceColumnReferences = new List<OracleColumnReference>();
				var columnReferences = new List<OracleColumnReference>();
				foreach (var columnReference in queryBlock.AllColumnReferences)
				{
					if (columnReference.Placement == StatementPlacement.TableReference)
					{
						tableReferenceColumnReferences.Add(columnReference);
					}
					else if (columnReference.Placement != StatementPlacement.Model && columnReference.Placement != StatementPlacement.PivotClause &&
					         columnReference.SelectListColumn?.HasExplicitDefinition != false)
					{
						columnReferences.Add(columnReference);
					}
				}

				var correlatedReferences = GetAllCorrelatedDataObjectReferences(queryBlock);

				ResolveColumnObjectReferences(tableReferenceColumnReferences, queryBlock.ObjectReferences, correlatedReferences);

				ExposeAsteriskColumns(queryBlock);

				ApplyExplicitCommonTableExpressionColumnNames(queryBlock);

				if (queryBlock.Type == QueryBlockType.ScalarSubquery && queryBlock.Parent != null)
				{
					ResolveColumnObjectReferences(tableReferenceColumnReferences, OracleDataObjectReference.EmptyArray, queryBlock.Parent.ObjectReferences);
				}

				ResolveColumnObjectReferences(columnReferences, queryBlock.ObjectReferences, correlatedReferences);

				ResolveDatabaseLinks(queryBlock);
			}
		}

		private IReadOnlyCollection<OracleDataObjectReference> GetAllCorrelatedDataObjectReferences(OracleQueryBlock queryBlock)
		{
			var correlatedReferences = new List<OracleDataObjectReference>();
			if (queryBlock.OuterCorrelatedQueryBlock != null)
			{
				correlatedReferences.AddRange(queryBlock.OuterCorrelatedQueryBlock.ObjectReferences);
			}

			var parentQueryBlockRoot = queryBlock.RootNode.GetAncestor(NonTerminals.QueryBlock);
			if (parentQueryBlockRoot != null)
			{
				var parentObjectReferences = QueryBlockNodes[parentQueryBlockRoot].ObjectReferences;
				var parentReference = parentObjectReferences.SingleOrDefault(o => o.QueryBlocks.Any(qb => qb == queryBlock));
				if (parentReference != null)
				{
					var addPrecedingFromClauseRowSources = parentReference.IsLateral;
					if (!addPrecedingFromClauseRowSources)
					{
						var joinClauseNode = queryBlock.RootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.FromClause), NonTerminals.JoinClause);
						addPrecedingFromClauseRowSources = joinClauseNode?[NonTerminals.CrossOrOuterApplyClause] != null;
					}

					if (addPrecedingFromClauseRowSources)
					{
						correlatedReferences.AddRange(parentObjectReferences.TakeWhile(r => r != parentReference));
					}
				}
			}

			return correlatedReferences;
		}

		private void ResolveModelColumnAndProgramReferences(OracleQueryBlock queryBlock)
		{
			if (queryBlock.ModelReference == null)
			{
				return;
			}

			foreach (var referenceContainer in queryBlock.ModelReference.ChildContainers)
			{
				ResolveColumnObjectReferences(referenceContainer.ColumnReferences, referenceContainer.ObjectReferences, OracleDataObjectReference.EmptyArray);
				ResolveProgramReferences(referenceContainer.ProgramReferences);
			}
		}

		private static void ResolveNaturalJoinColumnsAndOuterJoinReferences(OracleQueryBlock queryBlock)
		{
			foreach (var joinDescription in queryBlock.JoinDescriptions)
			{
				var masterReference = joinDescription.MasterObjectReference;
				if (masterReference == null)
				{
					continue;
				}

				if (joinDescription.Type == JoinType.Right || joinDescription.Type == JoinType.Full)
				{
					masterReference.IsOuterJoined = true;
				}

				var childReference = joinDescription.SlaveObjectReference;
				if (childReference == null)
				{
					continue;
				}

				if (joinDescription.Type == JoinType.Left || joinDescription.Type == JoinType.Full)
				{
					childReference.IsOuterJoined = true;
				}

				if (joinDescription.Columns != null || joinDescription.Definition == JoinDefinition.Explicit)
				{
					continue;
				}

				var columnReferenceSource = masterReference.Columns;
				var columnsToCompare = childReference.Columns;
				if (childReference.Columns.Count > masterReference.Columns.Count)
				{
					columnReferenceSource = childReference.Columns;
					columnsToCompare = masterReference.Columns;
				}

				var columns = columnReferenceSource.Select(c => c.Name).ToHashSet();
				columns.IntersectWith(columnsToCompare.Select(c => c.Name));
				joinDescription.Columns = columns;
			}
		}

		private void ResolvePivotTableColumnReferences(OracleReferenceContainer container)
		{
			foreach (var pivotTableReference in container.ObjectReferences.OfType<OraclePivotTableReference>())
			{
				ResolveColumnObjectReferences(pivotTableReference.SourceReferenceContainer.ColumnReferences, pivotTableReference.SourceReferenceContainer.ObjectReferences, OracleDataObjectReference.EmptyArray);
				ResolveProgramReferences(pivotTableReference.SourceReferenceContainer.ProgramReferences);
			}
		}

		private void ResolveDatabaseLinks(OracleQueryBlock queryBlock)
		{
			if (!HasDatabaseModel)
			{
				return;
			}

			foreach (var databaseLinkReference in queryBlock.DatabaseLinkReferences)
			{
				var databaseLinkBuilder = new StringBuilder(128);
				var databaseLinkWithoutInstanceBuilder = new StringBuilder(128);
				var includesDomain = false;
				var hasInstanceDefinition = false;
				foreach (var terminal in databaseLinkReference.DatabaseLinkNode.Terminals)
				{
					var isLinkIdentifier = String.Equals(terminal.Id, Terminals.DatabaseLinkIdentifier);
					if (String.Equals(terminal.Id, Terminals.Dot) || (isLinkIdentifier && terminal.Token.Value.Contains('.')))
					{
						includesDomain = true;
					}

					var characterIndex = 0;
					if (String.Equals(terminal.Id, Terminals.AtCharacter) || (isLinkIdentifier && (characterIndex = terminal.Token.Value.IndexOf('@')) != -1))
					{
						hasInstanceDefinition = true;

						if (isLinkIdentifier)
						{
							databaseLinkWithoutInstanceBuilder.Append(terminal.Token.Value.Substring(0, characterIndex).Trim('"'));
						}
					}

					var tokenValue = terminal.Token.Value.Trim('"');
					databaseLinkBuilder.Append(tokenValue);

					if (!hasInstanceDefinition)
					{
						databaseLinkWithoutInstanceBuilder.Append(tokenValue);
					}
				}

				var potentialIdentifiers = _databaseModel.GetPotentialSchemaObjectIdentifiers(null, databaseLinkBuilder.ToString()).ToList();

				if (hasInstanceDefinition)
				{
					potentialIdentifiers.AddRange(_databaseModel.GetPotentialSchemaObjectIdentifiers(null, databaseLinkWithoutInstanceBuilder.ToString()));
				}

				if (!includesDomain && !String.IsNullOrEmpty(DatabaseModel.DatabaseDomainName))
				{
					databaseLinkWithoutInstanceBuilder.Append(".");
					databaseLinkWithoutInstanceBuilder.Append(DatabaseModel.DatabaseDomainName.ToUpperInvariant());
					potentialIdentifiers.AddRange(_databaseModel.GetPotentialSchemaObjectIdentifiers(null, databaseLinkWithoutInstanceBuilder.ToString()));
				}

				databaseLinkReference.DatabaseLink = _databaseModel.GetFirstDatabaseLink(potentialIdentifiers.ToArray());
			}
		}

		private void ExposeAsteriskColumns(OracleQueryBlock queryBlock)
		{
			var queryBlockAsteriskColumns = _asteriskTableReferences.Where(kvp => kvp.Key.Owner == queryBlock).ToArray();
			foreach (var asteriskTableReference in queryBlockAsteriskColumns)
			{
				var asteriskColumn = asteriskTableReference.Key;
				var columnIndex = queryBlock.IndexOf(asteriskColumn);

				_asteriskTableReferences.Remove(asteriskColumn);

				foreach (var objectReference in asteriskTableReference.Value)
				{
					var sourceColumns = ResolveSourceColumnDescriptions(objectReference);

					IEnumerable<OracleSelectListColumn> exposedColumns;
					switch (objectReference.Type)
					{
						case ReferenceType.SchemaObject:
							var dataObject = objectReference.SchemaObject.GetTargetSchemaObject() as OracleDataObject;
							if (dataObject == null)
							{
								continue;
							}

							goto case ReferenceType.PivotTable;

						case ReferenceType.XmlTable:
						case ReferenceType.JsonTable:
							var specialTableReference = (OracleSpecialTableReference)objectReference;
							exposedColumns = ExposeUsingAsterisk(specialTableReference, asteriskColumn, specialTableReference.ColumnDefinitions);
							break;

						case ReferenceType.TableCollection:
						case ReferenceType.SqlModel:
						case ReferenceType.PivotTable:
							exposedColumns = sourceColumns
								.Where(c => !c.Hidden)
								.Select(c =>
									new OracleSelectListColumn(this, asteriskColumn)
									{
										IsDirectReference = true,
										ColumnDescription = c
									});
							break;

						case ReferenceType.CommonTableExpression:
						case ReferenceType.InlineView:
							exposedColumns = ExposeUsingAsterisk(objectReference, asteriskColumn, objectReference.QueryBlocks.SelectMany(qb => qb.Columns).Where(c => !c.IsAsterisk));
							break;

						default:
							throw new NotImplementedException($"Reference '{objectReference.Type}' is not implemented yet. ");
					}

					_joinTableReferenceNodes.TryGetValue(objectReference.RootNode, out OracleJoinDescription joinDescription);

					var exposedColumnDictionary = new Dictionary<string, OracleColumnReference>();
					foreach (var exposedColumn in exposedColumns)
					{
						if (joinDescription != null && joinDescription.Definition == JoinDefinition.Natural && joinDescription.Columns.Contains(exposedColumn.NormalizedName))
						{
							continue;
						}

						exposedColumn.Owner = queryBlock;

						if (String.IsNullOrEmpty(exposedColumn.NormalizedName) || !exposedColumnDictionary.TryGetValue(exposedColumn.NormalizedName, out OracleColumnReference columnReference))
						{
							columnReference = CreateColumnReference(exposedColumn, exposedColumn.Owner, exposedColumn, StatementPlacement.SelectList, asteriskColumn.RootNode.LastTerminalNode, null);

							if (!String.IsNullOrEmpty(exposedColumn.NormalizedName))
							{
								exposedColumnDictionary.Add(exposedColumn.NormalizedName, columnReference);
							}

							columnReference.ColumnNodeObjectReferences.Add(objectReference);
						}

						columnReference.ColumnNodeColumnReferences.Add(exposedColumn.ColumnDescription);

						exposedColumn.ColumnReferences.Add(columnReference);

						queryBlock.AddSelectListColumn(exposedColumn, ++columnIndex);
					}
				}
			}
		}

		private static IEnumerable<OracleColumn> ResolveSourceColumnDescriptions(OracleDataObjectReference objectReference)
		{
			foreach (var column in objectReference.Columns)
			{
				var exposedColumn = column.Clone();
				exposedColumn.Nullable = column.Nullable || objectReference.IsOuterJoined;

				yield return exposedColumn;
			}
		}

		private static IEnumerable<OracleSelectListColumn> ExposeUsingAsterisk(OracleDataObjectReference objectReference, OracleSelectListColumn asteriskColumn, IEnumerable<OracleSelectListColumn> columns)
		{
			foreach (var column in columns)
			{
				column.RegisterOuterReference();
				var exposedColumn = column.AsImplicit(asteriskColumn);
				exposedColumn.ColumnDescription.Nullable = column.ColumnDescription.Nullable || objectReference.IsOuterJoined;

				yield return exposedColumn;
			}
		}

		private void ResolveProgramReferences(IEnumerable<OracleProgramReference> programReferences)
		{
			ResolveProgramReferences(programReferences, false);
		}

		protected void ResolveProgramReferences(IEnumerable<OracleProgramReference> programReferences, bool includePlSqlObjects)
		{
			var programsTransferredToTypes = new List<OracleProgramReference>();
			foreach (var programReference in programReferences)
			{
				var programMetadata = UpdateProgramReferenceWithMetadata(programReference, includePlSqlObjects);
				if (programMetadata != null && programMetadata.Type != ProgramType.CollectionConstructor)
				{
					continue;
				}

				var typeReference = ResolveTypeReference(programReference);
				if (typeReference == null)
				{
					continue;
				}

				programsTransferredToTypes.Add(programReference);
				programReference.Container.TypeReferences.Add(typeReference);

				if (_rowSourceTableCollectionReferences.TryGetValue(programReference, out OracleTableCollectionReference tableCollectionReference))
				{
					tableCollectionReference.RowSourceReference = typeReference;
				}
			}

			programsTransferredToTypes.ForEach(f => f.Container.ProgramReferences.Remove(f));
		}

		private OracleTypeReference ResolveTypeReference(OracleProgramReference programReference)
		{
			var identifierCandidates = _databaseModel.GetPotentialSchemaObjectIdentifiers(programReference.FullyQualifiedObjectName.NormalizedName, programReference.NormalizedName);

			var schemaObject = _databaseModel.GetFirstSchemaObject<OracleTypeBase>(identifierCandidates);
			if (schemaObject == null)
			{
				return null;
			}

			var typeReference =
				new OracleTypeReference
				{
					OwnerNode = programReference.OwnerNode ?? programReference.ObjectNode,
					DatabaseLinkNode = programReference.DatabaseLinkNode,
					DatabaseLink = programReference.DatabaseLink,
					Owner = programReference.Owner,
					Container = programReference.Container,
					ParameterReferences = programReference.ParameterReferences,
					ParameterListNode = programReference.ParameterListNode,
					RootNode = programReference.RootNode,
					SchemaObject = schemaObject,
					SelectListColumn = programReference.SelectListColumn,
					ObjectNode = programReference.ProgramIdentifierNode
				};
			
			return typeReference;
		}

		private OracleProgramMetadata UpdateProgramReferenceWithMetadata(OracleProgramReference programReference, bool includePlSqlObjects)
		{
			if (!HasDatabaseModel || programReference.DatabaseLinkNode != null)
			{
				return null;
			}

			var owner = String.IsNullOrEmpty(programReference.FullyQualifiedObjectName.NormalizedOwner)
				? _databaseModel.CurrentSchema
				: programReference.FullyQualifiedObjectName.NormalizedOwner;

			var originalIdentifier = OracleProgramIdentifier.CreateFromValues(owner, programReference.FullyQualifiedObjectName.NormalizedName, programReference.NormalizedName);
			var hasAnalyticClause = programReference.AnalyticClauseNode != null;
			var parameterCount = programReference.ParameterReferences?.Count ?? 0;
			var result = _databaseModel.GetProgramMetadata(originalIdentifier, parameterCount, true, hasAnalyticClause, includePlSqlObjects);
			if (result.Metadata == null && !String.IsNullOrEmpty(originalIdentifier.Package) && String.IsNullOrEmpty(programReference.FullyQualifiedObjectName.NormalizedOwner))
			{
				var identifier = OracleProgramIdentifier.CreateFromValues(originalIdentifier.Package, null, originalIdentifier.Name);
				result = _databaseModel.GetProgramMetadata(identifier, parameterCount, false, hasAnalyticClause, includePlSqlObjects);
			}

			if (result.Metadata == null && programReference.Owner != null && programReference.ObjectNode == null)
			{
				var attachedFunction = programReference.Owner.AccessibleAttachedFunctions
					.OrderBy(m => Math.Abs(parameterCount - m.Parameters.Count + 1))
					.FirstOrDefault(m => String.Equals(m.Identifier.Name, programReference.NormalizedName));

				if (attachedFunction != null)
				{
					return programReference.Metadata = attachedFunction;
				}
			}

			if (result.Metadata == null && String.IsNullOrEmpty(programReference.FullyQualifiedObjectName.NormalizedOwner))
			{
				var identifier = OracleProgramIdentifier.CreateFromValues(OracleObjectIdentifier.SchemaPublic, originalIdentifier.Package, originalIdentifier.Name);
				result = _databaseModel.GetProgramMetadata(identifier, parameterCount, false, hasAnalyticClause, includePlSqlObjects);
			}

			if (result.Metadata != null)
			{
				if (programReference.ObjectNode == null && programReference.Name[0] == '"' &&
				    (result.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramLevel || result.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramRowNum))
				{
					return null;
				}

				if (String.IsNullOrEmpty(result.Metadata.Identifier.Package) &&
				    programReference.ObjectNode != null)
				{
					programReference.OwnerNode = programReference.ObjectNode;
					programReference.ObjectNode = null;
				}
			}
			else if (programReference.ObjectNode == null)
			{
				if (programReference.Container is OraclePlSqlProgram plSqlProgram)
				{
					var accessiblePrograms = (IEnumerable<OraclePlSqlProgram>)plSqlProgram.SubPrograms;
					if (plSqlProgram.Master.Type == PlSqlProgramType.PackageProgram && plSqlProgram.Owner != null)
					{
						accessiblePrograms = accessiblePrograms.Concat(plSqlProgram.Owner.SubPrograms.Where(p => p.RootNode.SourcePosition.IndexStart < plSqlProgram.RootNode.SourcePosition.IndexStart));
					}

					foreach (var subProgram in accessiblePrograms)
					{
						if (String.Equals(programReference.NormalizedName, subProgram.Name))
						{
							result.Metadata = subProgram.Metadata;
						}
					}
				}
			}

			programReference.SchemaObject = result.SchemaObject;

			return programReference.Metadata = result.Metadata;
		}

		public OracleQueryBlock GetQueryBlock(int position)
		{
			return QueryBlockNodes.Values
				.Where(qb => qb.RootNode.SourcePosition.ContainsIndex(position))
				.OrderByDescending(qb => qb.RootNode.Level)
				.FirstOrDefault();
		}

		public OracleColumnReference GetColumnReference(StatementGrammarNode columnIdentifer)
		{
			return AllReferenceContainers.SelectMany(c => c.ColumnReferences).SingleOrDefault(c => c.ColumnNode == columnIdentifer);
		}

		public OracleProgramReference GetProgramReference(StatementGrammarNode identifer)
		{
			return AllReferenceContainers.SelectMany(c => c.ProgramReferences).SingleOrDefault(c => c.ProgramIdentifierNode == identifer);
		}

		public OracleTypeReference GetTypeReference(StatementGrammarNode typeIdentifer)
		{
			return AllReferenceContainers.SelectMany(c => c.TypeReferences).SingleOrDefault(c => c.ObjectNode == typeIdentifer);
		}

		public OraclePlSqlVariableReference GetPlSqlVariableReference(StatementGrammarNode identifer)
		{
			return AllReferenceContainers.SelectMany(c => c.PlSqlVariableReferences).SingleOrDefault(c => c.IdentifierNode == identifer);
		}

		public OraclePlSqlExceptionReference GetPlSqlExceptionReference(StatementGrammarNode identifer)
		{
			return AllReferenceContainers.SelectMany(c => c.PlSqlExceptionReferences).SingleOrDefault(c => c.IdentifierNode == identifer);
		}

		public OracleSequenceReference GetSequenceReference(StatementGrammarNode sequenceIdentifer)
		{
			return AllReferenceContainers.SelectMany(c => c.SequenceReferences).SingleOrDefault(c => c.ObjectNode == sequenceIdentifer);
		}

		public T GetReference<T>(StatementGrammarNode identifer) where T : OracleReference
		{
			return AllReferenceContainers.SelectMany(c => c.AllReferences).OfType<T>().FirstOrDefault(r => r.AllIdentifierTerminals.Contains(identifer));
		}

		public OracleQueryBlock GetQueryBlock(StatementGrammarNode node)
		{
			var queryBlockNode = node.GetAncestor(NonTerminals.QueryBlock);
			if (queryBlockNode == null)
			{
				OracleQueryBlock queryBlock = null;
				Func<StatementGrammarNode, bool> queryBlockOrderByClausePredicate = n => String.Equals(n.Id, NonTerminals.OrderByClause) && String.Equals(n.ParentNode?.Id, NonTerminals.Subquery);

				var orderByClauseNode = queryBlockOrderByClausePredicate(node)
					? node
					: node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.NestedQuery), queryBlockOrderByClausePredicate);

				if (orderByClauseNode != null)
				{
					queryBlock = QueryBlockNodes.Values.SingleOrDefault(qb => qb.OrderByClause == orderByClauseNode);
				}
				else
				{
					var explicitColumnListNode = node.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.NestedQuery), NonTerminals.ParenthesisEnclosedIdentifierList);
					if (explicitColumnListNode != null)
					{
						queryBlock = QueryBlockNodes.Values.SingleOrDefault(qb => qb.AliasNode != null && qb.ExplicitColumnNameList == explicitColumnListNode);
					}
				}

				if (queryBlock == null)
				{
					return null;
				}

				queryBlockNode = queryBlock.RootNode;
			}

			return queryBlockNode == null ? null : QueryBlockNodes[queryBlockNode];
		}

		private void ResolveColumnObjectReferences(IEnumerable<OracleColumnReference> columnReferences, ICollection<OracleDataObjectReference> accessibleRowSourceReferences, IEnumerable<OracleDataObjectReference> parentCorrelatedRowSourceReferences)
		{
			foreach (var columnReference in columnReferences.ToArray())
			{
				if (columnReference.Placement == StatementPlacement.OrderBy)
				{
					if (columnReference.Owner.FollowingConcatenatedQueryBlock != null)
					{
						ResolveConcatenatedQueryBlockOrderByReferences(columnReference);

						continue;
					}

					if (columnReference.ObjectNode == null)
					{
						var isSelectColumnReferred = false;
						foreach (var column in columnReference.Owner.Columns)
						{
							if (String.Equals(column.NormalizedName, columnReference.NormalizedName) &&
							    (column.HasExplicitDefinition || column.AsteriskColumn.ColumnReferences[0].ObjectNode != null))
							{
								columnReference.ColumnNodeColumnReferences.Add(column.ColumnDescription);

								isSelectColumnReferred |= column.HasExplicitDefinition;
							}
						}

						if (isSelectColumnReferred)
						{
							columnReference.ColumnNodeObjectReferences.Add(columnReference.Owner.SelfObjectReference);
						}
					}
				}

				if (HasResolveHierachicalPseudocolumnRefences(columnReference))
				{
					continue;
				}

				IEnumerable<OracleDataObjectReference> effectiveAccessibleRowSourceReferences = accessibleRowSourceReferences;
				if (columnReference.Placement == StatementPlacement.Join || columnReference.Placement == StatementPlacement.TableReference)
				{
					effectiveAccessibleRowSourceReferences = effectiveAccessibleRowSourceReferences
						.Where(r => r.RootNode.SourcePosition.IndexEnd < columnReference.RootNode.SourcePosition.IndexStart);

					if (columnReference.Placement == StatementPlacement.Join)
					{
						var effectiveFromClause = columnReference.RootNode.GetAncestor(NonTerminals.FromClause);
						effectiveAccessibleRowSourceReferences = effectiveAccessibleRowSourceReferences
							.Where(r => r.RootNode.GetAncestor(NonTerminals.FromClause) == effectiveFromClause);
					}
				}

				if (columnReference.Placement == StatementPlacement.RecursiveSearchOrCycleClause)
				{
					var matchedColumns = columnReference.Owner.Columns
						.Where(c => !c.IsAsterisk && String.Equals(c.NormalizedName, columnReference.NormalizedName) && !columnReference.Owner.AttachedColumns.Contains(c))
						.Join(columnReference.Owner.SelfObjectReference.Columns, c => c.NormalizedName, c => c.Name, (sc, c) => c);
					
					columnReference.ColumnNodeColumnReferences.AddRange(matchedColumns);
				}
				else
				{
					ResolveColumnReference(effectiveAccessibleRowSourceReferences, columnReference, false);
					if (columnReference.ColumnNodeObjectReferences.Count == 0)
					{
						ResolveColumnReference(parentCorrelatedRowSourceReferences, columnReference, true);
					}
				}

				var referencesSelectListColumn =
					(columnReference.Placement == StatementPlacement.OrderBy || columnReference.Placement == StatementPlacement.RecursiveSearchOrCycleClause) &&
					columnReference.ColumnNodeObjectReferences.Count == 0 &&
					columnReference.OwnerNode == null && columnReference.ColumnNodeColumnReferences.Count > 0;
				if (referencesSelectListColumn)
				{
					columnReference.ColumnNodeObjectReferences.Add(columnReference.Owner.SelfObjectReference);
				}

				var columnDescription = columnReference.ColumnNodeColumnReferences.FirstOrDefault();

				if (columnDescription != null && columnReference.ColumnNodeObjectReferences.Count == 1)
				{
					columnReference.ColumnDescription = columnDescription;
				}

				TryColumnReferenceAsProgramOrSequenceReference(columnReference, false);
			}
		}

		private static bool HasResolveHierachicalPseudocolumnRefences(OracleColumnReference columnReference)
		{
			var hierarchicalClauseReference = columnReference.Owner?.HierarchicalClauseReference;
			if (hierarchicalClauseReference == null || columnReference.ObjectNode != null || columnReference.Name.IsQuoted())
			{
				return false;
			}

			switch (columnReference.NormalizedName)
			{
				case OracleHierarchicalClauseReference.ColumnNameConnectByIsLeaf:
					columnReference.ColumnNodeColumnReferences.Add(hierarchicalClauseReference.ConnectByIsLeafColumn);
					columnReference.ColumnDescription = hierarchicalClauseReference.ConnectByIsLeafColumn;
					columnReference.ColumnNodeObjectReferences.Add(hierarchicalClauseReference);
					return true;

				case OracleHierarchicalClauseReference.ColumnNameConnectByIsCycle:
					columnReference.ColumnNodeColumnReferences.Add(hierarchicalClauseReference.ConnectByIsCycleColumn);
					columnReference.ColumnDescription = hierarchicalClauseReference.ConnectByIsCycleColumn;
					columnReference.ColumnNodeObjectReferences.Add(hierarchicalClauseReference);
					return true;

				default:
					return false;
			}
		}

		private void ResolveColumnReference(IEnumerable<OracleDataObjectReference> rowSources, OracleColumnReference columnReference, bool correlatedRowSources)
		{
			var hasColumnReferencesToSelectList = columnReference.Placement == StatementPlacement.OrderBy && columnReference.ColumnNodeColumnReferences.Count > 0;

			foreach (var rowSourceReference in rowSources)
			{
				if (columnReference.Placement == StatementPlacement.Join && _joinPartitionColumnTableReferenceRootNodes.TryGetValue(columnReference.ColumnNode, out OracleDataObjectReference joinPartitionObjectReference) &&
	joinPartitionObjectReference != rowSourceReference)
				{
					continue;
				}

				if (columnReference.ObjectNode != null)
				{
					if (rowSourceReference.FullyQualifiedObjectName == columnReference.FullyQualifiedObjectName ||
					    (columnReference.OwnerNode == null &&
					     rowSourceReference.Type == ReferenceType.SchemaObject &&
					     String.Equals(rowSourceReference.FullyQualifiedObjectName.NormalizedName, columnReference.FullyQualifiedObjectName.NormalizedName)))
					{
						columnReference.ObjectNodeObjectReferences.Add(rowSourceReference);
						columnReference.IsCorrelated = correlatedRowSources;
					}
				}
				else if (String.Equals(columnReference.ColumnNode.Id, Terminals.RowNumberPseudocolumn))
				{
					break;
				}

				if (!String.IsNullOrEmpty(columnReference.FullyQualifiedObjectName.NormalizedName) &&
				columnReference.ObjectNodeObjectReferences.Count == 0)
				{
					continue;
				}

				if (rowSourceReference.RootNode != null && _joinTableReferenceNodes.TryGetValue(rowSourceReference.RootNode, out OracleJoinDescription joinDescription) &&
	joinDescription.Definition == JoinDefinition.Natural && joinDescription.Columns.Contains(columnReference.NormalizedName))
				{
					continue;
				}

				AddColumnNodeColumnReferences(rowSourceReference, columnReference, hasColumnReferencesToSelectList);
			}
		}

		private void AddColumnNodeColumnReferences(OracleDataObjectReference rowSourceReference, OracleColumnReference columnReference, bool hasColumnReferencesToSelectList)
		{
			var matchedColumns = Enumerable.Empty<OracleSelectListColumn>();
			var newColumnReferences = new List<OracleColumn>();

			if (columnReference.RootNode != null &&
	_oldOuterJoinColumnReferences.TryGetValue(columnReference.RootNode, out StatementGrammarNode oldOuterJoinOperatorNode))
			{
				columnReference.OldOuterJoinOperatorNode = oldOuterJoinOperatorNode;
				rowSourceReference.IsOuterJoined = true;
			}

			switch (rowSourceReference.Type)
			{
				case ReferenceType.SchemaObject:
					if (rowSourceReference.SchemaObject == null)
					{
						return;
					}

					goto case ReferenceType.TableCollection;

				case ReferenceType.JsonTable:
				case ReferenceType.XmlTable:
					var specialTableReference = (OracleSpecialTableReference)rowSourceReference;
					matchedColumns = GetColumnReferenceMatchingColumns(specialTableReference, columnReference, specialTableReference.ColumnDefinitions.Where(c => String.Equals(c.NormalizedName, columnReference.NormalizedName)));
					break;

				case ReferenceType.SqlModel:
				case ReferenceType.PivotTable:
				case ReferenceType.TableCollection:
					newColumnReferences.AddRange(GetColumnReferenceMatchingColumns(rowSourceReference, columnReference, rowSourceReference.Columns.Concat(rowSourceReference.Pseudocolumns).Where(c => String.Equals(c.Name, columnReference.NormalizedName))));
					break;

				case ReferenceType.InlineView:
				case ReferenceType.CommonTableExpression:
					if (columnReference.ObjectNode != null && !String.Equals(columnReference.FullyQualifiedObjectName.NormalizedName, rowSourceReference.FullyQualifiedObjectName.NormalizedName))
					{
						break;
					}

					matchedColumns = rowSourceReference.QueryBlocks.SelectMany(qb => qb.NamedColumns[columnReference.NormalizedName]);
					break;

				case ReferenceType.HierarchicalClause:
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}

			foreach (var column in matchedColumns)
			{
				column.RegisterOuterReference();
				newColumnReferences.Add(column.ColumnDescription);
			}

			if (hasColumnReferencesToSelectList || newColumnReferences.Count == 0)
			{
				return;
			}

			columnReference.ColumnNodeColumnReferences.AddRange(newColumnReferences);
			columnReference.ColumnNodeObjectReferences.Add(rowSourceReference);
		}

		private static IEnumerable<T> GetColumnReferenceMatchingColumns<T>(OracleDataObjectReference rowSourceReference, OracleColumnReference columnReference, IEnumerable<T> matchedColumns)
		{
			return columnReference.ObjectNode == null || IsTableReferenceValid(columnReference, rowSourceReference) ? matchedColumns : Enumerable.Empty<T>();
		}

		protected bool TryColumnReferenceAsProgramOrSequenceReference(OracleColumnReference columnReference, bool includePlSqlObjects)
		{
			if (columnReference.ColumnNodeColumnReferences.Count != 0 || columnReference.ReferencesAllColumns || columnReference.Container == null)
			{
				return false;
			}

			var programReference =
				new OracleProgramReference
				{
					ProgramIdentifierNode = columnReference.ColumnNode,
					ParameterReferences = ProgramParameterReference.EmptyArray,
					DatabaseLinkNode = OracleReferenceBuilder.GetDatabaseLinkFromIdentifier(columnReference.ColumnNode)
				};

			programReference.CopyPropertiesFrom(columnReference);

			UpdateProgramReferenceWithMetadata(programReference, includePlSqlObjects);

			if (programReference.Metadata == null && programReference.SchemaObject == null)
			{
				return TryResolveSequenceReference(columnReference);
			}

			if (_rowSourceTableCollectionReferences.TryGetValue(columnReference, out OracleTableCollectionReference tableCollectionReference))
			{
				tableCollectionReference.RowSourceReference = programReference;
			}

			columnReference.Container.ProgramReferences.Add(programReference);
			columnReference.Container.ColumnReferences.Remove(columnReference);
			return true;
		}

		private static void ResolveConcatenatedQueryBlockOrderByReferences(OracleColumnReference columnReference)
		{
			if (columnReference.ObjectNode != null)
			{
				return;
			}

			var isRecognized = true;
			var maximumReferences = new OracleColumn[0];
			var concatenatedQueryBlocks = new List<OracleQueryBlock> { columnReference.Owner };
			concatenatedQueryBlocks.AddRange(columnReference.Owner.AllFollowingConcatenatedQueryBlocks);
			for (var i = 0; i < concatenatedQueryBlocks.Count; i++)
			{
				var queryBlockColumnAliasReferences = concatenatedQueryBlocks[i].Columns.Where(c => String.Equals(c.NormalizedName, columnReference.NormalizedName)).Select(c => c.ColumnDescription).ToArray();

				isRecognized &= queryBlockColumnAliasReferences.Length > 0 || i == concatenatedQueryBlocks.Count - 1;

				if (queryBlockColumnAliasReferences.Length > maximumReferences.Length)
				{
					maximumReferences = queryBlockColumnAliasReferences;
				}
			}

			if (isRecognized)
			{
				columnReference.ColumnNodeObjectReferences.Add(columnReference.Owner.SelfObjectReference);
				columnReference.ColumnNodeColumnReferences.AddRange(maximumReferences);
			}
		}

		private bool TryResolveSequenceReference(OracleColumnReference columnReference)
		{
			if (columnReference.ObjectNode == null || columnReference.ObjectNodeObjectReferences.Count > 0)
			{
				return false;
			}

			var identifierCandidates = _databaseModel.GetPotentialSchemaObjectIdentifiers(columnReference.FullyQualifiedObjectName);
			var schemaObject = _databaseModel.GetFirstSchemaObject<OracleSequence>(identifierCandidates);
			if (schemaObject == null)
			{
				return false;
			}

			var sequenceReference = new OracleSequenceReference
			{
				ObjectNode = columnReference.ObjectNode, DatabaseLinkNode = OracleReferenceBuilder.GetDatabaseLinkFromIdentifier(columnReference.ColumnNode), SchemaObject = schemaObject
			};

			sequenceReference.CopyPropertiesFrom(columnReference);

			var sequence = (OracleSequence)schemaObject.GetTargetSchemaObject();
			columnReference.Container.SequenceReferences.Add(sequenceReference);
			var pseudoColumn = sequence.Columns.SingleOrDefault(c => c.Name == columnReference.NormalizedName);
			if (pseudoColumn != null)
			{
				columnReference.ColumnNodeObjectReferences.Add(sequenceReference);
				columnReference.ColumnNodeColumnReferences.Add(pseudoColumn);
				columnReference.ColumnDescription = pseudoColumn;
			}

			if (columnReference.ObjectNode != null)
			{
				columnReference.ObjectNodeObjectReferences.Add(sequenceReference);
			}

			return true;
		}

		private static bool IsTableReferenceValid(OracleReference column, OracleReference schemaObject)
		{
			var objectName = column.FullyQualifiedObjectName;
			return (String.IsNullOrEmpty(objectName.NormalizedName) ||
			        String.Equals(objectName.NormalizedName, schemaObject.FullyQualifiedObjectName.NormalizedName)) &&
			       (String.IsNullOrEmpty(objectName.NormalizedOwner) || String.Equals(objectName.NormalizedOwner, schemaObject.FullyQualifiedObjectName.NormalizedOwner));
		}

		private void FindJoinColumnReferences(OracleQueryBlock queryBlock)
		{
			var fromClauses = StatementGrammarNode.GetAllChainedClausesByPath(queryBlock.RootNode[NonTerminals.FromClause], null, NonTerminals.FromClauseChained, NonTerminals.FromClause);
			foreach (var fromClause in fromClauses)
			{
				var joinClauses = fromClause.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.NestedQuery) && !String.Equals(n.Id, NonTerminals.FromClause), NonTerminals.JoinClause);
				foreach (var joinClause in joinClauses)
				{
					var joinCondition = joinClause.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.JoinClause), NonTerminals.JoinColumnsOrCondition).SingleOrDefault();
					var joinDefinition = String.Equals(joinClause[NonTerminals.InnerJoinClause]?.FirstTerminalNode.Id, Terminals.Natural) || String.Equals(joinCondition?.FirstTerminalNode.Id, Terminals.Using) ? JoinDefinition.Natural : JoinDefinition.Explicit;

					var joinDescription = new OracleJoinDescription
					{
						Definition = joinDefinition, Type = JoinType.Inner
					};

					IReadOnlyList<StatementGrammarNode> masterPartitionIdentifiers = null;
					IReadOnlyList<StatementGrammarNode> slavePartitionIdentifiers = null;
					var outerJoinClause = joinClause[NonTerminals.OuterJoinClause];
					if (outerJoinClause != null)
					{
						var joinTypeNode = outerJoinClause[NonTerminals.NaturalOrOuterJoinType, NonTerminals.OuterJoinTypeWithKeyword];
						if (joinTypeNode != null)
						{
							switch (joinTypeNode.FirstTerminalNode.Id)
							{
								case Terminals.Left:
									joinDescription.Type = JoinType.Left;
									break;
								case Terminals.Right:
									joinDescription.Type = JoinType.Right;
									break;
								case Terminals.Full:
									joinDescription.Type = JoinType.Full;
									break;
							}
						}

						var masterJoinPartitionClauseCandidate = outerJoinClause[0];
						if (String.Equals(masterJoinPartitionClauseCandidate?.Id, NonTerminals.OuterJoinPartitionClause))
						{
							joinDescription.MasterPartitionClause = masterJoinPartitionClauseCandidate;
							masterPartitionIdentifiers = masterJoinPartitionClauseCandidate.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBlock, Terminals.Identifier).ToArray();
							ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, masterPartitionIdentifiers, StatementPlacement.Join, null);
							CreateGrammarSpecificFunctionReferences(GetGrammarSpecificFunctionNodes(masterJoinPartitionClauseCandidate), queryBlock, queryBlock, StatementPlacement.Join, null);
						}

						joinDescription.SlavePartitionClause = outerJoinClause.ChildNodes.SingleOrDefault(n => n != masterJoinPartitionClauseCandidate && String.Equals(n.Id, NonTerminals.OuterJoinPartitionClause));
						if (joinDescription.SlavePartitionClause != null)
						{
							slavePartitionIdentifiers = joinDescription.SlavePartitionClause.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBlock, Terminals.Identifier).ToArray();
							ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, slavePartitionIdentifiers, StatementPlacement.Join, null);
							CreateGrammarSpecificFunctionReferences(GetGrammarSpecificFunctionNodes(joinDescription.SlavePartitionClause), queryBlock, queryBlock, StatementPlacement.Join, null);
						}
					}

					var joinClauseParent = joinClause.ParentNode;
					var masterTableReferenceNode = String.Equals(joinClauseParent.Id, NonTerminals.TableReferenceJoinClause) ? joinClauseParent[NonTerminals.TableReference] : joinClauseParent[0]?[NonTerminals.TableReference];

					queryBlock.JoinDescriptions.Add(joinDescription);

					if (masterTableReferenceNode != null && _rootNodeObjectReference.TryGetValue(masterTableReferenceNode, out OracleDataObjectReference objectReference))
					{
						joinDescription.MasterObjectReference = objectReference;
						StorePartitionColumnIdentifierTableReferenceRelations(masterPartitionIdentifiers, objectReference);
					}

					var tableReferenceNode = joinClause[0][NonTerminals.TableReference];
					if (tableReferenceNode != null)
					{
						_rootNodeObjectReference.TryGetValue(tableReferenceNode, out objectReference);
						joinDescription.SlaveObjectReference = objectReference;

						_joinTableReferenceNodes.Add(tableReferenceNode, joinDescription);

						StorePartitionColumnIdentifierTableReferenceRelations(slavePartitionIdentifiers, objectReference);
					}

					if (joinCondition == null)
					{
						continue;
					}

					var identifiers = joinCondition.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBlock, Terminals.Identifier).ToArray();
					if (joinDescription.Definition == JoinDefinition.Natural)
					{
						joinDescription.Columns = identifiers.Select(i => i.Token.Value.ToQuotedIdentifier()).ToHashSet();
					}

					ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, identifiers, StatementPlacement.Join, null);

					var joinCondifitionClauseGrammarSpecificFunctions = GetGrammarSpecificFunctionNodes(joinCondition);
					CreateGrammarSpecificFunctionReferences(joinCondifitionClauseGrammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.Join, null);
				}
			}
		}

		private void StorePartitionColumnIdentifierTableReferenceRelations(IReadOnlyList<StatementGrammarNode> partitionColumnIdentifiers, OracleDataObjectReference objectReference)
		{
			if (partitionColumnIdentifiers == null || objectReference == null)
			{
				return;
			}

			foreach (var identifier in partitionColumnIdentifiers)
			{
				_joinPartitionColumnTableReferenceRootNodes.Add(identifier, objectReference);
			}
		}

		private void FindWhereGroupByHavingReferences(OracleQueryBlock queryBlock)
		{
			IEnumerable<StatementGrammarNode> identifiers;
			queryBlock.WhereClause = queryBlock.RootNode[NonTerminals.WhereClause];
			if (queryBlock.WhereClause != null)
			{
				identifiers = queryBlock.WhereClause.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level, Terminals.RowNumberPseudocolumn);
				ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, identifiers, StatementPlacement.Where, null);

				var whereClauseGrammarSpecificFunctions = GetGrammarSpecificFunctionNodes(queryBlock.WhereClause);
				CreateGrammarSpecificFunctionReferences(whereClauseGrammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.Where, null);
			}

			queryBlock.GroupByClause = queryBlock.RootNode[NonTerminals.GroupByClause];
			if (queryBlock.GroupByClause != null)
			{
				identifiers = queryBlock.GroupByClause.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.NestedQuery), Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level, Terminals.RowNumberPseudocolumn);
				ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, identifiers, StatementPlacement.GroupBy, null);

				var groupByClauseGrammarSpecificFunctions = GetGrammarSpecificFunctionNodes(queryBlock.GroupByClause);
				CreateGrammarSpecificFunctionReferences(groupByClauseGrammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.GroupBy, null);
			}

			queryBlock.HavingClause = queryBlock.RootNode[NonTerminals.HavingClause];
			if (queryBlock.HavingClause == null)
			{
				return;
			}

			identifiers = queryBlock.HavingClause.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level, Terminals.RowNumberPseudocolumn);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, identifiers, StatementPlacement.Having, null);

			var havingClauseGrammarSpecificFunctions = GetGrammarSpecificFunctionNodes(queryBlock.HavingClause);
			CreateGrammarSpecificFunctionReferences(havingClauseGrammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.Having, null);
		}

		private void ResolveOrderByReferences(OracleQueryBlock queryBlock)
		{
			if (queryBlock.PrecedingConcatenatedQueryBlock != null)
			{
				return;
			}

			queryBlock.OrderByClause = queryBlock.RootNode.GetAncestor(NonTerminals.Subquery)[NonTerminals.OrderByClause];
			if (queryBlock.OrderByClause == null)
			{
				return;
			}

			var identifiers = queryBlock.OrderByClause.GetDescendantsWithinSameQueryBlock(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, queryBlock, identifiers, StatementPlacement.OrderBy, null);
			var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(queryBlock.OrderByClause);
			CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, queryBlock, queryBlock, StatementPlacement.OrderBy, null);
		}

		protected IEnumerable<StatementGrammarNode> GetGrammarSpecificFunctionNodes(StatementGrammarNode sourceNode)
		{
			return sourceNode.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBlock, Terminals.Count, Terminals.Trim, Terminals.CharacterCode, Terminals.Cast, Terminals.Extract, Terminals.XmlRoot, Terminals.XmlElement, Terminals.XmlSerialize, Terminals.XmlForest, Terminals.NegationOrNull, Terminals.JsonExists, Terminals.JsonQuery, Terminals.JsonValue, Terminals.ListAggregation, NonTerminals.AggregateFunction, NonTerminals.AnalyticFunction, NonTerminals.WithinGroupAggregationFunction, NonTerminals.ConversionFunction);
		}

		protected void ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(OracleQueryBlock queryBlock, OracleReferenceContainer referenceContainer, IEnumerable<StatementGrammarNode> identifiers, StatementPlacement placement, OracleSelectListColumn selectListColumn, Func<StatementGrammarNode, StatementGrammarNode> getPrefixNonTerminalFromIdentiferFunction = null, Func<StatementGrammarNode, IEnumerable<StatementGrammarNode>> getFunctionCallNodesFromIdentifierFunction = null)
		{
			foreach (var identifier in identifiers)
			{
				ResolveColumnFunctionOrDataTypeReferenceFromIdentifier(queryBlock, referenceContainer, identifier, placement, selectListColumn, getPrefixNonTerminalFromIdentiferFunction, getFunctionCallNodesFromIdentifierFunction);
			}
		}

		private static OracleReference ResolveColumnFunctionOrDataTypeReferenceFromIdentifier(OracleQueryBlock queryBlock, OracleReferenceContainer referenceContainer, StatementGrammarNode identifier, StatementPlacement placement, OracleSelectListColumn selectListColumn, Func<StatementGrammarNode, StatementGrammarNode> getPrefixNonTerminalFromIdentiferFunction, Func<StatementGrammarNode, IEnumerable<StatementGrammarNode>> getExtraFunctionCallNodesFromIdentifierFunction)
		{
			var hasNotDatabaseLink = OracleReferenceBuilder.GetDatabaseLinkFromIdentifier(identifier) == null;
			var dataTypeReference = ResolveDataTypeReference(queryBlock, referenceContainer, identifier, placement, selectListColumn);
			if (dataTypeReference != null)
			{
				return dataTypeReference;
			}

			var prefixNonTerminal = getPrefixNonTerminalFromIdentiferFunction == null ? GetPrefixNodeFromPrefixedColumnReference(identifier) : getPrefixNonTerminalFromIdentiferFunction(identifier);

			var functionCallNodesSource = GetFunctionCallNodes(identifier);
			if (getExtraFunctionCallNodesFromIdentifierFunction != null)
			{
				functionCallNodesSource = functionCallNodesSource.Concat(getExtraFunctionCallNodesFromIdentifierFunction(identifier));
			}

			var functionCallNodes = functionCallNodesSource.ToArray();

			var isSequencePseudocolumnCandidate = prefixNonTerminal != null && identifier.Token.Value.ToQuotedIdentifier().In(OracleSequence.NormalizedColumnNameNextValue, OracleSequence.NormalizedColumnNameCurrentValue);
			if (functionCallNodes.Length == 0 && (hasNotDatabaseLink || isSequencePseudocolumnCandidate))
			{
				var columnReference = CreateColumnReference(referenceContainer, queryBlock, selectListColumn, placement, identifier, prefixNonTerminal);
				referenceContainer.ColumnReferences.Add(columnReference);
				return columnReference;
			}

			var programReference = CreateProgramReference(referenceContainer, queryBlock, selectListColumn, placement, identifier, prefixNonTerminal, functionCallNodes);
			referenceContainer.ProgramReferences.Add(programReference);
			return programReference;
		}

		private static OracleDataTypeReference ResolveDataTypeReference(OracleQueryBlock queryBlock, OracleReferenceContainer referenceContainer, StatementGrammarNode identifier, StatementPlacement placement, OracleSelectListColumn selectListColumn)
		{
			if (!String.Equals(identifier.ParentNode.ParentNode.Id, NonTerminals.DataType))
			{
				return null;
			}

			var dataTypeReference = OracleReferenceBuilder.CreateDataTypeReference(queryBlock, selectListColumn, placement, identifier);
			referenceContainer.DataTypeReferences.Add(dataTypeReference);
			return dataTypeReference;
		}

		private static StatementGrammarNode GetPrefixNodeFromPrefixedColumnReference(StatementGrammarNode identifier)
		{
			var prefixedColumnReferenceNode = identifier.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.Expression), NonTerminals.PrefixedColumnReference) ?? identifier.ParentNode;
			return prefixedColumnReferenceNode?[NonTerminals.Prefix];
		}

		private void FindSelectListReferences(OracleQueryBlock queryBlock)
		{
			var distinctModifierNode = queryBlock.RootNode[NonTerminals.DistinctModifier];
			queryBlock.HasDistinctResultSet = distinctModifierNode != null && distinctModifierNode.FirstTerminalNode.Id.In(Terminals.Distinct, Terminals.Unique);

			queryBlock.SelectList = queryBlock.RootNode[NonTerminals.SelectList];
			if (queryBlock.SelectList?.FirstTerminalNode == null)
			{
				return;
			}

			if (queryBlock.Type == QueryBlockType.CommonTableExpression)
			{
				queryBlock.ExplicitColumnNameList = queryBlock.AliasNode.ParentNode[NonTerminals.ParenthesisEnclosedIdentifierList];
				if (queryBlock.ExplicitColumnNameList != null)
				{
					queryBlock.ExplicitColumnNames = queryBlock.ExplicitColumnNameList.GetDescendants(Terminals.Identifier).ToDictionary(t => t, t => t.Token.Value.ToQuotedIdentifier());
				}
			}

			if (String.Equals(queryBlock.SelectList.FirstTerminalNode.Id, Terminals.Asterisk))
			{
				var asteriskNode = queryBlock.SelectList.ChildNodes[0];
				var column = new OracleSelectListColumn(this, null)
				{
					RootNode = asteriskNode, Owner = queryBlock, IsAsterisk = true
				};

				column.ColumnReferences.Add(CreateColumnReference(column, queryBlock, column, StatementPlacement.SelectList, asteriskNode, null));

				_asteriskTableReferences[column] = new List<OracleDataObjectReference>(queryBlock.ObjectReferences);

				queryBlock.AddSelectListColumn(column);
			}
			else
			{
				var columnExpressions = StatementGrammarNode.GetAllChainedClausesByPath(queryBlock.SelectList[NonTerminals.AliasedExpressionOrAllTableColumns], n => n.ParentNode, NonTerminals.SelectExpressionExpressionChainedList, NonTerminals.AliasedExpressionOrAllTableColumns);
				var columnExpressionsIdentifierLookup = queryBlock.Terminals
					.Where(t => t.SourcePosition.IndexStart >= queryBlock.SelectList.SourcePosition.IndexStart && t.SourcePosition.IndexEnd <= queryBlock.SelectList.SourcePosition.IndexEnd && (StandardIdentifierIds.Contains(t.Id) || t.ParentNode.Id.In(NonTerminals.AggregateFunction, NonTerminals.AnalyticFunction, NonTerminals.WithinGroupAggregationFunction, NonTerminals.ConversionFunction) || String.Equals(t.ParentNode.ParentNode.Id, NonTerminals.DataType)))
					.ToLookup(t => t.GetAncestor(NonTerminals.AliasedExpressionOrAllTableColumns));

				foreach (var columnExpression in columnExpressions)
				{
					CancellationToken.ThrowIfCancellationRequested();

					var columnAliasNode = columnExpression.LastTerminalNode != null && String.Equals(columnExpression.LastTerminalNode.Id, Terminals.ColumnAlias) ? columnExpression.LastTerminalNode : null;

					var column = new OracleSelectListColumn(this, null)
					{
						AliasNode = columnAliasNode, RootNode = columnExpression, Owner = queryBlock
					};

					var asteriskNode = columnExpression.LastTerminalNode != null && String.Equals(columnExpression.LastTerminalNode.Id, Terminals.Asterisk) ? columnExpression.LastTerminalNode : null;

					if (asteriskNode != null)
					{
						column.IsAsterisk = true;

						var prefixNonTerminal = asteriskNode.ParentNode.ChildNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.Prefix));
						var columnReference = CreateColumnReference(column, queryBlock, column, StatementPlacement.SelectList, asteriskNode, prefixNonTerminal);
						column.ColumnReferences.Add(columnReference);

						var tableReferences = queryBlock.ObjectReferences.Where(t => t.FullyQualifiedObjectName == columnReference.FullyQualifiedObjectName || (columnReference.ObjectNode == null && String.Equals(t.FullyQualifiedObjectName.NormalizedName, columnReference.FullyQualifiedObjectName.NormalizedName)));
						_asteriskTableReferences[column] = new List<OracleDataObjectReference>(tableReferences);
					}
					else
					{
						var columnExpressionIdentifiers = columnExpressionsIdentifierLookup[columnExpression].ToArray();
						var identifiers = columnExpressionIdentifiers.Where(t => t.Id.In(Terminals.Identifier, Terminals.RowIdPseudocolumn, Terminals.Level, Terminals.RowNumberPseudocolumn, Terminals.User) || String.Equals(t.ParentNode.ParentNode.Id, NonTerminals.DataType)).ToArray();

						var previousColumnReferences = column.ColumnReferences.Count;
						ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(queryBlock, column, identifiers, StatementPlacement.SelectList, column);

						if (identifiers.Length == 1)
						{
							var identifier = identifiers[0];
							var parentExpression = String.Equals(identifier.Id, Terminals.RowNumberPseudocolumn) ? identifier.ParentNode : identifier.ParentNode.ParentNode.ParentNode;
							column.IsDirectReference = String.Equals(parentExpression.Id, NonTerminals.Expression) && parentExpression.ChildNodes.Count == 1 && String.Equals(parentExpression.ParentNode.Id, NonTerminals.AliasedExpression);
						}

						var columnReferenceAdded = column.ColumnReferences.Count > previousColumnReferences;
						if (columnReferenceAdded && columnAliasNode == null && column.IsDirectReference)
						{
							column.AliasNode = identifiers[0];
						}

						var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(columnExpression);

						CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, column, queryBlock, StatementPlacement.SelectList, column);
					}

					queryBlock.AddSelectListColumn(column);
				}
			}
		}

		protected static IReadOnlyList<OracleProgramReference> CreateGrammarSpecificFunctionReferences(IEnumerable<StatementGrammarNode> grammarSpecificFunctions, OracleReferenceContainer container, OracleQueryBlock queryBlock, StatementPlacement placement, OracleSelectListColumn selectListColumn)
		{
			var newProgramReferences = new List<OracleProgramReference>();
			foreach (var identifierNode in grammarSpecificFunctions.Select(n => n.FirstTerminalNode).Distinct())
			{
				var isNegationOrNull = String.Equals(identifierNode.Id, Terminals.NegationOrNull);
				var rootNode =
					isNegationOrNull
						? identifierNode.GetAncestor(NonTerminals.Condition)
						: identifierNode.GetAncestor(NonTerminals.Expression);

				StatementGrammarNode analyticClauseNode = null;
				var functionRootNode = rootNode;
				if (!isNegationOrNull)
					functionRootNode = rootNode[0];

				switch (functionRootNode.Id)
				{
					case NonTerminals.AnalyticFunctionCall:
						analyticClauseNode = functionRootNode[NonTerminals.AnalyticClause];
						break;
					case NonTerminals.AggregateFunctionCall:
						analyticClauseNode = functionRootNode[NonTerminals.AnalyticOrKeepClauseOrModelAggregateFunctionExpression, NonTerminals.AnalyticClause] ?? functionRootNode[NonTerminals.OverQueryPartitionClause];
						break;
				}

				var parameterList = functionRootNode.ChildNodes.SingleOrDefault(n => n.Id.In(NonTerminals.ParenthesisEnclosedExpressionListWithMandatoryExpressions, NonTerminals.ParenthesisEnclosedAggregationFunctionParameters, NonTerminals.CountAsteriskParameter, NonTerminals.AggregateFunctionParameter, NonTerminals.ParenthesisEnclosedExpressionListWithIgnoreNulls, NonTerminals.ParenthesisEnclosedCondition, NonTerminals.XmlExistsParameterClause, NonTerminals.XmlElementParameterClause, NonTerminals.XmlParseFunctionParameterClause, NonTerminals.XmlRootFunctionParameterClause, NonTerminals.XmlSerializeFunctionParameterClause, NonTerminals.XmlSimpleFunctionParameterClause, NonTerminals.XmlQueryParameterClause, NonTerminals.CastFunctionParameterClause, NonTerminals.JsonQueryParameterClause, NonTerminals.JsonExistsParameterClause, NonTerminals.JsonValueParameterClause, NonTerminals.ExtractFunctionParameterClause, NonTerminals.TrimParameterClause, NonTerminals.CharacterCodeParameterClause, NonTerminals.ListAggregationParameters, NonTerminals.ConversionFunctionParameters));
				var parameterNodes = new List<StatementGrammarNode>();
				StatementGrammarNode firstParameterExpression = null;
				if (parameterList != null)
				{
					switch (parameterList.Id)
					{
						case NonTerminals.ParenthesisEnclosedCondition:
							parameterNodes.Add(parameterList[NonTerminals.Condition]);
							break;
						case NonTerminals.CountAsteriskParameter:
							parameterNodes.Add(parameterList[Terminals.Asterisk]);
							break;
						case NonTerminals.CastFunctionParameterClause:
							parameterNodes.AddIfNotNull(parameterList[NonTerminals.ExpressionOrMultiset]);
							parameterNodes.AddIfNotNull(parameterList[NonTerminals.AsDataType, NonTerminals.DataType]);
							break;
						case NonTerminals.XmlQueryParameterClause:
							parameterNodes.AddIfNotNull(parameterList[NonTerminals.Expression]);
							break;
						case NonTerminals.XmlElementParameterClause:
							var xmlElementParameter = parameterList[NonTerminals.XmlNameOrEvaluatedName];
							if (xmlElementParameter != null)
							{
								parameterNodes.AddIfNotNull(xmlElementParameter[Terminals.XmlAlias]);
								parameterNodes.AddIfNotNull(xmlElementParameter[NonTerminals.Expression]);
							}
							break;
						case NonTerminals.ExtractFunctionParameterClause:
						case NonTerminals.XmlParseFunctionParameterClause:
						case NonTerminals.XmlSerializeFunctionParameterClause:
						case NonTerminals.TrimParameterClause:
						case NonTerminals.CharacterCodeParameterClause:
						case NonTerminals.XmlRootFunctionParameterClause:
							parameterNodes.AddIfNotNull(parameterList[NonTerminals.Expression]);
							break;
						case NonTerminals.JsonQueryParameterClause:
						case NonTerminals.JsonValueParameterClause:
						case NonTerminals.JsonExistsParameterClause:
							parameterNodes.AddIfNotNull(parameterList[NonTerminals.Expression]);
							parameterNodes.AddIfNotNull(parameterList[Terminals.StringLiteral]);
							break;
						case NonTerminals.XmlSimpleFunctionParameterClause:
							var expressionNodes = parameterList.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.NestedQuery), NonTerminals.Expression);
							parameterNodes.AddRange(expressionNodes);
							break;
						case NonTerminals.ConversionFunctionParameters:
							firstParameterExpression = parameterList[NonTerminals.OptionalParameterExpression];
							parameterNodes.Add(firstParameterExpression);
							goto default;
						case NonTerminals.AggregateFunctionParameter:
						case NonTerminals.ParenthesisEnclosedExpressionListWithIgnoreNulls:
							firstParameterExpression = parameterList[NonTerminals.Expression];
							parameterNodes.Add(firstParameterExpression);
							goto default;
						default:
							var expressionListNodes = parameterList.GetPathFilterDescendants(n => n != firstParameterExpression && !n.Id.In(NonTerminals.NestedQuery, NonTerminals.ParenthesisEnclosedAggregationFunctionParameters, NonTerminals.Expression), NonTerminals.ExpressionList, NonTerminals.OptionalParameterExpressionList).Select(n => n.ChildNodes.FirstOrDefault());
							parameterNodes.AddRange(expressionListNodes);
							break;
					}
				}

				var programReference =
					new OracleProgramReference
					{
						ProgramIdentifierNode = identifierNode,
						RootNode = rootNode,
						Owner = queryBlock,
						Container = container,
						AnalyticClauseNode = analyticClauseNode,
						ParameterListNode = parameterList,
						ParameterReferences =
							parameterNodes
								.Select(ResolveParameterReference)
								.ToArray(),
						SelectListColumn = selectListColumn,
						Placement = placement
					};

				container.ProgramReferences.Add(programReference);
				newProgramReferences.Add(programReference);
			}

			return newProgramReferences.AsReadOnly();
		}

		private static IEnumerable<StatementGrammarNode> GetFunctionCallNodes(StatementNode identifier)
		{
			return identifier.ParentNode.ChildNodes.Where(n => n.Id.In(NonTerminals.ParenthesisEnclosedAggregationFunctionParameters, NonTerminals.AnalyticClause));
		}

		protected static IReadOnlyList<ProgramParameterReference> ResolveParameterReferences(StatementGrammarNode parameterList)
		{
			if (parameterList == null)
			{
				return ProgramParameterReference.EmptyArray;
			}

			return parameterList
				.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.NestedQuery, NonTerminals.ParenthesisEnclosedAggregationFunctionParameters, NonTerminals.AggregateFunctionCall, NonTerminals.AnalyticFunctionCall, NonTerminals.AnalyticClause, NonTerminals.Expression), NonTerminals.ExpressionList, NonTerminals.OptionalParameterExpressionList).Select(n => n.ChildNodes.FirstOrDefault())
				.Select(ResolveParameterReference)
				.ToArray();
		}

		private static ProgramParameterReference ResolveParameterReference(StatementGrammarNode node)
		{
			var parameterReference =
				new ProgramParameterReference
				{
					ParameterNode = node,
					ValueNode = node
				};

			if (String.Equals(node.ParentNode.Id, NonTerminals.OptionalParameterExpression))
			{
				parameterReference.ParameterNode = node.ParentNode;
			}
			else if (String.Equals(node.Id, NonTerminals.OptionalParameterExpression))
			{
				parameterReference.ValueNode = node[NonTerminals.Expression];
			}

			if (String.Equals(parameterReference.ParameterNode.FirstTerminalNode?.Id, Terminals.ParameterIdentifier))
				parameterReference.OptionalIdentifierTerminal = parameterReference.ParameterNode.FirstTerminalNode;

			return parameterReference;
		}

		private static OracleProgramReference CreateProgramReference(OracleReferenceContainer container, OracleQueryBlock queryBlock, OracleSelectListColumn selectListColumn, StatementPlacement placement, StatementGrammarNode identifierNode, StatementGrammarNode prefixNonTerminal, IReadOnlyCollection<StatementGrammarNode> functionCallNodes)
		{
			var analyticClauseNode = functionCallNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.AnalyticClause));

			var parameterList = functionCallNodes.SingleOrDefault(n => String.Equals(n.Id, NonTerminals.ParenthesisEnclosedAggregationFunctionParameters) || String.Equals(n.Id, NonTerminals.ParenthesisEnclosedFunctionParameters));

			var programReference =
				new OracleProgramReference
				{
					ProgramIdentifierNode = identifierNode,
					DatabaseLinkNode = OracleReferenceBuilder.GetDatabaseLinkFromIdentifier(identifierNode),
					RootNode =
						identifierNode.GetAncestor(NonTerminals.Expression)
						?? identifierNode.GetAncestor(NonTerminals.TableCollectionInnerExpression)
						?? identifierNode.GetAncestor(NonTerminals.PlSqlProcedureCall),
					Owner = queryBlock,
					Placement = placement,
					AnalyticClauseNode = analyticClauseNode,
					ParameterListNode = parameterList,
					ParameterReferences = ResolveParameterReferences(parameterList),
					SelectListColumn = selectListColumn,
					Container = container
				};

			AddPrefixNodes(programReference, prefixNonTerminal);

			return programReference;
		}

		private static StatementGrammarNode GetDatabaseLinkFromQueryTableExpression(StatementGrammarNode queryTableExpression)
		{
			var partitionOrDatabaseLink = queryTableExpression[NonTerminals.PartitionOrDatabaseLink];
			return partitionOrDatabaseLink == null ? null : OracleReferenceBuilder.GetDatabaseLinkFromNode(partitionOrDatabaseLink);
		}

		private static OracleColumnReference CreateColumnReference(OracleReferenceContainer container, OracleQueryBlock queryBlock, OracleSelectListColumn selectListColumn, StatementPlacement placement, StatementGrammarNode identifierNode, StatementGrammarNode prefixNonTerminal)
		{
			StatementGrammarNode rootNode;
			if (String.Equals(identifierNode.ParentNode.Id, NonTerminals.IdentifierList) ||
				String.Equals(identifierNode.ParentNode.Id, NonTerminals.ColumnIdentifierChainedList) ||
				String.Equals(identifierNode.Id, Terminals.RowNumberPseudocolumn) ||
				String.Equals(identifierNode.Id, Terminals.Level) ||
				String.Equals(identifierNode.Id, Terminals.User) ||
				String.Equals(identifierNode.ParentNode.Id, NonTerminals.IdentifierOrParenthesisEnclosedIdentifierList) ||
				String.Equals(identifierNode.ParentNode.Id, NonTerminals.CellAssignment))
			{
				rootNode = identifierNode;
			}
			else if (String.Equals(identifierNode.ParentNode.Id, NonTerminals.PrefixedIdentifier))
			{
				rootNode = identifierNode.ParentNode;
			}
			else if (String.Equals(identifierNode.Id, Terminals.PlSqlIdentifier))
			{
				rootNode = identifierNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.PlSqlAssignmentStatement), NonTerminals.AssignmentStatementTarget) ?? identifierNode;
			}
			else
			{
				rootNode =
					identifierNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.AliasedExpressionOrAllTableColumns), NonTerminals.PrefixedColumnReference)
					?? identifierNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.AliasedExpressionOrAllTableColumns), NonTerminals.PrefixedAsterisk)
					?? identifierNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.QueryTableExpression), NonTerminals.TableCollectionInnerExpression)
					?? identifierNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.AliasedExpressionOrAllTableColumns), NonTerminals.Expression);
			}

			var columnReference =
				new OracleColumnReference(container)
				{
					RootNode = rootNode,
					ColumnNode = identifierNode,
					DatabaseLinkNode = OracleReferenceBuilder.GetDatabaseLinkFromIdentifier(identifierNode),
					Placement = placement,
					Owner = queryBlock,
					SelectListColumn = selectListColumn
				};

			AddPrefixNodes(columnReference, prefixNonTerminal);

			return columnReference;
		}

		public ICollection<IReferenceDataSource> ApplyReferenceConstraints(IReadOnlyList<ColumnHeader> columnHeaders)
		{
			var childDataSources = new List<IReferenceDataSource>();

			if (MainQueryBlock == null)
			{
				return childDataSources;
			}

			var uniqueConstraints = new HashSet<OracleUniqueConstraint>();
			foreach (var columnHeader in columnHeaders)
			{
				var selectedColumns = MainQueryBlock.NamedColumns[$"\"{columnHeader.Name}\""].Where(c => c.ColumnDescription.DataType.IsPrimitive);

				foreach (var selectedColumn in selectedColumns)
				{
					var sourceObject = GetSourceObject(selectedColumn, out string localColumnName);
					if (sourceObject == null)
					{
						continue;
					}

					var parentReferenceDataSources = new List<OracleReferenceDataSource>();
					foreach (var constraint in sourceObject.Constraints)
					{
						if (constraint is OracleReferenceConstraint referenceConstraint && referenceConstraint.Columns.Count == 1 && String.Equals(referenceConstraint.Columns[0], localColumnName))
						{
							var referenceColumnName = referenceConstraint.ReferenceConstraint.Columns[0];
							var statementText = StatementText = $"SELECT * FROM {referenceConstraint.TargetObject.FullyQualifiedName} WHERE {referenceColumnName} = :KEY0";
							var objectName = referenceConstraint.TargetObject.FullyQualifiedName.ToString();
							var constraintName = referenceConstraint.FullyQualifiedName.ToString();
							var keyDataType = selectedColumn.ColumnDescription.DataType.FullyQualifiedName.Name.Trim('"');
							var peferenceDataSource = new OracleReferenceDataSource(objectName, constraintName, statementText, new[] { columnHeader }, new[] { keyDataType });
							parentReferenceDataSources.Add(peferenceDataSource);
						}

						if (constraint is OracleUniqueConstraint uniqueConstraint)
						{
							uniqueConstraints.Add(uniqueConstraint);
						}
					}

					columnHeader.ParentReferenceDataSources = parentReferenceDataSources.AsReadOnly();
				}
			}

			foreach (var uniqueConstraint in uniqueConstraints)
			{
				var matchedHeaders = columnHeaders.Where(h => MainQueryBlock.NamedColumns[$"\"{h.Name}\""].Any(c => SelectColumnMatchesUniqueConstraintColumns(c, uniqueConstraint))).ToArray();
				if (matchedHeaders.Length != uniqueConstraint.Columns.Count)
				{
					continue;
				}

				var remoteReferenceConstraints = DatabaseModel.UniqueConstraintReferringReferenceConstraints[uniqueConstraint.FullyQualifiedName];
				foreach (var remoteReferenceConstraint in remoteReferenceConstraints)
				{
					var predicate = String.Join(" AND ", remoteReferenceConstraint.Columns.Select((c, i) => $"{c} = :KEY{i}"));
					var statementText = StatementText = $"SELECT * FROM {remoteReferenceConstraint.OwnerObject.FullyQualifiedName} WHERE {predicate}";
					var objectName = remoteReferenceConstraint.OwnerObject.FullyQualifiedName.ToString();
					var constraintName = remoteReferenceConstraint.FullyQualifiedName.ToString();
					var dataObject = (OracleDataObject)remoteReferenceConstraint.OwnerObject;

					var incompatibleDataFound = false;
					var dataTypes = new List<string>();
					foreach (var constraintColumn in remoteReferenceConstraint.Columns)
					{
						if (!dataObject.Columns.TryGetValue(constraintColumn, out OracleColumn column) || !column.DataType.IsPrimitive)
						{
							incompatibleDataFound = true;
							var message = column == null ? $"Column '{constraintColumn}' not found in object '{dataObject.FullyQualifiedName}' metadata. " : $"Column '{dataObject.FullyQualifiedName}.{constraintColumn}' does not have primitive data type. ";

							TraceLog.WriteLine($"Reference constraint data source cannot be created. {message}");

							break;
						}

						dataTypes.Add(column.DataType.FullyQualifiedName.Name.Trim('"'));
					}

					if (incompatibleDataFound)
					{
						continue;
					}

					var referenceDataSource = new OracleReferenceDataSource(objectName, constraintName, statementText, matchedHeaders, dataTypes);
					childDataSources.Add(referenceDataSource);
				}
			}

			return childDataSources;
		}

		private static bool SelectColumnMatchesUniqueConstraintColumns(OracleSelectListColumn selectColumn, OracleUniqueConstraint constraint)
		{
			var sourceObject = GetSourceObject(selectColumn, out string originalColumnName);
			return sourceObject == constraint.OwnerObject && constraint.Columns.Any(c => String.Equals(c, originalColumnName));
		}

		private static OracleDataObject GetSourceObject(OracleSelectListColumn column, out string physicalColumnName)
		{
			physicalColumnName = null;

			do
			{
				if (!column.IsDirectReference || column.ColumnReferences.Count != 1)
				{
					return null;
				}

				var columnReference = column.ColumnReferences[0];
				var objectReference = columnReference.ValidObjectReference;
				if (objectReference == null || columnReference.ColumnNodeColumnReferences.Count != 1)
				{
					return null;
				}

				if (objectReference.SchemaObject.GetTargetSchemaObject() is OracleDataObject dataObject)
				{
					physicalColumnName = columnReference.ColumnNodeColumnReferences.First().Name;
					return dataObject;
				}

				if (objectReference.QueryBlocks.Count != 1)
				{
					return null;
				}

				var columnNormalizedName =
					columnReference.ReferencesAllColumns
						? column.NormalizedName
						: columnReference.NormalizedName;

				column = objectReference.QueryBlocks.First().NamedColumns[columnNormalizedName].First();
			} while (true);
		}

		private static void AddPrefixNodes(OracleReference reference, StatementGrammarNode prefixNonTerminal)
		{
			if (prefixNonTerminal == null)
			{
				return;
			}

			reference.OwnerNode = prefixNonTerminal.GetSingleDescendant(Terminals.SchemaIdentifier);
			reference.ObjectNode = prefixNonTerminal.GetSingleDescendant(Terminals.ObjectIdentifier);
		}

		private IEnumerable<CommonTableExpressionReference> ResolveAccessibleCommonTableExpressions(StatementGrammarNode queryBlockRoot)
		{
			var accessibleAliases = new HashSet<string>();
			return GetCommonTableExpressionReferences(queryBlockRoot).Where(cteReference => accessibleAliases.Add(cteReference.CteAlias));
		}

		private IEnumerable<CommonTableExpressionReference> GetCommonTableExpressionReferences(StatementGrammarNode queryBlockRoot)
		{
			var nestedQuery = queryBlockRoot.GetAncestor(NonTerminals.NestedQuery);
			var subQueryCompondentNode = queryBlockRoot.GetAncestor(NonTerminals.CommonTableExpressionList);
			var cteReferencesWithinSameClause = new List<CommonTableExpressionReference>();
			if (subQueryCompondentNode != null)
			{
				var cteNodeWithinSameClause = subQueryCompondentNode.GetAncestor(NonTerminals.CommonTableExpressionList);
				while (cteNodeWithinSameClause != null)
				{
					cteReferencesWithinSameClause.Add(GetCteReference(cteNodeWithinSameClause));
					cteNodeWithinSameClause = cteNodeWithinSameClause.GetAncestor(NonTerminals.CommonTableExpressionList);
				}

				if (queryBlockRoot.Level - nestedQuery.Level > queryBlockRoot.Level - subQueryCompondentNode.Level)
				{
					nestedQuery = nestedQuery.GetAncestor(NonTerminals.NestedQuery);
				}
			}

			if (nestedQuery == null)
			{
				return cteReferencesWithinSameClause;
			}

			var commonTableExpressions = nestedQuery.GetPathFilterDescendants(n => n.Id != NonTerminals.QueryBlock, NonTerminals.CommonTableExpressionList).Select(GetCteReference);
			return commonTableExpressions.Concat(cteReferencesWithinSameClause).Concat(GetCommonTableExpressionReferences(nestedQuery));
		}

		private static CommonTableExpressionReference GetCteReference(StatementGrammarNode cteListNode)
		{
			var cteNode = cteListNode[0];
			var objectIdentifierNode = cteNode[0];
			var cteAlias = objectIdentifierNode?.Token.Value.ToQuotedIdentifier();
			return new CommonTableExpressionReference { CteNode = cteNode, CteAlias = cteAlias };
		}

		private static OracleLiteral CreateLiteral(StatementGrammarNode terminal)
		{
			var literal = new OracleLiteral { Terminal = terminal.FollowingTerminal };

			switch (terminal.Id)
			{
				case Terminals.Date:
					literal.Type = LiteralType.Date;
					break;
				case Terminals.Timestamp:
					literal.Type = LiteralType.Timestamp;
					break;
				case Terminals.StringLiteral:
					literal.Type = LiteralType.Char;
					break;
				case Terminals.NumberLiteral:
					switch (terminal.Token.Value[terminal.Token.Value.Length - 1])
					{
						case 'f':
						case 'F':
							literal.Type = LiteralType.SinglePrecision;
							break;
						case 'd':
						case 'D':
							literal.Type = LiteralType.DoublePrecision;
							break;
						default:
							literal.Type = LiteralType.Number;
							break;
					}
					
					break;
				case Terminals.Interval:
					var intervalTypeNode = terminal.ParentNode[2, 0];
					if (intervalTypeNode != null)
					{
						literal.Type = String.Equals(intervalTypeNode.Id, NonTerminals.IntervalDayToSecond) ? LiteralType.IntervalDayToSecond : LiteralType.IntervalYearToMonth;
					}

					break;
				default:
					throw new ArgumentException($"Unsupported terminal ID: {terminal.Id}");
			}

			return literal;
		}

		private struct CommonTableExpressionReference
		{
			public StatementGrammarNode CteNode;
			public string CteAlias;
		}
	}
}
