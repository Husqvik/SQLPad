using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleStatementFormatter : IStatementFormatter
	{
		struct LineBreakSettings
		{
			public string NonTerminalId { get; set; }
			public string ChildNodeId { get; set; }
			public LineBreakPosition BreakPosition { get; set; }
		}

		enum LineBreakPosition
		{
			BeforeNode,
			AfterNode
		}

		private readonly SqlFormatterOptions _options;

		private static readonly Dictionary<string, string> LineBreaks =
			new Dictionary<string, string>
			{
				{ NonTerminals.SelectExpressionExpressionChainedList, Terminals.Comma },
				{ NonTerminals.FromClauseChained, Terminals.Comma },
				{ NonTerminals.JoinClause, null },
				{ NonTerminals.ChainedCondition, null },
			};

		/*private static readonly HashSet<string> Indents =
			new HashSet<string>
			{
				{ NonTerminals.SelectList },
				{ NonTerminals.FromClause },
				{ NonTerminals.WhereClause },
				{ NonTerminals.OrderByClause },
			};*/

		public OracleStatementFormatter(SqlFormatterOptions options)
		{
			_options = options;
		}

		public string FormatStatement(StatementCollection statements, int selectionStart, int selectionLength)
		{
			var formattedStatements = statements.Where(s => s.SourcePosition.IndexStart <= selectionStart && s.SourcePosition.IndexEnd >= selectionStart + selectionLength);
			var stringBuilder = new StringBuilder(selectionLength - selectionStart);

			foreach (var statement in formattedStatements)
			{
				var startNode = statement.SourcePosition.IndexStart < selectionStart
					? statement.GetNodeAtPosition(selectionStart)
					: statement.NodeCollection.FirstOrDefault();

				var endNode = statement.SourcePosition.IndexEnd > selectionStart + selectionLength
					? statement.GetNodeAtPosition(selectionStart + selectionLength)
					: statement.NodeCollection.LastOrDefault();

				if (startNode == null)
					continue;

				var queryLevel = startNode.GetNestedQueryLevel();

				FormatNode(startNode, endNode, stringBuilder);
			}

			return stringBuilder.ToString();
		}

		private void FormatNode(StatementDescriptionNode startNode, StatementDescriptionNode endNode, StringBuilder stringBuilder)
		{
			var isLineBreakNonTerminal = LineBreaks.ContainsKey(startNode.Id);
			var breakTerminalId = isLineBreakNonTerminal ? LineBreaks[startNode.Id] : null;
			
			foreach (var childNode in startNode.ChildNodes)
			{
				if (childNode.Type == NodeType.Terminal)
				{
					stringBuilder.Append(childNode.Token.Value);
					stringBuilder.Append(' ');
				}
				else
				{
					FormatNode(childNode, null, stringBuilder);
				}

				if (isLineBreakNonTerminal && (breakTerminalId == null || breakTerminalId == childNode.Id))
				{
					stringBuilder.Append(Environment.NewLine);
				}
			}
		}
	}
}