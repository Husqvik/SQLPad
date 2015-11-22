using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleMultiNodeEditorDataProvider : IMultiNodeEditorDataProvider
	{
		public MultiNodeEditorData GetMultiNodeEditorData(ActionExecutionContext executionContext)
		{
			var multiNodeData = new MultiNodeEditorData { SynchronizedSegments = new SourcePosition[0] };
			var terminal = executionContext.DocumentRepository.Statements.GetTerminalAtPosition(executionContext.CaretOffset, n => !String.Equals(n.Id, Terminals.Comma) && !String.Equals(n.Id, Terminals.Dot) && !String.Equals(n.Id, Terminals.RightParenthesis));
			if (executionContext.SelectionLength > 0 || terminal?.Id.IsIdentifierOrAlias() != true)
			{
				return multiNodeData;
			}

			multiNodeData.CurrentNode = terminal;

			var semanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[terminal.Statement].SemanticModel;

			OracleColumnReference columnReference;
			OracleQueryBlock cteQueryBlock;
			switch (terminal.Id)
			{
				case Terminals.ObjectAlias:
					var objectReference = semanticModel.AllReferenceContainers
						.SelectMany(c => c.ObjectReferences)
						.SingleOrDefault(o => o.AliasNode == terminal);

					var synchronizedSegments = new List<SourcePosition>();
					cteQueryBlock = semanticModel.QueryBlocks.SingleOrDefault(qb => qb.AliasNode == terminal);
					if (objectReference == null && cteQueryBlock != null)
					{
						synchronizedSegments.AddRange(GetDataObjectReferences(cteQueryBlock).Select(o => o.ObjectNode.SourcePosition));
					}

					var objectQualifiedColumnSynchronizedSegments = semanticModel.AllReferenceContainers
						.SelectMany(c => c.ColumnReferences)
						.Where(c => c.ObjectNode != null && c.ValidObjectReference != null && (c.ValidObjectReference == objectReference || (c.ValidObjectReference.QueryBlocks.Count == 1 && c.ValidObjectReference.QueryBlocks.First() == cteQueryBlock)))
						.Concat(GetCommonTableExpressionObjectPrefixedColumnReferences(cteQueryBlock))
						.Select(c => c.ObjectNode.SourcePosition);

					synchronizedSegments.AddRange(objectQualifiedColumnSynchronizedSegments);
					multiNodeData.SynchronizedSegments = synchronizedSegments.AsReadOnly();

					break;

				case Terminals.ObjectIdentifier:
					var editNodes = new List<SourcePosition>();

					columnReference = semanticModel.GetReference<OracleColumnReference>(terminal);

					var objectReferences = new HashSet<OracleDataObjectReference>();
					var dataObjectReferenceCandidate = columnReference?.ValidObjectReference as OracleDataObjectReference;
					if (dataObjectReferenceCandidate != null)
					{
						objectReferences.Add(dataObjectReferenceCandidate);
					}
					else
					{
						dataObjectReferenceCandidate = semanticModel.GetReference<OracleDataObjectReference>(terminal);
					}

					if (dataObjectReferenceCandidate?.Type == ReferenceType.CommonTableExpression)
					{
						objectReferences.Add(dataObjectReferenceCandidate);

						if (dataObjectReferenceCandidate.QueryBlocks.Count == 1)
						{
							cteQueryBlock = dataObjectReferenceCandidate.QueryBlocks.First();
							var cteColumnReferences = GetCommonTableExpressionObjectPrefixedColumnReferences(cteQueryBlock);
							editNodes.AddRange(cteColumnReferences.Select(c => c.ObjectNode.SourcePosition));
							editNodes.AddRange(GetDataObjectReferences(cteQueryBlock).Where(o => o != dataObjectReferenceCandidate).Select(o => o.ObjectNode.SourcePosition));

							StatementGrammarNode cteAliasNode;
							if (cteQueryBlock.AliasNode == null)
							{
								cteQueryBlock = cteQueryBlock.AllPrecedingConcatenatedQueryBlocks.Last();
								cteAliasNode = cteQueryBlock.AliasNode;
								var referencesToCte = semanticModel.AllReferenceContainers.SelectMany(c => c.ObjectReferences).Where(o => o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == cteQueryBlock);
								objectReferences.AddRange(referencesToCte);
							}
							else
							{
								cteAliasNode = cteQueryBlock.AliasNode;
							}

							editNodes.Add(cteAliasNode.SourcePosition);
						}
					}

					foreach (var dataObjectReference in objectReferences)
					{
						if (dataObjectReference.AliasNode != null)
						{
							editNodes.Add(dataObjectReference.AliasNode.SourcePosition);
						}
						else if (dataObjectReference.Type == ReferenceType.CommonTableExpression && dataObjectReference.ObjectNode != terminal)
						{
							editNodes.Add(dataObjectReference.ObjectNode.SourcePosition);
						}

						var editNodesSource = semanticModel.AllReferenceContainers
							.SelectMany(c => c.ColumnReferences)
							.Where(r => r.ObjectNode != null && r != columnReference && r.ValidObjectReference == dataObjectReference)
							.Select(r => r.ObjectNode.SourcePosition);

						editNodes.AddRange(editNodesSource);
					}

					multiNodeData.SynchronizedSegments = editNodes.AsReadOnly();
					break;

				case Terminals.BindVariableIdentifier:
					var bindVariable = FindUsagesCommand.GetBindVariable(semanticModel, terminal.Token.Value);
					multiNodeData.SynchronizedSegments = bindVariable.Nodes.Where(n => n != terminal).Select(t => t.SourcePosition).ToArray();
					break;

				case Terminals.ColumnAlias:
					var selectListColumn = semanticModel.AllReferenceContainers
						.OfType<OracleSelectListColumn>()
						.SingleOrDefault(c => c.AliasNode == terminal);

					if (selectListColumn != null)
					{
						multiNodeData.SynchronizedSegments = FindUsagesCommand.GetParentQueryBlockReferences(selectListColumn)
							.TakeWhile(t => String.Equals(t.Token.Value.ToQuotedIdentifier(), selectListColumn.NormalizedName))
							.Select(t => t.SourcePosition)
							.ToArray();
					}
					else
					{
						goto case Terminals.Identifier;
					}

					break;

				case Terminals.Identifier:
					multiNodeData.SynchronizedSegments = FindUsagesCommand.GetColumnUsages(semanticModel, terminal, true)
						.Where(t => t != terminal)
						.Select(t => t.SourcePosition)
						.ToArray();
					break;
			}

			return multiNodeData;
		}

		private IEnumerable<OracleReference> GetCommonTableExpressionObjectPrefixedColumnReferences(OracleQueryBlock cteQueryBlock)
		{
			if (cteQueryBlock == null)
			{
				return Enumerable.Empty<OracleReference>();
			}

			var sourceColumnReferences = cteQueryBlock.AllColumnReferences;
			if (cteQueryBlock.IsRecursive)
			{
				sourceColumnReferences = sourceColumnReferences.Concat(cteQueryBlock.AllFollowingConcatenatedQueryBlocks.SelectMany(qb => qb.AllColumnReferences));
			}

			return sourceColumnReferences
				.Where(c => c.ObjectNode != null && c.ValidObjectReference != null &&
				            c.OwnerNode == null && String.Equals(c.ValidObjectReference.FullyQualifiedObjectName.NormalizedName, cteQueryBlock.NormalizedAlias));
		}

		private IEnumerable<OracleDataObjectReference> GetDataObjectReferences(OracleQueryBlock cteQueryBlock)
		{
			var dataObjectReferences = cteQueryBlock.SemanticModel.AllReferenceContainers
				.SelectMany(c => c.ObjectReferences)
				.Where(o => o.QueryBlocks.Count == 1 && o.QueryBlocks.First() == cteQueryBlock);

			if (cteQueryBlock.IsRecursive)
			{
				var recursiveDataObjectReferences =
					cteQueryBlock.AllFollowingConcatenatedQueryBlocks
						.SelectMany(c => c.ObjectReferences)
						.Where(o => o.OwnerNode == null && o.ObjectNode != null && String.Equals(o.ObjectNode.Token.Value.ToQuotedIdentifier(), cteQueryBlock.NormalizedAlias));

				dataObjectReferences = dataObjectReferences.Concat(recursiveDataObjectReferences);
			}

			return dataObjectReferences;
		}
	}
}
