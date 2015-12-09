using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SqlPad.Commands;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class CreateScriptCommand : OracleCommandBase
	{
		private OracleSchemaObject _objectReference;
		
		public const string Title = "Create Script";

		private CreateScriptCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || !CurrentNode.Id.In(Terminals.ObjectIdentifier, Terminals.Identifier))
			{
				return false;
			}

			_objectReference = SemanticModel.AllReferenceContainers
				.SelectMany(c => c.AllReferences)
				.Where(c => (c.ObjectNode == CurrentNode || (c as OracleProgramReference)?.FunctionIdentifierNode == CurrentNode) && c.SchemaObject != null)
				.Select(c => c.SchemaObject)
				.SingleOrDefault();

			if (_objectReference == null)
			{
				_objectReference = SemanticModel.AllReferenceContainers
					.SelectMany(c => c.ColumnReferences)
					.Where(c => c.ObjectNode == CurrentNode && c.ValidObjectReference?.SchemaObject != null)
					.Select(c => c.ValidObjectReference.SchemaObject)
					.FirstOrDefault();
			}

			return
				new CommandCanExecuteResult
				{
					CanExecute = _objectReference != null,
					IsLongOperation = true
				};
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
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;

			var storeToClipboard = settingsModel.UseDefaultSettings != null && !settingsModel.UseDefaultSettings();

			var databaseModel = (OracleDatabaseModelBase)ExecutionContext.DocumentRepository.ValidationModels[CurrentNode.Statement].SemanticModel.DatabaseModel;

			var script = await databaseModel.GetObjectScriptAsync(_objectReference, cancellationToken);
			if (String.IsNullOrEmpty(script))
			{
				return;
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

			if (builder[builder.Length - 1] != ';')
			{
				builder.Append(';');
			}

			if (storeToClipboard)
			{
				Clipboard.SetText(builder.ToString());
			}
			else
			{
				var addedSegment = new TextSegment
				{
					IndextStart = indextStart,
					Text = builder.ToString()
				};

				ExecutionContext.SegmentsToReplace.Add(addedSegment);
			}
		}
	}
}
