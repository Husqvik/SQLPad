using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OraclePivotTableReference (Columns={Columns.Count})")]
	public class OraclePivotTableReference : OracleDataObjectReference
	{
		private IReadOnlyList<OracleColumn> _columns;

		private readonly List<string> _columnNameExtensions = new List<string>();

		public StatementGrammarNode PivotClause { get; }
		
		public OracleDataObjectReference SourceReference { get; }
		
		public OracleReferenceContainer SourceReferenceContainer { get; }

		public IReadOnlyList<StatementGrammarNode> AggregateFunctions { get; private set; }

		public IReadOnlyList<StatementGrammarNode> UnpivotColumnSelectorValues { get; private set; }

		public OraclePivotTableReference(OracleStatementSemanticModel semanticModel, OracleDataObjectReference sourceReference, StatementGrammarNode pivotClause)
			: base(ReferenceType.PivotTable)
		{
			foreach (var sourceColumn in sourceReference.QueryBlocks.SelectMany(qb => qb.Columns).Where(c => !c.IsAsterisk))
			{
				sourceColumn.RegisterOuterReference();
			}

			PivotClause = pivotClause;
			SourceReference = sourceReference;

			RootNode = sourceReference.RootNode;
			Owner = sourceReference.Owner;
			Owner.ObjectReferences.Remove(sourceReference);
			Owner.ObjectReferences.Add(this);

			SourceReferenceContainer = new OracleReferenceContainer(semanticModel);
			SourceReferenceContainer.ObjectReferences.Add(sourceReference);

			var aggregateExpressions = new List<StatementGrammarNode>();

			var pivotExpressions = PivotClause[NonTerminals.PivotAliasedAggregationFunctionList];
			if (pivotExpressions != null)
			{
				foreach (var pivotAggregationFunction in pivotExpressions.GetDescendants(NonTerminals.PivotAliasedAggregationFunction))
				{
					var aliasNode = pivotAggregationFunction[NonTerminals.ColumnAsAlias, Terminals.ColumnAlias];
					_columnNameExtensions.Add(aliasNode == null ? String.Empty : $"_{aliasNode.Token.Value.ToQuotedIdentifier().Trim('"')}");
					aggregateExpressions.Add(pivotAggregationFunction);
				}
			}

			AggregateFunctions = aggregateExpressions.AsReadOnly();
		}

		public override IReadOnlyList<OracleColumn> Columns => _columns ?? ResolvePivotClauseColumns();

	    private IReadOnlyList<OracleColumn> ResolvePivotClauseColumns()
		{
			var columns = new List<OracleColumn>();

		    var pivotForColumnList = PivotClause[NonTerminals.PivotForClause, NonTerminals.IdentifierOrParenthesisEnclosedIdentifierList];
			if (pivotForColumnList != null)
			{
				var groupingColumnSource = pivotForColumnList.GetDescendants(Terminals.Identifier).Select(i => i.Token.Value.ToQuotedIdentifier());
				var groupingColumns = new HashSet<string>(groupingColumnSource);

				switch (PivotClause.Id)
				{
					case NonTerminals.PivotClause:
						var sourceColumns = SourceReference.Columns
							.Where(c => !groupingColumns.Contains(c.Name))
							.Select(c => c.Clone());

						columns.AddRange(sourceColumns);
						columns.AddRange(ResolvePivotColumns());

						break;

					case NonTerminals.UnpivotClause:
						var unpivotedColumns = new HashSet<string>();
						var unpivotColumnSelectorValues = new List<StatementGrammarNode>();
						var columnTransformations = PivotClause[NonTerminals.UnpivotInClause].GetDescendants(NonTerminals.UnpivotValueToColumnTransformationList);
						foreach (var columnTransformation in columnTransformations)
						{
							unpivotedColumns.AddRange(columnTransformation.GetDescendants(Terminals.Identifier).Select(t => t.Token.Value.ToQuotedIdentifier()));
							unpivotColumnSelectorValues.Add(columnTransformation[NonTerminals.UnpivotValueSelector, NonTerminals.StringOrNumberLiteralOrParenthesisEnclosedStringOrIntegerLiteralList]);
						}

						var unpivotColumnDataTypes = OracleDataType.FromUnpivotColumnSelectorValues(unpivotColumnSelectorValues);

						UnpivotColumnSelectorValues = unpivotColumnSelectorValues.AsReadOnly();

						columns.AddRange(SourceReference.Columns
							.Where(c => !unpivotedColumns.Contains(c.Name))
							.Select(c => c.Clone()));

						columns.AddRange(groupingColumns.Select(
							(c, i) =>
								new OracleColumn
								{
									Name = c,
									DataType = groupingColumns.Count == unpivotColumnDataTypes?.Count
										? unpivotColumnDataTypes[i]
										: OracleDataType.Empty
								}));

						var unpivotColumns = PivotClause[NonTerminals.IdentifierOrParenthesisEnclosedIdentifierList].GetDescendants(Terminals.Identifier);
						columns.AddRange(unpivotColumns.Select(i => new OracleColumn { Name = i.Token.Value.ToQuotedIdentifier(), DataType = OracleDataType.Empty }));

						break;
				}
			}

			return _columns = columns.AsReadOnly();
		}

		private IEnumerable<OracleColumn> ResolvePivotColumns()
		{
			var columnDefinitions = PivotClause[NonTerminals.PivotInClause, NonTerminals.PivotExpressionsOrAnyListOrNestedQuery, NonTerminals.AliasedExpressionListOrAliasedGroupingExpressionList];
			if (columnDefinitions == null)
			{
				yield break;
			}

			var columnSources = columnDefinitions.GetDescendants(NonTerminals.AliasedExpression, NonTerminals.ParenthesisEnclosedExpressionListWithMandatoryExpressions).ToArray();
			foreach (var nameExtension in _columnNameExtensions)
			{
				foreach (var columnSource in columnSources)
				{
					var aliasSourceNode = String.Equals(columnSource.Id, NonTerminals.AliasedExpression)
						? columnSource
						: columnSource.ParentNode;

					var columnAlias = aliasSourceNode[NonTerminals.ColumnAsAlias, Terminals.ColumnAlias];

					var columnName = columnAlias == null
						? OracleSelectListColumn.BuildNonAliasedColumnName(aliasSourceNode.Terminals).ToQuotedIdentifier()
						: columnAlias.Token.Value.ToQuotedIdentifier();

					if (!String.IsNullOrEmpty(columnName))
					{
						columnName = columnName.Insert(columnName.Length - 1, nameExtension);
					}

					yield return
						new OracleColumn
						{
							Name = columnName,
							DataType = OracleDataType.Empty,
							Nullable = true
						};
				}
			}
		}
	}
}
