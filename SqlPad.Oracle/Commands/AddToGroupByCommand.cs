using System.Linq;
using SqlPad.Commands;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class AddToGroupByCommand : OracleCommandBase
	{
		private readonly StatementDescriptionNode _fromClause;

		public const string Title = "Add to GROUP BY clause";

		public static CommandExecutionHandler ExecutionHandler = new CommandExecutionHandler
		{
			Name = "AddToGroupByClause",
			ExecutionHandler = ExecutionHandlerImplementation,
			CanExecuteHandler = CanExecuteHandlerImplementation
		};

		private static void ExecutionHandlerImplementation(CommandExecutionContext executionContext)
		{
			var commandInstance = new AddToGroupByCommand((OracleCommandExecutionContext)executionContext);
			if (commandInstance.CanExecute())
			{
				commandInstance.Execute();
			}
		}

		private static bool CanExecuteHandlerImplementation(CommandExecutionContext executionContext)
		{
			return new AddToGroupByCommand((OracleCommandExecutionContext)executionContext).CanExecute();
		}

		private AddToGroupByCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
			_fromClause = CurrentQueryBlock == null
				? null
				: CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.FromClause).FirstOrDefault();
		}

		private bool CanExecute()
		{
			// TODO: Check ambiguous references
			return CurrentNode != null && _fromClause != null && _fromClause.IsGrammarValid;
		}

		private void Execute()
		{
			var selectedTerminals = CurrentQueryBlock.RootNode.Terminals.Where(t => t.SourcePosition.IndexEnd >= ExecutionContext.SelectionStart && t.SourcePosition.IndexStart <= ExecutionContext.SelectionStart + ExecutionContext.SelectionLength).ToArray();
			var expressions = selectedTerminals.Select(t => t.GetTopAncestor(NonTerminals.Expression)).Distinct().ToArray();

			var firstTerminalStartIndex = selectedTerminals[0].SourcePosition.IndexStart;
			
			var groupByClause = CurrentQueryBlock.RootNode.GetDescendantsWithinSameQuery(NonTerminals.GroupByClause).SingleOrDefault();
			if (groupByClause == null)
			{
				ExecutionContext.SegmentsToReplace.Add(new TextSegment
				                      {
					                      IndextStart = _fromClause.LastTerminalNode.SourcePosition.IndexEnd + 1,
					                      Length = 0,
										  Text = " GROUP BY " + ExecutionContext.StatementText.Substring(firstTerminalStartIndex, selectedTerminals[selectedTerminals.Length - 1].SourcePosition.IndexEnd - firstTerminalStartIndex + 1)
				                      });
			}
			else
			{
				var groupingExpressions = groupByClause.GetDescendantsWithinSameQuery(NonTerminals.GroupingClause).Where(n => n.ChildNodes.First().Id == NonTerminals.Expression);
				// TODO: Find existing elements
			}

			// TODO: Find handle multiple selected columns
		}
	}
}
