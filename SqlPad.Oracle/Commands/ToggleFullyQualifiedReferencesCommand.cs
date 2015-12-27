using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class ToggleFullyQualifiedReferencesCommand : OracleCommandBase
	{
		public const string Title = "Toggle fully qualified references";

		private ToggleFullyQualifiedReferencesCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			return ExecutionContext.SelectionLength == 0 && CurrentNode != null &&
			       CurrentQueryBlock != null &&
			       CurrentNode.Id.In(Terminals.Select, Terminals.Update, Terminals.Insert, Terminals.Delete) &&
			       GetMissingQualifications().Any();
		}

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace
				.AddRange(GetMissingQualifications());
		}

		private IEnumerable<TextSegment> GetMissingQualifications()
		{
			return GetMissingObjectReferenceQualifications().Concat(GetMissingColumnReferenceObjectQualifications()).Concat(GetMissingFunctionQualifications());
		}

		private IEnumerable<TextSegment> GetMissingFunctionQualifications()
		{
			return CurrentQueryBlock.AllProgramReferences
				.Where(f => f.OwnerNode == null && f.Metadata != null && !f.Metadata.IsBuiltIn)
				.Select(f =>
					new TextSegment
					{
						IndextStart = (f.ObjectNode ?? f.ProgramIdentifierNode).SourcePosition.IndexStart,
						Length = 0,
						Text = $"{f.Metadata.Identifier.Owner.ToSimpleIdentifier()}."
					});
		}

		private IEnumerable<TextSegment> GetMissingObjectReferenceQualifications()
		{
			return CurrentQueryBlock.ObjectReferences
				.Where(o => o.OwnerNode == null && o.Type == ReferenceType.SchemaObject && o.SchemaObject != null && !String.Equals(o.SchemaObject.FullyQualifiedName.Owner, OracleDatabaseModelBase.SchemaPublic))
				.Select(o =>
					new TextSegment
					{
						IndextStart = o.ObjectNode.SourcePosition.IndexStart,
						Length = 0,
						Text = $"{o.SchemaObject.Owner.ToSimpleIdentifier()}."
					});
		}

		private IEnumerable<TextSegment> GetMissingColumnReferenceObjectQualifications()
		{
			var columnReferences = CurrentQueryBlock.AllColumnReferences
				.Where(c => c.ValidObjectReference != null && (!c.ReferencesAllColumns || c.ObjectNode != null));
			
			var qualificationBuilder = new StringBuilder();
			foreach (var column in columnReferences)
			{
				qualificationBuilder.Clear();

				var validObjectReference = column.ValidObjectReference;
				var tableReference = validObjectReference as OracleDataObjectReference;
				if (column.OwnerNode == null && validObjectReference.Type == ReferenceType.SchemaObject &&
					(tableReference == null || tableReference.AliasNode == null) &&
					validObjectReference.SchemaObject != null && !String.Equals(validObjectReference.SchemaObject.FullyQualifiedName.Owner, OracleDatabaseModelBase.SchemaPublic))
				{
					qualificationBuilder.Append(validObjectReference.SchemaObject.Owner.ToSimpleIdentifier());
					qualificationBuilder.Append(".");
				}

				int indexStart;
				if (column.ObjectNode == null)
				{
					if (!String.IsNullOrEmpty(validObjectReference.FullyQualifiedObjectName.Name))
					{
						qualificationBuilder.Append(validObjectReference.FullyQualifiedObjectName.Name);
						qualificationBuilder.Append(".");
					}
					
					indexStart = column.ColumnNode.SourcePosition.IndexStart;
				}
				else
				{
					indexStart = column.ObjectNode.SourcePosition.IndexStart;
				}

				if (qualificationBuilder.Length == 0)
					continue;

				yield return
					new TextSegment
					{
						IndextStart = indexStart,
						Length = 0,
						Text = qualificationBuilder.ToString()
					};
			}
		}
	}
}
