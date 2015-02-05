using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	public abstract class OracleObjectWithColumnsReference : OracleReference
	{
		private readonly List<OracleQueryBlock> _queryBlocks = new List<OracleQueryBlock>();

		public abstract IReadOnlyList<OracleColumn> Columns { get; }

		public virtual ICollection<OracleQueryBlock> QueryBlocks { get { return _queryBlocks; } }

		public abstract ReferenceType Type { get; }
	}

	[DebuggerDisplay("OracleDataObjectReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={Type != SqlPad.Oracle.ReferenceType.InlineView ? ObjectNode.Token.Value : \"<Nested subquery>\"}; Alias={AliasNode == null ? null : AliasNode.Token.Value}; Type={Type})")]
	public class OracleDataObjectReference : OracleObjectWithColumnsReference
	{
		private IReadOnlyList<OracleColumn> _columns;
		private readonly ReferenceType _referenceType;

		public OracleDataObjectReference(ReferenceType referenceType)
		{
			_referenceType = referenceType;
		}

		public override string Name { get { throw new NotImplementedException(); } }

		public override OracleObjectIdentifier FullyQualifiedObjectName
		{
			get
			{
				return OracleObjectIdentifier.Create(
					AliasNode == null ? OwnerNode : null,
					Type == ReferenceType.InlineView ? null : ObjectNode, AliasNode);
			}
		}

		public override IReadOnlyList<OracleColumn> Columns
		{
			get
			{
				if (_columns != null)
				{
					return _columns;
				}

				var columns = new List<OracleColumn>();
				if (Type == ReferenceType.SchemaObject)
				{
					var dataObject = SchemaObject.GetTargetSchemaObject() as OracleDataObject;
					if (dataObject != null)
					{
						columns.AddRange(dataObject.Columns.Values);
					}
				}
				else
				{
					var queryColumns = QueryBlocks.SelectMany(qb => qb.Columns)
						.Where(c => !c.IsAsterisk)
						.Select(c => c.ColumnDescription);

					columns.AddRange(queryColumns);
				}

				return _columns = columns.AsReadOnly();
			}
		}
		
		public StatementGrammarNode AliasNode { get; set; }
		
		public override ReferenceType Type { get { return _referenceType; } }
	}
}
