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
			public Indentation Indentation { get; set; }
			public LineBreakPosition BreakPosition { get; set; }
		}

		[Flags]
		enum LineBreakPosition
		{
			None = 0,
			BeforeNode = 1,
			AfterNode = 2,
		}

		[Flags]
		enum Indentation
		{
			None = 0,
			AddBeforeNode = 1,
			AddAfterNode = 2,
			RemoveBeforeNode = 4,
			RemoveAfterNode = 8
		}

		private readonly SqlFormatterOptions _options;

		private static readonly HashSet<LineBreakSettings> LineBreaks =
			new HashSet<LineBreakSettings>
			{
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.Select, BreakPosition = LineBreakPosition.AfterNode, Indentation = Indentation.AddAfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.From, BreakPosition = LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, Indentation = Indentation.RemoveBeforeNode | Indentation.AddAfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.WhereClause, ChildNodeId = Terminals.Where, BreakPosition = LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, Indentation = Indentation.RemoveBeforeNode | Indentation.AddAfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.SelectExpressionExpressionChainedList, ChildNodeId = Terminals.Comma, BreakPosition = LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.FromClauseChained, ChildNodeId = Terminals.Comma, BreakPosition = LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.JoinClause, ChildNodeId = null, BreakPosition = LineBreakPosition.BeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.ChainedCondition, ChildNodeId = NonTerminals.LogicalOperator, BreakPosition = LineBreakPosition.BeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupByClause, ChildNodeId = Terminals.Group, BreakPosition = LineBreakPosition.BeforeNode, Indentation = Indentation.RemoveBeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupByClause, ChildNodeId = Terminals.By, BreakPosition = LineBreakPosition.AfterNode, Indentation = Indentation.AddAfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.HavingClause, ChildNodeId = Terminals.Having, BreakPosition = LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, Indentation = Indentation.RemoveBeforeNode | Indentation.AddAfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderByClause, ChildNodeId = Terminals.Order, BreakPosition = LineBreakPosition.BeforeNode, Indentation = Indentation.RemoveBeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderByClause, ChildNodeId = Terminals.By, BreakPosition = LineBreakPosition.AfterNode, Indentation = Indentation.AddAfterNode },
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

				string indentation = null;
				FormatNode(startNode, endNode, stringBuilder, false, ref indentation);
			}

			return stringBuilder.ToString();
		}

		private void FormatNode(StatementDescriptionNode startNode, StatementDescriptionNode endNode, StringBuilder stringBuilder, bool previousLineBreak, ref string indentation)
		{
			foreach (var childNode in startNode.ChildNodes)
			{
				var lineBreakSettings = LineBreaks.SingleOrDefault(s => s.NonTerminalId == startNode.Id && (String.IsNullOrEmpty(s.ChildNodeId) || s.ChildNodeId == childNode.Id));
				var breakSettingsFound = !String.IsNullOrEmpty(lineBreakSettings.NonTerminalId);

				if (breakSettingsFound)
				{
					if ((lineBreakSettings.Indentation & Indentation.AddBeforeNode) != Indentation.None)
					{
						indentation += "\t";
					}
					else if ((lineBreakSettings.Indentation & Indentation.RemoveBeforeNode) != Indentation.None)
					{
						indentation = indentation.Remove(0, 1);
					}

					if ((lineBreakSettings.BreakPosition & LineBreakPosition.BeforeNode) != LineBreakPosition.None)
					{
						stringBuilder.Append(Environment.NewLine);
						stringBuilder.Append(indentation);

						if (childNode.Type == NodeType.NonTerminal)
						{
							previousLineBreak = true;
						}
					}
				}

				if (childNode.Type == NodeType.Terminal)
				{
					if (!breakSettingsFound && !OracleGrammarDescription.SingleCharacterTerminals.Contains(childNode.Id) && !previousLineBreak)
					{
						stringBuilder.Append(' ');
					}
					
					stringBuilder.Append(childNode.Token.Value);
				}
				else
				{
					FormatNode(childNode, null, stringBuilder, previousLineBreak, ref indentation);
				}

				previousLineBreak = false;

				if (breakSettingsFound)
				{
					if ((lineBreakSettings.Indentation & Indentation.AddAfterNode) != Indentation.None)
					{
						indentation += "\t";
					}
					else if ((lineBreakSettings.Indentation & Indentation.RemoveAfterNode) != Indentation.None)
					{
						indentation = indentation.Remove(0, 1);
					}

					if ((lineBreakSettings.BreakPosition & LineBreakPosition.AfterNode) != LineBreakPosition.None)
					{
						stringBuilder.Append(Environment.NewLine);
						previousLineBreak = true;
						stringBuilder.Append(indentation);
					}
				}
			}
		}
	}
}
