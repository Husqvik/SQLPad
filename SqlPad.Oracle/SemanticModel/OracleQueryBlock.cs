using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock : OracleReferenceContainer
	{
		private readonly List<OracleSelectListColumn> _columns = new List<OracleSelectListColumn>();
		private readonly List<OracleSelectListColumn> _attachedColumns = new List<OracleSelectListColumn>();
		private readonly List<OracleSelectListColumn> _asteriskColumns = new List<OracleSelectListColumn>();
		private readonly List<OracleQueryBlock> _commonTableExpressions = new List<OracleQueryBlock>();
		private readonly List<OracleProgramMetadata> _attachedFunctions = new List<OracleProgramMetadata>();
		private readonly List<OracleJoinDescription> _joinDescriptions = new List<OracleJoinDescription>();

		private OracleDataObjectReference _selfObjectReference;
		private bool? _hasRemoteAsteriskReferences;
		private ILookup<string, OracleSelectListColumn> _namedColumns;

		internal static readonly Func<OracleSelectListColumn, bool> PredicateContainsAnalyticFuction = c => c.ProgramReferences.Any(p => p.AnalyticClauseNode != null);

		public OracleQueryBlock(OracleStatement statement, StatementGrammarNode rootNode, OracleStatementSemanticModel semanticModel) : base(semanticModel)
		{
			if (rootNode == null)
			{
				throw new ArgumentNullException(nameof(rootNode));
			}

			if (!String.Equals(rootNode.Id, NonTerminals.QueryBlock))
			{
				throw new ArgumentException($"'rootNode' parameter must be '{NonTerminals.QueryBlock}' non-terminal. ", nameof(rootNode));
			}

			Statement = statement;
			RootNode = rootNode;

			FromClause = rootNode[NonTerminals.FromClause];
			HierarchicalQueryClause = rootNode[NonTerminals.HierarchicalQueryClause];

			if (HierarchicalQueryClause != null)
			{
				var hasNoCycleSupport = HierarchicalQueryClause[NonTerminals.HierarchicalQueryConnectByClause, OracleGrammarDescription.Terminals.NoCycle] != null;
				HierarchicalClauseReference = new OracleHierarchicalClauseReference(hasNoCycleSupport);
			}
		}

		private bool IsFrozen => _namedColumns != null;

		public ILookup<string, OracleSelectListColumn> NamedColumns
		{
			get { return _namedColumns ?? (_namedColumns = _columns.ToLookup(c => c.NormalizedName)); }
		}

		public OracleDataObjectReference SelfObjectReference => _selfObjectReference ?? BuildSelfObjectReference();

		public bool HasRemoteAsteriskReferences => _hasRemoteAsteriskReferences ?? (_hasRemoteAsteriskReferences = HasRemoteAsteriskReferencesInternal(this)).Value;

		private OracleDataObjectReference BuildSelfObjectReference()
		{
			_selfObjectReference =
				new OracleDataObjectReference(ReferenceType.InlineView)
				{
					AliasNode = AliasNode,
					Owner = this
				};

			_selfObjectReference.QueryBlocks.Add(this);

			return _selfObjectReference;
		}

		public string Alias => AliasNode?.Token.Value;

		public string NormalizedAlias => Alias.ToQuotedIdentifier();

		public StatementGrammarNode AliasNode { get; set; }

		public QueryBlockType Type { get; set; }

		public StatementGrammarNode RootNode { get; }

		public StatementGrammarNode WhereClause { get; set; }

		public StatementGrammarNode SelectList { get; set; }

		public bool HasDistinctResultSet { get; set; }

		public StatementGrammarNode FromClause { get; }

		public StatementGrammarNode GroupByClause { get; set; }

		public StatementGrammarNode HavingClause { get; set; }
		
		public StatementGrammarNode OrderByClause { get; set; }
		
		public StatementGrammarNode HierarchicalQueryClause { get; }

		public StatementGrammarNode ExplicitColumnNameList { get; set; }

		public IReadOnlyDictionary<StatementGrammarNode, string> ExplicitColumnNames { get; set; }

		public StatementGrammarNode RecursiveSearchClause { get; set; }

		public StatementGrammarNode RecursiveCycleClause { get; set; }

		public OracleStatement Statement { get; }

		public bool IsRecursive { get; set; }

		public bool IsRedundant { get; set; }

		public IList<OracleJoinDescription> JoinDescriptions => _joinDescriptions;

		public IList<OracleProgramMetadata> AttachedFunctions => _attachedFunctions;

		public IEnumerable<OracleProgramMetadata> AccessibleAttachedFunctions => GetAccessibleAttachedFunctions(this);

		private static IEnumerable<OracleProgramMetadata> GetAccessibleAttachedFunctions(OracleQueryBlock queryBlock)
		{
			var attachedFunctions = (IEnumerable<OracleProgramMetadata>)queryBlock._attachedFunctions;
			if (queryBlock.Parent != null)
			{
				attachedFunctions = attachedFunctions.Concat(GetAccessibleAttachedFunctions(queryBlock.Parent));
			}

			return attachedFunctions;
		}
		
		public IReadOnlyList<OracleQueryBlock> CommonTableExpressions => _commonTableExpressions;

		public IReadOnlyList<OracleSelectListColumn> Columns => _columns;

		public IReadOnlyList<OracleSelectListColumn> AttachedColumns => _attachedColumns;

		public IReadOnlyCollection<OracleSelectListColumn> AsteriskColumns => _asteriskColumns;

		public OracleSqlModelReference ModelReference { get; set; }

		public OracleHierarchicalClauseReference HierarchicalClauseReference { get; private set; }

		public IEnumerable<OracleReferenceContainer> ChildContainers
		{
			get
			{
				var containers = (IEnumerable<OracleReferenceContainer>)Columns;
				containers = containers.Concat(ObjectReferences.OfType<OraclePivotTableReference>().Select(pt => pt.SourceReferenceContainer));
				
				if (ModelReference != null)
				{
					containers = containers.Concat(ModelReference.ChildContainers);
				}

				return containers;
			}
		}

		public IEnumerable<OracleProgramReference> AllProgramReferences { get { return ChildContainers.SelectMany(c => c.ProgramReferences).Concat(ProgramReferences); } }

		public IEnumerable<OracleColumnReference> AllColumnReferences { get { return ChildContainers.SelectMany(c => c.ColumnReferences).Concat(ColumnReferences); } }

		public IEnumerable<OracleTypeReference> AllTypeReferences { get { return ChildContainers.SelectMany(c => c.TypeReferences).Concat(TypeReferences); } }

		public IEnumerable<OracleSequenceReference> AllSequenceReferences { get { return ChildContainers.SelectMany(c => c.SequenceReferences).Concat(SequenceReferences); } }

		public ICollection<OracleQueryBlock> AccessibleQueryBlocks { get; } = new List<OracleQueryBlock>();

		public IReadOnlyList<StatementGrammarNode> Terminals { get; set; }

		public OracleQueryBlock FollowingConcatenatedQueryBlock { get; set; }
		
		public OracleQueryBlock PrecedingConcatenatedQueryBlock { get; set; }

		public OracleQueryBlock OuterCorrelatedQueryBlock { get; set; }
		
		public OracleQueryBlock Parent { get; set; }

		public bool ContainsAnsiJoin { get; set; }

		public bool IsMainQueryBlock
		{
			get
			{
				var hasParentQueryBlock = RootNode.GetAncestor(NonTerminals.QueryBlock) != null;
				return !hasParentQueryBlock && RootNode.GetAncestor(NonTerminals.SubqueryFactoringClause) == null;
			}
		}

		public bool IsInSelectStatement => RootNode.GetAncestor(NonTerminals.SelectStatement) != null;

		public bool IsInInsertStatement => RootNode.GetAncestor(NonTerminals.InsertStatement) != null;

		public IEnumerable<OracleQueryBlock> AllPrecedingConcatenatedQueryBlocks
		{
			get { return GetAllConcatenatedQueryBlocks(qb => qb.PrecedingConcatenatedQueryBlock); }
		}
		
		public IEnumerable<OracleQueryBlock> AllFollowingConcatenatedQueryBlocks
		{
			get { return GetAllConcatenatedQueryBlocks(qb => qb.FollowingConcatenatedQueryBlock); }
		}

		private IEnumerable<OracleQueryBlock> GetAllConcatenatedQueryBlocks(Func<OracleQueryBlock, OracleQueryBlock> getConcatenatedQueryBlockFunction)
		{
			var concatenatedQueryBlock = getConcatenatedQueryBlockFunction(this);
			while (concatenatedQueryBlock != null)
			{
				yield return concatenatedQueryBlock;

				concatenatedQueryBlock = getConcatenatedQueryBlockFunction(concatenatedQueryBlock);
			}
		}

		public IEnumerable<OracleReference> DatabaseLinkReferences
		{
			get
			{
				var programReferences = (IEnumerable<OracleReference>)AllProgramReferences.Where(p => p.DatabaseLinkNode != null);
				var sequenceReferences = AllSequenceReferences.Where(p => p.DatabaseLinkNode != null);
				var columnReferences = AllColumnReferences.Where(p => p.DatabaseLinkNode != null);
				var objectReferences = ObjectReferences.Where(p => p.DatabaseLinkNode != null);
				return programReferences.Concat(sequenceReferences).Concat(columnReferences).Concat(objectReferences);
			}
		}

		public bool ContainsAnalyticFunction()
		{
			return Columns.Any(PredicateContainsAnalyticFuction);
		}

		public void AddAttachedColumn(OracleSelectListColumn column)
		{
			CheckIfFrozen();

			_columns.Add(column);
			_attachedColumns.Add(column);
		}

		public void AddCommonTableExpressions(OracleQueryBlock queryBlock)
		{
			if (queryBlock == null)
			{
				throw new ArgumentNullException(nameof(queryBlock));
			}

			if (queryBlock.Type != QueryBlockType.CommonTableExpression)
			{
				throw new ArgumentException("Query block must be of type 'CommonTableExpression'. ", nameof(queryBlock));
			}

			CheckIfFrozen();

			var index = _commonTableExpressions.FindIndex(qb => qb.RootNode.SourcePosition.IndexStart > queryBlock.RootNode.SourcePosition.IndexEnd);
			if (index == -1)
			{
				_commonTableExpressions.Add(queryBlock);
			}
			else
			{
				_commonTableExpressions.Insert(index, queryBlock);
			}
		}

		public void AddSelectListColumn(OracleSelectListColumn column, int? index = null)
		{
			if (_attachedColumns.Count > 0)
			{
				throw new InvalidOperationException("Query block has attached columns associated. ");
			}

			CheckIfFrozen();

			if (index.HasValue)
			{
				_columns.Insert(index.Value, column);
			}
			else
			{
				_columns.Add(column);
			}

			if (column.IsAsterisk)
			{
				_asteriskColumns.Add(column);
			}
		}

		private void CheckIfFrozen()
		{
			if (IsFrozen)
			{
				throw new InvalidOperationException("Query block has been frozen. ");
			}
		}

		public int IndexOf(OracleSelectListColumn column)
		{
			return _columns.IndexOf(column);
		}

		private static bool HasRemoteAsteriskReferencesInternal(OracleQueryBlock queryBlock)
		{
			var hasRemoteAsteriskReferences = queryBlock._asteriskColumns.Any(c => (c.RootNode.TerminalCount == 1 && queryBlock.ObjectReferences.Any(r => r.DatabaseLinkNode != null)) || (c.ColumnReferences.Any(r => r.ValidObjectReference?.DatabaseLinkNode != null)));

			OracleQueryBlock childQueryBlock;
			return hasRemoteAsteriskReferences ||
				   queryBlock.ObjectReferences.Any(o => o.QueryBlocks.Count == 1 && (childQueryBlock = o.QueryBlocks.First()) != queryBlock && HasRemoteAsteriskReferencesInternal(childQueryBlock));
		}

		public IEnumerable<OracleOrderByColumnIndexReference> OrderByColumnIndexReferences
		{
			get
			{
				return OrderByClause == null
					? Enumerable.Empty<OracleOrderByColumnIndexReference>()
					: OrderByClause.GetDescendantsWithinSameQueryBlock(NonTerminals.OrderExpression)
						.Select(GetColumnIndexReference).Where(r => r.ColumnIndex != OracleOrderByColumnIndexReference.None.ColumnIndex);
			}
		}

		private OracleOrderByColumnIndexReference GetColumnIndexReference(StatementGrammarNode orderExpression)
		{
			var expression = orderExpression[NonTerminals.Expression];
			if (expression == null || expression.TerminalCount != 1 || expression.FirstTerminalNode.Id != OracleGrammarDescription.Terminals.NumberLiteral ||
				expression.FirstTerminalNode.Token.Value.IndexOf('.') != -1 || !Int32.TryParse(expression.FirstTerminalNode.Token.Value, out var columnIndex))
			{
				return OracleOrderByColumnIndexReference.None;
			}

			return
				new OracleOrderByColumnIndexReference
				{
					ColumnIndex = columnIndex,
					Terminal = expression.FirstTerminalNode,
					IsValid = columnIndex <= _columns.Count - _asteriskColumns.Count
				};
		}
	}

	public struct OracleOrderByColumnIndexReference
	{
		public static readonly OracleOrderByColumnIndexReference None = new OracleOrderByColumnIndexReference { ColumnIndex = -1 };

		public int ColumnIndex { get; set; }

		public StatementGrammarNode Terminal { get; set; }

		public bool IsValid { get; set; }
	}
}
