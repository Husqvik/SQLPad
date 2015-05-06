using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SqlPad.Commands;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.Commands
{
	internal class ExpandAsteriskCommand : OracleCommandBase
	{
		private CommandSettingsModel _settingsModel;
		private SourcePosition _sourcePosition;
		public const string Title = "Expand";

		private ExpandAsteriskCommand(CommandExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null ||
			    CurrentNode.Id != OracleGrammarDescription.Terminals.Asterisk)
			{
				return false;
			}

			return ExistExpandableColumns();
		}

		private CommandCanExecuteResult ExistExpandableColumns()
		{
			var expandedColumns = new List<ExpandedColumn>();
			var databaseLinkReferences = new List<OracleObjectWithColumnsReference>();
			FillColumnNames(expandedColumns, databaseLinkReferences, false);

			var isLongOperation = databaseLinkReferences.Count > 0;
			return
				new CommandCanExecuteResult
				{
					CanExecute = expandedColumns.Count > 0 || isLongOperation,
					IsLongOperation = isLongOperation
				};
		}

		private async Task ConfigureSettings(CancellationToken cancellationToken)
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			_settingsModel = ExecutionContext.SettingsProvider.Settings;

			_settingsModel.TextInputVisibility = Visibility.Collapsed;
			_settingsModel.BooleanOptionsVisibility = Visibility.Visible;

			var expandedColumns = new List<ExpandedColumn>();
			var databaseLinkReferences = new List<OracleObjectWithColumnsReference>();
			_sourcePosition = FillColumnNames(expandedColumns, databaseLinkReferences, true);

			var useDefaultSettings = _settingsModel.UseDefaultSettings == null || _settingsModel.UseDefaultSettings();

			foreach (var databaseLinkReference in databaseLinkReferences)
			{
				var databaseLinkIdentifier = String.Concat(databaseLinkReference.DatabaseLinkNode.Terminals.Select(t => t.Token.Value));
				var remoteObjectIdenrifier = OracleObjectIdentifier.Create(databaseLinkReference.OwnerNode, databaseLinkReference.ObjectNode, null);
				var columnNames = await CurrentQueryBlock.SemanticModel.DatabaseModel.GetRemoteTableColumnsAsync(databaseLinkIdentifier, remoteObjectIdenrifier, cancellationToken);
				expandedColumns.AddRange(columnNames.Select(n => new ExpandedColumn { ColumnName = String.Format("{0}.{1}", databaseLinkReference.FullyQualifiedObjectName, n.ToSimpleIdentifier()) }));
			}

			foreach (var expandedColumn in expandedColumns)
			{
				_settingsModel.AddBooleanOption(
					new BooleanOption
					{
						OptionIdentifier = expandedColumn.ColumnName,
						Description = expandedColumn.ColumnName,
						Value = !expandedColumn.IsRowId && useDefaultSettings,
						Tag = expandedColumn
					});
			}

			_settingsModel.Title = "Expand Asterisk";
			_settingsModel.Heading = _settingsModel.Title;
		}

		protected async override Task ExecuteAsync(CancellationToken cancellationToken)
		{
			await ConfigureSettings(cancellationToken);

			if (!ExecutionContext.SettingsProvider.GetSettings())
				return;

			var segmentToReplace = GetSegmentToReplace();
			if (!segmentToReplace.Equals(TextSegment.Empty))
			{
				ExecutionContext.SegmentsToReplace.Add(segmentToReplace);
			}
		}

		protected override void Execute()
		{
			ExecuteAsync(CancellationToken.None).Wait();
		}

		private TextSegment GetSegmentToReplace()
		{
			var columnNames = _settingsModel.BooleanOptions.Values
				.Where(v => v.Value)
				.Select(v => v.OptionIdentifier)
				.ToArray();

			if (columnNames.Length == 0)
				return TextSegment.Empty;

			var textSegment =
				new TextSegment
				{
					IndextStart = _sourcePosition.IndexStart,
					Length = _sourcePosition.Length,
					Text = String.Join(", ", columnNames)
				};

			return textSegment;
		}

		private SourcePosition FillColumnNames(List<ExpandedColumn> columnNames, List<OracleObjectWithColumnsReference> databaseLinkReferences, bool includeRowId)
		{
			var sourcePosition = SourcePosition.Empty;
			var asteriskReference = CurrentQueryBlock.Columns.FirstOrDefault(c => c.RootNode == CurrentNode);
			if (asteriskReference == null)
			{
				var columnReference = CurrentQueryBlock.Columns.SelectMany(c => c.ColumnReferences).FirstOrDefault(c => c.ColumnNode == CurrentNode);
				if (columnReference == null || columnReference.ObjectNodeObjectReferences.Count != 1)
					return sourcePosition;

				sourcePosition = columnReference.SelectListColumn.RootNode.SourcePosition;
				var objectReference = columnReference.ObjectNodeObjectReferences.First();

				if (objectReference.DatabaseLink == null)
				{
					columnNames.AddRange(objectReference.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name))
						.Select(c => GetExpandedColumn(objectReference, c.Name, false)));

					var dataObject = objectReference.SchemaObject as OracleTable;
					if (includeRowId && dataObject != null &&
					    dataObject.Organization.In(OrganizationType.Heap, OrganizationType.Index))
					{
						columnNames.Insert(0, GetExpandedColumn(objectReference, TerminalValues.RowIdDataType, true));
					}
				}
				else
				{
					databaseLinkReferences.Add(objectReference);
				}
			}
			else
			{
				columnNames.AddRange(CurrentQueryBlock.Columns
					.Where(c => !c.IsAsterisk && !String.IsNullOrEmpty(c.NormalizedName) && GetObjectReference(c).DatabaseLinkNode == null)
					.Select(c => GetExpandedColumn(GetObjectReference(c), c.NormalizedName, false)));

				databaseLinkReferences.AddRange(CurrentQueryBlock.ObjectReferences.Where(o => o.DatabaseLink != null));

				if (!includeRowId)
					return sourcePosition;

				var rowIdColumns = CurrentQueryBlock.ObjectReferences
					.Select(o => new { ObjectReference = o, DataObject = o.SchemaObject.GetTargetSchemaObject() as OracleTable })
					.Where(o => o.DataObject != null && o.ObjectReference.DatabaseLinkNode == null && o.DataObject.Organization.In(OrganizationType.Heap, OrganizationType.Index))
					.Select(o => GetExpandedColumn(o.ObjectReference, TerminalValues.RowIdDataType, true));

				columnNames.InsertRange(0, rowIdColumns);

				sourcePosition = asteriskReference.RootNode.SourcePosition;
			}

			return sourcePosition;
		}

		private static OracleDataObjectReference GetObjectReference(OracleReferenceContainer column)
		{
			var columnReference = column.ColumnReferences.FirstOrDefault();
			return columnReference != null && columnReference.ColumnNodeObjectReferences.Count == 1
				? columnReference.ColumnNodeObjectReferences.First() as OracleDataObjectReference
				: null;
		}

		private static ExpandedColumn GetExpandedColumn(OracleReference objectReference, string columnName, bool isRowId)
		{
			return new ExpandedColumn { ColumnName = GetColumnName(objectReference, columnName), IsRowId = isRowId };
		}

		private static string GetColumnName(OracleReference objectReference, string columnName)
		{
			var simpleColumnName = columnName.ToSimpleIdentifier();
			if (objectReference == null)
				return simpleColumnName;

			var objectPrefix = objectReference.FullyQualifiedObjectName.ToString();
			var usedObjectPrefix = String.IsNullOrEmpty(objectPrefix)
				? null
				: String.Format("{0}.", objectPrefix);

			return String.Format("{0}{1}", usedObjectPrefix, simpleColumnName);
		}

		private struct ExpandedColumn
		{
			public string ColumnName { get; set; }
			public bool IsRowId { get; set; }
		}
	}
}
