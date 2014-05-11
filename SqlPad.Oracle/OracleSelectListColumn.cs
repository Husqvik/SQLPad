using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
				return _columnDescription ?? BuildColumnDescription();
			}
			set { _columnDescription = value; }
		}

		private OracleColumn BuildColumnDescription()
		{
			OracleColumnReference columnReference;
			var columnDescription = IsDirectColumnReference && (columnReference = ColumnReferences.Single()).ColumnNodeObjectReferences.Count == 1 && columnReference.ColumnNodeColumnReferences == 1
				? columnReference.ColumnNodeObjectReferences.Single().Columns.Single(c => c.Name == columnReference.NormalizedName)
				: null;

			_columnDescription =
				new OracleColumn
				{
					Name = AliasNode == null ? null : AliasNode.Token.Value.ToQuotedIdentifier(),
					Nullable = columnDescription == null || columnDescription.Nullable,
					Type = columnDescription == null ? null : columnDescription.Type,
					Precision = columnDescription == null ? Int32.MinValue : columnDescription.Precision,
					Scale = columnDescription == null ? Int32.MinValue : columnDescription.Scale,
					Size = columnDescription == null ? Int32.MinValue : columnDescription.Size
				};

			return _columnDescription;
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
