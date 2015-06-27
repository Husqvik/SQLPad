using System;
using System.Collections.Generic;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	public abstract class OracleReference
	{
		private OracleReferenceContainer _container;
		private OracleObjectIdentifier? _fullyQualifiedName;

		protected OracleReference()
		{
			ObjectNodeObjectReferences = new HashSet<OracleObjectWithColumnsReference>();
		}

		public OracleObjectIdentifier FullyQualifiedObjectName
		{
			get { return _fullyQualifiedName ?? (_fullyQualifiedName = BuildFullyQualifiedObjectName()).Value; }
		}

		protected virtual OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(OwnerNode, ObjectNode, null);
		}

		public bool HasExplicitDefinition { get { return SelectListColumn == null || SelectListColumn.HasExplicitDefinition; } }

		public abstract string Name { get; }

		public virtual string NormalizedName { get { return Name.ToQuotedIdentifier(); } }

		public StatementPlacement Placement { get; set; }

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

		public virtual void Accept(OracleReferenceVisitor visitor)
		{
			throw new NotSupportedException();
		}
	}
}
