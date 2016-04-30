using System;
using System.Diagnostics;
using System.Globalization;

namespace SqlPad
{
	public partial class Configuration
	{
		private const string DefaultNullValuePlaceholder = "(null)";
		private const byte DefaultDataModelRefreshPeriod = 10;
		private const int DefaultRowBatchSize = 100;
		private const int DefaultIndentationSize = 4;
		private const int DefaultMaximumHistoryEntries = 1000;
		private const int DefaultMaximumHistoryEntrySize = 8192;

		public static readonly Configuration Default =
			new Configuration
			{
				dataModelField = new ConfigurationDataModel { DataModelRefreshPeriod = DefaultDataModelRefreshPeriod },
				resultGridField = new ConfigurationResultGrid { FetchRowsBatchSize = DefaultRowBatchSize },
				editorField = new ConfigurationEditor { IndentationSize = DefaultIndentationSize },
				miscellaneousField =
					new ConfigurationMiscellaneous
					{
						MaximumHistoryEntries = DefaultMaximumHistoryEntries,
						MaximumHistoryEntrySize = DefaultMaximumHistoryEntrySize
					}
			};

		public void Validate()
		{
			if (!ResultGrid.FetchRowsBatchSizeSpecified || ResultGrid.FetchRowsBatchSize == 0)
			{
				ResultGrid.FetchRowsBatchSize = DefaultRowBatchSize;
			}

			if (String.IsNullOrEmpty(ResultGrid.NullPlaceholder))
			{
				ResultGrid.NullPlaceholder = DefaultNullValuePlaceholder;
			}

			editorField = editorField ?? new ConfigurationEditor();

			if (!editorField.IndentationSizeSpecified || editorField.IndentationSize == 0)
			{
				editorField.IndentationSize = DefaultIndentationSize;
			}

			miscellaneousField = miscellaneousField ?? new ConfigurationMiscellaneous();

			if (!miscellaneousField.MaximumHistoryEntriesSpecified || miscellaneousField.MaximumHistoryEntries == 0)
			{
				miscellaneousField.MaximumHistoryEntries = DefaultMaximumHistoryEntries;
			}

			if (!miscellaneousField.MaximumHistoryEntrySizeSpecified || miscellaneousField.MaximumHistoryEntrySize == 0)
			{
				miscellaneousField.MaximumHistoryEntrySize = DefaultMaximumHistoryEntrySize;
			}

			if (String.IsNullOrEmpty(resultGridField.DateFormat))
			{
				return;
			}
			
			try
			{
				Trace.WriteLine($"DateTime format test '{resultGridField.DateFormat}' => {DateTime.Now.ToString(resultGridField.DateFormat)} succeeded. ");
			}
			catch
			{
				var dateFormat = CultureInfo.CurrentUICulture.DateTimeFormat.UniversalSortableDateTimePattern;
				Trace.WriteLine($"DateFormat mask '{resultGridField.DateFormat}' is invalid. Using system UI culture - {dateFormat} ({DateTime.Now}). ");
				resultGridField.DateFormat = dateFormat;
			}
		}
	}
}
