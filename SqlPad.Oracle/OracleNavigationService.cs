using System;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleNavigationService : INavigationService
	{
		public int? NavigateToQueryBlockRoot(SqlDocumentRepository documentRepository, int currentPosition)
		{
			var statement = documentRepository.Statements.GetStatementAtPosition(currentPosition);
			if (statement == null)
				return null;

			var semanticModel = (OracleStatementSemanticModel)documentRepository.ValidationModels[statement].SemanticModel;
			var queryBlock = semanticModel.GetQueryBlock(currentPosition);
			return queryBlock == null ? null : (int?)queryBlock.RootNode.SourcePosition.IndexStart;
		}

		public int? NavigateToDefinition(SqlDocumentRepository documentRepository, int currentPosition)
		{
			var terminal = documentRepository.Statements.GetTerminalAtPosition(currentPosition, n => !n.Id.IsZeroOffsetTerminalId());
			if (terminal == null || !terminal.Id.In(Terminals.Identifier, Terminals.ObjectIdentifier))
				return null;

			var semanticModel = (OracleStatementSemanticModel)documentRepository.ValidationModels[terminal.Statement].SemanticModel;
			var queryBlock = semanticModel.GetQueryBlock(currentPosition);

			switch (terminal.Id)
			{
				case Terminals.Identifier:
					return NavigateToColumnDefinition(queryBlock, terminal);
				case Terminals.ObjectIdentifier:
					return NavigateToObjectDefinition(queryBlock, terminal);
				default:
					throw new NotSupportedException(String.Format("Terminal '{0}' is not supported. ", terminal.Id));
			}
		}

		private static int? NavigateToObjectDefinition(OracleQueryBlock queryBlock, StatementGrammarNode terminal)
		{
			var column = queryBlock.AllColumnReferences.SingleOrDefault(c => c.ObjectNode == terminal);

			var dataObjectReference = column == null
				? null
				: column.ValidObjectReference as OracleDataObjectReference;
			
			if (dataObjectReference == null)
				return null;

			var destinationNode = dataObjectReference.AliasNode ?? dataObjectReference.ObjectNode;
			return destinationNode.SourcePosition.IndexStart;
		}

		private static int? NavigateToColumnDefinition(OracleQueryBlock queryBlock, StatementGrammarNode terminal)
		{
			var column = queryBlock.AllColumnReferences.SingleOrDefault(c => c.ColumnNode == terminal);
			if (column == null || column.ValidObjectReference == null || column.ValidObjectReference.QueryBlocks.Count != 1)
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
				return selectListColumn.AliasNode.SourcePosition.IndexStart;
			}

			var columnReference = selectListColumn.ColumnReferences.Single();
			var objectReference = columnReference.ValidObjectReference;
			var isAliasedDirectReference = selectListColumn.AliasNode != columnReference.ColumnNode && !selectListColumn.IsAsterisk;
			if (isAliasedDirectReference || objectReference == null || objectReference.QueryBlocks.Count != 1)
			{
				var destinationNode = selectListColumn.ExplicitDefinition
					? selectListColumn.AliasNode
					: columnReference.ColumnNode;
				
				return destinationNode.SourcePosition.IndexStart;
			}

			return NavigateThroughQueryBlock(objectReference.QueryBlocks.Single(), normalizedColumnName);
		}
	}
}
