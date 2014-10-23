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

		private static readonly string[] SupportedNodeIds =
		{
			NonTerminals.AliasedExpressionOrAllTableColumns,
			NonTerminals.OrderExpression,
			NonTerminals.GroupingClause
		};

		private void MoveContent()
		{
			if (_executionContext.DocumentRepository == null)
				return;

			var currentNode = _executionContext.DocumentRepository.Statements.GetTerminalAtPosition(_executionContext.CaretOffset);
			if (currentNode == null)
				return;

			var movedNode = SupportedNodeIds.Select(id => currentNode.GetPathFilterAncestor(NodeFilters.BreakAtNestedQueryBoundary, id))
				.FirstOrDefault(n => n != null);
			if (movedNode == null)
				return;

			var positionToExchange = GetPositionToExchange(movedNode);
			if (positionToExchange == SourcePosition.Empty)
				return;

			var movedContextIndexEnd = GetLastNonChainingNodePosition(movedNode, movedNode.ParentNode.Id);
			var movedContentLength = movedContextIndexEnd - movedNode.SourcePosition.IndexStart + 1;

			_executionContext.SegmentsToReplace
				.Add(new TextSegment
				     {
					     IndextStart = movedNode.SourcePosition.IndexStart,
						 Length = movedContentLength,
					     Text = _executionContext.StatementText.Substring(positionToExchange.IndexStart, positionToExchange.Length)
				     });

			_executionContext.SegmentsToReplace
				.Add(new TextSegment
				     {
						 IndextStart = positionToExchange.IndexStart,
						 Length = positionToExchange.Length,
						 Text = _executionContext.StatementText.Substring(movedNode.SourcePosition.IndexStart, movedContentLength)
				     });

			var caretOffset = _direction == Direction.Up
				? -positionToExchange.Length - movedNode.SourcePosition.IndexStart + positionToExchange.IndexEnd + 1
				: positionToExchange.Length + positionToExchange.IndexStart - movedContextIndexEnd - 1;

			_executionContext.CaretOffset += caretOffset;
		}

		private SourcePosition GetPositionToExchange(StatementGrammarNode movedNode)
		{
			return _direction == Direction.Up
				? FindAncestorPositionToExchange(movedNode)
				: FindDescendantPositionToExchange(movedNode);
		}

		private SourcePosition FindDescendantPositionToExchange(StatementGrammarNode movedNode)
		{
			var nodeToExchange = movedNode.ParentNode.GetPathFilterDescendants(NodeFilters.BreakAtNestedQueryBoundary, movedNode.Id)
				.FirstOrDefault(n => n != movedNode);

			return CreateNodePosition(movedNode, nodeToExchange);
		}

		private SourcePosition FindAncestorPositionToExchange(StatementGrammarNode movedNode)
		{
			var parentCandidate = movedNode.ParentNode.ParentNode;
			StatementGrammarNode nodeToExchange = null;
			while (parentCandidate != null && nodeToExchange == null)
			{
				nodeToExchange = parentCandidate.ChildNodes.FirstOrDefault(n => n.Id == movedNode.Id);
				parentCandidate = parentCandidate.ParentNode;
			}

			return CreateNodePosition(movedNode, nodeToExchange);
		}

		private SourcePosition CreateNodePosition(StatementGrammarNode movedNode, StatementGrammarNode nodeToExchange)
		{
			return nodeToExchange == null
				? SourcePosition.Empty
				: SourcePosition.Create(nodeToExchange.SourcePosition.IndexStart, GetLastNonChainingNodePosition(nodeToExchange, movedNode.ParentNode.Id));
		}

		private int GetLastNonChainingNodePosition(StatementGrammarNode nodeToExchange, string chainingNodeId)
		{
			return nodeToExchange.ChildNodes.TakeWhile(n => n.Id != chainingNodeId || n != nodeToExchange.ChildNodes[nodeToExchange.ChildNodes.Count - 1]).First().SourcePosition.IndexEnd;
		}

		private enum Direction
		{
			Up,
			Down
		}
	}
}
