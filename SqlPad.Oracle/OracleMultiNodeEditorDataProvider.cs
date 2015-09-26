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
            switch (terminal.Id)
			{
				case Terminals.ObjectAlias:
					var objectReference = semanticModel.AllReferenceContainers
						.SelectMany(c => c.ObjectReferences)
						.Single(o => o.AliasNode == terminal);

					multiNodeData.SynchronizedSegments = semanticModel.AllReferenceContainers
						.SelectMany(c => c.ColumnReferences)
						.Where(c => c.ObjectNode != null && c.ValidObjectReference == objectReference)
						.Select(c => c.ObjectNode.SourcePosition)
						.ToArray();

					break;

				case Terminals.ObjectIdentifier:
					columnReference = semanticModel.GetReference<OracleColumnReference>(terminal);
					var dataObjectReference = columnReference.ValidObjectReference as OracleDataObjectReference;
					var objectAliasNode = dataObjectReference?.AliasNode;
					if (objectAliasNode != null)
					{
						var editNodes = new List<SourcePosition> { objectAliasNode.SourcePosition };
						var editNodesSource = semanticModel.AllReferenceContainers
							.SelectMany(c => c.ColumnReferences)
							.Where(r => r.ObjectNode != null && r != columnReference && r.ValidObjectReference == dataObjectReference)
							.Select(r => r.ObjectNode.SourcePosition);

						editNodes.AddRange(editNodesSource);
						multiNodeData.SynchronizedSegments = editNodes;
					}

					break;

				case Terminals.BindVariableIdentifier:
					var bindVariable = FindUsagesCommand.GetBindVariable(semanticModel, terminal.Token.Value);
					multiNodeData.SynchronizedSegments = bindVariable.Nodes.Where(n => n != terminal).Select(t => t.SourcePosition).ToArray();
					break;

				case Terminals.ColumnAlias:
					var selectListColumn = semanticModel.AllReferenceContainers
						.OfType<OracleSelectListColumn>()
						.Single(c => c.AliasNode == terminal);

					multiNodeData.SynchronizedSegments = FindUsagesCommand.GetParentQueryBlockReferences(selectListColumn)
						.TakeWhile(t => String.Equals(t.Token.Value.ToQuotedIdentifier(), selectListColumn.NormalizedName))
						.Select(t => t.SourcePosition)
						.ToArray();
					break;

				case Terminals.Identifier:
					columnReference = semanticModel.GetColumnReference(terminal);
					break;
			}

			return multiNodeData;
		}
	}
}