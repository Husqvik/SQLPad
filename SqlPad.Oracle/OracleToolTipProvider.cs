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

					var objectName = objectReference.Type == TableReferenceType.PhysicalObject
						? objectReference.SearchResult.SchemaObject.FullyQualifiedName
						: objectReference.FullyQualifiedName;
					tip = objectName + " (" + objectReference.Type.ToCategoryLabel() + ")";
					break;
				case Terminals.Identifier:
					var columnDescription = GetColumnDescription(queryBlock, terminal);
					if (columnDescription == null)
						return null;

					tip = columnDescription.Type + " (" + (columnDescription.Nullable ? String.Empty : "NOT ") + "NULL)";
					break;
			}

			return new ToolTipObject { DataContext = tip };
		}

		private OracleColumn GetColumnDescription(OracleQueryBlock queryBlock, StatementDescriptionNode terminal)
		{
			return queryBlock.AllColumnReferences
				.Where(c => c.ColumnNode == terminal)
				.Select(c => c.ColumnDescription)
				.FirstOrDefault();
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