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
		
		public CommandExecutionHandler ExecutionHandler { get; }

		public CommandExecutionHandler SingleLineExecutionHandler { get; }

		private static readonly HashSet<LineBreakSettings> LineBreaks =
			new HashSet<LineBreakSettings>
			{
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.Select, BreakPosition = GetSelectBreakPosition, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.From, BreakPosition = GetQueryBlockFromBreakPosition, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.WhereClause, ChildNodeId = Terminals.Where, BreakPosition = n => LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.Condition, ChildNodeId = Terminals.Exists, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SelectExpressionExpressionChainedList, ChildNodeId = Terminals.Comma, BreakPosition = n => LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.FromClauseChained, ChildNodeId = Terminals.Comma, BreakPosition = n => LineBreakPosition.AfterNode },
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
				new LineBreakSettings { NonTerminalId = NonTerminals.CommonTableExpressionListChained, ChildNodeId = Terminals.Comma, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryFactoringClause, ChildNodeId = Terminals.With, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryFactoringClause, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => -2 },
				new LineBreakSettings { NonTerminalId = NonTerminals.CommonTableExpression, ChildNodeId = Terminals.As, BreakPosition = n => LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.SelectStatement, ChildNodeId = Terminals.Semicolon, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.ConcatenatedSubquery, ChildNodeId = NonTerminals.SetOperation, BreakPosition = n => LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryTableExpression, ChildNodeId = Terminals.RightParenthesis, BreakPosition = n => LineBreakPosition.BeforeNode, GetIndentationBefore = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.Condition, ChildNodeId = Terminals.RightParenthesis, GetIndentationAfter = GetAfterConditionClosingParenthesisIndentation },
				new LineBreakSettings { NonTerminalId = NonTerminals.ParenthesisEnclosedNestedQuery, ChildNodeId = Terminals.RightParenthesis, GetIndentationAfter = GetAfterExpressionClosingParenthesisIndentation }
			};

		private static int GetAfterConditionClosingParenthesisIndentation(StatementGrammarNode node)
		{
			var conditionNode = node.ParentNode;
			return conditionNode[NonTerminals.ExpressionListOrNestedQuery, NonTerminals.NestedQuery] == null ? 0 : -1;
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
					throw new NotSupportedException($"Node '{node.Id}' is not supported. ");
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
				Terminals.Comma,
				Terminals.Semicolon
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
					ExecutionHandler = c => new StandardStatementFormatter(c).FormatStatements()
				};

			SingleLineExecutionHandler =
				new CommandExecutionHandler
				{
					Name = "FormatStatementAsSingleLine",
					DefaultGestures = GenericCommands.FormatStatementAsSingleLine.InputGestures,
					ExecutionHandler = c => new SingleLineStatementFormatter(c).FormatStatements()
				};
		}

		private abstract class StatementFormatterBase
		{
			protected readonly ActionExecutionContext ExecutionContext;

			protected StatementFormatterBase(ActionExecutionContext executionContext)
			{
				ExecutionContext = executionContext;
			}

			public abstract void FormatStatements();

			protected IEnumerable<StatementBase> Statements
			{
				get
				{
					var statements = ExecutionContext.DocumentRepository.Statements.Where(s => s.RootNode != null);
					return ExecutionContext.SelectionLength == 0
						? statements.Where(s => s.SourcePosition.ContainsIndex(ExecutionContext.CaretOffset))
						: statements.Where(s => s.SourcePosition.IndexStart < ExecutionContext.SelectionEnd && s.SourcePosition.IndexEnd >= ExecutionContext.SelectionStart);
				}
			} 
		}

		private class SingleLineStatementFormatter : StatementFormatterBase
		{
			public SingleLineStatementFormatter(ActionExecutionContext executionContext)
				: base(executionContext)
			{
			}

			public override void FormatStatements()
			{
				foreach (var statement in Statements)
				{
					FormatStatementAsSingleLine(statement);
				}
			}

			private void FormatStatementAsSingleLine(StatementBase statement)
			{
				var builder = new StringBuilder();
				IEnumerable<StatementNode> terminals = ((IEnumerable<StatementNode>)statement.AllTerminals).Concat(statement.Comments).OrderBy(t => t.Token.Index);
				if (ExecutionContext.SelectionLength > 0)
				{
					terminals = terminals.Where(t => t.SourcePosition.IndexStart <= ExecutionContext.SelectionEnd && t.SourcePosition.IndexEnd + 1 >= ExecutionContext.SelectionStart);
				}

				StatementNode precedingNode = null;
				var textSegment = new TextSegment();
				foreach (var terminal in terminals)
				{
					var precedingGrammarTerminal = precedingNode as StatementGrammarNode;
					var grammarTerminal = terminal as StatementGrammarNode;
					if (precedingNode != null && precedingGrammarTerminal != null && !SingleLineNoSpaceAfterTerminals.Contains(precedingGrammarTerminal.Id) && grammarTerminal != null && !SingleLineNoSpaceBeforeTerminals.Contains(grammarTerminal.Id))
					{
						builder.Append(' ');
					}

					builder.Append(terminal.Token.Value);

					if (precedingNode == null)
					{
						textSegment.IndextStart = terminal.SourcePosition.IndexStart;
					}

					precedingNode = terminal;
				}

				if (precedingNode == null)
				{
					return;
				}

				textSegment.Length = precedingNode.SourcePosition.IndexEnd - textSegment.IndextStart + 1;
				textSegment.Text = builder.ToString();

				ExecutionContext.SegmentsToReplace.Add(textSegment);
			}
		}

		private class StandardStatementFormatter : StatementFormatterBase
		{
			public StandardStatementFormatter(ActionExecutionContext executionContext) :base(executionContext)
			{
			}

			public override void FormatStatements()
			{
				var skipSpaceBeforeToken = false;

				foreach (var statement in Statements)
				{
					var builder = new StringBuilder();
					var indentation = String.Empty;
					var textSegment = new TextSegment { IndextStart = -1 };
					FormatNode(statement.RootNode, builder, ref skipSpaceBeforeToken, ref indentation, ref textSegment);

					textSegment.Text = builder.ToString();

					ExecutionContext.SegmentsToReplace.Add(textSegment);
				}
			}

			private void FormatNode(StatementGrammarNode startNode, StringBuilder stringBuilder, ref bool skipSpaceBeforeToken, ref string indentation, ref TextSegment textSegment)
			{
				var childIndex = 0;
				var childNodes = ((IEnumerable<StatementNode>)startNode.ChildNodes)
					.Concat(startNode.Comments)
					.OrderBy(n => n.SourcePosition.IndexStart);

				var formatSelectionOnly = ExecutionContext.SelectionLength > 0;

				foreach (var childNode in childNodes)
				{
					if (formatSelectionOnly)
					{
						if (childNode.SourcePosition.IndexEnd <= ExecutionContext.SelectionStart)
						{
							continue;
						}

						if (childNode.SourcePosition.IndexStart >= ExecutionContext.SelectionEnd)
						{
							break;
						}
					}

					if (childNode.Token != null)
					{
						if (textSegment.IndextStart == -1)
						{
							textSegment.IndextStart = childNode.SourcePosition.IndexStart;
						}

						textSegment.Length = childNode.SourcePosition.IndexEnd - textSegment.IndextStart + 1;
					}

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

						if (formatSelectionOnly && grammarNode.SourcePosition.IndexEnd >= ExecutionContext.SelectionEnd)
						{
							break;
						}
					}
					else
					{
						FormatNode(grammarNode, stringBuilder, ref skipSpaceBeforeToken, ref indentation, ref textSegment);
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
}
