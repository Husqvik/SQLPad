using System;
using System.Linq;
using SqlPad.Commands;

namespace SqlPad.Oracle.Commands
{
	internal class ExpandAsteriskCommand : OracleCommandBase
	{
		public const string Title = "Expand";

		public static CommandExecutionHandler ExecutionHandler = CreateStandardExecutionHandler<ExpandAsteriskCommand>("ExpandAsterisk");

		private ExpandAsteriskCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null &&
			       CurrentNode.Id == OracleGrammarDescription.Terminals.Asterisk &&
			       !GetSegmentToReplace().Equals(TextSegment.Empty);
		}

		protected override void Execute()
		{
			ExecutionContext.SegmentsToReplace.Add(GetSegmentToReplace());
		}

		private TextSegment GetSegmentToReplace()
		{
			var columnNames = new string[0];
			var segmentToReplace = SourcePosition.Empty;
			var asteriskReference = CurrentQueryBlock.Columns.FirstOrDefault(c => c.RootNode == CurrentNode);
			if (asteriskReference == null)
			{
				var columnReference = CurrentQueryBlock.Columns.SelectMany(c => c.ColumnReferences).FirstOrDefault(c => c.ColumnNode == CurrentNode);
				if (columnReference != null && columnReference.ObjectNodeObjectReferences.Count == 1)
				{
					segmentToReplace = columnReference.SelectListColumn.RootNode.SourcePosition;
					var objectReference = columnReference.ObjectNodeObjectReferences.First();

					columnNames = objectReference.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name))
						.Select(c => GetFullyQualifiedColumnName(objectReference, c.Name))
						.ToArray();
				}
			}
			else
			{
				segmentToReplace = asteriskReference.RootNode.SourcePosition;
				columnNames = CurrentQueryBlock.Columns
					.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
					.Select(c => GetFullyQualifiedColumnName(GetObjectReference(c), c.NormalizedName))
					.ToArray();
			}

			if (columnNames.Length == 0)
				return TextSegment.Empty;

			var textSegment = new TextSegment
			                  {
				                  IndextStart = segmentToReplace.IndexStart,
				                  Length = segmentToReplace.Length,
				                  Text = String.Join(", ", columnNames)
			                  };

			return textSegment;
		}

		private static OracleObjectReference GetObjectReference(OracleSelectListColumn column)
		{
			var columnReference = column.ColumnReferences.FirstOrDefault();
			return columnReference != null && columnReference.ColumnNodeObjectReferences.Count == 1
				? columnReference.ColumnNodeObjectReferences.First()
				: null;
		}

		private static string GetFullyQualifiedColumnName(OracleObjectReference objectReference, string columnName)
		{
			var simpleColumnName = columnName.ToSimpleIdentifier();
			if (objectReference == null)
				return simpleColumnName;

			var objectPrefix = objectReference.FullyQualifiedName.ToString();
			var usedObjectPrefix = String.IsNullOrEmpty(objectPrefix)
				? null
				: String.Format("{0}.", objectPrefix);

			return String.Format("{0}{1}", usedObjectPrefix, simpleColumnName);
		}
	}
}
