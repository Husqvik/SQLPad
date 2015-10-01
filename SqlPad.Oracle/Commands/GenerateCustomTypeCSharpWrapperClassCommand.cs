using System;
using System.IO;
using System.Text;
using System.Windows;
using SqlPad.Commands;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class GenerateCustomTypeCSharpWrapperClassCommand : OracleCommandBase
	{
		public const string Title = "Generate C# class";

		private OracleTypeBase _oracleObjectType;

		private GenerateCustomTypeCSharpWrapperClassCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (!String.Equals(CurrentNode?.Id, Terminals.Identifier))
			{
				return false;
			}

			var semanticModel = (OracleStatementSemanticModel)ExecutionContext.DocumentRepository.ValidationModels[CurrentNode.Statement].SemanticModel;
			_oracleObjectType = (OracleTypeBase)semanticModel.GetTypeReference(CurrentNode)?.SchemaObject.GetTargetSchemaObject();

			return _oracleObjectType != null && _oracleObjectType.FullyQualifiedName != OracleDataType.XmlType.FullyQualifiedName;
		}

		protected override void Execute()
		{
			var builder = new StringBuilder();
			using (var writer = new StringWriter(builder))
			{
				CustomTypeCSharpWrapperClassGenerator.Generate(_oracleObjectType, writer);
			}

			Clipboard.SetText(builder.ToString());
		}
	}
}
