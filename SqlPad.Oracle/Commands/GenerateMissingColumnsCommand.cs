using System.Linq;
using System.Text;

namespace SqlPad.Oracle.Commands
{
	internal class GenerateMissingColumnsCommand : OracleCommandBase
	{
		private OracleColumnReference[] _missingColumns;
		
		internal const string OptionIdentifierIncludeRowId = "IncludeRowId";
		
		public const string Title = "Generate missing columns";

		private GenerateMissingColumnsCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null)
				return false;

			var canExecute = CurrentNode.Id == OracleGrammarDescription.Terminals.Select &&
			                 CurrentQueryBlock.AllColumnReferences.All(c => c.ObjectNodeObjectReferences.Count == 0 || c.ObjectNodeObjectReferences.Count == 1);

			_missingColumns = CurrentQueryBlock.AllColumnReferences
				.Where(c => c.ColumnNodeObjectReferences.Count == 0)
				.ToArray();

			return canExecute && _missingColumns.Length > 0 && _missingColumns.All(c => GetSingleObjectReference(c) != null);
		}

		private static OracleDataObject GetSingleObjectReference(OracleColumnReference column)
		{
			OracleDataObject dataObject = null;
			var queryBlockHasSingleObjectReference = column.Owner.ObjectReferences.Count == 1 && (dataObject = column.Owner.ObjectReferences.First().SearchResult.SchemaObject) != null &&
			                                         dataObject.Type == OracleDatabaseModel.DataObjectTypeTable;

			var schemaObjectReference = column.ValidObjectReference;
			var hasValidObjectReference = schemaObjectReference != null && schemaObjectReference.Type == TableReferenceType.SchemaObject &&
			                              schemaObjectReference.SearchResult.SchemaObject != null && schemaObjectReference.SearchResult.SchemaObject.Type == OracleDatabaseModel.DataObjectTypeTable;

			return queryBlockHasSingleObjectReference || hasValidObjectReference
				? dataObject ?? schemaObjectReference.SearchResult.SchemaObject
				: null;
		}

		protected override void Execute()
		{
			var builder = new StringBuilder();
			builder.AppendLine();
			builder.AppendLine();
			builder.Append("ALTER TABLE ");

			var missingColumns = CurrentQueryBlock.AllColumnReferences.Where(c => c.ColumnNodeObjectReferences.Count == 0).ToArray();
			var table = GetSingleObjectReference(missingColumns[0]);
			builder.Append(table.FullyQualifiedName);
			builder.AppendLine(" ADD");
			builder.AppendLine("(");

			var addComma = false;
			foreach (var column in missingColumns)
			{
				if (addComma)
				{
					builder.AppendLine(",");
				}

				builder.Append("\t");
				builder.Append(column.Name.ToSimpleIdentifier());
				builder.Append(" VARCHAR2(100) NULL");

				addComma = true;
			}

			builder.AppendLine(");");

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = CurrentQueryBlock.Statement.RootNode.LastTerminalNode.SourcePosition.IndexEnd + 1,
					Text = builder.ToString()
				});
		}
	}
}
