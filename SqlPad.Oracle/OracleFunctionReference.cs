using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleFunctionReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Function={FunctionIdentifierNode.Token.Value})")]
	public class OracleFunctionReference
	{
		public OracleFunctionReference()
		{
			ObjectNodeObjectReferences = new HashSet<OracleObjectReference>();
			ColumnNodeObjectReferences = new HashSet<OracleObjectReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null); }
		}

		public string Name { get { return FunctionIdentifierNode.Token.Value; } }

		public string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public string ObjectName { get { return ObjectNode == null ? null : ObjectNode.Token.Value; } }

		public string ObjectNormalizedName { get { return ObjectNode == null ? null : ObjectName.ToQuotedIdentifier(); } }

		public OracleQueryBlock Owner { get; set; }

		//public OracleSelectListColumn SelectListColumn { get; set; }
		
		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode ObjectNode { get; set; }

		public StatementDescriptionNode FunctionIdentifierNode { get; set; }

		public ICollection<OracleObjectReference> ObjectNodeObjectReferences { get; set; }
		
		public ICollection<OracleObjectReference> ColumnNodeObjectReferences { get; set; }
	}
}