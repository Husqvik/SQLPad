using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class ResolveAmbiguousColumnCommand : OracleCommandBase
	{
		private readonly string _resolvedName;

		public static ICollection<ResolveAmbiguousColumnCommand> ResolveCommands(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
		{
			CheckParameters(semanticModel, currentTerminal);

			var commands = new List<ResolveAmbiguousColumnCommand>();
			if (currentTerminal.Id != Terminals.Identifier)
				return commands.AsReadOnly();

			var columnReference = semanticModel.QueryBlocks.SelectMany(qb => qb.Columns).SelectMany(c => c.ColumnReferences).SingleOrDefault(c => c.ColumnNode == currentTerminal);
			if (columnReference == null || columnReference.ColumnNodeObjectReferences.Count <= 1)
				return commands.AsReadOnly();

			var identifiers = OracleObjectIdentifier.GetUniqueReferences(columnReference.ColumnNodeObjectReferences.Select(r => r.FullyQualifiedName).ToArray());
			var actions = columnReference.ColumnNodeObjectReferences
				.Where(r => identifiers.Contains(r.FullyQualifiedName))
				.Select(r => new ResolveAmbiguousColumnCommand(semanticModel, currentTerminal, r.FullyQualifiedName + "." + columnReference.Name));

			commands.AddRange(actions);

			return commands.AsReadOnly();
		}

		private ResolveAmbiguousColumnCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentNode, string resolvedName)
			: base(semanticModel, currentNode)
		{	
			if (String.IsNullOrWhiteSpace(resolvedName))
				throw new ArgumentException("resolvedName");

			_resolvedName = resolvedName;
		}

		public override bool CanExecute(object parameter)
		{
			return true;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			var prefixedColumnReference = CurrentNode.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);

			var textSegment = new TextSegment
			                  {
				                  IndextStart = prefixedColumnReference.SourcePosition.IndexStart,
								  Length = prefixedColumnReference.SourcePosition.Length,
								  Text = _resolvedName
			                  };
			
			segmentsToReplace.Add(textSegment);
		}

		public override string Title
		{
			get { return _resolvedName; }
		}
	}
}
