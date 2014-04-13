using System;
using System.Collections.Generic;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

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
			
			var actions = columnReference.ColumnNodeReferences.Select(
				t => new ResolveAmbiguousColumnCommand(semanticModel, currentTerminal, t.FullyQualifiedName + "." + columnReference.Name));

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

		public override void Execute(object parameter)
		{
			// TODO
		}

		public override event EventHandler CanExecuteChanged = delegate { };

		public string ResolvedName { get; private set; }
	}
}
