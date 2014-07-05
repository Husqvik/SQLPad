using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleDataObjectReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={Type != SqlPad.Oracle.TableReferenceType.InlineView ? ObjectNode.Token.Value : \"<Nested subquery>\"}; Alias={AliasNode == null ? null : AliasNode.Token.Value}; Type={Type})")]
	public class OracleDataObjectReference : OracleReference
	{
		private List<OracleColumn> _columns;

		public OracleDataObjectReference()
		{
			QueryBlocks = new List<OracleQueryBlock>();
		}

		public override string Name { get { throw new NotImplementedException(); } }

		public OracleObjectIdentifier FullyQualifiedName
		{
			get
			{
				return OracleObjectIdentifier.Create(
					AliasNode == null ? OwnerNode : null,
					Type == TableReferenceType.InlineView ? null : ObjectNode, AliasNode);
			}
		}

		public ICollection<OracleColumn> Columns
		{
			get
			{
				if (_columns == null)
				{
					_columns = new List<OracleColumn>();
					if (Type == TableReferenceType.SchemaObject)
					{
						if (SearchResult.SchemaObject != null)
						{
							_columns.AddRange(SearchResult.SchemaObject.Columns.Values);
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

		public SchemaObjectResult<OracleDataObject> SearchResult { get; set; }
		
		public StatementDescriptionNode AliasNode { get; set; }
		
		public ICollection<OracleQueryBlock> QueryBlocks { get; private set; }

		public TableReferenceType Type { get; set; }
	}
}
