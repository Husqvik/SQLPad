using System;
using System.Threading;
using System.Threading.Tasks;
using SqlPad.Commands;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;

namespace SqlPad.Oracle.Commands
{
	internal class ExpandViewCommand : OracleCommandBase
	{
		private OracleView _view;
		private OracleDataObjectReference _objectReference;
		public const string Title = "Expand";

		private ExpandViewCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null)
			{
				return false;
			}

			_objectReference = SemanticModel.GetReference<OracleDataObjectReference>(CurrentNode);
			_view = _objectReference?.SchemaObject.GetTargetSchemaObject() as OracleView;
			return _view != null;
		}

		protected override void Execute()
		{
			ExecuteAsync(CancellationToken.None).Wait();
		}

		protected override async Task ExecuteAsync(CancellationToken cancellationToken)
		{
			var viewText = await SemanticModel.DatabaseModel.ObjectScriptExtractor.ExtractViewTextAsync(_view.FullyQualifiedName, cancellationToken);
			if (String.IsNullOrEmpty(viewText))
			{
				return;
			}

			var segment =
				new TextSegment
				{
					IndextStart = _objectReference.RootNode.SourcePosition.IndexStart,
					Text = $"({viewText}) "
				};

			ExecutionContext.SegmentsToReplace.Add(segment);
		}
	}
}
