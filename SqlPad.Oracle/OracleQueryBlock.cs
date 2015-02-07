using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock : OracleReferenceContainer
	{
		private OracleDataObjectReference _selfObjectReference;
		private bool? _hasRemoteAsteriskReferences;
		private readonly List<OracleSelectListColumn> _columns = new List<OracleSelectListColumn>();
		private readonly List<OracleSelectListColumn> _attachedColumns = new List<OracleSelectListColumn>();
		private readonly List<OracleSelectListColumn> _asteriskColumns = new List<OracleSelectListColumn>();

		public OracleQueryBlock(OracleStatementSemanticModel semanticModel) : base(semanticModel)
		{
			AccessibleQueryBlocks = new List<OracleQueryBlock>();
		}

		public OracleDataObjectReference SelfObjectReference
		{
			get { return _selfObjectReference ?? BuildSelfObjectReference(); }
		}

		public bool HasRemoteAsteriskReferences
		{
			get { return _hasRemoteAsteriskReferences ?? (_hasRemoteAsteriskReferences = HasRemoteAsteriskReferencesInternal(this)).Value; }
		}

		private OracleDataObjectReference BuildSelfObjectReference()
		{
			_selfObjectReference = new OracleDataObjectReference(ReferenceType.InlineView)
			                       {
									   AliasNode = AliasNode,
									   Owner = this
			                       };

			_selfObjectReference.QueryBlocks.Add(this);

			return _selfObjectReference;
		}

		public string Alias { get { return AliasNode == null ? null : AliasNode.Token.Value; } }

		public string NormalizedAlias { get { return Alias.ToQuotedIdentifier(); } }

		public StatementGrammarNode AliasNode { get; set; }

		public QueryBlockType Type { get; set; }
		
		public StatementGrammarNode RootNode { get; set; }

		public StatementGrammarNode WhereClause { get; set; }

		public StatementGrammarNode SelectList { get; set; }

		public bool HasDistinctResultSet { get; set; }

		public StatementGrammarNode FromClause { get; set; }

		public StatementGrammarNode GroupByClause { get; set; }

		public StatementGrammarNode HavingClause { get; set; }
		
		public StatementGrammarNode OrderByClause { get; set; }
		
		public StatementGrammarNode HierarchicalQueryClause { get; set; }

		public StatementGrammarNode ExplicitColumnNameList { get; set; }
		
		public StatementGrammarNode RecursiveSearchClause { get; set; }
		
		public StatementGrammarNode RecursiveCycleClause { get; set; }

		public OracleStatement Statement { get; set; }
		
		public bool IsRecursive { get; set; }
		
		public bool IsRedundant { get; set; }

		public IReadOnlyList<OracleSelectListColumn> Columns { get { return _columns; } }

		public IReadOnlyList<OracleSelectListColumn> AttachedColumns { get { return _attachedColumns; } }
		
		public IReadOnlyCollection<OracleSelectListColumn> AsteriskColumns { get { return _asteriskColumns; } }

		public OracleSqlModelReference ModelReference { get; set; }

		public IEnumerable<OracleReferenceContainer> ChildContainers
		{
			get
			{
				var containers = (IEnumerable<OracleReferenceContainer>)Columns;
				if (ModelReference != null)
				{
					containers = containers.Concat(ModelReference.ChildContainers);
				}

				return containers;
			}
		}

		public IEnumerable<OracleProgramReference> AllProgramReferences { get { return Columns.SelectMany(c => c.ProgramReferences).Concat(ProgramReferences); } }

		public IEnumerable<OracleColumnReference> AllColumnReferences { get { return Columns.SelectMany(c => c.ColumnReferences).Concat(ColumnReferences); } }

		public IEnumerable<OracleTypeReference> AllTypeReferences { get { return Columns.SelectMany(c => c.TypeReferences).Concat(TypeReferences); } }

		public IEnumerable<OracleSequenceReference> AllSequenceReferences { get { return Columns.SelectMany(c => c.SequenceReferences).Concat(SequenceReferences); } }

		public ICollection<OracleQueryBlock> AccessibleQueryBlocks { get; private set; }

		public OracleQueryBlock FollowingConcatenatedQueryBlock { get; set; }
		
		public OracleQueryBlock PrecedingConcatenatedQueryBlock { get; set; }

		public OracleQueryBlock ParentCorrelatedQueryBlock { get; set; }

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

		public void AddAttachedColumn(OracleSelectListColumn column)
		{
			_columns.Add(column);
			_attachedColumns.Add(column);
		}

		public void AddSelectListColumn(OracleSelectListColumn column, int? index = null)
		{
			if (_attachedColumns.Count > 0)
			{
				throw new InvalidOperationException("Query block has attached columns associated. ");
			}

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

		public int IndexOf(OracleSelectListColumn column)
		{
			return _columns.IndexOf(column);
		}

		private static bool HasRemoteAsteriskReferencesInternal(OracleQueryBlock queryBlock)
		{
			var hasRemoteAsteriskReferences = queryBlock._asteriskColumns.Any(c => (c.RootNode.TerminalCount == 1 && queryBlock.ObjectReferences.Any(r => r.DatabaseLinkNode != null)) || (c.ColumnReferences.Any(r => r.ValidObjectReference != null && r.ValidObjectReference.DatabaseLinkNode != null)));

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
					: OrderByClause.GetDescendantsWithinSameQuery(NonTerminals.OrderExpression)
						.Select(GetColumnIndexReference).Where(r => r.ColumnIndex != OracleOrderByColumnIndexReference.None.ColumnIndex);
			}
		}

		private OracleOrderByColumnIndexReference GetColumnIndexReference(StatementGrammarNode orderExpression)
		{
			if (orderExpression.TerminalCount != 1 || orderExpression.FirstTerminalNode.Id != Terminals.NumberLiteral ||
			    orderExpression.FirstTerminalNode.Token.Value.IndexOf('.') != -1)
			{
				return OracleOrderByColumnIndexReference.None;
			}

			var columnIndex = Convert.ToInt32(orderExpression.FirstTerminalNode.Token.Value);
			return
				new OracleOrderByColumnIndexReference
				{
					ColumnIndex = columnIndex,
					Terminal = orderExpression.FirstTerminalNode,
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

	public struct OracleLiteral
	{
		public LiteralType Type;
		
		public StatementGrammarNode Terminal;

		public bool IsMultibyte
		{
			get
			{
				if (Terminal == null || Terminal.Id != Terminals.StringLiteral)
				{
					return false;
				}

				return Terminal.Token.Value[0] == 'n' || Terminal.Token.Value[0] == 'N';
			}
		}
	}

	public enum LiteralType
	{
		Date,
		Timestamp
	}
}
