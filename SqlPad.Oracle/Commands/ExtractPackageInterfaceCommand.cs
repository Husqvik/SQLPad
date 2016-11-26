using System;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.Commands
{
	internal class ExtractPackageInterfaceCommand : OracleCommandBase
	{
		private OraclePlSqlProgram _program;
		public const string Title = "Extract interface";

		private readonly OraclePlSqlStatementSemanticModel _plSqlModel;

		private ExtractPackageInterfaceCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
			_plSqlModel = SemanticModel as OraclePlSqlStatementSemanticModel;
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || _plSqlModel == null)
			{
				return false;
			}

			_program = _plSqlModel.Programs.SingleOrDefault(p => p.RootNode.SourcePosition.Contains(CurrentNode.SourcePosition));
			if (_program == null)
			{
				return false;
			}

			return
				_program.Type == PlSqlProgramType.PackageProgram && String.Equals(_program.RootNode.Id, NonTerminals.CreatePackageBody) &&
				CurrentNode.HasAncestor(_program.RootNode[NonTerminals.SchemaObject]);
		}

		protected override void Execute()
		{
			var interfaceBuilder = new StringBuilder();
			interfaceBuilder.AppendLine();
			interfaceBuilder.AppendLine();

			var packageName = _program.ObjectIdentifier.ToFormattedString();
			interfaceBuilder.AppendLine($"CREATE OR REPLACE PACKAGE {packageName} AS");

			foreach (var subProgram in _program.SubPrograms)
			{
				var signatureNode = subProgram.RootNode[0];
				if (!signatureNode.Id.In(NonTerminals.ProcedureHeading, NonTerminals.FunctionHeading))
					continue;

				interfaceBuilder.AppendLine($"\t{signatureNode.GetText(_plSqlModel.StatementText)};");
			}

			interfaceBuilder.AppendLine($"END {packageName};");

			var segment =
				new TextSegment
				{
					IndextStart = _program.RootNode.SourcePosition.IndexEnd + 1,
					Text = interfaceBuilder.ToString()
				};

			ExecutionContext.SegmentsToReplace.Add(segment);
		}
	}
}
