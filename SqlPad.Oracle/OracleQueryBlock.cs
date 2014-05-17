using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleQueryBlock (Alias={Alias}; Type={Type}; RootNode={RootNode}; Columns={Columns.Count})")]
	public class OracleQueryBlock
	{
		private OracleObjectReference _selfObjectReference;

		public OracleQueryBlock()
		{
			ObjectReferences = new List<OracleObjectReference>();
			Columns = new List<OracleSelectListColumn>();
			AccessibleQueryBlocks = new List<OracleQueryBlock>();
			ColumnReferences = new List<OracleColumnReference>();
			FunctionReferences = new List<OracleFunctionReference>();
		}

		public OracleObjectReference SelfObjectReference
		{
			get { return _selfObjectReference ?? BuildSelfObjectReference(); }
		}

		private OracleObjectReference BuildSelfObjectReference()
		{
			_selfObjectReference = new OracleObjectReference
			                       {
									   AliasNode = AliasNode,
									   Owner = this,
									   Type = TableReferenceType.NestedQuery
			                       };

			_selfObjectReference.QueryBlocks.Add(this);

			return _selfObjectReference;
		}

		public string Alias { get { return AliasNode == null ? null : AliasNode.Token.Value; } }

		public string NormalizedAlias { get { return Alias.ToQuotedIdentifier(); } }

		public StatementDescriptionNode AliasNode { get; set; }

		public QueryBlockType Type { get; set; }
		
		public bool HasAsteriskClause { get; set; }

		public StatementDescriptionNode RootNode { get; set; }
		
		public OracleStatement Statement { get; set; }

		public ICollection<OracleObjectReference> ObjectReferences { get; private set; }

		public ICollection<OracleSelectListColumn> Columns { get; private set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }
		
		public ICollection<OracleFunctionReference> FunctionReferences { get; private set; }

		public IEnumerable<OracleFunctionReference> AllFunctionReferences { get { return Columns.SelectMany(c => c.FunctionReferences).Concat(FunctionReferences); } }

		public IEnumerable<OracleColumnReference> AllColumnReferences { get { return Columns.SelectMany(c => c.ColumnReferences).Concat(ColumnReferences); } }

		public ICollection<OracleQueryBlock> AccessibleQueryBlocks { get; private set; }

		public OracleQueryBlock FollowingConcatenatedQueryBlock { get; set; }
		
		public OracleQueryBlock PrecedingConcatenatedQueryBlock { get; set; }

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
	}
}
