using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.ToolTips
{
	public class OracleToolTipProvider : IToolTipProvider
	{
		private static readonly HashSet<string> TerminalCandidatesToSkip =
			new HashSet<string>
			{
				Terminals.PlSqlCompilationParameter,
				Terminals.CharacterCode,
				Terminals.Avg,
				Terminals.ConnectByRoot,
				Terminals.Count,
				Terminals.CumulativeDistribution,
				Terminals.DenseRank,
				Terminals.FirstValue,
				Terminals.Lag,
				Terminals.LastValue,
				Terminals.Lead,
				Terminals.Level,
				Terminals.ListAggregation,
				Terminals.NegationOrNull,
				Terminals.Max,
				Terminals.Min,
				Terminals.PercentileContinuousDistribution,
				Terminals.PercentileDiscreteDistribution,
				Terminals.Rank,
				Terminals.RowIdPseudocolumn,
				Terminals.StandardDeviation,
				Terminals.Sum,
				Terminals.Time,
				Terminals.Trim,
				Terminals.User,
				Terminals.Variance
			};

		public IToolTip GetToolTip(SqlDocumentRepository sqlDocumentRepository, int cursorPosition)
		{
			if (sqlDocumentRepository == null)
			{
				throw new ArgumentNullException(nameof(sqlDocumentRepository));
			}

			var node = sqlDocumentRepository.Statements.GetNodeAtPosition(cursorPosition);
			if (node == null)
			{
				var statement = sqlDocumentRepository.Statements.GetStatementAtPosition(cursorPosition);
				if (statement?.FirstUnparsedToken == null)
				{
					return null;
				}

				if (cursorPosition < statement.FirstUnparsedToken.Index || cursorPosition > statement.FirstUnparsedToken.Index + statement.FirstUnparsedToken.Value.Length)
				{
					return null;
				}

				return BuildExpectedTokenListToolTip(statement.LastTerminalNode);
			}

			var tip = node.Type == NodeType.Terminal && !node.Id.IsIdentifier() ? node.Id : null;

			var validationModel = (OracleValidationModel)sqlDocumentRepository.ValidationModels[node.Statement];

			var nodeSemanticError = validationModel.SemanticErrors
				.Concat(validationModel.Suggestions)
				.FirstOrDefault(v => node.HasAncestor(v.Node, true));

			if (nodeSemanticError != null)
			{
				tip = nodeSemanticError.ToolTipText;
			}
			else
			{
				var semanticModel = validationModel.SemanticModel;
				var queryBlock = semanticModel.GetQueryBlock(node);

				var toolTipBuilderVisitor = new OracleToolTipBuilderVisitor(node);

				switch (node.Id)
				{
					case Terminals.Asterisk:
						return BuildAsteriskToolTip(queryBlock, node);
					case Terminals.Min:
					case Terminals.Max:
					case Terminals.Sum:
					case Terminals.Avg:
					case Terminals.FirstValue:
					case Terminals.Count:
					case Terminals.Cast:
					case Terminals.Trim:
					case Terminals.CharacterCode:
					case Terminals.Variance:
					case Terminals.StandardDeviation:
					case Terminals.LastValue:
					case Terminals.Lead:
					case Terminals.Lag:
					case Terminals.ListAggregation:
					case Terminals.CumulativeDistribution:
					case Terminals.Rank:
					case Terminals.DenseRank:
					case Terminals.PercentileDiscreteDistribution:
					case Terminals.PercentileContinuousDistribution:
					case Terminals.NegationOrNull:
					case Terminals.RowIdPseudocolumn:
					case Terminals.RowNumberPseudocolumn:
					case Terminals.User:
					case Terminals.Level:
					case Terminals.Extract:
					case Terminals.JsonQuery:
					case Terminals.JsonExists:
					case Terminals.JsonValue:
					case Terminals.XmlCast:
					case Terminals.XmlElement:
					case Terminals.XmlSerialize:
					case Terminals.XmlParse:
					case Terminals.XmlQuery:
					case Terminals.XmlRoot:
					case Terminals.XmlForest:
					case Terminals.ToNumber:
					case Terminals.ToDate:
					case Terminals.ToTimestamp:
					case Terminals.ToTimestampWithTimeZone:
					case Terminals.ToBinaryFloat:
					case Terminals.ToBinaryDouble:
					case Terminals.ValidateConversion:
					case Terminals.PlSqlIdentifier:
					case Terminals.ExceptionIdentifier:
					case Terminals.CursorIdentifier:
					case Terminals.DataTypeIdentifier:
					case Terminals.ObjectIdentifier:
					case Terminals.SchemaIdentifier:
					case Terminals.Identifier:
						var reference = semanticModel.GetReference<OracleReference>(node);
						reference?.Accept(toolTipBuilderVisitor);
						if (toolTipBuilderVisitor.ToolTip != null)
						{
							return toolTipBuilderVisitor.ToolTip;
						}

						goto default;
					case Terminals.DatabaseLinkIdentifier:
					case Terminals.Dot:
					case Terminals.AtCharacter:
						var databaseLink = semanticModel.GetReference<OracleReference>(node)?.DatabaseLink;
						if (databaseLink == null)
						{
							return null;
						}

						return
							new ToolTipDatabaseLink(databaseLink)
							{
								ScriptExtractor = semanticModel.DatabaseModel.ObjectScriptExtractor,
							};

					case Terminals.ParameterIdentifier:
						return BuildParameterToolTip(semanticModel, node);

					default:
						var missingTokenLookupTerminal = GetTerminalForCandidateLookup(node, node.LastTerminalNode);
						if (missingTokenLookupTerminal != null)
						{
							return BuildExpectedTokenListToolTip(node);
						}

						break;
				}
			}

			return String.IsNullOrEmpty(tip) ? null : new ToolTipObject { DataContext = tip };
		}

		private static ToolTipObject BuildExpectedTokenListToolTip(StatementGrammarNode node)
		{
			var candidates = OracleSqlParser.Instance.GetTerminalCandidates(node.LastTerminalNode)
				.Where(c => !TerminalCandidatesToSkip.Contains(c.Id))
				.ToArray();

			if (candidates.Length == 0)
			{
				return null;
			}

			var candidateLabels = candidates
				.Select(c => c.ToString())
				.Distinct()
				.OrderBy(l => l);

			var candidateList = String.Join(Environment.NewLine, candidateLabels);
			var stackPanel = new StackPanel();
			stackPanel.Children.Add(new TextBlock(new Bold(new Run("Expecting one of the following: "))));
			stackPanel.Children.Add(
				new TextBox
				{
					Text = candidateList,
					IsReadOnly = true,
					IsReadOnlyCaretVisible = true,
					Background = Brushes.Transparent,
					BorderThickness = new Thickness()
				});

			return new ToolTipObject { Content = stackPanel };
		}

		private static StatementGrammarNode GetTerminalForCandidateLookup(StatementGrammarNode node, StatementGrammarNode sourceTerminal)
		{
			if (sourceTerminal == null)
			{
				return null;
			}

			while (true)
			{
				if (node == null)
				{
					return null;
				}

				if (!node.IsGrammarValid)
				{
					return node.LastTerminalNode == sourceTerminal ? sourceTerminal : null;
				}

				node = node.ParentNode;
			}
		}

		private static IToolTip BuildParameterToolTip(OracleStatementSemanticModel semanticModel, StatementGrammarNode node)
		{
			Func<ProgramParameterReference, bool> parameterFilter = p => p.OptionalIdentifierTerminal == node;
			var programReference = semanticModel.AllReferenceContainers
				.SelectMany(c => c.AllReferences)
				.OfType<OracleProgramReferenceBase>()
				.SingleOrDefault(r => r.ParameterReferences != null && r.ParameterReferences.Any(parameterFilter));

			if (programReference?.Metadata == null)
			{
				return null;
			}

			var parameter = programReference.ParameterReferences.Single(parameterFilter);
			var parameterName = parameter.OptionalIdentifierTerminal.Token.Value.ToQuotedIdentifier();

			if (!programReference.Metadata.NamedParameters.TryGetValue(parameterName, out OracleProgramParameterMetadata parameterMetadata))
			{
				return null;
			}

			var comment = String.Empty;
			if (OracleHelpProvider.PackageProgramDocumentations.TryGetValue(programReference.Metadata.Identifier, out DocumentationPackageSubProgram documentation))
			{
				comment = documentation.Parameters
					?.SingleOrDefault(p => String.Equals(p.Name.ToQuotedIdentifier(), parameterName))
					?.Value;
			}

			return
				new ToolTipView
				{
					DataContext =
						new ObjectDetailsModel
						{
							Title = $"{parameterMetadata.Name.ToSimpleIdentifier()}: {parameterMetadata.FullDataTypeName}",
							Comment = comment
						}
				};
		}

		private static IToolTip BuildAsteriskToolTip(OracleQueryBlock queryBlock, StatementGrammarNode asteriskTerminal)
		{
			var asteriskColumn = queryBlock.AsteriskColumns.SingleOrDefault(c => c.RootNode.LastTerminalNode == asteriskTerminal);
			if (asteriskColumn == null)
			{
				return null;
			}

			var columns = queryBlock.Columns.Where(c => c.AsteriskColumn == asteriskColumn)
				.Select((c, i) =>
				{
					var validObjectReference = c.ColumnReferences[0].ValidObjectReference;
					var nullable = validObjectReference.SchemaObject.GetTargetSchemaObject() is OracleView
						? (bool?)null
						: c.ColumnDescription.Nullable;

					return
						new OracleColumnModel
						{
							Name = String.IsNullOrEmpty(c.ColumnDescription.Name)
								? OracleSelectListColumn.BuildNonAliasedOutputColumnName(c.RootNode.Terminals)
								: c.ColumnDescription.Name,
							FullTypeName = c.ColumnDescription.FullTypeName,
							Nullable = nullable,
							ColumnIndex = i + 1,
							RowSourceName = validObjectReference?.FullyQualifiedObjectName.ToLabel()
						};
				}).ToArray();

			return columns.Length == 0
				? null
				: new ToolTipAsterisk { Columns = columns };
		}
	}
}
