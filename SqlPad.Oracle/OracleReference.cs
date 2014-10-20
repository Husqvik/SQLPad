using System;
using System.Collections.Generic;

namespace SqlPad.Oracle
{
	public abstract class OracleReference
	{
		private OracleReferenceContainer _container;

		protected OracleReference()
		{
			ObjectNodeObjectReferences = new HashSet<OracleObjectWithColumnsReference>();
		}

		public virtual OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null); }
		}

		public bool HasExplicitDefinition { get { return SelectListColumn == null || SelectListColumn.HasExplicitDefinition; } }

		public abstract string Name { get; }

		public string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public QueryBlockPlacement Placement { get; set; }

		public OracleQueryBlock Owner { get; set; }

		public StatementGrammarNode RootNode { get; set; }

		public StatementGrammarNode OwnerNode { get; set; }

		public StatementGrammarNode ObjectNode { get; set; }

		public ICollection<OracleObjectWithColumnsReference> ObjectNodeObjectReferences { get; set; }

		public OracleSchemaObject SchemaObject { get; set; }

		public OracleSelectListColumn SelectListColumn { get; set; }

		public void SetContainer(OracleReferenceContainer container)
		{
			if (_container != null)
			{
				throw new InvalidOperationException("Container has been already set. ");	
			}

			_container = container;
		}

		public OracleReferenceContainer Container
		{
			get { return SelectListColumn ?? Owner ?? _container; }
		}

		public StatementGrammarNode DatabaseLinkNode { get; set; }

		public OracleDatabaseLink DatabaseLink { get; set; }
	}
}
