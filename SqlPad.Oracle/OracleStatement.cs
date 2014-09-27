using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (ChildNodes={RootNode == null ? 0 : RootNode.ChildNodes.Count})")]
	public class OracleStatement : StatementBase
	{
		private ICollection<BindVariableConfiguration> _bindVariables;

		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ProcessingStatus = ProcessingStatus.Success,
				SourcePosition = new SourcePosition { IndexStart = -1, IndexEnd = -1 }
			};

		public override ICollection<BindVariableConfiguration> BindVariables
		{
			get { return _bindVariables ?? (_bindVariables = BuildBindVariableCollection()); }
		}

		public override IEnumerable<FoldingSection> Sections
		{
			get
			{
				return RootNode == null
					? base.Sections
					: RootNode.GetDescendants(NonTerminals.NestedQuery, NonTerminals.Subquery)
						.Where(n => n.Id == NonTerminals.NestedQuery || n.ParentNode.Id != NonTerminals.NestedQuery)
						.Select(n =>
							new FoldingSection
							{
								FoldingStart = n.SourcePosition.IndexStart,
								FoldingEnd = n.SourcePosition.IndexEnd + 1,
								Node = n,
								IsNestedSection = n.ParentNode.ParentNode != null,
								Placeholder = "Subquery"
							});
			}
		}

		private ICollection<BindVariableConfiguration> BuildBindVariableCollection()
		{
			return BuildBindVariableIdentifierTerminalLookup()
				.OrderBy(g => g.Key)
				.Select(g => new BindVariableConfiguration { Name = g.Key, DataType = OracleBindVariable.DataTypeVarchar2, Nodes = g.ToList().AsReadOnly(), DataTypes = OracleBindVariable.DataTypes })
				.ToArray();
		}

		private IEnumerable<IGrouping<string, StatementGrammarNode>> BuildBindVariableIdentifierTerminalLookup()
		{
			var sourceTerminals = RootNode == null
				? new StatementGrammarNode[0]
				: RootNode.Terminals.Where(t => t.Id == OracleGrammarDescription.Terminals.BindVariableIdentifier);

			return sourceTerminals.ToLookup(t => t.Token.Value.Trim('"'));
		}
	}
}
