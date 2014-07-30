using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	public abstract class OracleReferenceContainer
	{
		protected OracleReferenceContainer()
		{
			TypeReferences = new List<OracleTypeReference>();
			ColumnReferences = new List<OracleColumnReference>();
			FunctionReferences = new List<OracleProgramReference>();
			SequenceReferences = new List<OracleSequenceReference>();
		}

		public ICollection<OracleTypeReference> TypeReferences { get; private set; }

		public ICollection<OracleSequenceReference> SequenceReferences { get; private set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }

		public ICollection<OracleProgramReference> FunctionReferences { get; private set; }

	}

	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock : OracleReferenceContainer
	{
		private OracleDataObjectReference _selfObjectReference;

		public OracleQueryBlock()
		{
			ObjectReferences = new List<OracleDataObjectReference>();
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

		public bool ContainsSchemaQualifiers
		{
			get
			{
				return ObjectReferences.Select(o => o.OwnerNode)
					.Concat(AllColumnReferences.Select(c => c.OwnerNode))
					.Concat(AllProgramReferences.Select(f => f.OwnerNode))
					.Any(n => n != null);
			}
		}

		public StatementGrammarNode GroupByClause { get; set; }

		public StatementGrammarNode HavingClause { get; set; }
		
		public StatementGrammarNode OrderByClause { get; set; }
		
		public OracleStatement Statement { get; set; }

		public ICollection<OracleDataObjectReference> ObjectReferences { get; private set; }

		public ICollection<OracleSelectListColumn> Columns { get; private set; }
		
		public IEnumerable<OracleProgramReference> AllProgramReferences { get { return Columns.SelectMany(c => c.FunctionReferences).Concat(FunctionReferences); } }

		public IEnumerable<OracleColumnReference> AllColumnReferences { get { return Columns.SelectMany(c => c.ColumnReferences).Concat(ColumnReferences); } }

		public IEnumerable<OracleTypeReference> AllTypeReferences { get { return Columns.SelectMany(c => c.TypeReferences).Concat(TypeReferences); } }

		public IEnumerable<OracleSequenceReference> AllSequenceReferences { get { return Columns.SelectMany(c => c.SequenceReferences).Concat(SequenceReferences); } }

		public ICollection<OracleQueryBlock> AccessibleQueryBlocks { get; private set; }

		public OracleQueryBlock FollowingConcatenatedQueryBlock { get; set; }
		
		public OracleQueryBlock PrecedingConcatenatedQueryBlock { get; set; }

		public OracleQueryBlock ParentCorrelatedQueryBlock { get; set; }

		public IEnumerable<OracleQueryBlock> AllFollowingConcatenatedQueryBlocks
		{
			get
			{
				var concatenatedQueryBlock = FollowingConcatenatedQueryBlock;
				while (concatenatedQueryBlock != null)
				{
					yield return concatenatedQueryBlock;

					concatenatedQueryBlock = concatenatedQueryBlock.FollowingConcatenatedQueryBlock;
				}
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
