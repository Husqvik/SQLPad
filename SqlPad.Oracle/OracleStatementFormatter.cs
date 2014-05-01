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
			public Func<StatementDescriptionNode, int> GetIndentationBefore { get; set; }
			public Func<StatementDescriptionNode, int> GetIndentationAfter { get; set; }
			public LineBreakPosition BreakPosition { get; set; }
		}

		[Flags]
		enum LineBreakPosition
		{
			None = 0,
			BeforeNode = 1,
			AfterNode = 2,
		}

		private readonly SqlFormatterOptions _options;

		private static readonly HashSet<LineBreakSettings> LineBreaks =
			new HashSet<LineBreakSettings>
			{
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.Select, BreakPosition = LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.QueryBlock, ChildNodeId = Terminals.From, BreakPosition = LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.WhereClause, ChildNodeId = Terminals.Where, BreakPosition = LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SelectExpressionExpressionChainedList, ChildNodeId = Terminals.Comma, BreakPosition = LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.FromClauseChained, ChildNodeId = Terminals.Comma, BreakPosition = LineBreakPosition.AfterNode/*, GetIndentationAfter = GetAfterTableReferenceIndentation*/ },
				//new LineBreakSettings { NonTerminalId = NonTerminals.FromClause, GetIndentationAfter = GetAfterTableReferenceIndentation },
				new LineBreakSettings { NonTerminalId = NonTerminals.TableReference, GetIndentationAfter = GetAfterTableReferenceIndentation },
				
				new LineBreakSettings { NonTerminalId = NonTerminals.JoinClause, ChildNodeId = null, BreakPosition = LineBreakPosition.BeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.JoinColumnsOrCondition, ChildNodeId = Terminals.On, BreakPosition = LineBreakPosition.BeforeNode, GetIndentationBefore = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.JoinColumnsOrCondition, ChildNodeId = NonTerminals.Condition, GetIndentationAfter = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.ChainedCondition, ChildNodeId = NonTerminals.LogicalOperator, BreakPosition = LineBreakPosition.BeforeNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupByClause, ChildNodeId = Terminals.Group, BreakPosition = LineBreakPosition.BeforeNode, GetIndentationBefore = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupByClause, ChildNodeId = Terminals.By, BreakPosition = LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.GroupingClauseChained, ChildNodeId = Terminals.Comma, BreakPosition = LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.HavingClause, ChildNodeId = Terminals.Having, BreakPosition = LineBreakPosition.BeforeNode | LineBreakPosition.AfterNode, GetIndentationBefore = n => -1, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderByClause, ChildNodeId = Terminals.Order, BreakPosition = LineBreakPosition.BeforeNode, GetIndentationBefore = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderByClause, ChildNodeId = Terminals.By, BreakPosition = LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.OrderExpressionChained, ChildNodeId = Terminals.Comma, BreakPosition = LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryComponentChained, ChildNodeId = Terminals.Comma, BreakPosition = LineBreakPosition.AfterNode, GetIndentationAfter = n => -1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryFactoringClause, ChildNodeId = Terminals.With, BreakPosition = LineBreakPosition.AfterNode, GetIndentationAfter = n => 1 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryFactoringClause, BreakPosition = LineBreakPosition.AfterNode, GetIndentationAfter = n => -2 },
				new LineBreakSettings { NonTerminalId = NonTerminals.SubqueryComponent, ChildNodeId = Terminals.As, BreakPosition = LineBreakPosition.AfterNode },
				new LineBreakSettings { NonTerminalId = NonTerminals.SelectStatement, ChildNodeId = Terminals.Semicolon, BreakPosition = LineBreakPosition.AfterNode, GetIndentationAfter = n => -1 },
			};

		private static int GetAfterTableReferenceIndentation(StatementDescriptionNode node)
		{
			var tableReferenceNode = node.ParentNode;//.ParentNode;
			return tableReferenceNode.GetDescendants(NonTerminals.NestedQuery).Any() ? -1 : 0;
		}

		private static readonly HashSet<string> SkipSpaceTerminalIds =
			new HashSet<string>
			{
				Terminals.LeftParenthesis,
				NonTerminals.FromClause,
				NonTerminals.WhereClause,
				NonTerminals.OrderByClause,
			};

		public OracleStatementFormatter(SqlFormatterOptions options)
		{
			_options = options;
		}

		public ICollection<TextSegment> FormatStatement(StatementCollection statements, int selectionStart, int selectionLength)
		{
			var formattedStatements = statements.Where(s => s.SourcePosition.IndexStart <= selectionStart + selectionLength && s.SourcePosition.IndexEnd >= selectionStart)
				.OrderBy(s => s.SourcePosition.IndexStart)
				.ToArray();

			if (formattedStatements.Length == 0)
				return new TextSegment[0];

			var stringBuilder = new StringBuilder();
			var skipSpaceBeforeToken = false;

			foreach (var statement in formattedStatements)
			{
				if (statement.RootNode == null)
					continue;

				//var queryLevel = startNode.GetNestedQueryLevel();
				string indentation = null;

				FormatNode(statement.RootNode, null, stringBuilder, ref skipSpaceBeforeToken, ref indentation);
			}

			var indextStart = formattedStatements[0].RootNode.FirstTerminalNode.SourcePosition.IndexStart;
			
			return new [] { new TextSegment
			                {
				                Text = stringBuilder.ToString(),
								IndextStart = indextStart,
								Length = formattedStatements[formattedStatements.Length - 1].RootNode.LastTerminalNode.SourcePosition.IndexEnd - indextStart + 1,
			                } };
		}

		private void FormatNode(StatementDescriptionNode startNode, StatementDescriptionNode endNode, StringBuilder stringBuilder, ref bool skipSpaceBeforeToken, ref string indentation)
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
				if (breakSettingsFound && (!String.IsNullOrEmpty(lineBreakSettings.ChildNodeId) || childIndex == 0))
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

					breakBefore = (lineBreakSettings.BreakPosition & LineBreakPosition.BeforeNode) != LineBreakPosition.None;
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
						(!OracleGrammarDescription.SingleCharacterTerminals.Contains(childNode.Id) || OracleGrammarDescription.MathTerminals.Contains(childNode.Id) || childNode.Id == Terminals.Colon) &&
						!skipSpaceBeforeToken && stringBuilder.Length > 0)
					{
						stringBuilder.Append(' ');
					}
					
					stringBuilder.Append(childNode.Token.Value);

					skipSpaceBeforeToken = SkipSpaceTerminalIds.Contains(childNode.Id) || childNode.Id.In(Terminals.Dot, Terminals.Colon);
				}
				else
				{
					FormatNode(childNode, null, stringBuilder, ref skipSpaceBeforeToken, ref indentation);
				}

				if (breakSettingsFound && (!String.IsNullOrEmpty(lineBreakSettings.ChildNodeId) || childIndex + 1 == startNode.ChildNodes.Count))
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

					if ((lineBreakSettings.BreakPosition & LineBreakPosition.AfterNode) != LineBreakPosition.None)
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
