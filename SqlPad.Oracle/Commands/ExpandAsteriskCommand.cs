using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
		private StatementGrammarNode _asteriskNode;
		public const string Title = "Expand";

		private ExpandAsteriskCommand(ActionExecutionContext executionContext)
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
			var expandedColumns = new List<ColumnDescriptionItem>();
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

			_settingsModel.IsTextInputVisible = false;

			var expandedColumns = new List<ColumnDescriptionItem>();
			var databaseLinkReferences = new List<OracleObjectWithColumnsReference>();
			_asteriskNode = FillColumnNames(expandedColumns, databaseLinkReferences, true);

			var useDefaultSettings = _settingsModel.UseDefaultSettings == null || _settingsModel.UseDefaultSettings();

			foreach (var databaseLinkReference in databaseLinkReferences)
			{
				var databaseLinkIdentifier = String.Concat(databaseLinkReference.DatabaseLinkNode.Terminals.Select(t => t.Token.Value));
				var remoteObjectIdenrifier = OracleObjectIdentifier.Create(databaseLinkReference.OwnerNode, databaseLinkReference.ObjectNode, null);
				var columnNames = await CurrentQueryBlock.SemanticModel.DatabaseModel.GetRemoteTableColumnsAsync(databaseLinkIdentifier, remoteObjectIdenrifier, cancellationToken);
				expandedColumns.AddRange(
					columnNames.Select(
						n =>
							new ColumnDescriptionItem
							{
								OwnerIdentifier = databaseLinkReference.FullyQualifiedObjectName,
								ColumnName = n.ToSimpleIdentifier()
							}));
			}

			foreach (var expandedColumn in expandedColumns.Distinct(c => c.ColumnNameLabel))
			{
				_settingsModel.AddBooleanOption(
					new BooleanOption
					{
						OptionIdentifier = expandedColumn.ColumnNameLabel,
						DescriptionContent = BuildColumnOptionDescription(expandedColumn),
						Value = !expandedColumn.IsPseudocolumn && !expandedColumn.IsHidden && useDefaultSettings,
						Tag = expandedColumn
					});
			}

			_settingsModel.Title = "Expand Asterisk";
			_settingsModel.Heading = _settingsModel.Title;
		}

		protected override async Task ExecuteAsync(CancellationToken cancellationToken)
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
				.Select(v => ((ColumnDescriptionItem)v.Tag).FormattedColumn)
				.ToArray();

			if (columnNames.Length == 0)
			{
				return TextSegment.Empty;
			}

			var builder = new StringBuilder(String.Join(", ", columnNames));
			var addSpacePrefix =
				_asteriskNode.SourcePosition.IndexStart - 1 == _asteriskNode.PrecedingTerminal?.SourcePosition.IndexEnd &&
				!String.Equals(_asteriskNode.PrecedingTerminal.Id, Terminals.Comma);
			if (addSpacePrefix)
			{
				builder.Insert(0, " ");
			}

			var addSpacePostfix =
				_asteriskNode.SourcePosition.IndexEnd + 1 == _asteriskNode.FollowingTerminal?.SourcePosition.IndexStart &&
				!String.Equals(_asteriskNode.FollowingTerminal.Id, Terminals.Comma);
			if (addSpacePostfix)
			{
				builder.Append(" ");
			}

			var textSegment =
				new TextSegment
				{
					IndextStart = _asteriskNode.SourcePosition.IndexStart,
					Length = _asteriskNode.SourcePosition.Length,
					Text = builder.ToString()
				};

			return textSegment;
		}

		private StatementGrammarNode FillColumnNames(List<ColumnDescriptionItem> columnNames, List<OracleObjectWithColumnsReference> databaseLinkReferences, bool includePseudocolumns)
		{
			StatementGrammarNode asteriskNode;
			var asteriskReference = CurrentQueryBlock.Columns.FirstOrDefault(c => c.RootNode == CurrentNode);
			if (asteriskReference == null)
			{
				var columnReference = CurrentQueryBlock.Columns.SelectMany(c => c.ColumnReferences).FirstOrDefault(c => c.ColumnNode == CurrentNode);
				if (columnReference == null || columnReference.ObjectNodeObjectReferences.Count != 1)
					return null;

				asteriskNode = columnReference.SelectListColumn.RootNode;
				var objectReference = columnReference.ObjectNodeObjectReferences.First();

				if (objectReference.DatabaseLink == null)
				{
					columnNames.AddRange(objectReference.Columns
						.Where(c => !String.IsNullOrEmpty(c.Name))
						.Select(c => GetExpandedColumn(objectReference, c, false)));

					var table = objectReference.SchemaObject.GetTargetSchemaObject() as OracleTable;
					if (includePseudocolumns && table != null)
					{
						var pseudoColumns = ((OracleDataObjectReference)objectReference).Pseudocolumns.Select(c => GetExpandedColumn(objectReference, c, true));
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

				if (!includePseudocolumns)
				{
					return null;
				}

				var pseudoColumns = CurrentQueryBlock.ObjectReferences.SelectMany(GetPseudocolumns);

				columnNames.InsertRange(0, pseudoColumns);

				if (CurrentQueryBlock.HierarchicalClauseReference != null)
				{
					columnNames.InsertRange(0, CurrentQueryBlock.HierarchicalClauseReference.Pseudocolumns.Select(c => GetExpandedColumn(null, c, true)));
				}

				asteriskNode = asteriskReference.RootNode;
			}

			return asteriskNode;
		}

		private static IEnumerable<ColumnDescriptionItem> GetPseudocolumns(OracleDataObjectReference reference)
		{
			return reference.Pseudocolumns.Select(c => GetExpandedColumn(reference, c, true));
		}

		private static OracleDataObjectReference GetObjectReference(OracleReferenceContainer column)
		{
			var columnReference = column.ColumnReferences.FirstOrDefault();
			return columnReference != null && columnReference.ColumnNodeObjectReferences.Count == 1
				? columnReference.ColumnNodeObjectReferences.First() as OracleDataObjectReference
				: null;
		}

		private static ColumnDescriptionItem GetExpandedColumn(OracleReference objectReference, OracleColumn column, bool isPseudocolumn)
		{
			var nullable = objectReference?.SchemaObject.GetTargetSchemaObject() is OracleView
				? (bool?)null
				: column.Nullable;

			return
				new ColumnDescriptionItem
				{
					OwnerIdentifier = objectReference?.FullyQualifiedObjectName ?? OracleObjectIdentifier.Empty,
					ColumnName = OracleCodeCompletionProvider.GetPrettyColumnName(column.Name),
					DataType = column.FullTypeName,
					Nullable = nullable,
					IsPseudocolumn = isPseudocolumn,
					IsHidden = column.Hidden
				};
		}

		internal static Grid BuildColumnOptionDescription(ColumnDescriptionItem column)
		{
			var descriptionContent = new Grid();
			descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = "ColumnName" });
			descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "DataType" });
			descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Nullable" });

			var columnNameLabel = new TextBlock();
			descriptionContent.Children.Add(columnNameLabel);

			var dataTypeLabel =
				new TextBlock
				{
					HorizontalAlignment = HorizontalAlignment.Right,
					Text = column.DataType,
					Margin = new Thickness(20, 0, 0, 0)
				};

			Grid.SetColumn(dataTypeLabel, 1);
			descriptionContent.Children.Add(dataTypeLabel);

			var nullableLabel =
				new TextBlock
				{
					HorizontalAlignment = HorizontalAlignment.Right,
					Margin = new Thickness(4, 0, 0, 0)
				};

			if (column.Nullable.HasValue)
			{
				nullableLabel.Text = column.Nullable.Value ? "NULL" : "NOT NULL";
				Grid.SetColumn(nullableLabel, 2);
				descriptionContent.Children.Add(nullableLabel);
			}

			string extraInformation = null;
			if (column.IsPseudocolumn)
			{
				extraInformation = " (pseudocolumn)";
				columnNameLabel.Foreground = dataTypeLabel.Foreground = nullableLabel.Foreground = Brushes.CornflowerBlue;
			}
			else if (column.IsHidden)
			{
				extraInformation = " (invisible)";
				columnNameLabel.Foreground = dataTypeLabel.Foreground = nullableLabel.Foreground = Brushes.DimGray;
			}

			columnNameLabel.Text = $"{column.ColumnNameLabel}{extraInformation}";

			return descriptionContent;
		}
	}

	[DebuggerDisplay("ColumnDescriptionItem (OwnerIdentifier={OwnerIdentifier}; ColumnName={ColumnName}; IsPseudocolumn={IsPseudocolumn}; IsHidden={IsHidden})")]
	internal struct ColumnDescriptionItem
	{
		public OracleObjectIdentifier OwnerIdentifier { private get; set; }

		public string ColumnName { private get; set; }

		public string DataType { get; set; }

		public bool IsPseudocolumn { get; set; }

		public bool IsHidden { get; set; }

		public bool? Nullable { get; set; }

		public string ColumnNameLabel => BuildQualifiedColumnLabel(OwnerIdentifier, ColumnName, false);

		public string FormattedColumn => BuildQualifiedColumnLabel(OwnerIdentifier, ColumnName, true);

		private static string BuildQualifiedColumnLabel(OracleObjectIdentifier columnOwner, string columnName, bool applyFormatting)
		{
			var prefix = String.IsNullOrEmpty(columnOwner.Name)
				? null
				: $"{(applyFormatting ? columnOwner.ToFormattedString() : columnOwner.ToLabel())}.";

			columnName = columnName.ToSimpleIdentifier();

			if (applyFormatting)
			{
				var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier;
				columnName = OracleStatementFormatter.FormatTerminalValue(columnName, formatOption);
			}

			return $"{prefix}{columnName}";
		}
	}
}
