using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
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
			if (executionContext.DocumentRepository == null)
				return;

			var currentNode = executionContext.DocumentRepository.Statements.GetTerminalAtPosition(executionContext.CaretOffset, n => n.Id.IsAlias());
			if (currentNode == null)
				return;

			var semanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[currentNode.Statement].SemanticModel;
			var queryBlock = semanticModel.GetQueryBlock(currentNode);
			if (queryBlock == null)
				return;

			switch (currentNode.Id)
			{
				case Terminals.ObjectAlias:
					AddObjectAliasNodesToReplace(executionContext.SegmentsToReplace, queryBlock, currentNode);
					break;
				case Terminals.ColumnAlias:
					AddColumnAliasNodesToReplace(executionContext.SegmentsToReplace, queryBlock, currentNode);
					break;
			}
		}

		private static void AddColumnAliasNodesToReplace(ICollection<TextSegment> segmentsToReplace, OracleQueryBlock queryBlock, StatementGrammarNode currentNode)
		{
			var column = queryBlock.Columns.SingleOrDefault(c => c.HasExplicitDefinition && c.IsDirectReference && c.RootNode.Terminals.Any(t => t == currentNode));
			if (column == null)
			{
				return;
			}

			foreach (var terminal in FindUsagesCommand.GetParentQueryBlockReferences(column))
			{
				segmentsToReplace.Add
					(new TextSegment
					{
						IndextStart = terminal.SourcePosition.IndexStart,
						Length = terminal.SourcePosition.Length,
						Text = currentNode.PrecedingTerminal.Token.Value
					});
			}

			segmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = currentNode.SourcePosition.IndexStart,
					Length = currentNode.SourcePosition.Length,
					Text = String.Empty
				});
		}

		private static void AddObjectAliasNodesToReplace(ICollection<TextSegment> segmentsToReplace, OracleQueryBlock queryBlock, StatementGrammarNode currentNode)
		{
			var objectReference = queryBlock.ObjectReferences.Single(o => o.AliasNode == currentNode);
			if (objectReference.Type == ReferenceType.InlineView)
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
