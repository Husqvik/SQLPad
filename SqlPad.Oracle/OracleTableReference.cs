using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleTableReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={Type != SqlPad.Oracle.TableReferenceType.NestedQuery ? TableNode.Token.Value : \"<Nested subquery>\"}; Alias={AliasNode == null ? null : AliasNode.Token.Value}; Type={Type})")]
	public class OracleTableReference : IOracleTableReference
	{
		private List<OracleColumn> _columns;

		public OracleTableReference()
		{
			Nodes = new StatementDescriptionNode[0];
			QueryBlocks = new List<OracleQueryBlock>();
		}

		public OracleObjectIdentifier FullyQualifiedName
		{
			get
			{
				return OracleObjectIdentifier.Create(
					AliasNode == null ? OwnerNode : null,
					Type == TableReferenceType.NestedQuery ? null : TableNode, AliasNode);
			}
		}

		public ICollection<OracleColumn> Columns
		{
			get
			{
				if (_columns == null)
				{
					_columns = new List<OracleColumn>();
					if (Type == TableReferenceType.PhysicalObject)
					{
						if (SearchResult.SchemaObject != null)
						{
							_columns.AddRange(SearchResult.SchemaObject.Columns);
						}
					}
					else
					{
						var queryColumns = QueryBlocks.SelectMany(qb => qb.Columns).Select(c => c.ColumnDescription);
						_columns.AddRange(queryColumns);
					}
				}

				return _columns;
			}
		}

		public SchemaObjectResult SearchResult { get; set; }

		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode TableNode { get; set; }
		
		public StatementDescriptionNode AliasNode { get; set; }
		
		public StatementDescriptionNode TableReferenceNode { get; set; }

		public ICollection<StatementDescriptionNode> Nodes { get; set; }
		
		public ICollection<OracleQueryBlock> QueryBlocks { get; set; }

		public TableReferenceType Type { get; set; }
	}
}
