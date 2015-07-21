﻿using System;
using System.Linq;
using SqlPad.Commands;
using SqlPad.Oracle.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleNavigationService : INavigationService
	{
		public int? NavigateToQueryBlockRoot(CommandExecutionContext executionContext)
		{
			var documentRepository = executionContext.DocumentRepository;
			var statement = documentRepository.Statements.GetStatementAtPosition(executionContext.CaretOffset);
			if (statement == null)
				return null;

			var semanticModel = (OracleStatementSemanticModel)documentRepository.ValidationModels[statement].SemanticModel;
			var queryBlock = semanticModel.GetQueryBlock(executionContext.CaretOffset);
			return queryBlock?.RootNode.SourcePosition.IndexStart;
		}

		public int? NavigateToDefinition(CommandExecutionContext executionContext)
		{
			var documentRepository = executionContext.DocumentRepository;
			var terminal = documentRepository.Statements.GetTerminalAtPosition(executionContext.CaretOffset, n => !n.Id.IsZeroOffsetTerminalId());
			if (terminal == null || !terminal.Id.In(Terminals.Identifier, Terminals.ObjectIdentifier))
			{
				return null;
			}

			var semanticModel = (OracleStatementSemanticModel)documentRepository.ValidationModels[terminal.Statement].SemanticModel;
			var queryBlock = semanticModel.GetQueryBlock(executionContext.CaretOffset);

			if (queryBlock == null)
			{
				return null;
			}

			switch (terminal.Id)
			{
				case Terminals.Identifier:
					return NavigateToColumnDefinition(queryBlock, terminal);
				case Terminals.ObjectIdentifier:
					return NavigateToObjectDefinition(queryBlock, terminal);
				default:
					throw new NotSupportedException($"Terminal '{terminal.Id}' is not supported. ");
			}
		}

		public void FindUsages(CommandExecutionContext executionContext)
		{
			FindUsagesCommand.FindUsages.ExecutionHandler(executionContext);
		}

		public void DisplayBindVariableUsages(CommandExecutionContext executionContext)
		{
			var terminal = executionContext.DocumentRepository.Statements.GetTerminalAtPosition(executionContext.CaretOffset);
			if (terminal == null || !String.Equals(terminal.Id, Terminals.BindVariableIdentifier))
			{
				return;
			}
			
			var nodes = executionContext.DocumentRepository.ValidationModels.Values
				.Select(v => FindUsagesCommand.GetBindVariable((OracleStatementSemanticModel)v.SemanticModel, terminal.Token.Value))
				.Where(v => v != null)
				.SelectMany(v => v.Nodes);

			executionContext.SegmentsToReplace.AddRange(
				nodes.Select(n =>
					new TextSegment
					{
						IndextStart = n.SourcePosition.IndexStart,
						Length = n.SourcePosition.Length,
						DisplayOptions = DisplayOptions.Usage
					}));
		}

		private static int? NavigateToObjectDefinition(OracleQueryBlock queryBlock, StatementGrammarNode terminal)
		{
			var column = queryBlock.AllColumnReferences.SingleOrDefault(c => c.ObjectNode == terminal);

			var dataObjectReference = column?.ValidObjectReference as OracleDataObjectReference;
			
			if (dataObjectReference == null)
				return null;

			var destinationNode = dataObjectReference.AliasNode ?? GetObjectNode(dataObjectReference);
			return destinationNode.SourcePosition.IndexStart;
		}

		private static StatementGrammarNode GetObjectNode(OracleObjectWithColumnsReference dataObjectReference)
		{
			return dataObjectReference.Type == ReferenceType.CommonTableExpression && dataObjectReference.QueryBlocks.Count == 1
				? dataObjectReference.QueryBlocks.First().AliasNode
				: dataObjectReference.ObjectNode;
		}

		private static int? NavigateToColumnDefinition(OracleQueryBlock queryBlock, StatementGrammarNode terminal)
		{
			var column = queryBlock.AllColumnReferences.SingleOrDefault(c => c.ColumnNode == terminal);
			if (column?.ValidObjectReference == null || column.ValidObjectReference.QueryBlocks.Count != 1)
				return null;

			var childQueryBlock = column.ValidObjectReference.QueryBlocks.Single();
			return NavigateThroughQueryBlock(childQueryBlock, column.NormalizedName);
		}

		private static int? NavigateThroughQueryBlock(OracleQueryBlock queryBlock, string normalizedColumnName)
		{
			var selectListColumn = queryBlock.Columns.SingleOrDefault(c => c.NormalizedName == normalizedColumnName);
			if (selectListColumn == null)
				return null;

			if (!selectListColumn.IsDirectReference)
			{
				return selectListColumn.AliasNode?.SourcePosition.IndexStart ?? selectListColumn.RootNode.SourcePosition.IndexStart;
			}

			var columnReference = selectListColumn.ColumnReferences.Single();
			var objectReference = columnReference.ValidObjectReference;
			var isAliasedDirectReference = selectListColumn.AliasNode != columnReference.ColumnNode && !selectListColumn.IsAsterisk;
			if (isAliasedDirectReference || objectReference == null || objectReference.QueryBlocks.Count != 1)
			{
				var destinationNode = selectListColumn.HasExplicitDefinition
					? selectListColumn.AliasNode
					: columnReference.ColumnNode;
				
				return destinationNode.SourcePosition.IndexStart;
			}

			return NavigateThroughQueryBlock(objectReference.QueryBlocks.Single(), normalizedColumnName);
		}
	}
}
