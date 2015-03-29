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

		private IReadOnlyList<OracleSelectListColumn> _unquotableColumns;
		private IReadOnlyList<OracleDataObjectReference> _unquotableObjects;

		private UnquoteCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (ExecutionContext.SelectionLength > 0 || CurrentNode == null ||
			    !CurrentNode.Id.In(Terminals.Select, Terminals.Identifier, Terminals.ObjectIdentifier))
			{
				return false;
			}

			Func<OracleSelectListColumn, bool> columnFilter = c => true;
			Func<OracleDataObjectReference, bool> objectFilter = o => true;
			if (String.Equals(CurrentNode.Id, Terminals.Identifier))
			{
				columnFilter = c => c.RootNode.FirstTerminalNode == CurrentNode;
				objectFilter = o => false;
			}
			else if (String.Equals(CurrentNode.Id, Terminals.ObjectIdentifier))
			{
				columnFilter = c => false;
				objectFilter = o => o.ObjectNode == CurrentNode;
			}

			_unquotableColumns = GetQuotedColumns(columnFilter).ToArray();
			_unquotableObjects = GetQuotedObjects(objectFilter).ToArray();

			return _unquotableColumns.Count > 0 || _unquotableObjects.Count > 0;
		}

		protected override void Execute()
		{
			var commandHelper = new AliasCommandHelper(ExecutionContext);
			foreach (var column in _unquotableColumns)
			{
				var unquotedColumnAlias = column.NormalizedName.Trim('"');
				if (column.HasExplicitAlias)
				{
					foreach (var terminal in FindUsagesCommand.GetParentQueryBlockReferences(column))
					{
						ExecutionContext.SegmentsToReplace.Add(
							new TextSegment
							{
								IndextStart = terminal.SourcePosition.IndexStart,
								Length = terminal.SourcePosition.Length,
								Text = unquotedColumnAlias
							});
					}

					ExecutionContext.SegmentsToReplace.Add(
						new TextSegment
						{
							IndextStart = column.AliasNode.SourcePosition.IndexStart,
							Length = column.AliasNode.SourcePosition.Length,
							Text = unquotedColumnAlias
						});
				}
				else
				{
					commandHelper.AddColumnAlias(column.RootNode, column.Owner, column.NormalizedName, unquotedColumnAlias);
				}
			}

			foreach (var unquotableObject in _unquotableObjects)
			{
				if (unquotableObject.AliasNode != null)
				{
					var unquotedObjectAlias = unquotableObject.AliasNode.Token.Value.Trim('"');
					ExecutionContext.SegmentsToReplace.Add(
						new TextSegment
						{
							IndextStart = unquotableObject.AliasNode.SourcePosition.IndexStart,
							Length = unquotableObject.AliasNode.SourcePosition.Length,
							Text = unquotedObjectAlias
						});

					var columnReferences = unquotableObject.Owner == null
						? unquotableObject.Container.ColumnReferences
						: unquotableObject.Owner.AllColumnReferences;
					
					var referenceColumnObjectNodes = columnReferences
						.Where(r => r.ObjectNode != null && r.ValidObjectReference == unquotableObject)
						.Select(r => r.ObjectNode);

					foreach (var objectNode in referenceColumnObjectNodes)
					{
						ExecutionContext.SegmentsToReplace.Add(
							new TextSegment
							{
								IndextStart = objectNode.SourcePosition.IndexStart,
								Length = objectNode.SourcePosition.Length,
								Text = unquotedObjectAlias
							});
					}
				}
				else
				{
					commandHelper.AddObjectAlias(unquotableObject, unquotableObject.ObjectNode.Token.Value.Trim('"'));
				}
			}
		}

		private IEnumerable<OracleSelectListColumn> GetQuotedColumns(Func<OracleSelectListColumn, bool> columnFilter)
		{
			return CurrentQueryBlock == null
				? Enumerable.Empty<OracleSelectListColumn>()
				: CurrentQueryBlock.Columns.Where(c => !c.IsAsterisk && c.HasExplicitDefinition && c.AliasNode != null && IsUnquotable(c.AliasNode.Token.Value) && columnFilter(c));
		}

		private IEnumerable<OracleDataObjectReference> GetQuotedObjects(Func<OracleDataObjectReference, bool> objectFilter)
		{
			return SemanticModel.AllReferenceContainers.SelectMany(c => c.ObjectReferences).Where(o => o.ObjectNode != null && o.Type == ReferenceType.SchemaObject && IsUnquotable(o.ObjectNode.Token.Value) && objectFilter(o));
		}

		private static bool IsUnquotable(string value)
		{
			return value.IsQuoted() && Char.IsLetter(value[1]) && value.Substring(1, value.Length - 2).All(c => Char.IsLetterOrDigit(c) || c.In('_', '$', '#'));
		}
	}
}
