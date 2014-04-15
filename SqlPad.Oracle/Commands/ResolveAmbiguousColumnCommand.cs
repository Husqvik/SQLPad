using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	public class ResolveAmbiguousColumnCommand : OracleCommandBase
	{
		public static ICollection<ResolveAmbiguousColumnCommand> ResolveCommands(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal)
		{
			CheckParameters(semanticModel, currentTerminal);

			var commands = new List<ResolveAmbiguousColumnCommand>();
			if (currentTerminal.Id != Terminals.Identifier)
				return commands.AsReadOnly();

			var columnReference = semanticModel.QueryBlocks.SelectMany(qb => qb.Columns).SelectMany(c => c.ColumnReferences).SingleOrDefault(c => c.ColumnNode == currentTerminal);
			if (columnReference == null || columnReference.ColumnNodeReferences.Count <= 1)
				return commands.AsReadOnly();

			var identifiers = OracleObjectIdentifier.GetUniqueReferences(columnReference.ColumnNodeReferences.Select(r => r.FullyQualifiedName).ToArray());
			var actions = columnReference.ColumnNodeReferences
				.Where(r => identifiers.Contains(r.FullyQualifiedName))
				.Select(r => new ResolveAmbiguousColumnCommand(semanticModel, currentTerminal, r.FullyQualifiedName + "." + columnReference.Name));

			commands.AddRange(actions);

			return commands.AsReadOnly();
		}

		private ResolveAmbiguousColumnCommand(OracleStatementSemanticModel semanticModel, StatementDescriptionNode currentTerminal, string resolvedName)
			: base(semanticModel, currentTerminal)
		{	
			if (String.IsNullOrWhiteSpace(resolvedName))
				throw new ArgumentException("resolvedName");

			ResolvedName = resolvedName;
		}

		public override bool CanExecute(object parameter)
		{
			return true;
		}

		protected override void ExecuteInternal(string statementText, ICollection<TextSegment> segmentsToReplace)
		{
			var prefixedColumnReference = CurrentTerminal.GetPathFilterAncestor(n => n.Id != NonTerminals.Expression, NonTerminals.PrefixedColumnReference);

			var textSegment = new TextSegment
			                  {
				                  IndextStart = prefixedColumnReference.SourcePosition.IndexStart,
								  Length = prefixedColumnReference.SourcePosition.Length,
								  Text = ResolvedName
			                  };
			
			segmentsToReplace.Add(textSegment);
		}

		public string ResolvedName { get; private set; }
	}
}
