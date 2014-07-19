using System;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleSelectListColumn (Alias={AliasNode == null ? null : AliasNode.Token.Value}; IsDirectReference={IsDirectReference})")]
	public class OracleSelectListColumn : OracleReferenceContainer
	{
		private OracleColumn _columnDescription;

		public bool IsDirectReference { get; set; }
		
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

		public StatementGrammarNode AliasNode { get; set; }

		public StatementGrammarNode RootNode { get; set; }
		
		public OracleQueryBlock Owner { get; set; }

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
			/*OracleColumnReference columnReference;
			var columnDescription = IsDirectReference && (columnReference = ColumnReferences.Single()).ColumnNodeObjectReferences.Count == 1 && columnReference.ColumnNodeColumnReferences == 1
				? columnReference.ColumnNodeObjectReferences.Single().Columns.Single(c => c.Name == columnReference.NormalizedName)
				: null;*/

			var columnDescription = IsDirectReference && ColumnReferences.Count == 1
				? ColumnReferences.First().ColumnDescription
				: null;

			_columnDescription =
				new OracleColumn
				{
					Name = AliasNode == null ? null : AliasNode.Token.Value.ToQuotedIdentifier(),
					Nullable = columnDescription == null || columnDescription.Nullable,
					Type = columnDescription == null ? null : columnDescription.Type,
					Precision = columnDescription == null ? null : columnDescription.Precision,
					Scale = columnDescription == null ? null : columnDescription.Scale,
					Size = columnDescription == null ? Int32.MinValue : columnDescription.Size,
					CharacterSize = columnDescription == null ? Int32.MinValue : columnDescription.CharacterSize,
					Unit = columnDescription == null ? DataUnit.NotApplicable : columnDescription.Unit
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
				       IsDirectReference = true,
					   _columnDescription = _columnDescription
			       };
		}
	}
}
