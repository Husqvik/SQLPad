using System.Collections.Generic;

namespace SqlPad.Oracle
{
	public abstract class OracleReference
	{
		protected OracleReference()
		{
			ObjectNodeObjectReferences = new HashSet<OracleObjectReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null); }
		}

		public abstract string Name { get; }

		public string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public string ObjectName { get { return ObjectNode == null ? null : ObjectNode.Token.Value; } }

		public string ObjectNormalizedName { get { return ObjectNode == null ? null : ObjectName.ToQuotedIdentifier(); } }

		public OracleQueryBlock Owner { get; set; }

		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode ObjectNode { get; set; }

		public ICollection<OracleObjectReference> ObjectNodeObjectReferences { get; set; }

		public OracleSelectListColumn SelectListColumn { get; set; }
	}
}