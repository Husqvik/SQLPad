using System.Linq;
using System.Text;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	internal class CreateScriptCommand : OracleCommandBase
	{
		private OracleSchemaObject _objectReference;
		
		public const string Title = "Create Script";

		private CreateScriptCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null || CurrentNode.Id != OracleGrammarDescription.Terminals.ObjectIdentifier)
				return false;

			_objectReference = CurrentQueryBlock.AllColumnReferences
				.Where(c => c.ObjectNode == CurrentNode && c.ValidObjectReference != null && c.ValidObjectReference.SchemaObject != null)
				.Select(c => c.ValidObjectReference.SchemaObject)
				.FirstOrDefault();

			if (_objectReference == null)
			{
				_objectReference = CurrentQueryBlock.ObjectReferences
					.Where(o => o.ObjectNode == CurrentNode && o.SchemaObject != null)
					.Select(o => o.SchemaObject)
					.FirstOrDefault();
			}

			return _objectReference != null;
		}

		protected override void Execute()
		{
			var databaseModel = (OracleDatabaseModel)ExecutionContext.DocumentRepository.ValidationModels[CurrentNode.Statement].SemanticModel.DatabaseModel;
			var script = databaseModel.GetObjectScript(_objectReference);

			var indextStart = CurrentQueryBlock.Statement.LastTerminalNode.SourcePosition.IndexEnd + 1;

			var builder = new StringBuilder();
			if (CurrentQueryBlock.Statement.TerminatorNode == null)
			{
				builder.Append(';');
			}

			builder.AppendLine();
			builder.AppendLine();
			builder.Append(script.Trim());

			var addedSegment = new TextSegment
			{
				IndextStart = indextStart,
				Text = builder.ToString()
			};

			ExecutionContext.SegmentsToReplace.Add(addedSegment);
		}
	}
}
