using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal static class SafeDeleteCommand
	{
		public static readonly CommandExecutionHandler SafeDelete = new CommandExecutionHandler
		{
			Name = "SafeDelete",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.Delete, ModifierKeys.Alt) },
			ExecutionHandler = ExecutionHandlerImplementation
		};

		private static void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			if (executionContext.Statements == null)
				return;

			var currentNode = executionContext.Statements.GetTerminalAtPosition(executionContext.CaretOffset, n => n.Id.IsAlias());
			if (currentNode == null)
				return;

			var semanticModel = new OracleStatementSemanticModel(executionContext.StatementText, (OracleStatement)currentNode.Statement, (OracleDatabaseModelBase)executionContext.DatabaseModel);
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			if (queryBlock == null)
				return;

			switch (currentNode.Id)
			{
				case Terminals.ObjectAlias:
					AddObjectAliasNodesToRemove(executionContext.SegmentsToReplace, queryBlock, currentNode);
					break;
				case Terminals.ColumnAlias:
					break;
			}
		}

		private static void AddObjectAliasNodesToRemove(ICollection<TextSegment> segmentsToReplace, OracleQueryBlock queryBlock, StatementDescriptionNode currentNode)
		{
			var objectReference = queryBlock.ObjectReferences.Single(o => o.AliasNode == currentNode);
			if (objectReference.Type == TableReferenceType.InlineView)
				return;

			foreach (var columnReference in queryBlock.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 1 && c.ColumnNodeObjectReferences.Single() == objectReference &&
				            c.ObjectNode != null))
			{
				segmentsToReplace.Add(new TextSegment
				                      {
										  IndextStart = columnReference.ObjectNode.SourcePosition.IndexStart,
										  Length = columnReference.ObjectNode.SourcePosition.Length,
										  Text = objectReference.ObjectNode.Token.Value
				                      });
			}

			segmentsToReplace.Add(new TextSegment
			{
				IndextStart = currentNode.SourcePosition.IndexStart,
				Length = currentNode.SourcePosition.Length,
				Text = String.Empty
			});
		}
	}
}
