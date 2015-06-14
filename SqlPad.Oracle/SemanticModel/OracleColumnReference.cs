using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleColumnReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Object={ObjectNode == null ? null : ObjectNode.Token.Value}; Column={ColumnNode.Token.Value}; HasExplicitDefinition={HasExplicitDefinition})")]
	public class OracleColumnReference : OracleReference
	{
		private StatementGrammarNode _columnNode;
		private string _normalizedName;

		public OracleColumnReference(OracleReferenceContainer referenceContainer)
		{
			ColumnNodeObjectReferences = new HashSet<OracleObjectWithColumnsReference>();
			ColumnNodeColumnReferences = new List<OracleColumn>();
			SetContainer(referenceContainer);
		}

		public override string Name { get { return _columnNode.Token.Value; } }

		public override string NormalizedName
		{
			get { return _normalizedName; }
		}

		public bool ReferencesAllColumns { get { return _columnNode.Token.Value == "*"; } }

		public StatementGrammarNode ColumnNode
		{
			get { return _columnNode; }
			set
			{
				if (_columnNode == value)
				{
					return;
				}

				_columnNode = value;
				_normalizedName = _columnNode == null ? null : _columnNode.Token.Value.ToQuotedIdentifier();
			}
		}

		public ICollection<OracleObjectWithColumnsReference> ColumnNodeObjectReferences { get; private set; }

		public ICollection<OracleColumn> ColumnNodeColumnReferences { get; set; }
		
		public OracleColumn ColumnDescription { get; set; }
		
		public bool IsCorrelated { get; set; }

		public OracleObjectWithColumnsReference ValidObjectReference
		{
			get
			{
				if (ColumnNodeObjectReferences.Count == 1)
					return ColumnNodeObjectReferences.First();

				return ObjectNodeObjectReferences.Count == 1
					? ObjectNodeObjectReferences.First()
					: null;
			}
		}
	}
}
