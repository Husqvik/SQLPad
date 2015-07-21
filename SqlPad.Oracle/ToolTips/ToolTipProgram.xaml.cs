using System;
using System.Globalization;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.ToolTips
{
	public partial class ToolTipProgram
	{
		public ToolTipProgram(string title, string documentation, OracleProgramMetadata programMetadata)
		{
			InitializeComponent();

			LabelTitle.Text = title;
			LabelDocumentation.Text = documentation;

			DataContext = programMetadata;
		}
	}

	public class AuthIdConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null
				? ValueNotAvailable
				: (AuthId)value == AuthId.CurrentUser
					? "Current user"
					: "Definer";
		}
	}

	public class ProgramTypeConverter : ValueConverterBase
	{
		public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return value == null
				? ValueNotAvailable
				: BuildTypeLabel((OracleProgramMetadata)value);
		}

		private static string BuildTypeLabel(OracleProgramMetadata metadata)
		{
			if (metadata.Type == ProgramType.StatementFunction)
			{
				return "Statement defined function";
			}

			var label = String.IsNullOrEmpty(metadata.Identifier.Owner)
				? "SQL "
				: metadata.IsBuiltIn ? "Built-in " : null;

			if (!String.IsNullOrEmpty(metadata.Identifier.Package))
			{
				label = $"{label}{(label == null ? "Package " : "package ")}";
			}
			else if (!String.IsNullOrEmpty(metadata.Identifier.Owner))
			{
				label = "Schema ";
			}
			
			var programType = metadata.Type.ToString().ToLowerInvariant();
			return $"{label}{programType}";
		}
	}
}
