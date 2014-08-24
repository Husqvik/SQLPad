using System;
using System.Collections.Generic;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleStatementSemanticModel : IStatementSemanticModel
	{
		private Dictionary<StatementGrammarNode, OracleQueryBlock> _queryBlockNodes = new Dictionary<StatementGrammarNode, OracleQueryBlock>();
		private readonly OracleDatabaseModelBase _databaseModel;
		private readonly Dictionary<OracleSelectListColumn, ICollection<OracleDataObjectReference>> _asteriskTableReferences = new Dictionary<OracleSelectListColumn, ICollection<OracleDataObjectReference>>();
		private readonly List<ICollection<OracleColumnReference>> _joinClauseColumnReferences = new List<ICollection<OracleColumnReference>>();
		private readonly Dictionary<OracleQueryBlock, ICollection<StatementGrammarNode>> _accessibleQueryBlockRoot = new Dictionary<OracleQueryBlock, ICollection<StatementGrammarNode>>();
		private readonly Dictionary<OracleDataObjectReference, ICollection<KeyValuePair<StatementGrammarNode, string>>> _objectReferenceCteRootNodes = new Dictionary<OracleDataObjectReference, ICollection<KeyValuePair<StatementGrammarNode, string>>>();

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

		IDatabaseModel IStatementSemanticModel.DatabaseModel { get { return DatabaseModel; } }

		public OracleStatement Statement { get; private set; }

		StatementBase IStatementSemanticModel.Statement { get { return Statement; } }
		
		public string StatementText { get; private set; }
		
		public bool IsSimpleModel { get { return _databaseModel == null; } }

		public ICollection<OracleQueryBlock> QueryBlocks
		{
			get { return _queryBlockNodes.Values; }
		}

		public OracleQueryBlock MainQueryBlock
		{
			get
			{
				return _queryBlockNodes.Values
					.Where(qb => qb.Type == QueryBlockType.Normal)
					.OrderBy(qb => qb.RootNode.SourcePosition.IndexStart)
					.FirstOrDefault();
			}
		}

		public OracleStatementSemanticModel(string statementText, OracleStatement statement) : this(statement, null)
		{
			StatementText = statementText;
		}

		public OracleStatementSemanticModel(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel)
			: this(statement, databaseModel)
		{
			if (databaseModel == null)
				throw new ArgumentNullException("databaseModel");

			StatementText = statementText;
		}

		private OracleStatementSemanticModel(OracleStatement statement, OracleDatabaseModelBase databaseModel)
		{
			if (statement == null)
				throw new ArgumentNullException("statement");

			Statement = statement;
			_databaseModel = databaseModel;

			if (statement.RootNode == null)
				return;

			Build();
		}

		private void Build()
		{
			_queryBlockNodes = Statement.RootNode.GetDescendants(NonTerminals.QueryBlock)
				.OrderByDescending(q => q.Level)
				.ToDictionary(n => n, n => new OracleQueryBlock(this) { RootNode = n, Statement = Statement });

			foreach (var kvp in _queryBlockNodes)
			{
				var queryBlockRoot = kvp.Key;
				var queryBlock = kvp.Value;

				var scalarSubqueryExpression = queryBlockRoot.GetAncestor(NonTerminals.Expression);
				if (scalarSubqueryExpression != null)
				{
					queryBlock.Type = QueryBlockType.ScalarSubquery;
				}

				var commonTableExpression = queryBlockRoot.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBoundary, NonTerminals.SubqueryComponent);
				if (commonTableExpression != null)
				{
					queryBlock.AliasNode = commonTableExpression.ChildNodes.First();
					queryBlock.Type = QueryBlockType.CommonTableExpression;
				}
				else
				{
					var selfTableReference = queryBlockRoot.GetAncestor(NonTerminals.TableReference);
					if (selfTableReference != null)
					{
						queryBlock.Type = QueryBlockType.Normal;

						var nestedSubqueryAlias = selfTableReference.ChildNodes.SingleOrDefault(n => n.Id == Terminals.ObjectAlias);
						if (nestedSubqueryAlias != null)
						{
							queryBlock.AliasNode = nestedSubqueryAlias;
						}
					}
				}

				var fromClause = queryBlockRoot.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
				var tableReferenceNonterminals = fromClause == null
					? Enumerable.Empty<StatementGrammarNode>()
					: fromClause.GetDescendantsWithinSameQuery(NonTerminals.TableReference).ToArray();

				// TODO: Check possible issue within GetCommonTableExpressionReferences to remove the Distinct.
				var cteReferences = GetCommonTableExpressionReferences(queryBlockRoot).Distinct().ToDictionary(qb => qb.Key, qb => qb.Value);
				_accessibleQueryBlockRoot.Add(queryBlock, cteReferences.Keys);

				foreach (var tableReferenceNonterminal in tableReferenceNonterminals)
				{
					var queryTableExpression = tableReferenceNonterminal.GetDescendantsWithinSameQuery(NonTerminals.QueryTableExpression).SingleOrDefault();
					if (queryTableExpression == null)
						continue;

					var tableReferenceAlias = tableReferenceNonterminal.GetDescendantsWithinSameQuery(Terminals.ObjectAlias).SingleOrDefault();

					var nestedQueryTableReference = queryTableExpression.GetPathFilterDescendants(f => f.Id != NonTerminals.Subquery, NonTerminals.NestedQuery).SingleOrDefault();
					if (nestedQueryTableReference != null)
					{
						var nestedQueryTableReferenceQueryBlock = nestedQueryTableReference.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery && n.Id != NonTerminals.SubqueryFactoringClause, NonTerminals.QueryBlock).FirstOrDefault();
						if (nestedQueryTableReferenceQueryBlock != null)
						{
							queryBlock.ObjectReferences.Add(
								new OracleDataObjectReference(ReferenceType.InlineView)
								{
									Owner = queryBlock,
									RootNode = tableReferenceNonterminal,
									ObjectNode = nestedQueryTableReferenceQueryBlock,
									AliasNode = tableReferenceAlias
								});
						}

						continue;
					}

					var tableIdentifierNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == Terminals.ObjectIdentifier);

					if (tableIdentifierNode == null)
						continue;

					var schemaPrefixNode = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.SchemaPrefix);
					if (schemaPrefixNode != null)
					{
						schemaPrefixNode = schemaPrefixNode.ChildNodes.First();
					}

					var tableName = tableIdentifierNode.Token.Value.ToQuotedIdentifier();
					var commonTableExpressions = schemaPrefixNode != null
						? (ICollection<KeyValuePair<StatementGrammarNode, string>>)new Dictionary<StatementGrammarNode, string>()
						: cteReferences.Where(n => n.Value == tableName).ToArray();

					var referenceType = ReferenceType.CommonTableExpression;

					OracleSchemaObject schemaObject = null;
					if (commonTableExpressions.Count == 0)
					{
						referenceType = ReferenceType.SchemaObject;

						var objectName = tableIdentifierNode.Token.Value;
						var owner = schemaPrefixNode == null ? null : schemaPrefixNode.Token.Value;

						if (!IsSimpleModel)
						{
							schemaObject = _databaseModel.GetFirstSchemaObject<OracleDataObject>(_databaseModel.GetPotentialSchemaObjectIdentifiers(owner, objectName));
						}
					}

					var objectReference =
						new OracleDataObjectReference(referenceType)
						{
							Owner = queryBlock,
							RootNode = tableReferenceNonterminal,
							OwnerNode = schemaPrefixNode,
							ObjectNode = tableIdentifierNode,
							DatabaseLinkNode = GetDatabaseLinkFromQueryTableExpression(tableIdentifierNode.ParentNode),
							AliasNode = tableReferenceAlias,
							SchemaObject = schemaObject
						};

					queryBlock.ObjectReferences.Add(objectReference);

					if (commonTableExpressions.Count > 0)
					{
						_objectReferenceCteRootNodes[objectReference] = commonTableExpressions;
					}
				}

				FindSelectListReferences(queryBlock);

				FindWhereGroupByHavingReferences(queryBlock);

				FindJoinColumnReferences(queryBlock);
			}

			foreach (var queryBlock in _queryBlockNodes.Values)
			{
				ResolveConcatenatedQueryBlocks(queryBlock);

				ResolveParentCorrelatedQueryBlock(queryBlock);

				foreach (var nestedQueryReference in queryBlock.ObjectReferences.Where(t => t.Type != ReferenceType.SchemaObject))
				{
					if (nestedQueryReference.Type == ReferenceType.InlineView)
					{
						nestedQueryReference.QueryBlocks.Add(_queryBlockNodes[nestedQueryReference.ObjectNode]);
					}
					else if (_objectReferenceCteRootNodes.ContainsKey(nestedQueryReference))
					{
						var commonTableExpressionNode = _objectReferenceCteRootNodes[nestedQueryReference];
						foreach (var referencedQueryBlock in commonTableExpressionNode
							.Where(nodeName => OracleObjectIdentifier.Create(null, nodeName.Value) == OracleObjectIdentifier.Create(null, nestedQueryReference.ObjectNode.Token.Value)))
						{
							var cteQueryBlockNode = referencedQueryBlock.Key.GetDescendants(NonTerminals.QueryBlock).FirstOrDefault();
							if (cteQueryBlockNode != null)
							{
								nestedQueryReference.QueryBlocks.Add(_queryBlockNodes[cteQueryBlockNode]);
							}
						}
					}
				}

				foreach (var accessibleQueryBlock in _accessibleQueryBlockRoot[queryBlock])
				{
					var accesibleQueryBlockRoot = accessibleQueryBlock.GetDescendants(NonTerminals.QueryBlock).FirstOrDefault();
					if (accesibleQueryBlockRoot != null)
					{
						queryBlock.AccessibleQueryBlocks.Add(_queryBlockNodes[accesibleQueryBlockRoot]);
					}
				}
			}

			ExposeAsteriskColumns();

			ResolveReferences();
		}

		private void ResolveParentCorrelatedQueryBlock(OracleQueryBlock queryBlock, bool allowMoreThanOneLevel = false)
		{
			var nestedQueryRoot = queryBlock.RootNode.ParentNode.ParentNode;
			
			foreach (var parentId in new[] { NonTerminals.Expression, NonTerminals.Condition })
			{
				var parentExpression = nestedQueryRoot.GetPathFilterAncestor(n => allowMoreThanOneLevel || n.Id != NonTerminals.NestedQuery, parentId);
				if (parentExpression == null)
					continue;
				
				queryBlock.ParentCorrelatedQueryBlock = GetQueryBlock(parentExpression);
				return;
			}
		}

		private void ResolveConcatenatedQueryBlocks(OracleQueryBlock queryBlock)
		{
			if (queryBlock.RootNode.ParentNode.ParentNode.Id != NonTerminals.ConcatenatedSubquery)
				return;
			
			var parentSubquery = queryBlock.RootNode.ParentNode.GetAncestor(NonTerminals.Subquery);
			if (parentSubquery == null)
				return;
			
			var parentQueryBlockNode = parentSubquery.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.QueryBlock);
			if (parentQueryBlockNode == null)
				return;
			
			var precedingQueryBlock = _queryBlockNodes[parentQueryBlockNode];
			precedingQueryBlock.FollowingConcatenatedQueryBlock = queryBlock;
			queryBlock.PrecedingConcatenatedQueryBlock = precedingQueryBlock;
		}

		private void ResolveJoinReferences()
		{
			foreach (var joinClauseColumnReferences in _joinClauseColumnReferences)
			{
				var queryBlock = joinClauseColumnReferences.First().Owner;
				foreach (var columnReference in joinClauseColumnReferences)
				{
					var fromClauseNode = columnReference.ColumnNode.GetAncestor(NonTerminals.FromClause);
					var relatedTableReferences = queryBlock.ObjectReferences
						.Where(t => t.RootNode.SourcePosition.IndexStart >= fromClauseNode.SourcePosition.IndexStart &&
									t.RootNode.SourcePosition.IndexEnd <= columnReference.ColumnNode.SourcePosition.IndexStart).ToArray();

					queryBlock.ColumnReferences.Add(columnReference);
					ResolveColumnObjectReferences(new [] { columnReference }, relatedTableReferences, new OracleDataObjectReference[0]);
				}
			}
		}

		private void ResolveReferences()
		{
			foreach (var queryBlock in _queryBlockNodes.Values)
			{
				ResolveOrderByReferences(queryBlock);

				ResolveFunctionReferences(queryBlock);

				var parentCorrelatedQueryBlockObjectReferences = queryBlock.ParentCorrelatedQueryBlock == null
					? new OracleDataObjectReference[0]
					: queryBlock.ParentCorrelatedQueryBlock.ObjectReferences;

				var columnReferences = queryBlock.AllColumnReferences.Where(c => c.SelectListColumn == null || c.SelectListColumn.ExplicitDefinition);
				ResolveColumnObjectReferences(columnReferences, queryBlock.ObjectReferences, parentCorrelatedQueryBlockObjectReferences);

				ResolveDatabaseLinks(queryBlock);
			}

			ResolveJoinReferences();
		}

		private void ResolveDatabaseLinks(OracleQueryBlock queryBlock)
		{
			if (IsSimpleModel)
				return;

			foreach (var databaseLinkReference in queryBlock.DatabaseLinkReferences)
			{
				databaseLinkReference.DatabaseLink = _databaseModel.GetFirstDatabaseLink(_databaseModel.GetPotentialSchemaObjectIdentifiers(null, databaseLinkReference.DatabaseLinkNode.Token.Value));
			}
		}

		private void ExposeAsteriskColumns()
		{
			foreach (var asteriskTableReference in _asteriskTableReferences)
			{
				foreach (var objectReference in asteriskTableReference.Value)
				{
					IEnumerable<OracleSelectListColumn> exposedColumns;
					if (objectReference.Type == ReferenceType.SchemaObject)
					{
						var dataObject = objectReference.SchemaObject.GetTargetSchemaObject() as OracleDataObject;
						if (dataObject == null)
							continue;

						exposedColumns = dataObject.Columns.Values
							.Select(c => new OracleSelectListColumn
							{
								ExplicitDefinition = false,
								IsDirectReference = true,
								ColumnDescription = c
							});
					}
					else
					{
						exposedColumns = objectReference.QueryBlocks.SelectMany(qb => qb.Columns)
							.Where(c => !c.IsAsterisk)
							.Select(c => c.AsImplicit());
					}

					var exposedColumnDictionary = new Dictionary<string, OracleColumnReference>();
					foreach (var exposedColumn in exposedColumns)
					{
						exposedColumn.Owner = asteriskTableReference.Key.Owner;

						OracleColumnReference columnReference;
						if (String.IsNullOrEmpty(exposedColumn.NormalizedName) || !exposedColumnDictionary.TryGetValue(exposedColumn.NormalizedName, out columnReference))
						{
							columnReference = CreateColumnReference(exposedColumn.Owner, exposedColumn, QueryBlockPlacement.SelectList, asteriskTableReference.Key.RootNode.LastTerminalNode, null);

							if (!String.IsNullOrEmpty(exposedColumn.NormalizedName))
							{
								exposedColumnDictionary.Add(exposedColumn.NormalizedName, columnReference);
							}

							columnReference.ColumnNodeObjectReferences.Add(objectReference);
						}

						columnReference.ColumnNodeColumnReferences.Add(exposedColumn.ColumnDescription);

						exposedColumn.ColumnReferences.Add(columnReference);

						asteriskTableReference.Key.Owner.Columns.Add(exposedColumn);
					}
				}
			}
		}

		private void ResolveFunctionReferences(OracleQueryBlock queryBlock)
		{
			var functionsTransferredToTypes = new List<OracleProgramReference>();
			foreach (var functionReference in queryBlock.AllProgramReferences)
			{
				if (UpdateFunctionReferenceWithMetadata(functionReference) != null)
					continue;

				var typeReference = ResolveTypeReference(functionReference);
				if (typeReference == null)
					continue;

				functionsTransferredToTypes.Add(functionReference);
				functionReference.Container.TypeReferences.Add(typeReference);
			}

			functionsTransferredToTypes.ForEach(f => f.Container.FunctionReferences.Remove(f));
		}

		private OracleTypeReference ResolveTypeReference(OracleProgramReference functionReference)
		{
			if (functionReference.ParameterListNode == null || functionReference.OwnerNode != null)
				return null;

			var identifierCandidates = _databaseModel.GetPotentialSchemaObjectIdentifiers(functionReference.FullyQualifiedObjectName.NormalizedName, functionReference.NormalizedName);

			var schemaObject = _databaseModel.GetFirstSchemaObject<OracleTypeBase>(identifierCandidates);
			if (schemaObject == null)
				return null;

			return
				new OracleTypeReference
				{
					OwnerNode = functionReference.ObjectNode,
					DatabaseLinkNode = functionReference.DatabaseLinkNode,
					DatabaseLink = functionReference.DatabaseLink,
					Owner = functionReference.Owner,
					ParameterNodes = functionReference.ParameterNodes,
					ParameterListNode = functionReference.ParameterListNode,
					RootNode = functionReference.RootNode,
					SchemaObject = schemaObject,
					SelectListColumn = functionReference.SelectListColumn,
					ObjectNode = functionReference.FunctionIdentifierNode
				};
		}

		private OracleFunctionMetadata UpdateFunctionReferenceWithMetadata(OracleProgramReference programReference)
		{
			if (IsSimpleModel)
				return null;

			var owner = String.IsNullOrEmpty(programReference.FullyQualifiedObjectName.NormalizedOwner)
				? _databaseModel.CurrentSchema
				: programReference.FullyQualifiedObjectName.NormalizedOwner;

			var originalIdentifier = OracleFunctionIdentifier.CreateFromValues(owner, programReference.FullyQualifiedObjectName.NormalizedName, programReference.NormalizedName);
			var parameterCount = programReference.ParameterNodes == null ? 0 : programReference.ParameterNodes.Count;
			var result = _databaseModel.GetFunctionMetadata(originalIdentifier, parameterCount, true);
			if (result.Metadata == null && !String.IsNullOrEmpty(originalIdentifier.Package) && String.IsNullOrEmpty(programReference.FullyQualifiedObjectName.NormalizedOwner))
			{
				var identifier = OracleFunctionIdentifier.CreateFromValues(originalIdentifier.Package, null, originalIdentifier.Name);
				result = _databaseModel.GetFunctionMetadata(identifier, parameterCount, false);
			}

			if (result.Metadata == null && String.IsNullOrEmpty(programReference.FullyQualifiedObjectName.NormalizedOwner))
			{
				var identifier = OracleFunctionIdentifier.CreateFromValues(OracleDatabaseModelBase.SchemaPublic, originalIdentifier.Package, originalIdentifier.Name);
				result = _databaseModel.GetFunctionMetadata(identifier, parameterCount, false);
			}

			if (result.Metadata != null && String.IsNullOrEmpty(result.Metadata.Identifier.Package) &&
				programReference.ObjectNode != null)
			{
				programReference.OwnerNode = programReference.ObjectNode;
				programReference.ObjectNode = null;
			}

			programReference.SchemaObject = result.SchemaObject;

			return programReference.Metadata = result.Metadata;
		}

		public OracleQueryBlock GetQueryBlock(int position)
		{
			return _queryBlockNodes.Values
				.Where(qb => qb.RootNode.SourcePosition.ContainsIndex(position))
				.OrderByDescending(qb => qb.RootNode.Level)
				.FirstOrDefault();
		}

		public OracleQueryBlock GetQueryBlock(StatementGrammarNode node)
		{
			var queryBlockNode = node.GetAncestor(NonTerminals.QueryBlock);
			if (queryBlockNode == null)
			{
				var orderByClauseNode = node.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.OrderByClause, true);
				if (orderByClauseNode == null)
					return null;
				
				var queryBlock = _queryBlockNodes.Values.SingleOrDefault(qb => qb.OrderByClause == orderByClauseNode);
				if (queryBlock == null)
					return null;

				queryBlockNode = queryBlock.RootNode;
			}

			return queryBlockNode == null ? null : _queryBlockNodes[queryBlockNode];
		}

		private void ResolveColumnObjectReferences(IEnumerable<OracleColumnReference> columnReferences, ICollection<OracleDataObjectReference> accessibleRowSourceReferences, ICollection<OracleDataObjectReference> parentCorrelatedRowSourceReferences)
		{
			foreach (var columnReference in columnReferences.ToArray())
			{
				if (columnReference.Placement == QueryBlockPlacement.OrderBy)
				{
					if (columnReference.Owner.FollowingConcatenatedQueryBlock != null)
					{
						ResolveConcatenatedQueryBlockOrderByReferences(columnReference);

						continue;
					}

					var orderByColumnAliasOrAsteriskReferences = columnReference.Owner.Columns
						.Where(c => columnReference.ObjectNode == null && c.NormalizedName == columnReference.NormalizedName && (!c.IsDirectReference || (c.ColumnReferences.Count > 0 && c.ColumnReferences.First().NormalizedName != c.NormalizedName)));

					var ambiguousAsteriskRefences = new Dictionary<OracleColumn, int>();
					foreach (var reference in orderByColumnAliasOrAsteriskReferences)
					{
						if (reference.ExplicitDefinition)
						{
							columnReference.ColumnNodeColumnReferences.Add(reference.ColumnDescription);
						}
						else
						{
							if (!ambiguousAsteriskRefences.ContainsKey(reference.ColumnDescription))
							{
								ambiguousAsteriskRefences.Add(reference.ColumnDescription, 0);	
							}

							ambiguousAsteriskRefences[reference.ColumnDescription]++;
						}
					}

					var ambiguousAsteriskReferences = ambiguousAsteriskRefences.Where(kvp => kvp.Value > 1).Select(kvp => kvp.Key);
					columnReference.ColumnNodeColumnReferences.AddRange(ambiguousAsteriskReferences);
					
					if (columnReference.ColumnNodeColumnReferences.Count > 0)
					{
						columnReference.ColumnNodeObjectReferences.Add(columnReference.Owner.SelfObjectReference);
					}
				}

				ResolveColumnReference(accessibleRowSourceReferences, columnReference);
				if (columnReference.ColumnNodeObjectReferences.Count == 0)
				{
					ResolveColumnReference(parentCorrelatedRowSourceReferences, columnReference);
				}

				var columnDescription = columnReference.ColumnNodeColumnReferences.FirstOrDefault();

				if (columnDescription != null &&
					columnReference.ColumnNodeObjectReferences.Count == 1)
				{
					columnReference.ColumnDescription = columnDescription;
				}

				TryColumnReferenceAsProgramOrSequenceReference(columnReference);
			}
		}

		private void ResolveColumnReference(IEnumerable<OracleDataObjectReference> rowSources, OracleColumnReference columnReference)
		{
			foreach (var rowSourceReference in rowSources)
			{
				if (columnReference.ObjectNode != null &&
				    (rowSourceReference.FullyQualifiedObjectName == columnReference.FullyQualifiedObjectName ||
				     (columnReference.OwnerNode == null &&
				      rowSourceReference.Type == ReferenceType.SchemaObject && rowSourceReference.FullyQualifiedObjectName.NormalizedName == columnReference.FullyQualifiedObjectName.NormalizedName)))
				{
					columnReference.ObjectNodeObjectReferences.Add(rowSourceReference);
				}

				var columnNodeColumnReferences = GetColumnNodeColumnReferences(rowSourceReference, columnReference);

				if (columnNodeColumnReferences.Count > 0 &&
				    (String.IsNullOrEmpty(columnReference.FullyQualifiedObjectName.NormalizedName) ||
				     columnReference.ObjectNodeObjectReferences.Count > 0))
				{
					columnReference.ColumnNodeColumnReferences.AddRange(columnNodeColumnReferences);
					columnReference.ColumnNodeObjectReferences.Add(rowSourceReference);
				}
			}
		}

		private void TryColumnReferenceAsProgramOrSequenceReference(OracleColumnReference columnReference)
		{
			if (columnReference.ColumnNodeColumnReferences.Count != 0 || columnReference.ReferencesAllColumns)
				return;
			
			var programReference =
				new OracleProgramReference
				{
					FunctionIdentifierNode = columnReference.ColumnNode,
					DatabaseLinkNode = GetDatabaseLinkFromIdentifier(columnReference.ColumnNode),
					ObjectNode = columnReference.ObjectNode,
					OwnerNode = columnReference.OwnerNode,
					RootNode = columnReference.ColumnNode,
					Owner = columnReference.Owner,
					SelectListColumn = columnReference.SelectListColumn,
					AnalyticClauseNode = null,
					ParameterListNode = null,
					ParameterNodes = null
				};

			UpdateFunctionReferenceWithMetadata(programReference);
			if (programReference.SchemaObject != null)
			{
				columnReference.Container.FunctionReferences.Add(programReference);
				columnReference.Container.ColumnReferences.Remove(columnReference);
			}
			else
			{
				ResolveSequenceReference(columnReference);
			}
		}

		private static void ResolveConcatenatedQueryBlockOrderByReferences(OracleColumnReference columnReference)
		{
			var isRecognized = true;
			var maximumReferences = new OracleColumn[0];
			var concatenatedQueryBlocks = new List<OracleQueryBlock> { columnReference.Owner };
			concatenatedQueryBlocks.AddRange(columnReference.Owner.AllFollowingConcatenatedQueryBlocks);
			for (var i = 0; i < concatenatedQueryBlocks.Count; i++)
			{
				var queryBlockColumnAliasReferences = concatenatedQueryBlocks[i].Columns
					.Where(c => columnReference.ObjectNode == null && c.NormalizedName == columnReference.NormalizedName)
					.Select(c => c.ColumnDescription)
					.ToArray();

				isRecognized = isRecognized && (queryBlockColumnAliasReferences.Length > 0 || i == concatenatedQueryBlocks.Count - 1);

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

		private void ResolveSequenceReference(OracleColumnReference columnReference)
		{
			if (columnReference.ObjectNode == null ||
				columnReference.ObjectNodeObjectReferences.Count > 0)
				return;

			var identifierCandidates = _databaseModel.GetPotentialSchemaObjectIdentifiers(columnReference.FullyQualifiedObjectName);	
			var schemaObject = _databaseModel.GetFirstSchemaObject<OracleSequence>(identifierCandidates);
			if (schemaObject == null)
				return;
			
			var sequenceReference =
				new OracleSequenceReference
				{
					ObjectNode = columnReference.ObjectNode,
					DatabaseLinkNode = GetDatabaseLinkFromIdentifier(columnReference.ColumnNode),
					Owner = columnReference.Owner,
					OwnerNode = columnReference.OwnerNode,
					RootNode = columnReference.RootNode,
					SelectListColumn = columnReference.SelectListColumn,
					SchemaObject = schemaObject
				};

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
		}

		private IList<OracleColumn> GetColumnNodeColumnReferences(OracleDataObjectReference rowSourceReference, OracleColumnReference columnReference)
		{
			var columnNodeColumnReferences = new List<OracleColumn>();
			if (rowSourceReference.Type == ReferenceType.SchemaObject)
			{
				if (rowSourceReference.SchemaObject == null)
					return new OracleColumn[0];

				var dataObject = (OracleDataObject)rowSourceReference.SchemaObject.GetTargetSchemaObject();
				var oracleTable = dataObject as OracleTable;
				if (columnReference.ColumnNode.Id == Terminals.RowIdPseudoColumn)
				{
					if (oracleTable != null && oracleTable.RowIdPseudoColumn != null &&
						(columnReference.ObjectNode == null || IsTableReferenceValid(columnReference, rowSourceReference)))
					{
						columnNodeColumnReferences.Add(oracleTable.RowIdPseudoColumn);
					}
				}
				else
				{
					columnNodeColumnReferences.AddRange(dataObject.Columns.Values
						.Where(c => c.Name == columnReference.NormalizedName && (columnReference.ObjectNode == null || IsTableReferenceValid(columnReference, rowSourceReference))));
				}
			}
			else
			{
				columnNodeColumnReferences.AddRange(rowSourceReference.QueryBlocks.SelectMany(qb => qb.Columns)
					.Where(c => c.NormalizedName == columnReference.NormalizedName && (columnReference.ObjectNode == null || columnReference.FullyQualifiedObjectName.NormalizedName == rowSourceReference.FullyQualifiedObjectName.NormalizedName))
					.Select(c => c.ColumnDescription));
			}

			return columnNodeColumnReferences;
		}

		private bool IsTableReferenceValid(OracleColumnReference column, OracleDataObjectReference schemaObject)
		{
			var objectName = column.FullyQualifiedObjectName;
			return (String.IsNullOrEmpty(objectName.NormalizedName) || objectName.NormalizedName == schemaObject.FullyQualifiedObjectName.NormalizedName) &&
			       (String.IsNullOrEmpty(objectName.NormalizedOwner) || objectName.NormalizedOwner == schemaObject.FullyQualifiedObjectName.NormalizedOwner);
		}

		private void FindJoinColumnReferences(OracleQueryBlock queryBlock)
		{
			var fromClauses = queryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause);
			foreach (var fromClause in fromClauses)
			{
				var joinClauses = fromClause.GetPathFilterDescendants(n => n.Id != NonTerminals.NestedQuery && n.Id != NonTerminals.FromClause, NonTerminals.JoinClause);
				foreach (var joinClause in joinClauses)
				{
					var joinCondition = joinClause.GetPathFilterDescendants(n => n.Id != NonTerminals.JoinClause, NonTerminals.JoinColumnsOrCondition).SingleOrDefault();
					if (joinCondition == null)
						continue;

					var identifiers = joinCondition.GetDescendants(Terminals.Identifier);
					var columnReferences = new List<OracleColumnReference>();
					ResolveColumnAndFunctionReferenceFromIdentifiers(queryBlock, columnReferences, identifiers, QueryBlockPlacement.Join, null);

					if (columnReferences.Count > 0)
					{
						_joinClauseColumnReferences.Add(columnReferences);
					}
				}
			}
		}

		private void FindWhereGroupByHavingReferences(OracleQueryBlock queryBlock)
		{
			var whereClauseRootNode = queryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.WhereClause).FirstOrDefault();
			if (whereClauseRootNode != null)
			{
				queryBlock.WhereClause = whereClauseRootNode;
				var whereClauseIdentifiers = whereClauseRootNode.GetDescendantsWithinSameQuery(Terminals.Identifier, Terminals.RowIdPseudoColumn);
				ResolveColumnAndFunctionReferenceFromIdentifiers(queryBlock, queryBlock.ColumnReferences, whereClauseIdentifiers, QueryBlockPlacement.Where, null);
			}

			var groupByClauseRootNode = queryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.GroupByClause).FirstOrDefault();
			if (groupByClauseRootNode == null)
			{
				return;
			}
			
			queryBlock.GroupByClause = groupByClauseRootNode;
			var identifiers = groupByClauseRootNode.GetPathFilterDescendants(n => !n.Id.In(NonTerminals.NestedQuery, NonTerminals.HavingClause), Terminals.Identifier, Terminals.RowIdPseudoColumn);
			ResolveColumnAndFunctionReferenceFromIdentifiers(queryBlock, queryBlock.ColumnReferences, identifiers, QueryBlockPlacement.GroupBy, null);

			var havingClauseRootNode = groupByClauseRootNode.GetDescendantsWithinSameQuery(NonTerminals.HavingClause).FirstOrDefault();
			if (havingClauseRootNode == null)
			{
				return;
			}

			queryBlock.HavingClause = havingClauseRootNode;
			identifiers = havingClauseRootNode.GetDescendantsWithinSameQuery(Terminals.Identifier, Terminals.RowIdPseudoColumn);
			ResolveColumnAndFunctionReferenceFromIdentifiers(queryBlock, queryBlock.ColumnReferences, identifiers, QueryBlockPlacement.Having, null);

			var grammarSpecificFunctions = havingClauseRootNode.GetDescendantsWithinSameQuery(Terminals.Count, NonTerminals.AggregateFunction, NonTerminals.AnalyticFunction).ToArray();
			CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, queryBlock, queryBlock.FunctionReferences, null);
		}

		private void ResolveOrderByReferences(OracleQueryBlock queryBlock)
		{
			if (queryBlock.PrecedingConcatenatedQueryBlock != null)
				return;

			queryBlock.OrderByClause = queryBlock.RootNode.ParentNode.GetPathFilterDescendants(n => n.Id != NonTerminals.QueryBlock, NonTerminals.OrderByClause).FirstOrDefault();
			if (queryBlock.OrderByClause == null)
				return;

			var identifiers = queryBlock.OrderByClause.GetDescendantsWithinSameQuery(Terminals.Identifier, Terminals.RowIdPseudoColumn);
			ResolveColumnAndFunctionReferenceFromIdentifiers(queryBlock, queryBlock.ColumnReferences, identifiers, QueryBlockPlacement.OrderBy, null);
		}

		private void ResolveColumnAndFunctionReferenceFromIdentifiers(OracleQueryBlock queryBlock, ICollection<OracleColumnReference> columnReferences, IEnumerable<StatementGrammarNode> identifiers, QueryBlockPlacement type, OracleSelectListColumn selectListColumn)
		{
			foreach (var identifier in identifiers)
			{
				var prefixNonTerminal = identifier.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference)
					.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);

				var functionCallNodes = GetFunctionCallNodes(identifier);
				if (functionCallNodes.Length == 0)
				{
					var columnReference = CreateColumnReference(queryBlock, selectListColumn, type, identifier, prefixNonTerminal);
					columnReferences.Add(columnReference);
				}
				else
				{
					var functionReference = CreateFunctionReference(queryBlock, selectListColumn, identifier, prefixNonTerminal, functionCallNodes);
					functionReference.Container.FunctionReferences.Add(functionReference);
				}
			}
		}

		private void FindSelectListReferences(OracleQueryBlock queryBlock)
		{
			var queryBlockRoot = queryBlock.RootNode;

			queryBlock.SelectList = queryBlockRoot.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.SelectList);
			if (queryBlock.SelectList == null)
				return;

			var distinctModifierNode = queryBlockRoot.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.DistinctModifier);
			queryBlock.HasDistinctResultSet = distinctModifierNode != null && distinctModifierNode.FirstTerminalNode.Id.In(Terminals.Distinct, Terminals.Unique);

			if (queryBlock.SelectList.FirstTerminalNode == null)
				return;

			if (queryBlock.SelectList.FirstTerminalNode.Id == Terminals.Asterisk)
			{
				var asteriskNode = queryBlock.SelectList.ChildNodes[0];
				var column = new OracleSelectListColumn
				{
					RootNode = asteriskNode,
					Owner = queryBlock,
					ExplicitDefinition = true,
					IsAsterisk = true
				};

				queryBlock.HasAsteriskClause = true;

				column.ColumnReferences.Add(CreateColumnReference(queryBlock, column, QueryBlockPlacement.SelectList, asteriskNode, null));

				_asteriskTableReferences[column] = new List<OracleDataObjectReference>(queryBlock.ObjectReferences);

				queryBlock.Columns.Add(column);
			}
			else
			{
				var columnExpressions = queryBlock.SelectList.GetDescendantsWithinSameQuery(NonTerminals.AliasedExpressionOrAllTableColumns).ToArray();
				foreach (var columnExpression in columnExpressions)
				{
					var columnAliasNode = columnExpression.LastTerminalNode != null && columnExpression.LastTerminalNode.Id == Terminals.ColumnAlias
						? columnExpression.LastTerminalNode
						: null;
					
					var column = new OracleSelectListColumn
					{
						AliasNode = columnAliasNode,
						RootNode = columnExpression,
						Owner = queryBlock,
						ExplicitDefinition = true
					};

					var asteriskNode = columnExpression.LastTerminalNode != null && columnExpression.LastTerminalNode.Id == Terminals.Asterisk
						? columnExpression.LastTerminalNode
						: null;
					
					if (asteriskNode != null)
					{
						column.IsAsterisk = true;

						var prefixNonTerminal = asteriskNode.ParentNode.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Prefix);
						var columnReference = CreateColumnReference(queryBlock, column, QueryBlockPlacement.SelectList, asteriskNode, prefixNonTerminal);
						column.ColumnReferences.Add(columnReference);

						var tableReferences = queryBlock.ObjectReferences.Where(t => t.FullyQualifiedObjectName == columnReference.FullyQualifiedObjectName || (columnReference.ObjectNode == null && t.FullyQualifiedObjectName.NormalizedName == columnReference.FullyQualifiedObjectName.NormalizedName));
						_asteriskTableReferences[column] = new List<OracleDataObjectReference>(tableReferences);
					}
					else
					{
						var columnGrammarLookup = columnExpression.GetDescendantsWithinSameQuery(Terminals.Identifier, Terminals.RowIdPseudoColumn, Terminals.Count, NonTerminals.AggregateFunction, NonTerminals.AnalyticFunction)
							.ToLookup(n => n.Id, n => n);

						var identifiers = columnGrammarLookup[Terminals.Identifier].Concat(columnGrammarLookup[Terminals.RowIdPseudoColumn]).ToArray();

						var previousColumnReferences = column.ColumnReferences.Count;
						ResolveColumnAndFunctionReferenceFromIdentifiers(queryBlock, column.ColumnReferences, identifiers, QueryBlockPlacement.SelectList, column);
						
						column.IsDirectReference = identifiers.Length == 1 && identifiers[0].GetAncestor(NonTerminals.Expression).ChildNodes.Count == 1;
						var columnReferenceAdded = column.ColumnReferences.Count > previousColumnReferences;
						if (columnReferenceAdded && column.IsDirectReference && columnAliasNode == null)
						{
							column.AliasNode = identifiers[0];
						}

						var grammarSpecificFunctions = columnGrammarLookup[Terminals.Count].Concat(columnGrammarLookup[NonTerminals.AggregateFunction]).Concat(columnGrammarLookup[NonTerminals.AnalyticFunction]);
						CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, queryBlock, column.FunctionReferences, column);
					}

					queryBlock.Columns.Add(column);
				}
			}
		}

		private static void CreateGrammarSpecificFunctionReferences(IEnumerable<StatementGrammarNode> grammarSpecificFunctions, OracleQueryBlock queryBlock, ICollection<OracleProgramReference> functionReferences, OracleSelectListColumn selectListColumn)
		{
			foreach (var identifierNode in grammarSpecificFunctions.Select(n => n.FirstTerminalNode).Distinct())
			{
				var rootNode = identifierNode.GetAncestor(NonTerminals.AnalyticFunctionCall) ?? identifierNode.GetAncestor(NonTerminals.AggregateFunctionCall);
				var analyticClauseNode = rootNode.GetSingleDescendant(NonTerminals.AnalyticClause);

				var parameterList = rootNode.ChildNodes.SingleOrDefault(n => n.Id.In(NonTerminals.ParenthesisEnclosedExpressionListWithMandatoryExpressions, NonTerminals.CountAsteriskParameter, NonTerminals.AggregateFunctionParameter, NonTerminals.ParenthesisEnclosedExpressionListWithIgnoreNulls));
				var parameterNodes = new List<StatementGrammarNode>();
				StatementGrammarNode firstParameterExpression = null;
				if (parameterList != null)
				{
					switch (parameterList.Id)
					{
						case NonTerminals.CountAsteriskParameter:
							parameterNodes.Add(parameterList.ChildNodes.SingleOrDefault(n => n.Id == Terminals.Asterisk));
							break;
						case NonTerminals.AggregateFunctionParameter:
						case NonTerminals.ParenthesisEnclosedExpressionListWithIgnoreNulls:
							firstParameterExpression = parameterList.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.Expression);
							parameterNodes.Add(firstParameterExpression);
							goto default;
						default:
							var nodes = parameterList.GetPathFilterDescendants(n => n != firstParameterExpression && !n.Id.In(NonTerminals.NestedQuery, NonTerminals.ParenthesisEnclosedAggregationFunctionParameters), NonTerminals.ExpressionList, NonTerminals.OptionalParameterExpressionList)
								.Select(n => n.ChildNodes.FirstOrDefault());
							parameterNodes.AddRange(nodes);
							break;
					}
				}

				var functionReference =
					new OracleProgramReference
					{
						FunctionIdentifierNode = identifierNode,
						RootNode = rootNode,
						Owner = queryBlock,
						AnalyticClauseNode = analyticClauseNode,
						ParameterListNode = parameterList,
						ParameterNodes = parameterNodes.AsReadOnly(),
						SelectListColumn = selectListColumn
					};

				functionReferences.Add(functionReference);
			}
		}

		private static StatementGrammarNode[] GetFunctionCallNodes(StatementGrammarNode identifier)
		{
			return identifier.ParentNode.ChildNodes.Where(n => n.Id.In(NonTerminals.ParenthesisEnclosedAggregationFunctionParameters, NonTerminals.AnalyticClause)).ToArray();
		}

		private static OracleProgramReference CreateFunctionReference(OracleQueryBlock queryBlock, OracleSelectListColumn selectListColumn, StatementGrammarNode identifierNode, StatementGrammarNode prefixNonTerminal, ICollection<StatementGrammarNode> functionCallNodes)
		{
			var analyticClauseNode = functionCallNodes.SingleOrDefault(n => n.Id == NonTerminals.AnalyticClause);

			var parameterList = functionCallNodes.SingleOrDefault(n => n.Id == NonTerminals.ParenthesisEnclosedAggregationFunctionParameters);
			var parameterExpressionRootNodes = parameterList != null
				? parameterList
					.GetPathFilterDescendants(
						n => !n.Id.In(NonTerminals.NestedQuery, NonTerminals.ParenthesisEnclosedAggregationFunctionParameters, NonTerminals.AggregateFunctionCall, NonTerminals.AnalyticFunctionCall),
						NonTerminals.ExpressionList, NonTerminals.OptionalParameterExpressionList)
					.Select(n => n.ChildNodes.FirstOrDefault())
					.ToArray()
				: null;

			var functionReference =
				new OracleProgramReference
				{
					FunctionIdentifierNode = identifierNode,
					DatabaseLinkNode = GetDatabaseLinkFromIdentifier(identifierNode),
					RootNode = identifierNode.GetAncestor(NonTerminals.Expression),
					Owner = queryBlock,
					AnalyticClauseNode = analyticClauseNode,
					ParameterListNode = parameterList,
					ParameterNodes = parameterExpressionRootNodes,
					SelectListColumn = selectListColumn
				};

			AddPrefixNodes(functionReference, prefixNonTerminal);

			return functionReference;
		}

		private static StatementGrammarNode GetDatabaseLinkFromQueryTableExpression(StatementGrammarNode queryTableExpression)
		{
			var partitionOrDatabaseLink = queryTableExpression.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.PartitionOrDatabaseLink);
			return partitionOrDatabaseLink == null
				? null
				: GetDatabaseLinkFromNode(partitionOrDatabaseLink);
		}

		private static StatementGrammarNode GetDatabaseLinkFromIdentifier(StatementGrammarNode identifier)
		{
			return GetDatabaseLinkFromNode(identifier.ParentNode);
		}

		private static StatementGrammarNode GetDatabaseLinkFromNode(StatementGrammarNode node)
		{
			var databaseLink = node.ChildNodes.SingleOrDefault(n => n.Id == NonTerminals.DatabaseLink);
			return databaseLink == null ? null : databaseLink.ChildNodes.SingleOrDefault(n => n.Id == Terminals.DatabaseLinkIdentifier);
		}

		private static OracleColumnReference CreateColumnReference(OracleQueryBlock queryBlock, OracleSelectListColumn selectListColumn, QueryBlockPlacement type, StatementGrammarNode identifierNode, StatementGrammarNode prefixNonTerminal)
		{
			var columnReference =
				new OracleColumnReference
				{
					ColumnNode = identifierNode,
					DatabaseLinkNode = GetDatabaseLinkFromIdentifier(identifierNode),
					Placement = type,
					Owner = queryBlock,
					SelectListColumn = selectListColumn
				};

			AddPrefixNodes(columnReference, prefixNonTerminal);

			return columnReference;
		}

		private static void AddPrefixNodes(OracleReference reference, StatementGrammarNode prefixNonTerminal)
		{
			if (prefixNonTerminal == null)
				return;
			
			var objectIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.ObjectIdentifier);
			var schemaIdentifier = prefixNonTerminal.GetSingleDescendant(Terminals.SchemaIdentifier);

			reference.OwnerNode = schemaIdentifier;
			reference.ObjectNode = objectIdentifier;
		}

		private IEnumerable<KeyValuePair<StatementGrammarNode, string>> GetCommonTableExpressionReferences(StatementGrammarNode node)
		{
			var queryRoot = node.GetAncestor(NonTerminals.NestedQuery);
			var subQueryCompondentNode = node.GetAncestor(NonTerminals.SubqueryComponent);
			var cteReferencesWithinSameClause = new List<KeyValuePair<StatementGrammarNode, string>>();
			if (subQueryCompondentNode != null)
			{
				var cteNodeWithinSameClause = subQueryCompondentNode.GetAncestor(NonTerminals.SubqueryComponent);
				while (cteNodeWithinSameClause != null)
				{
					cteReferencesWithinSameClause.Add(GetCteNameNode(cteNodeWithinSameClause));
					cteNodeWithinSameClause = cteNodeWithinSameClause.GetAncestor(NonTerminals.SubqueryComponent);
				}

				if (node.Level - queryRoot.Level > node.Level - subQueryCompondentNode.Level)
				{
					queryRoot = queryRoot.GetAncestor(NonTerminals.NestedQuery);
				}
			}

			if (queryRoot == null)
				return cteReferencesWithinSameClause;

			var commonTableExpressions = queryRoot
				.GetPathFilterDescendants(n => n.Id != NonTerminals.QueryBlock, NonTerminals.SubqueryComponent)
				.Select(GetCteNameNode);
			return commonTableExpressions
				.Concat(cteReferencesWithinSameClause)
				.Concat(GetCommonTableExpressionReferences(queryRoot));
		}

		private KeyValuePair<StatementGrammarNode, string> GetCteNameNode(StatementGrammarNode cteNode)
		{
			var objectIdentifierNode = cteNode.ChildNodes.FirstOrDefault();
			var cteName = objectIdentifierNode == null ? null : objectIdentifierNode.Token.Value.ToQuotedIdentifier();
			return new KeyValuePair<StatementGrammarNode, string>(cteNode, cteName);
		}
	}

	public enum ReferenceType
	{
		SchemaObject,
		CommonTableExpression,
		InlineView
	}

	public enum QueryBlockType
	{
		Normal,
		ScalarSubquery,
		CommonTableExpression
	}
}
