using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock : OracleReferenceContainer
	{
		private OracleDataObjectReference _selfObjectReference;

		public OracleQueryBlock(OracleStatementSemanticModel semanticModel) : base(semanticModel)
		{
			Columns = new List<OracleSelectListColumn>();
			AccessibleQueryBlocks = new List<OracleQueryBlock>();
		}

		public OracleDataObjectReference SelfObjectReference
		{
			get { return _selfObjectReference ?? BuildSelfObjectReference(); }
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
		
		public bool HasAsteriskClause { get; set; }

		public StatementGrammarNode RootNode { get; set; }

		public StatementGrammarNode WhereClause { get; set; }

		public StatementGrammarNode SelectList { get; set; }

		public bool HasDistinctResultSet { get; set; }

		public StatementGrammarNode FromClause { get; set; }

		public StatementGrammarNode GroupByClause { get; set; }

		public StatementGrammarNode HavingClause { get; set; }
		
		public StatementGrammarNode OrderByClause { get; set; }
		
		public StatementGrammarNode ModelClause { get; set; }

		public OracleStatement Statement { get; set; }

		public IList<OracleSelectListColumn> Columns { get; private set; }
		
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
	}
}
