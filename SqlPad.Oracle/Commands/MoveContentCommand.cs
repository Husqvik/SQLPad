using System.Linq;
using System.Windows.Input;
using SqlPad.Commands;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class MoveContentCommand
	{
		private readonly Direction _direction;
		private readonly CommandExecutionContext _executionContext;

		public static readonly CommandExecutionHandler MoveContentUp = new CommandExecutionHandler
		{
			Name = "MoveContentUp",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.Up, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) },
			ExecutionHandler = MoveContentUpHandler
		};

		public static readonly CommandExecutionHandler MoveContentDown = new CommandExecutionHandler
		{
			Name = "MoveContentDown",
			DefaultGestures = new InputGestureCollection { new KeyGesture(Key.Down, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift) },
			ExecutionHandler = MoveContentDownHandler
		};

		private static void MoveContentUpHandler(CommandExecutionContext executionContext)
		{
			new MoveContentCommand(executionContext, Direction.Up).MoveContent();
		}

		private static void MoveContentDownHandler(CommandExecutionContext executionContext)
		{
			new MoveContentCommand(executionContext, Direction.Down).MoveContent();
		}

		private MoveContentCommand(CommandExecutionContext executionContext, Direction direction)
		{
			_executionContext = executionContext;
			_direction = direction;
		}

		private static readonly string[] SupportedNodeIds = { NonTerminals.AliasedExpressionOrAllTableColumns };

		private void MoveContent()
		{
			if (_executionContext.Statements == null)
				return;

			var currentNode = _executionContext.Statements.GetTerminalAtPosition(_executionContext.CaretOffset);
			if (currentNode == null)
				return;

			var movedNode = SupportedNodeIds.Select(id => currentNode.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBoundary, id))
				.FirstOrDefault(n => n != null);
			if (movedNode == null)
				return;

			var nodeToExchange = GetNodeToExchange(movedNode);
			if (nodeToExchange == null)
				return;

			_executionContext.SegmentsToReplace
				.Add(new TextSegment
				     {
					     IndextStart = movedNode.SourcePosition.IndexStart,
					     Length = movedNode.SourcePosition.Length,
					     Text = nodeToExchange.GetStatementSubstring(_executionContext.StatementText)
				     });

			_executionContext.SegmentsToReplace
				.Add(new TextSegment
				     {
						 IndextStart = nodeToExchange.SourcePosition.IndexStart,
						 Length = nodeToExchange.SourcePosition.Length,
						 Text = movedNode.GetStatementSubstring(_executionContext.StatementText)
				     });

			_executionContext.Line += _direction == Direction.Up ? -1 : 1;
		}

		private StatementDescriptionNode GetNodeToExchange(StatementDescriptionNode movedNode)
		{
			return _direction == Direction.Up
				? movedNode.ParentNode.ParentNode.ChildNodes.FirstOrDefault(n => n.Id == movedNode.Id)
				: movedNode.ParentNode.GetPathFilterDescendants(n => n != movedNode && NodeFilters.BreakAtNestedQueryBoundary(n), movedNode.Id).FirstOrDefault();
		}

		private enum Direction
		{
			Up,
			Down
		}
	}
}
