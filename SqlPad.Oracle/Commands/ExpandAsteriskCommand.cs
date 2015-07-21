using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SqlPad.Commands;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

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

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction
		{
			get { return n => !n.Id.In(Terminals.Comma, Terminals.From, Terminals.Bulk, Terminals.Into); }
		}

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null || CurrentQueryBlock == null ||
			    CurrentNode.Id != Terminals.Asterisk)
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
				expandedColumns.AddRange(columnNames.Select(n => new ExpandedColumn { ColumnName = $"{databaseLinkReference.FullyQualifiedObjectName}.{n.ToSimpleIdentifier()}"}));
			}

			foreach (var expandedColumn in expandedColumns)
			{
				var descriptionContent = new Grid();
				descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

				var columnNameLabel = new TextBlock();
				descriptionContent.Children.Add(columnNameLabel);
				
				var dataTypeLabel =
					new TextBlock
					{
						HorizontalAlignment = HorizontalAlignment.Right,
						Text = expandedColumn.DataType,
						Margin = new Thickness(20, 0, 0, 0)
					};

				descriptionContent.Children.Add(dataTypeLabel);
				Grid.SetColumn(dataTypeLabel, 1);

				string extraInformation = null;
				if (expandedColumn.IsPseudoColumn)
				{
					extraInformation = " (pseudocolumn)";
					columnNameLabel.Foreground = dataTypeLabel.Foreground = Brushes.CornflowerBlue;
				}
				else if (expandedColumn.IsHidden)
				{
					extraInformation = " (invisible)";
					columnNameLabel.Foreground = dataTypeLabel.Foreground = Brushes.DimGray;
				}

				columnNameLabel.Text = $"{expandedColumn.ColumnName}{extraInformation}";

				_settingsModel.AddBooleanOption(
					new BooleanOption
					{
						OptionIdentifier = expandedColumn.ColumnName,
						DescriptionContent = descriptionContent,
						Value = (!expandedColumn.IsPseudoColumn && !expandedColumn.IsHidden) && useDefaultSettings,
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

		private SourcePosition FillColumnNames(List<ExpandedColumn> columnNames, List<OracleObjectWithColumnsReference> databaseLinkReferences, bool includePseudoColumns)
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
						.Select(c => GetExpandedColumn(objectReference, c, false)));

					var table = objectReference.SchemaObject.GetTargetSchemaObject() as OracleTable;
					if (includePseudoColumns && table != null)
					{
						var pseudoColumns = ((OracleDataObjectReference)objectReference).PseudoColumns.Select(c => GetExpandedColumn(objectReference, c, true));
						columnNames.InsertRange(0, pseudoColumns);
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
					.Select(c => GetExpandedColumn(GetObjectReference(c), c.ColumnDescription, false)));

				databaseLinkReferences.AddRange(CurrentQueryBlock.ObjectReferences.Where(o => o.DatabaseLink != null));

				if (!includePseudoColumns)
					return sourcePosition;

				var pseudoColumns = CurrentQueryBlock.ObjectReferences.SelectMany(GetPseudoColumns);

				columnNames.InsertRange(0, pseudoColumns);

				sourcePosition = asteriskReference.RootNode.SourcePosition;
			}

			return sourcePosition;
		}

		private static IEnumerable<ExpandedColumn> GetPseudoColumns(OracleDataObjectReference reference)
		{
			return reference.PseudoColumns.Select(c => GetExpandedColumn(reference, c, true));
		}

		private static OracleDataObjectReference GetObjectReference(OracleReferenceContainer column)
		{
			var columnReference = column.ColumnReferences.FirstOrDefault();
			return columnReference != null && columnReference.ColumnNodeObjectReferences.Count == 1
				? columnReference.ColumnNodeObjectReferences.First() as OracleDataObjectReference
				: null;
		}

		private static ExpandedColumn GetExpandedColumn(OracleReference objectReference, OracleColumn column, bool isPseudoColumn)
		{
			return new ExpandedColumn { ColumnName = GetColumnName(objectReference, OracleCodeCompletionProvider.GetPrettyColumnName(column.Name)), DataType = column.FullTypeName, IsPseudoColumn = isPseudoColumn, IsHidden = column.Hidden };
		}

		private static string GetColumnName(OracleReference objectReference, string columnName)
		{
			var simpleColumnName = columnName.ToSimpleIdentifier();
			if (objectReference == null)
				return simpleColumnName;

			var objectPrefix = objectReference.FullyQualifiedObjectName.ToString();
			var usedObjectPrefix = String.IsNullOrEmpty(objectPrefix)
				? null
				: $"{objectPrefix}.";

			return $"{usedObjectPrefix}{simpleColumnName}";
		}

		[DebuggerDisplay("ExpandedColumn (ColumnName={ColumnName}; IsPseudoColumn={IsPseudoColumn}; IsHidden={IsHidden})")]
		private struct ExpandedColumn
		{
			public string ColumnName { get; set; }
			
			public string DataType { get; set; }
			
			public bool IsPseudoColumn { get; set; }

			public bool IsHidden { get; set; }
		}
	}
}
