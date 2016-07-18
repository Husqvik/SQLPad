using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SqlPad.Commands;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;

namespace SqlPad.Oracle.Commands
{
	internal class ConfigureNamedParameterCommand : OracleCommandBase
	{
		public const string Title = "Add Named Parameters";

		private OracleProgramReferenceBase _programReference;

		private ConfigureNamedParameterCommand(ActionExecutionContext executionContext)
			: base(executionContext)
		{
		}

		protected override Func<StatementGrammarNode, bool> CurrentNodeFilterFunction { get; } = n => !String.Equals(n.Id, OracleGrammarDescription.Terminals.LeftParenthesis);

		protected override CommandCanExecuteResult CanExecute()
		{
			if (CurrentNode == null)
			{
				return false;
			}

			_programReference = SemanticModel.AllReferenceContainers
				.SelectMany(c => ((IEnumerable<OracleProgramReferenceBase>)c.ProgramReferences).Concat(c.TypeReferences))
				.SingleOrDefault(r => r.Metadata != null && r.AllIdentifierTerminals.Any(t => t == CurrentNode));

			return _programReference?.ParameterListNode != null && _programReference.Metadata.NamedParameters.Count > 0 && GetNamePrefixSegments().Any();
		}

		protected override void Execute()
		{
			/*var settingsModel = ConfigureSettings();
			var useDefaultSettings = settingsModel.UseDefaultSettings == null || settingsModel.UseDefaultSettings();

			if (!useDefaultSettings && !ExecutionContext.SettingsProvider.GetSettings())
			{
				return;
			}*/

			ExecutionContext.SegmentsToReplace.AddRange(GetNamePrefixSegments());
		}

		private IEnumerable<TextSegment> GetNamePrefixSegments()
		{
			var formatOption = OracleConfiguration.Configuration.Formatter.FormatOptions.Identifier;
			var parameterNameMetadata = _programReference.Metadata.NamedParameters.ToArray();
			var parameterReferences = _programReference.ParameterReferences;

			var parameterIndexStart = _programReference.ParameterListNode.FirstTerminalNode.SourcePosition.IndexEnd + 1;
			var segmentTextBuilder = new StringBuilder();
			var addCommaBeforeMissingParameters = false;
			for (var i = 0; i < parameterNameMetadata.Length; i++)
			{
				var parameterReference = i < parameterReferences.Count ? parameterReferences[i] : (ProgramParameterReference?)null;
				if (parameterReference?.OptionalIdentifierTerminal != null)
				{
					break;
				}

				var parameterMetadata = parameterNameMetadata[i];
				var parameterName = OracleStatementFormatter.FormatTerminalValue(parameterMetadata.Key.ToSimpleIdentifier(), formatOption);
				var namedParameterPrefix = $"{parameterName} => ";

				if (parameterReference.HasValue)
				{
					parameterIndexStart = parameterReference.Value.ParameterNode.SourcePosition.IndexStart;

					yield return
						new TextSegment
						{
							IndextStart = parameterIndexStart,
							Text = namedParameterPrefix
						};

					parameterIndexStart += namedParameterPrefix.Length;

					addCommaBeforeMissingParameters = true;
				}
				else
				{
					if (addCommaBeforeMissingParameters || i > 0)
					{
						segmentTextBuilder.Append(", ");
					}

					segmentTextBuilder.Append(namedParameterPrefix);
					segmentTextBuilder.Append("NULL");
				}
			}

			if (segmentTextBuilder.Length > 0)
			{
				yield return
					new TextSegment
					{
						IndextStart = parameterIndexStart,
						Text = segmentTextBuilder.ToString()
					};
			}
		}

		private CommandSettingsModel ConfigureSettings()
		{
			ExecutionContext.EnsureSettingsProviderAvailable();

			var settingsModel = ExecutionContext.SettingsProvider.Settings;

			settingsModel.IsTextInputVisible = false;
			settingsModel.Title = "Configure Named Parameters";
			settingsModel.Heading = "Parameters: ";

			foreach (var parameter in _programReference.Metadata.NamedParameters.Values)
			{
				settingsModel.AddBooleanOption(
					new BooleanOption
					{
						OptionIdentifier = parameter.Name.ToSimpleIdentifier(),
						DescriptionContent = BuildParameterDescription(parameter),
						Value = false
					});
			}

			return settingsModel;
		}

		private static Grid BuildParameterDescription(OracleProgramParameterMetadata parameterMetadata)
		{
			var descriptionContent = new Grid();
			descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), SharedSizeGroup = "ParameterName" });
			descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "DataType" });
			descriptionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Optional" });

			var columnNameLabel = new TextBlock { Text = parameterMetadata.Name.ToSimpleIdentifier() };
			descriptionContent.Children.Add(columnNameLabel);

			var dataTypeLabel =
				new TextBlock
				{
					HorizontalAlignment = HorizontalAlignment.Right,
					Text = parameterMetadata.FullDataTypeName,
					Margin = new Thickness(20, 0, 0, 0)
				};

			if (parameterMetadata.IsOptional)
			{
				var optionalLabel =
					new TextBlock
					{
						HorizontalAlignment = HorizontalAlignment.Right,
						Margin = new Thickness(4, 0, 0, 0),
						Text = "(optional)"
					};

				Grid.SetColumn(optionalLabel, 2);
				descriptionContent.Children.Add(optionalLabel);
			}

			Grid.SetColumn(dataTypeLabel, 1);

			descriptionContent.Children.Add(dataTypeLabel);

			return descriptionContent;
		}
	}
}