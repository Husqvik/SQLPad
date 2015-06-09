using System;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class WrapAsInlineViewCommand : OracleCommandBase
	{
		public const string Title = "Wrap as inline view";
		private OracleDataObjectReference _dataObjectReference;

		private WrapAsInlineViewCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null &&
				   (CanWrapSubquery() || CanWrapSchemaObjectReference());
		}

		private bool CanWrapSchemaObjectReference()
		{
			if (!CurrentNode.Id.In(Terminals.ObjectIdentifier, Terminals.ObjectAlias, Terminals.XmlTable, Terminals.JsonTable, Terminals.Table))
			{
				return false;
			}

			var dataObjectReference = CurrentQueryBlock.ObjectReferences
				.SingleOrDefault(r => (r.ObjectNode == CurrentNode || r.AliasNode == CurrentNode ||
				                       (r.RootNode.FirstTerminalNode == CurrentNode && r.RootNode.FirstTerminalNode.Id.In(Terminals.XmlTable, Terminals.JsonTable, Terminals.Table))) &&
				                      r.Columns.Count > 0);

			if (dataObjectReference != null &&
			    (dataObjectReference.Type.In(ReferenceType.TableCollection, ReferenceType.XmlTable, ReferenceType.JsonTable, ReferenceType.CommonTableExpression) || dataObjectReference.SchemaObject != null))
			{
				_dataObjectReference = dataObjectReference;
			}

			return _dataObjectReference != null;
		}

		private bool CanWrapSubquery()
		{
			return CurrentNode.Id == Terminals.Select &&
			       CurrentQueryBlock.Columns.Any(c => !String.IsNullOrEmpty(c.NormalizedName));
		}

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;
			settingsModel.ValidationRule = new OracleIdentifierValidationRule { AllowEmpty = true };
			
			settingsModel.Title = Title;
			settingsModel.Heading = settingsModel.Title;
			settingsModel.Description = "Enter an alias for the inline view";

			return settingsModel;
		}

		protected override void Execute()
		{
			if (_dataObjectReference == null)
			{
				WrapSubquery();
			}
			else
			{
				WrapSchemaObjectReference();
			}
		}

		private void WrapSchemaObjectReference()
		{
			var builder = new StringBuilder("(SELECT ");
			var columnList = String.Join(", ", _dataObjectReference.Columns
				.Select(c => c.Name.ToSimpleIdentifier()));

			builder.Append(columnList);
			builder.Append(" FROM ");
			
			var substringStart = _dataObjectReference.RootNode.SourcePosition.IndexStart;
			var tableReferenceTextLength = _dataObjectReference.AliasNode == null
				? _dataObjectReference.RootNode.SourcePosition.Length
				: _dataObjectReference.AliasNode.SourcePosition.IndexStart - substringStart;
			builder.Append(ExecutionContext.StatementText.Substring(substringStart, tableReferenceTextLength).Trim());
			
			builder.Append(")");

			if (!String.IsNullOrEmpty(_dataObjectReference.FullyQualifiedObjectName.Name))
			{
				builder.Append(String.Format(" {0}", _dataObjectReference.FullyQualifiedObjectName.Name.ToSimpleIdentifier()));	
			}

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = substringStart,
					Length = _dataObjectReference.RootNode.SourcePosition.Length,
					Text = builder.ToString()
				});
		}

		private void WrapSubquery()
		{
			var settingsModel = ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
			{
				return;
			}

			var tableAlias = settingsModel.Value;

			var builder = new StringBuilder("SELECT ");
			var objectQualifier = String.IsNullOrEmpty(tableAlias) ? null : String.Format("{0}.", tableAlias);
			var columnList = String.Join(", ", CurrentQueryBlock.Columns
				.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
				.Select(c => String.Format("{0}{1}", objectQualifier, c.NormalizedName.ToSimpleIdentifier())));

			builder.Append(columnList);
			builder.Append(" FROM (");

			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = CurrentQueryBlock.RootNode.SourcePosition.IndexStart,
					Length = 0,
					Text = builder.ToString()
				});

			var subqueryIndexEnd = CurrentQueryBlock.OrderByClause == null
				? CurrentQueryBlock.RootNode.SourcePosition.IndexEnd
				: CurrentQueryBlock.OrderByClause.SourcePosition.IndexEnd;
			
			ExecutionContext.SegmentsToReplace.Add(
				new TextSegment
				{
					IndextStart = subqueryIndexEnd + 1,
					Length = 0,
					Text = String.IsNullOrEmpty(tableAlias) ? ")" : String.Format(") {0}", tableAlias)
				});
		}
	}
}
