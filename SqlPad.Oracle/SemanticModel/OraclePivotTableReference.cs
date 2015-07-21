using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

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

			var pivotExpressions = PivotClause[OracleGrammarDescription.NonTerminals.PivotAliasedAggregationFunctionList];
			if (pivotExpressions != null)
			{
				foreach (var pivotAggregationFunction in pivotExpressions.GetDescendants(OracleGrammarDescription.NonTerminals.PivotAliasedAggregationFunction))
				{
					var aliasNode = pivotAggregationFunction[OracleGrammarDescription.NonTerminals.ColumnAsAlias, OracleGrammarDescription.Terminals.ColumnAlias];
					_columnNameExtensions.Add(aliasNode == null ? String.Empty : $"_{aliasNode.Token.Value.ToQuotedIdentifier().Trim('"')}");
					aggregateExpressions.Add(pivotAggregationFunction);
				}
			}

			AggregateFunctions = aggregateExpressions.AsReadOnly();
		}

		public override IReadOnlyList<OracleColumn> Columns => _columns ?? ResolvePivotClause();

	    private IReadOnlyList<OracleColumn> ResolvePivotClause()
		{
			var columns = new List<OracleColumn>();

			var columnDefinitions = PivotClause[OracleGrammarDescription.NonTerminals.PivotInClause, OracleGrammarDescription.NonTerminals.PivotExpressionsOrAnyListOrNestedQuery, OracleGrammarDescription.NonTerminals.AliasedExpressionListOrAliasedGroupingExpressionList];
			if (columnDefinitions != null)
			{
				var columnSources = columnDefinitions.GetDescendants(OracleGrammarDescription.NonTerminals.AliasedExpression, OracleGrammarDescription.NonTerminals.ParenthesisEnclosedExpressionListWithMandatoryExpressions).ToArray();
				foreach (var nameExtension in _columnNameExtensions)
				{
					foreach (var columnSource in columnSources)
					{
						var aliasSourceNode = String.Equals(columnSource.Id, OracleGrammarDescription.NonTerminals.AliasedExpression)
							? columnSource
							: columnSource.ParentNode;

						var columnAlias = aliasSourceNode[OracleGrammarDescription.NonTerminals.ColumnAsAlias, OracleGrammarDescription.Terminals.ColumnAlias];

						var columnName = columnAlias == null
							? OracleSelectListColumn.BuildNonAliasedColumnName(aliasSourceNode.Terminals).ToQuotedIdentifier()
							: columnAlias.Token.Value.ToQuotedIdentifier();

						if (!String.IsNullOrEmpty(columnName))
						{
							columnName = columnName.Insert(columnName.Length - 1, nameExtension);
						}

						var column =
							new OracleColumn
							{
								Name = columnName,
								DataType = OracleDataType.Empty,
								Nullable = true
							};

						columns.Add(column);
					}
				}
			}

			var pivotForColumnList = PivotClause[OracleGrammarDescription.NonTerminals.PivotForClause, OracleGrammarDescription.NonTerminals.IdentifierOrParenthesisEnclosedIdentifierList];
			if (pivotForColumnList != null)
			{
				var groupingColumnSource = pivotForColumnList.GetDescendants(OracleGrammarDescription.Terminals.Identifier).Select(i => i.Token.Value.ToQuotedIdentifier());
				var groupingColumns = new HashSet<string>(groupingColumnSource);
				var sourceColumns = SourceReference.Columns
					.Where(c => !groupingColumns.Contains(c.Name))
					.Select(c => c.Clone());

				columns.InsertRange(0, sourceColumns);
			}

			return _columns = columns.AsReadOnly();
		}
	}
}