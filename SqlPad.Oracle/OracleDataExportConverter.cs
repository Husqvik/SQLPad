using System;

namespace SqlPad.Oracle
{
	public class OracleDataExportConverter : IDataExportConverter
	{
		public string ToSqlValue(object value)
		{
			var stringValue = value as String;
			if (!String.IsNullOrEmpty(stringValue))
			{
				return String.Format("'{0}'", stringValue.Replace("'", "''"));
			}

			if (value is DateTime)
			{
				var date = (DateTime)value;
				return String.Format("TO_DATE('{0}', YYYY-MM-DD HH24:MI:SS)", date.ToString("yyyy-MM-dd HH:mm:ss"));
			}

			return value.ToString();
		}
	}
}
