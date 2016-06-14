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
				: value is Int16 || value is Int32 || value is Int64
					? stringValue
					: $"'{stringValue.Replace("'", "''")}'";
		}

		public string ToColumnName(string columnHeader)
		{
			if (columnHeader.Length > 30)
			{
				columnHeader = columnHeader.Substring(0, 30);
			}
			
			return columnHeader.RequiresQuotes()
				? $"\"{columnHeader.Replace('"', ' ')}\""
			    : columnHeader;
		}

		public string ToXml(object value)
		{
			var vendorValue = value as IValue;
			return vendorValue != null
				? vendorValue.ToXml()
				: value.ToString().ToXmlCompliant();
		}

		public string ToJson(object value)
		{
			var stringValue = value.ToString();
			var vendorValue = value as IValue;
			return vendorValue != null
				? vendorValue.ToJson()
				: value is Int16 || value is Int32 || value is Int64
					? stringValue
					: $"\"{stringValue.Replace("\"", "\\\"")}\"";
		}
	}
}
