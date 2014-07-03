using System.Linq;
using System.Text;

namespace SqlPad.Oracle.Commands
{
	internal class AddMissingColumnCommand : OracleCommandBase
	{
		private OracleDataObject _table;
		private OracleColumnReference _missingColumn;
		
		public const string Title = "Add missing column";

		private AddMissingColumnCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null || CurrentNode.Id != OracleGrammarDescription.Terminals.Identifier)
				return false;

			_missingColumn = CurrentQueryBlock.AllColumnReferences.SingleOrDefault(c => c.ColumnNode == CurrentNode);
			if (_missingColumn == null)
				return false;

			_table = GetSingleObjectReference(_missingColumn);
			return _missingColumn.ColumnNodeColumnReferences.Count == 0 && _missingColumn.ColumnNodeObjectReferences.Count <= 1 &&
			       _table != null && _table.Type == OracleSchemaObjectType.Table;
		}

		private static OracleDataObject GetSingleObjectReference(OracleColumnReference column)
		{
			OracleDataObject dataObject = null;
			var queryBlockHasSingleObjectReference = column.Owner.ObjectReferences.Count == 1 && (dataObject = column.Owner.ObjectReferences.First().SearchResult.SchemaObject) != null &&
													 dataObject.Type == OracleSchemaObjectType.Table;

			var schemaObjectReference = column.ValidObjectReference;
			var hasValidObjectReference = schemaObjectReference != null && schemaObjectReference.Type == TableReferenceType.SchemaObject &&
										  schemaObjectReference.SearchResult.SchemaObject != null && schemaObjectReference.SearchResult.SchemaObject.Type == OracleSchemaObjectType.Table;

			return queryBlockHasSingleObjectReference || hasValidObjectReference
				? dataObject ?? schemaObjectReference.SearchResult.SchemaObject
				: null;
		}

		protected override void Execute()
		{
			var indextStart = CurrentQueryBlock.Statement.RootNode.LastTerminalNode.SourcePosition.IndexEnd + 1;

			var builder = new StringBuilder();
			if (CurrentQueryBlock.Statement.TerminatorNode == null)
			{
				builder.Append(';');
			}

			builder.AppendLine();
			builder.AppendLine();
			builder.Append("ALTER TABLE ");

			builder.Append(_table.FullyQualifiedName);
			builder.AppendLine(" ADD");
			builder.AppendLine("(");
			builder.Append('\t');
			builder.Append(_missingColumn.Name.ToSimpleIdentifier());
			var newCaretOffset = builder.Length + indextStart + 1;

			builder.Append(" VARCHAR2(100) NULL");

			builder.AppendLine();
			builder.AppendLine(");");

			var addedSegment = new TextSegment
			                  {
				                  IndextStart = indextStart,
				                  Text = builder.ToString()
			                  };
			
			ExecutionContext.SegmentsToReplace.Add(addedSegment);

			ExecutionContext.CaretOffset = newCaretOffset;
		}
	}
}
