using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleSelectListColumn (Alias={AliasNode == null ? null : AliasNode.Token.Value}; IsDirectColumnReference={IsDirectColumnReference})")]
	public class OracleSelectListColumn
	{
		private OracleColumn _columnDescription;

		public OracleSelectListColumn()
		{
			ColumnReferences = new List<OracleColumnReference>();
			FunctionReferences = new List<OracleFunctionReference>();
		}

		public bool IsDirectColumnReference { get; set; }
		
		public bool IsAsterisk { get; set; }
		
		public bool IsGrouped { get; set; }

		public bool ExplicitDefinition { get; set; }

		public string NormalizedName
		{
			get
			{
				if (AliasNode != null)
					return AliasNode.Token.Value.ToQuotedIdentifier();

				return _columnDescription == null ? null : _columnDescription.Name;
			}
		}

		public StatementDescriptionNode AliasNode { get; set; }

		public StatementDescriptionNode RootNode { get; set; }
		
		public OracleQueryBlock Owner { get; set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }

		public ICollection<OracleFunctionReference> FunctionReferences { get; private set; }

		public OracleColumn ColumnDescription
		{
			get
			{
				return _columnDescription ??
				       (_columnDescription =
					       new OracleColumn
					       {
						       Name = AliasNode == null ? null : AliasNode.Token.Value.ToQuotedIdentifier()
						       // TODO: Fill other properties
					       });
			}
			set { _columnDescription = value; }
		}

		public OracleSelectListColumn AsImplicit()
		{
			return new OracleSelectListColumn
			       {
				       ExplicitDefinition = false,
				       AliasNode = AliasNode,
				       RootNode = RootNode,
				       IsDirectColumnReference = true,
					   _columnDescription = _columnDescription
			       };
		}
	}
}
