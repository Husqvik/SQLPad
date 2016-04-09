using System.Text;

namespace SqlPad.Oracle.Commands
{
	public class SqlTextBuilder
	{
		private static readonly OracleConfigurationFormatterFormatOptions FormatOptions = OracleConfiguration.Configuration.Formatter.FormatOptions;

		private readonly StringBuilder _builder = new StringBuilder();

		public override string ToString()
		{
			return _builder.ToString();
		}

		public void AppendIdentifier(string value)
		{
			AppendFormat(value, FormatOptions.Identifier);
		}

		public void AppendAlias(string value)
		{
			AppendFormat(value, FormatOptions.Alias);
		}

		public void AppendKeyword(string value)
		{
			AppendFormat(value, FormatOptions.Keyword);
		}

		public void AppendReservedWord(string value)
		{
			AppendFormat(value, FormatOptions.ReservedWord);
		}

		public void AppendText(string value)
		{
			_builder.Append(value);
		}

		public void AppendLine()
		{
			_builder.AppendLine();
		}

		private void AppendFormat(string value, FormatOption formatOption)
		{
			var formattedValue = OracleStatementFormatter.FormatTerminalValue(value, formatOption);
			_builder.Append(formattedValue);
		}
	}
}
