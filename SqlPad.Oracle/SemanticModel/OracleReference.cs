using System;
using System.Collections.Generic;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	public abstract class OracleReference
	{
		private OracleObjectIdentifier? _fullyQualifiedName;

		protected OracleReference()
		{
			ObjectNodeObjectReferences = new HashSet<OracleObjectWithColumnsReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName => _fullyQualifiedName ?? (_fullyQualifiedName = BuildFullyQualifiedObjectName()).Value;

		protected virtual OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null);
		}

		public bool HasExplicitDefinition => SelectListColumn == null || SelectListColumn.HasExplicitDefinition;

		public abstract string Name { get; }

		public virtual string NormalizedName => Name.ToQuotedIdentifier();

		public StatementPlacement Placement { get; set; }

		public OracleQueryBlock Owner { get; set; }

		public StatementGrammarNode RootNode { get; set; }

		public StatementGrammarNode OwnerNode { get; set; }

		public StatementGrammarNode ObjectNode { get; set; }

		public ICollection<OracleObjectWithColumnsReference> ObjectNodeObjectReferences { get; set; }

		public OracleSchemaObject SchemaObject { get; set; }

		public OracleSelectListColumn SelectListColumn { get; set; }

		public OracleReferenceContainer Container { get; set; }

		public StatementGrammarNode DatabaseLinkNode { get; set; }

		public OracleDatabaseLink DatabaseLink { get; set; }

		public virtual void Accept(IOracleReferenceVisitor visitor)
		{
			throw new NotSupportedException();
		}

		public void CopyPropertiesFrom(OracleReference reference)
		{
			ObjectNode = reference.ObjectNode;
			OwnerNode = reference.OwnerNode;
			RootNode = reference.RootNode;
			Owner = reference.Owner;
			SelectListColumn = reference.SelectListColumn;
			Container = reference.Container;
			Placement = reference.Placement;
		}
	}
}
