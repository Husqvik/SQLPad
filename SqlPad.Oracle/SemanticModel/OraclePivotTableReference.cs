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
		private readonly List<string> _columnNameExtensions = new List<string>();
		private bool? _areUnpivotColumnSourceDataTypesMatched;

		public StatementGrammarNode PivotClause { get; }
		
		public OracleDataObjectReference SourceReference { get; }
		
		public OracleReferenceContainer SourceReferenceContainer { get; }

		public IReadOnlyList<StatementGrammarNode> AggregateFunctions { get; private set; }

		public IReadOnlyList<StatementGrammarNode> UnpivotColumnSelectorValues { get; private set; }

		public IReadOnlyList<StatementGrammarNode> UnpivotColumnSources { get; private set; }

		public bool? AreUnpivotColumnSelectorValuesValid { get; private set; }

		public bool? AreUnpivotColumnSourceDataTypesMatched => _areUnpivotColumnSourceDataTypesMatched ?? (_areUnpivotColumnSourceDataTypesMatched = ResolveUnpivotColumnSourceDataTypesMatching());

		public override IEnumerable<OracleDataObjectReference> IncludeInnerReferences => base.IncludeInnerReferences.Concat(Enumerable.Repeat(SourceReference, 1));

		public IReadOnlyList<OracleSelectListColumn> PivotColumns { get; private set; }

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
			var innerTableReferenceRootNode = sourceReference.RootNode.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBlock, NonTerminals.InnerTableReference, NonTerminals.InnerSpecialTableReference).Last();
			sourceReference.RootNode = innerTableReferenceRootNode;

			Container = Owner = sourceReference.Owner;
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

		private bool? ResolveUnpivotColumnSourceDataTypesMatching()
		{
			if (UnpivotColumnSources == null)
			{
				return null;
			}

			var columnNodeReferences = SourceReferenceContainer.ColumnReferences.ToDictionary(c => c.ColumnNode);
			var referenceDataTypes = new List<OracleDataType>();
			var sourceIndex = 0;
			foreach (var unpivotColumnSource in UnpivotColumnSources)
			{
				var identifiers = unpivotColumnSource.GetDescendants(Terminals.Identifier);
				var typeIndex = 0;
				foreach (var identifier in identifiers)
				{
					OracleColumnReference columnReference;
					if (!columnNodeReferences.TryGetValue(identifier, out columnReference) || columnReference.ColumnDescription == null)
					{
						return null;
					}

					var dataType = columnReference.ColumnDescription.DataType;
					if (sourceIndex == 0)
					{
						referenceDataTypes.Add(dataType);
					}
					else
					{
						if (typeIndex >= referenceDataTypes.Count)
						{
							return false;
						}

						var referenceType = referenceDataTypes[typeIndex];
						if (String.IsNullOrEmpty(referenceType.FullyQualifiedName.Name))
						{
							referenceDataTypes[typeIndex] = dataType;
						}
						else if (!String.IsNullOrEmpty(dataType.FullyQualifiedName.Name) && referenceType.FullyQualifiedName != dataType.FullyQualifiedName)
						{
							return false;
						}
					}

					typeIndex++;
				}

				if (typeIndex != referenceDataTypes.Count)
				{
					return false;
				}

				sourceIndex++;
			}

			return true;
		}

		protected override IReadOnlyList<OracleColumn> BuildColumns()
		{
			var columns = new List<OracleColumn>();
			var pivotColumns = new List<OracleSelectListColumn>();

			var pivotForColumnList = PivotClause[NonTerminals.PivotForClause, NonTerminals.IdentifierOrParenthesisEnclosedIdentifierList];
			if (pivotForColumnList != null)
			{
				var groupingColumns = pivotForColumnList
					.GetDescendants(Terminals.Identifier)
					.Select(i => i.Token.Value.ToQuotedIdentifier())
					.ToHashSet();

				switch (PivotClause.Id)
				{
					case NonTerminals.PivotClause:
						var sourceColumns = SourceReference.Columns
							.Where(c => !groupingColumns.Contains(c.Name))
							.Select(c => c.Clone());

						columns.AddRange(sourceColumns);

						var withXmlTransformation = PivotClause[Terminals.Xml] != null;
						if (withXmlTransformation)
						{
							var xmlColumnName = String.Join("_", groupingColumns.Select(c => c.Trim('"')));
							if (xmlColumnName.Length > 26)
							{
								xmlColumnName = xmlColumnName.Substring(0, 26);
							}

							var xmlColumn =
								new OracleColumn
								{
									Name = $"\"{xmlColumnName}_XML\"",
									DataType = OracleDataType.XmlType,
									Nullable = true
								};

							columns.Add(xmlColumn);
						}
						else
						{
							pivotColumns.AddRange(ResolvePivotColumns());
							columns.AddRange(pivotColumns.Select(c => c.ColumnDescription));
						}

						break;

					case NonTerminals.UnpivotClause:
						var unpivotColumnSources = new List<StatementGrammarNode>();
						var unpivotedColumns = new HashSet<string>();
						var unpivotColumnSelectorValues = new List<StatementGrammarNode>();
						var columnTransformations = PivotClause[NonTerminals.UnpivotInClause].GetDescendants(NonTerminals.UnpivotValueToColumnTransformationList);
						var groupingColumnsNullable = false;
						foreach (var columnTransformation in columnTransformations)
						{
							unpivotedColumns.AddRange(columnTransformation.GetDescendants(Terminals.Identifier).Select(t => t.Token.Value.ToQuotedIdentifier()));
							var columnSelectorValue = columnTransformation[NonTerminals.UnpivotValueSelector, NonTerminals.NullOrStringOrNumberLiteralOrParenthesisEnclosedStringOrIntegerLiteralList];
							if (columnSelectorValue != null)
							{
								unpivotColumnSelectorValues.Add(columnSelectorValue);
								groupingColumnsNullable |= columnSelectorValue.TerminalCount == 1 || String.Equals(columnSelectorValue.FirstTerminalNode.Id, Terminals.Null);
							}

							unpivotColumnSources.AddIfNotNull(columnTransformation[NonTerminals.IdentifierOrParenthesisEnclosedIdentifierList]);
						}

						var unpivotColumnDataTypes = OracleDataType.FromUnpivotColumnSelectorValues(unpivotColumnSelectorValues);
						AreUnpivotColumnSelectorValuesValid = unpivotColumnDataTypes != null;

						UnpivotColumnSelectorValues = unpivotColumnSelectorValues.AsReadOnly();
						UnpivotColumnSources = unpivotColumnSources.AsReadOnly();

						columns.AddRange(SourceReference.Columns
							.Where(c => !unpivotedColumns.Contains(c.Name))
							.Select(c => c.Clone()));

						columns.AddRange(groupingColumns.Select(
							(c, i) =>
								new OracleColumn
								{
									Name = c,
									Nullable = groupingColumnsNullable,
									DataType = groupingColumns.Count == unpivotColumnDataTypes?.Count
										? unpivotColumnDataTypes[i]
										: OracleDataType.Empty
								}));

						var unpivotColumnsNullable = PivotClause[NonTerminals.UnpivotNullsClause, NonTerminals.IncludeOrExclude, Terminals.Include] != null;
						var unpivotColumns = PivotClause[NonTerminals.IdentifierOrParenthesisEnclosedIdentifierList].GetDescendants(Terminals.Identifier);
						columns.AddRange(
							unpivotColumns.Select(
								i => new OracleColumn
								{
									Name = i.Token.Value.ToQuotedIdentifier(),
									DataType = OracleDataType.Empty,
									Nullable = unpivotColumnsNullable
								}));

						break;
				}
			}

			PivotColumns = pivotColumns.AsReadOnly();

			return columns.AsReadOnly();
		}

		private IEnumerable<OracleSelectListColumn> ResolvePivotColumns()
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
						? OracleSelectListColumn.BuildNonAliasedColumnName(aliasSourceNode.Terminals)
						: columnAlias.Token.Value.ToQuotedIdentifier();

					if (!String.IsNullOrEmpty(columnName))
					{
						columnName = columnName.Insert(columnName.Length - 1, nameExtension);
					}

					yield return
						new OracleSelectListColumn(SourceReferenceContainer.SemanticModel, null)
						{
							RootNode = aliasSourceNode,
							AliasNode = columnAlias,
							Owner = Owner,
							ColumnDescription =
								new OracleColumn
								{
									Name = columnName,
									DataType = OracleDataType.Empty,
									Nullable = true
								}
						};
				}
			}
		}
	}
}
