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
				? vendorValue.ToLiteral()
				: String.Format("'{0}'", stringValue.Replace("'", "''"));
		}
	}
}
