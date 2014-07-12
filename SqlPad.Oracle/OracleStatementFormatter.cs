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
			public Func<StatementDescriptionNode, int> GetIndentationBefore { get; set; }
			public Func<StatementDescriptionNode, int> GetIndentationAfter { get; set; }
			public Func<StatementDescriptionNode, LineBreakPosition> BreakPosition { get; set; }
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

		private static readonly HashSet<LineBreakSettings> LineBreaks =
			new HashSet<LineBreakSettings>
			{
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.Select, BreakPosition = n => LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.From, BreakPosition = n => LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
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
			};

		private static int GetAfterTableReferenceIndentation(StatementDescriptionNode node)
		{
			var tableReferenceNode = node.ParentNode;
			return tableReferenceNode.GetDescendants(NonTerminals.NestedQuery).Any() ? -1 : 0;
		}

		private static LineBreakPosition GetOrderByBreakPosition(StatementDescriptionNode node)
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

		private static readonly HashSet<string> AddSpaceTerminalIds =
			new HashSet<string>
			{
				Terminals.In,
				Terminals.Over,
				Terminals.Keep
			};

		public OracleStatementFormatter(SqlFormatterOptions options)
		{
			_options = options;

			ExecutionHandler =
				new CommandExecutionHandler
				{
					Name = "FormatStatement",
					DefaultGestures = GenericCommands.FormatStatementCommand.InputGestures,
					ExecutionHandler = ExecutionHandlerImplementation
				};
		}

		private void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			if (executionContext.DocumentRepository == null)
				return;

			var formattedStatements = executionContext.DocumentRepository.Statements.Where(s => s.SourcePosition.IndexStart <= executionContext.SelectionStart + executionContext.SelectionLength && s.SourcePosition.IndexEnd + 1 >= executionContext.SelectionStart)
				.OrderBy(s => s.SourcePosition.IndexStart)
				.ToArray();

			if (formattedStatements.Length == 0)
				return;

			var stringBuilder = new StringBuilder();
			var skipSpaceBeforeToken = false;

			foreach (var statement in formattedStatements)
			{
				if (statement.RootNode == null)
					continue;

				string indentation = null;

				FormatNode(statement.RootNode, stringBuilder, ref skipSpaceBeforeToken, ref indentation);
			}

			var indextStart = formattedStatements[0].RootNode.FirstTerminalNode.SourcePosition.IndexStart;

			var formattedStatement =
				new TextSegment
				{
					Text = stringBuilder.ToString(),
					IndextStart = indextStart,
					Length = formattedStatements[formattedStatements.Length - 1].RootNode.LastTerminalNode.SourcePosition.IndexEnd - indextStart + 1,
				};

			executionContext.SegmentsToReplace.Add(formattedStatement);
		}

		private void FormatNode(StatementDescriptionNode startNode, StringBuilder stringBuilder, ref bool skipSpaceBeforeToken, ref string indentation)
		{
			var childIndex = 0;
			foreach (var childNode in startNode.ChildNodes)
			{
				var lineBreakSettings = LineBreaks.SingleOrDefault(s => s.NonTerminalId == startNode.Id && s.ChildNodeId == childNode.Id);
				if (String.IsNullOrEmpty(lineBreakSettings.NonTerminalId))
				{
					lineBreakSettings = LineBreaks.SingleOrDefault(s => s.NonTerminalId == startNode.Id && String.IsNullOrEmpty(s.ChildNodeId));
				}

				var breakSettingsFound = !String.IsNullOrEmpty(lineBreakSettings.NonTerminalId);

				var breakBefore = false;
				var breakBeforeSettingsFound = breakSettingsFound && (!String.IsNullOrEmpty(lineBreakSettings.ChildNodeId) || childIndex == 0);
				if (breakBeforeSettingsFound)
				{
					var indentationBefore = lineBreakSettings.GetIndentationBefore == null ? 0 : lineBreakSettings.GetIndentationBefore(childNode);
					if (indentationBefore > 0)
					{
						indentation += new String('\t', indentationBefore);
					}
					else if (indentationBefore < 0)
					{
						indentation = indentation.Remove(0, -indentationBefore);
					}

					breakBefore = lineBreakSettings.BreakPosition != null && (lineBreakSettings.BreakPosition(childNode) & LineBreakPosition.BeforeNode) != LineBreakPosition.None;
					if (breakBefore)
					{
						stringBuilder.Append(Environment.NewLine);
						stringBuilder.Append(indentation);

						if (childNode.Type == NodeType.NonTerminal)
						{
							skipSpaceBeforeToken = true;
						}
					}
				}

				if (childNode.Type == NodeType.Terminal)
				{
					if ((!breakSettingsFound || !breakBefore) &&
					    (!OracleGrammarDescription.SingleCharacterTerminals.Contains(childNode.Id) ||
					     OracleGrammarDescription.MathTerminals.Contains(childNode.Id) ||
						 childNode.Id == Terminals.Colon) &&
					    !skipSpaceBeforeToken && stringBuilder.Length > 0)
					{
						stringBuilder.Append(' ');
					}

					stringBuilder.Append(childNode.Token.Value);

					if (AddSpaceTerminalIds.Contains(childNode.Id))
					{
						stringBuilder.Append(' ');
					}

					skipSpaceBeforeToken = SkipSpaceTerminalIds.Contains(childNode.Id) || childNode.Id.In(Terminals.Dot, Terminals.Colon);
				}
				else
				{
					FormatNode(childNode, stringBuilder, ref skipSpaceBeforeToken, ref indentation);
				}

				var breakAfterSettingsFound = breakSettingsFound && (!String.IsNullOrEmpty(lineBreakSettings.ChildNodeId) || childIndex + 1 == startNode.ChildNodes.Count);
				if (breakAfterSettingsFound)
				{
					var indentationAfter = lineBreakSettings.GetIndentationAfter == null ? 0 : lineBreakSettings.GetIndentationAfter(childNode);
					if (indentationAfter > 0)
					{
						indentation += new String('\t', indentationAfter);
					}
					else if (indentationAfter < 0)
					{
						indentation = indentation.Remove(0, -indentationAfter);
					}

					if (lineBreakSettings.BreakPosition != null && (lineBreakSettings.BreakPosition(childNode) & LineBreakPosition.AfterNode) != LineBreakPosition.None)
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
