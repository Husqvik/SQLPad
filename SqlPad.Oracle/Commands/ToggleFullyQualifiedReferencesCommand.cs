using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlPad.Commands;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.Commands
{
	internal class ToggleFullyQualifiedReferencesCommand : OracleCommandBase
	{
		public const string Title = "Toggle fully qualified reference";

		private static readonly TextSegment[] EmptySegments = new TextSegment[0];

		private IEnumerable<OracleReferenceContainer> SourceReferenceContainers =>
			CurrentQueryBlock == null
				? SemanticModel.AllReferenceContainers
				: SemanticModel.QueryBlocks.Where(qb => CurrentQueryBlock.RootNode.SourcePosition.Contains(qb.RootNode.SourcePosition))
					.SelectMany(qb => Enumerable.Repeat((OracleReferenceContainer)qb, 1).Concat(qb.Columns));

		private Lazy<OracleReference> SingleReference => new Lazy<OracleReference>(() => SemanticModel.GetReference<OracleReference>(CurrentNode));

		private ToggleFullyQualifiedReferencesCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction => n => !n.Id.In(Terminals.Dot, Terminals.Comma, Terminals.LeftParenthesis, Terminals.RightParenthesis);

		protected override CommandCanExecuteResult CanExecute()
		{
			if (ExecutionContext.SelectionLength > 0 || CurrentNode == null)
			{
				return false;
			}

			var isSupportedTerminal = CurrentNode.Id.In(Terminals.Select, Terminals.Update, Terminals.Insert, Terminals.Delete) || CurrentNode.Id.IsIdentifier();
			return isSupportedTerminal && GetMissingQualifications().Any();
		}

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace.AddRange(GetMissingQualifications());
		}

		private IEnumerable<TextSegment> GetMissingQualifications()
		{
			return GetMissingObjectReferenceQualifications().Concat(GetMissingColumnReferenceObjectQualifications()).Concat(GetMissingFunctionQualifications());
		}

		private IEnumerable<TextSegment> GetMissingFunctionQualifications()
		{
			var programReference = SingleReference.Value as OracleProgramReference;
			if (SingleReference.Value != null && programReference == null)
			{
				return EmptySegments;
			}

			var sourceProgramReferences = programReference == null
				? SourceReferenceContainers.SelectMany(c => c.ProgramReferences)
				: Enumerable.Repeat(programReference, 1);

			var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier;
			return sourceProgramReferences
				.Where(f => f.OwnerNode == null && f.Metadata != null && !f.Metadata.IsBuiltIn)
				.Select(f =>
					new TextSegment
					{
						IndextStart = (f.ObjectNode ?? f.ProgramIdentifierNode).SourcePosition.IndexStart,
						Length = 0,
						Text = $"{OracleStatementFormatter.FormatTerminalValue(f.Metadata.Identifier.Owner.ToSimpleIdentifier(), formatOption)}."
					});
		}

		private IEnumerable<TextSegment> GetMissingObjectReferenceQualifications()
		{
			var objectReference = SingleReference.Value as OracleObjectWithColumnsReference;
			if (SingleReference.Value != null && objectReference == null)
			{
				var dataObjectReference = SingleReference.Value as OracleColumnReference;
				if (dataObjectReference?.ValidObjectReference == null)
				{
					return EmptySegments;
				}
			}

			var sourceObjectReferences = objectReference == null
				? SourceReferenceContainers.SelectMany(c => c.ObjectReferences)
				: Enumerable.Repeat(objectReference, 1);

			var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier;
			return sourceObjectReferences
				.Where(o => o.OwnerNode == null && o.Type == ReferenceType.SchemaObject && o.SchemaObject != null && !String.Equals(o.SchemaObject.FullyQualifiedName.Owner, OracleObjectIdentifier.SchemaPublic))
				.Select(o =>
					new TextSegment
					{
						IndextStart = o.ObjectNode.SourcePosition.IndexStart,
						Length = 0,
						Text = $"{OracleStatementFormatter.FormatTerminalValue(o.SchemaObject.Owner.ToSimpleIdentifier(), formatOption)}."
					});
		}

		private IEnumerable<TextSegment> GetMissingColumnReferenceObjectQualifications()
		{
			var columnReference = SingleReference.Value as OracleColumnReference;
			if (SingleReference.Value != null && columnReference?.ValidObjectReference == null)
			{
				yield break;
			}

			var sourceObjectReferences = columnReference == null
				? SourceReferenceContainers.SelectMany(c => c.ColumnReferences)
				: Enumerable.Repeat(columnReference, 1);

			var columnReferences = sourceObjectReferences
				.Where(c => c.ValidObjectReference != null && (!c.ReferencesAllColumns || c.ObjectNode != null));

			var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier;
			var qualificationBuilder = new StringBuilder();
			foreach (var column in columnReferences)
			{
				qualificationBuilder.Clear();

				var validObjectReference = column.ValidObjectReference;
				var tableReference = validObjectReference as OracleDataObjectReference;
				if (column.OwnerNode == null && validObjectReference.Type == ReferenceType.SchemaObject &&
				    tableReference?.AliasNode == null &&
				    validObjectReference.SchemaObject != null && !String.Equals(validObjectReference.SchemaObject.FullyQualifiedName.Owner, OracleObjectIdentifier.SchemaPublic))
				{
					qualificationBuilder.Append(OracleStatementFormatter.FormatTerminalValue(validObjectReference.SchemaObject.Owner.ToSimpleIdentifier(), formatOption));
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
				{
					continue;
				}

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
