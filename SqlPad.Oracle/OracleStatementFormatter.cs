using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlPad.Commands;
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
			public Func<StatementGrammarNode, int> GetIndentationBefore { get; set; }
			public Func<StatementGrammarNode, int> GetIndentationAfter { get; set; }
			public Func<StatementGrammarNode, LineBreakPosition> BreakPosition { get; set; }
		}

		[Flags]
		enum LineBreakPosition
		{
			None = 0,
			BeforeNode = 1,
			AfterNode = 2,
		}

		private readonly SqlFormatterOptions _options;
		
		public CommandExecutionHandler ExecutionHandler { get; private set; }

		public CommandExecutionHandler SingleLineExecutionHandler { get; private set; }

		private static readonly HashSet<LineBreakSettings> LineBreaks =
			new HashSet<LineBreakSettings>
			{
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.Select, BreakPosition = GetSelectBreakPosition, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.From, BreakPosition = GetQueryBlockFromBreakPosition, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.WhereClause, ChildNodeId = Terminals.Where, BreakPosition = n => LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.Condition, ChildNodeId = Terminals.Exists, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SelectExpressionExpressionChainedList, ChildNodeId = Terminals.Comma, BreakPosition = n => LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.FromClauseChained, ChildNodeId = Terminals.Comma, BreakPosition = n => LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.TableReference, GetIndentationAfter = GetAfterTableReferenceIndentation },
				new LineBreakSettings { NonTerminalId = NonTerminals.JoinClause, ChildNodeId = null, BreakPosition = n => LineBreakPosition.BeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.JoinColumnsOrCondition, ChildNodeId = Terminals.On, BreakPosition = n => LineBreakPosition.BeforeNode, GetIndentationBefore = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.JoinColumnsOrCondition, ChildNodeId = NonTerminals.Condition, GetIndentationAfter = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.ChainedCondition, ChildNodeId = NonTerminals.LogicalOperator, BreakPosition = n => LineBreakPosition.BeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupByClause, ChildNodeId = Terminals.Group, BreakPosition = n => LineBreakPosition.BeforeNode, GetIndentationBefore = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupByClause, ChildNodeId = Terminals.By, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupingClauseChained, ChildNodeId = Terminals.Comma, BreakPosition = n => LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.HavingClause, ChildNodeId = Terminals.Having, BreakPosition = n => LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderByClause, ChildNodeId = Terminals.Order, BreakPosition = GetOrderByBreakPosition, GetIndentationBefore = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderByClause, ChildNodeId = Terminals.By, BreakPosition = GetOrderByBreakPosition, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderExpressionChained, ChildNodeId = Terminals.Comma, BreakPosition = GetOrderByBreakPosition },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryComponentChained, ChildNodeId = Terminals.Comma, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryFactoringClause, ChildNodeId = Terminals.With, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryFactoringClause, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => -2 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryComponent, ChildNodeId = Terminals.As, BreakPosition = n => LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.SelectStatement, ChildNodeId = Terminals.Semicolon, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.ConcatenatedSubquery, ChildNodeId = NonTerminals.SetOperation, BreakPosition = n => LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryTableExpression, ChildNodeId = Terminals.RightParenthesis, BreakPosition = n => LineBreakPosition.BeforeNode, GetIndentationBefore = n => -2 },
				new LineBreakSettings { NonTerminalId = NonTerminals.Condition, ChildNodeId = Terminals.RightParenthesis, GetIndentationAfter = GetAfterConditionClosingParenthesisIndentation },
				new LineBreakSettings { NonTerminalId = NonTerminals.Expression, ChildNodeId = Terminals.RightParenthesis, GetIndentationAfter = GetAfterExpressionClosingParenthesisIndentation }
			};

		private static int GetAfterConditionClosingParenthesisIndentation(StatementGrammarNode node)
		{
			var conditionNode = node.ParentNode;
			return conditionNode.GetDescendantByPath(NonTerminals.ExpressionListOrNestedQuery) == null ? 0 : -1;
		}

		private static int GetAfterExpressionClosingParenthesisIndentation(StatementGrammarNode node)
		{
			var expressionNode = node.ParentNode;
			return expressionNode.GetDescendants(NonTerminals.NestedQuery).Any() ? -1 : 0;
		}

		private static LineBreakPosition GetQueryBlockFromBreakPosition(StatementGrammarNode node)
		{
			return node.FollowingTerminal != null && node.FollowingTerminal.Id == Terminals.LeftParenthesis
				? LineBreakPosition.BeforeNode
				: LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode;
		}

		private static LineBreakPosition GetSelectBreakPosition(StatementGrammarNode node)
		{
			return node.PrecedingTerminal != null && node.PrecedingTerminal.Id == Terminals.LeftParenthesis
				? LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode
				: LineBreakPosition.AfterNode;
		}

		private static int GetAfterTableReferenceIndentation(StatementGrammarNode node)
		{
			var tableReferenceNode = node.ParentNode;
			return tableReferenceNode.GetDescendants(NonTerminals.NestedQuery).Any() ? -1 : 0;
		}

		private static LineBreakPosition GetOrderByBreakPosition(StatementGrammarNode node)
		{
			var expression = node.GetPathFilterAncestor(n => n.Id != NonTerminals.NestedQuery, NonTerminals.Expression);
			if (expression != null)
				return LineBreakPosition.None;

			switch (node.Id)
			{
				case Terminals.Order:
					return LineBreakPosition.BeforeNode;
				case Terminals.By:
					return LineBreakPosition.AfterNode;
				case Terminals.Comma:
					return LineBreakPosition.AfterNode;
				default:
					throw new NotSupportedException(String.Format("Node '{0}' is not supported. ", node.Id));
			}
		}

		private static readonly HashSet<string> SkipSpaceTerminalIds =
			new HashSet<string>
			{
				Terminals.LeftParenthesis,
				NonTerminals.FromClause,
				NonTerminals.WhereClause,
				NonTerminals.OrderByClause,
			};

		private static readonly HashSet<string> SingleLineNoSpaceBeforeTerminals =
			new HashSet<string>
			{
				Terminals.Dot,
				Terminals.RightParenthesis,
				Terminals.AtCharacter,
				Terminals.Comma
			};

		private static readonly HashSet<string> SingleLineNoSpaceAfterTerminals =
			new HashSet<string>
			{
				Terminals.Colon,
				Terminals.Dot,
				Terminals.LeftParenthesis,
				Terminals.AtCharacter
			};

		private static readonly HashSet<string> SingleCharacterTerminals =
			new HashSet<string>
			{
				Terminals.RightParenthesis,
				Terminals.Comma,
				Terminals.Semicolon,
				Terminals.Dot,
				Terminals.AtCharacter,
				Terminals.Colon
			};

		public OracleStatementFormatter(SqlFormatterOptions options)
		{
			_options = options;

			ExecutionHandler =
				new CommandExecutionHandler
				{
					Name = "FormatStatement",
					DefaultGestures = GenericCommands.FormatStatement.InputGestures,
					ExecutionHandler = ExecutionHandlerImplementation
				};

			SingleLineExecutionHandler =
				new CommandExecutionHandler
				{
					Name = "FormatStatementAsSingleLine",
					DefaultGestures = GenericCommands.FormatStatementAsSingleLine.InputGestures,
					ExecutionHandler = SingleLineExecutionHandlerImplementation
				};
		}

		private void SingleLineExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			ExecutionHandlerImplementationInternal(executionContext, FormatStatementsAsSingleLine);
		}

		private void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			ExecutionHandlerImplementationInternal(executionContext, FormatStatements);
		}

		private void ExecutionHandlerImplementationInternal(CommandExecutionContext executionContext, Func<ICollection<StatementBase>, string> formatFunction)
		{
			if (executionContext.DocumentRepository == null)
				return;

			var statementsToFormat = executionContext.DocumentRepository.Statements.Where(s => s.SourcePosition.IndexStart <= executionContext.SelectionEnd && s.SourcePosition.IndexEnd + 1 >= executionContext.SelectionStart && s.RootNode != null)
				.OrderBy(s => s.SourcePosition.IndexStart)
				.ToArray();

			if (statementsToFormat.Length == 0)
				return;

			var indextStart = statementsToFormat[0].RootNode.FirstTerminalNode.SourcePosition.IndexStart;

			var lastStatement = statementsToFormat[statementsToFormat.Length - 1];
			var lastStatementIndexEnd = lastStatement.TerminatorNode == null
				? lastStatement.RootNode.LastTerminalNode.SourcePosition.IndexEnd
				: lastStatement.TerminatorNode.SourcePosition.IndexEnd;

			var formattedStatement =
				new TextSegment
				{
					Text = formatFunction(statementsToFormat),
					IndextStart = indextStart,
					Length = lastStatementIndexEnd - indextStart + 1,
				};

			executionContext.SegmentsToReplace.Add(formattedStatement);
		}

		private string FormatStatementsAsSingleLine(ICollection<StatementBase> formattedStatements)
		{
			var builder = new StringBuilder();

			var statementIndex = 0;
			foreach (var statement in formattedStatements)
			{
				FormatStatementAsSingleLine(statement, builder);

				if (++statementIndex < formattedStatements.Count)
				{
					builder.AppendLine();
				}
			}

			return builder.ToString();
		}

		private void FormatStatementAsSingleLine(StatementBase statement, StringBuilder builder)
		{
			var terminals = ((IEnumerable<StatementNode>) statement.AllTerminals).Concat(statement.Comments);
			StatementNode precedingNode = null;
			foreach (var terminal in terminals.OrderBy(t => t.Token.Index))
			{
				var precedingGrammarTerminal = precedingNode as StatementGrammarNode;
				var grammarTerminal = terminal as StatementGrammarNode;
				if (precedingNode != null && precedingGrammarTerminal != null && !SingleLineNoSpaceAfterTerminals.Contains(precedingGrammarTerminal.Id) && grammarTerminal != null && !SingleLineNoSpaceBeforeTerminals.Contains(grammarTerminal.Id))
				{
					builder.Append(' ');
				}

				builder.Append(terminal.Token.Value);

				precedingNode = terminal;
			}

			AppendTerminatorIfExists(statement, builder);
		}

		private void AppendTerminatorIfExists(StatementBase statement, StringBuilder builder)
		{
			if (statement.TerminatorNode != null)
			{
				builder.Append(statement.TerminatorNode.Token.Value);
			}			
		}

		private string FormatStatements(IEnumerable<StatementBase> formattedStatements)
		{
			var builder = new StringBuilder();
			var skipSpaceBeforeToken = false;

			foreach (var statement in formattedStatements)
			{
				var indentation = String.Empty;
				FormatNode(statement.RootNode, builder, ref skipSpaceBeforeToken, ref indentation);

				AppendTerminatorIfExists(statement, builder);
			}

			return builder.ToString();
		}

		private void FormatNode(StatementGrammarNode startNode, StringBuilder stringBuilder, ref bool skipSpaceBeforeToken, ref string indentation)
		{
			var childIndex = 0;
			var childNodes = ((IEnumerable<StatementNode>)startNode.ChildNodes)
				.Concat(startNode.Comments)
				.OrderBy(n => n.SourcePosition.IndexStart);
			
			foreach (var childNode in childNodes)
			{
				var grammarNode = childNode as StatementGrammarNode;
				if (grammarNode == null)
				{
					stringBuilder.Append(childNode.Token.Value);
					continue;
				}

				var lineBreakSettings = LineBreaks.SingleOrDefault(s => s.NonTerminalId == startNode.Id && s.ChildNodeId == grammarNode.Id);
				if (String.IsNullOrEmpty(lineBreakSettings.NonTerminalId))
				{
					lineBreakSettings = LineBreaks.SingleOrDefault(s => s.NonTerminalId == startNode.Id && String.IsNullOrEmpty(s.ChildNodeId));
				}

				var breakSettingsFound = !String.IsNullOrEmpty(lineBreakSettings.NonTerminalId);

				var breakBefore = false;
				var breakBeforeSettingsFound = breakSettingsFound && (!String.IsNullOrEmpty(lineBreakSettings.ChildNodeId) || childIndex == 0);
				if (breakBeforeSettingsFound)
				{
					var indentationBefore = lineBreakSettings.GetIndentationBefore == null ? 0 : lineBreakSettings.GetIndentationBefore(grammarNode);
					if (indentationBefore > 0)
					{
						indentation += new String('\t', indentationBefore);
					}
					else if (indentationBefore < 0)
					{
						indentation = indentation.Remove(0, Math.Min(-indentationBefore, indentation.Length));
					}

					breakBefore = lineBreakSettings.BreakPosition != null && (lineBreakSettings.BreakPosition(grammarNode) & LineBreakPosition.BeforeNode) != LineBreakPosition.None;
					if (breakBefore)
					{
						stringBuilder.Append(Environment.NewLine);
						stringBuilder.Append(indentation);

						if (grammarNode.Type == NodeType.NonTerminal)
						{
							skipSpaceBeforeToken = true;
						}
					}
				}

				if (grammarNode.Type == NodeType.Terminal)
				{
					if ((!breakSettingsFound || !breakBefore) &&
						(!SingleCharacterTerminals.Contains(grammarNode.Id) ||
					     OracleGrammarDescription.MathTerminals.Contains(grammarNode.Id) ||
						 grammarNode.Id == Terminals.Colon) &&
					    !skipSpaceBeforeToken && stringBuilder.Length > 0)
					{
						stringBuilder.Append(' ');
					}

					stringBuilder.Append(grammarNode.Token.Value);

					skipSpaceBeforeToken = SkipSpaceTerminalIds.Contains(grammarNode.Id) || grammarNode.Id.In(Terminals.Dot, Terminals.Colon, Terminals.AtCharacter);
				}
				else
				{
					FormatNode(grammarNode, stringBuilder, ref skipSpaceBeforeToken, ref indentation);
				}

				var breakAfterSettingsFound = breakSettingsFound && (!String.IsNullOrEmpty(lineBreakSettings.ChildNodeId) || childIndex + 1 == startNode.ChildNodes.Count);
				if (breakAfterSettingsFound)
				{
					var indentationAfter = lineBreakSettings.GetIndentationAfter == null ? 0 : lineBreakSettings.GetIndentationAfter(grammarNode);
					if (indentationAfter > 0)
					{
						indentation += new String('\t', indentationAfter);
					}
					else if (indentationAfter < 0)
					{
						indentation = indentation.Remove(0, Math.Min(-indentationAfter, indentation.Length));
					}

					if (lineBreakSettings.BreakPosition != null && (lineBreakSettings.BreakPosition(grammarNode) & LineBreakPosition.AfterNode) != LineBreakPosition.None)
					{
						stringBuilder.Append(Environment.NewLine);
						skipSpaceBeforeToken = true;
						stringBuilder.Append(indentation);
					}
				}

				childIndex++;
			}
		}
	}
}
