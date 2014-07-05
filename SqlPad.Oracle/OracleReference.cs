using System.Collections.Generic;

namespace SqlPad.Oracle
{
	public abstract class OracleReference
	{
		protected OracleReference()
		{
			ObjectNodeObjectReferences = new HashSet<OracleDataObjectReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null); }
		}

		public abstract string Name { get; }

		public string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public OracleQueryBlock Owner { get; set; }

		public StatementDescriptionNode RootNode { get; set; }

		public StatementDescriptionNode OwnerNode { get; set; }

		public StatementDescriptionNode ObjectNode { get; set; }

		public ICollection<OracleDataObjectReference> ObjectNodeObjectReferences { get; set; }

		public OracleSelectListColumn SelectListColumn { get; set; }

		public OracleReferenceContainer Container
		{
			get { return SelectListColumn ?? (OracleReferenceContainer)Owner; }
		}
	}
}
