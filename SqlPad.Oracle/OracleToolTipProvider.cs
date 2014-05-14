using System;
using System.Linq;
using SqlPad.Oracle.ToolTips;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleToolTipProvider : IToolTipProvider
	{
		public IToolTip GetToolTip(IDatabaseModel databaseModel, SqlDocument sqlDocument, int cursorPosition)
		{
			var terminal = sqlDocument.StatementCollection.GetTerminalAtPosition(cursorPosition);
			if (terminal == null)
				return null;

			var semanticModel = new OracleStatementSemanticModel(null, (OracleStatement)terminal.Statement, (OracleDatabaseModel)databaseModel);
			var queryBlock = semanticModel.GetQueryBlock(terminal);

			var tip = terminal.Id;
			switch (terminal.Id)
			{
				case Terminals.ObjectIdentifier:
					var objectReference = GetOracleObjectReference(queryBlock, terminal);
					if (objectReference == null)
						return null;

					tip = objectReference.Type.ToCategoryLabel();
					break;
			}

			return new ToolTipColumn { DataContext = tip };
		}

		private OracleObjectReference GetOracleObjectReference(OracleQueryBlock queryBlock, StatementDescriptionNode terminal)
		{
			var objectReference = queryBlock.AllColumnReferences
				.Where(c => c.ObjectNode == terminal && c.ColumnNodeObjectReferences.Count == 1)
				.Select(c => c.ColumnNodeObjectReferences.Single())
				.FirstOrDefault();

			if (objectReference != null)
				return objectReference;

			return queryBlock.ObjectReferences
				.FirstOrDefault(o => o.ObjectNode == terminal);
		}
	}
}