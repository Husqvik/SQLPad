using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;
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

		protected override Task ExecuteAsync(CancellationToken cancellationToken)
		{
			return ExecuteInternal(cancellationToken);
		}

		protected override void Execute()
		{
			ExecuteInternal(CancellationToken.None).Wait();
		}

		private async Task ExecuteInternal(CancellationToken cancellationToken)
		{
			var databaseModel = (OracleDatabaseModelBase)ExecutionContext.DocumentRepository.ValidationModels[CurrentNode.Statement].SemanticModel.DatabaseModel;

			string script;

			try
			{
				script = await databaseModel.GetObjectScriptAsync(_objectReference, cancellationToken);
			}
			catch (OracleException e)
			{
				if (e.Number == OracleDatabaseModel.OracleErrorCodeUserInvokedCancellation)
				{
					return;
				}
				
				throw;
			}

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
