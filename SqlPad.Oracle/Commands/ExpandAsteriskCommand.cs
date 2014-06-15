using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SqlPad.Oracle.Commands
{
	internal class ExpandAsteriskCommand : OracleCommandBase
	{
		internal const string OptionIdentifierIncludeRowId = "IncludeRowId";
		public const string Title = "Expand";

		private ExpandAsteriskCommand(OracleCommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override bool CanExecute()
		{
			return CurrentNode != null && CurrentQueryBlock != null &&
			       CurrentNode.Id == OracleGrammarDescription.Terminals.Asterisk &&
			       !GetSegmentToReplace(false).Equals(TextSegment.Empty);
		}

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;

			settingsModel.TextInputVisibility = Visibility.Collapsed;
			settingsModel.BooleanOptionsVisibility = Visibility.Visible;
			settingsModel.AddBooleanOption(new BooleanOption { OptionIdentifier = OptionIdentifierIncludeRowId, Description = "Include ROWID", Value = false });
			settingsModel.Title = "Expand Asterisk";
			settingsModel.Heading = settingsModel.Title;

			return settingsModel;
		}

		protected override void Execute()
		{
			var settingsModel = ConfigureSettings();

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			ExecutionContext.SegmentsToReplace.Add(GetSegmentToReplace(settingsModel.BooleanOptions[OptionIdentifierIncludeRowId].Value));
		}

		private TextSegment GetSegmentToReplace(bool includeRowId)
		{
			var columnNames = new List<string>();
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
						.ToList();

					if (includeRowId && objectReference.SearchResult.SchemaObject != null &&
					    objectReference.SearchResult.SchemaObject.Organization.In(OrganizationType.Heap, OrganizationType.Index))
					{
						columnNames.Insert(0, GetFullyQualifiedColumnName(objectReference, OracleColumn.RowId));
					}
				}
			}
			else
			{
				segmentToReplace = asteriskReference.RootNode.SourcePosition;
				columnNames = CurrentQueryBlock.Columns
					.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName))
					.Select(c => GetFullyQualifiedColumnName(GetObjectReference(c), c.NormalizedName))
					.ToList();

				if (includeRowId)
				{
					var rowIdColumns = CurrentQueryBlock.ObjectReferences
						.Where(o => o.SearchResult.SchemaObject != null && o.SearchResult.SchemaObject.Organization.In(OrganizationType.Heap, OrganizationType.Index))
						.Select(o => GetFullyQualifiedColumnName(o, OracleColumn.RowId));

					columnNames.InsertRange(0, rowIdColumns);
				}
			}

			if (columnNames.Count == 0)
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
