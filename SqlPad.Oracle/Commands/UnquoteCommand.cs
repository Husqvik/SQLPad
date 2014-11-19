using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class UnquoteCommand : OracleCommandBase
	{
		public const string Title = "Unquote";

		private ICollection<OracleSelectListColumn> _unquotableColumns;

		private UnquoteCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (ExecutionContext.SelectionLength > 0 || CurrentNode == null ||
			    CurrentQueryBlock == null ||
			    !CurrentNode.Id.In(Terminals.Select, Terminals.Identifier))
			{
				return false;
			}

			Func<OracleSelectListColumn, bool> filter = c => true;
			if (CurrentNode.Id == Terminals.Identifier)
			{
				filter = c => c.RootNode.FirstTerminalNode == CurrentNode;
			}

			_unquotableColumns = GetQuotedColumns(filter).ToArray();

			return _unquotableColumns.Count > 0;
		}

		protected override void Execute()
		{
			var commandHelper = new AliasCommandHelper(ExecutionContext);
			foreach (var column in _unquotableColumns)
			{
				if (column.HasExplicitAlias)
				{
					foreach (var terminal in FindUsagesCommand.GetParentQueryBlockReferences(column))
					{
						ExecutionContext.SegmentsToReplace.Add(
							new TextSegment
							{
								IndextStart = terminal.SourcePosition.IndexStart,
								Length = terminal.SourcePosition.Length,
								Text = column.NormalizedName.Trim('"')
							});
					}

					ExecutionContext.SegmentsToReplace.Add(
						new TextSegment
						{
							IndextStart = column.AliasNode.SourcePosition.IndexStart,
							Length = column.AliasNode.SourcePosition.Length,
							Text = column.NormalizedName.Trim('"')
						});
				}
				else
				{
					commandHelper.AddColumnAlias(column.RootNode, column.Owner, column.NormalizedName, column.NormalizedName.Trim('"'));
				}
			}
		}

		private IEnumerable<OracleSelectListColumn> GetQuotedColumns(Func<OracleSelectListColumn, bool> columnFilter)
		{
			return CurrentQueryBlock.Columns.Where(c => !c.IsAsterisk && c.HasExplicitDefinition && c.AliasNode != null && IsUnquotable(c) && columnFilter(c));
		}

		private static bool IsUnquotable(OracleSelectListColumn column)
		{
			var value = column.AliasNode.Token.Value;
			return value.IsQuoted() && Char.IsLetter(value[1]) && value.Substring(1, value.Length - 2).All(c => Char.IsLetterOrDigit(c) || c.In('_', '$', '#'));
		}
	}
}
