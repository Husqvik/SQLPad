using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Commands;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class ResolveAmbiguousColumnCommand : OracleCommandBase
	{
		private readonly string _resolvedName;

		public static ICollection<CommandExecutionHandler> ResolveCommandHandlers(OracleStatementSemanticModel semanticModel, StatementGrammarNode currentTerminal)
		{
			CheckParametersNotNull(semanticModel, currentTerminal);

			var commands = new List<CommandExecutionHandler>();
			if (currentTerminal.Id != Terminals.Identifier && currentTerminal.Id != Terminals.RowIdPseudoColumn)
				return EmptyHandlerCollection;

			var columnReference = semanticModel.AllReferenceContainers.SelectMany(qb => qb.ColumnReferences).SingleOrDefault(c => c.ColumnNode == currentTerminal);
			if (columnReference == null || columnReference.ColumnNodeObjectReferences.Count <= 1)
				return EmptyHandlerCollection;

			var identifiers = OracleObjectIdentifier.GetUniqueReferences(columnReference.ColumnNodeObjectReferences.Select(r => r.FullyQualifiedObjectName).ToArray());
			var actions = columnReference.ColumnNodeObjectReferences
				.Where(r => identifiers.Contains(r.FullyQualifiedObjectName))
				.Select(r => new CommandExecutionHandler
				             {
					             Name = r.FullyQualifiedObjectName + "." + columnReference.Name,
					             ExecutionHandler = c => new ResolveAmbiguousColumnCommand(c, r.FullyQualifiedObjectName + "." + columnReference.Name)
						             .Execute(),
					             CanExecuteHandler = c => true
				             });

			commands.AddRange(actions);

			return commands.AsReadOnly();
		}

		private ResolveAmbiguousColumnCommand(CommandExecutionContext executionContext, string resolvedName)
			: base(executionContext)
		{	
			_resolvedName = resolvedName;
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction
		{
			get { return n => !n.Id.In(Terminals.RightParenthesis, Terminals.Comma); }
		}

		protected override void Execute()
		{
			var prefixedColumnReference = CurrentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);

			var textSegment = new TextSegment
			                  {
				                  IndextStart = prefixedColumnReference.SourcePosition.IndexStart,
								  Length = prefixedColumnReference.SourcePosition.Length,
								  Text = _resolvedName
			                  };
			
			ExecutionContext.SegmentsToReplace.Add(textSegment);
		}
	}
}
