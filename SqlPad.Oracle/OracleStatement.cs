using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (ParseStatus={ParseStatus}; ChildNodes={RootNode == null ? 0 : RootNode.ChildNodes.Count})")]
	public class OracleStatement : StatementBase
	{
		private ICollection<BindVariableConfiguration> _bindVariables;

		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ParseStatus = ParseStatus.Success,
				SourcePosition = new SourcePosition { IndexStart = -1, IndexEnd = -1 }
			};

		public override ICollection<BindVariableConfiguration> BindVariables
		{
			get { return _bindVariables ?? (_bindVariables = BuildBindVariableCollection()); }
		}

		private ICollection<BindVariableConfiguration> BuildBindVariableCollection()
		{
			return BuildBindVariableIdentifierTerminalLookup()
				.OrderBy(g => g.Key)
				.Select(g =>
					new BindVariableConfiguration
					{
						Name = g.Key,
						DataType = TerminalValues.Varchar2,
						Nodes = g.ToList().AsReadOnly(),
						DataTypes = OracleBindVariable.DataTypes
					})
				.ToArray();
		}

		private IEnumerable<IGrouping<string, StatementGrammarNode>> BuildBindVariableIdentifierTerminalLookup()
		{
			return AllTerminals.Where(t => t.Id == Terminals.BindVariableIdentifier)
				.ToLookup(t => t.Token.Value.ToNormalizedBindVariableIdentifier());
		}

		public static bool TryGetPlSqlUnitName(StatementBase statement, out OracleObjectIdentifier objectIdentifier)
		{
			objectIdentifier = OracleObjectIdentifier.Empty;
			
			switch (statement.RootNode.Id)
			{
				case NonTerminals.CreatePlSqlStatement:
					var createPlSqlObjectClause = statement.RootNode[NonTerminals.CreatePlSqlObjectClause];
					if (createPlSqlObjectClause != null && createPlSqlObjectClause.ChildNodes.Count == 1)
					{
						var createFunctionNode = createPlSqlObjectClause[NonTerminals.CreateFunction, NonTerminals.PlSqlFunctionSource];
						createPlSqlObjectClause = createFunctionNode ?? createPlSqlObjectClause.ChildNodes[0];
						
						objectIdentifier = GetObjectIdentifierFromNode(createPlSqlObjectClause);
					}

					break;
				case NonTerminals.StandaloneStatement:
					var alterObjectClause = statement.RootNode[NonTerminals.Statement, NonTerminals.AlterStatement, NonTerminals.AlterObjectClause];
					if (alterObjectClause != null && alterObjectClause.ChildNodes.Count == 1 && alterObjectClause.ChildNodes[0].Id.In(NonTerminals.AlterProcedure, NonTerminals.AlterFunction, NonTerminals.AlterPackage))
					{
						objectIdentifier = GetObjectIdentifierFromNode(alterObjectClause.ChildNodes[0]);
					}

					break;
			}

			return !String.IsNullOrEmpty(objectIdentifier.Name);
		}

		private static OracleObjectIdentifier GetObjectIdentifierFromNode(StatementGrammarNode node)
		{
			var schemaIdentifierTerminal = node[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier];
			var objectIdentifierTerminal = node[Terminals.ObjectIdentifier];
			return objectIdentifierTerminal == null
				? OracleObjectIdentifier.Empty
				: OracleObjectIdentifier.Create(schemaIdentifierTerminal, objectIdentifierTerminal, null);
		}
	}

	public class OracleStatementCollection : StatementCollection
	{
		private static readonly OracleFoldingSectionProvider FoldingSectionProvider = new OracleFoldingSectionProvider();

		public OracleStatementCollection(IList<StatementBase> statements, IReadOnlyList<IToken> tokens, IEnumerable<StatementCommentNode> comments)
			: base(statements, tokens, comments)
		{
		}

		public override IEnumerable<FoldingSection> FoldingSections
		{
			get { return FoldingSectionProvider.GetFoldingSections(Tokens); }
		}
	}
}
