using System;

using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	public class OracleDataExportConverter : IDataExportConverter
	{
		public string ToSqlValue(object value)
		{
			var stringValue = value.ToString();
			if (String.IsNullOrEmpty(stringValue))
			{
				return TerminalValues.Null;
			}

			var vendorValue = value as IValue;
			return vendorValue != null
				? vendorValue.ToSqlLiteral()
				: String.Format("'{0}'", stringValue.Replace("'", "''"));
		}

		public string ToXml(object value)
		{
			var vendorValue = value as IValue;
			return vendorValue != null
				? vendorValue.ToXml()
				: value.ToString();
		}

		public string ToJson(object value)
		{
			var vendorValue = value as IValue;
			return vendorValue != null
				? vendorValue.ToJson()
				: String.Format("\"{0}\"", value.ToString().Replace("\"", "\\\""));
		}
	}
}
