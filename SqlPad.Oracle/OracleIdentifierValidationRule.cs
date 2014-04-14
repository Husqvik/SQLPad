using System.Globalization;
using System.Windows.Controls;

namespace SqlPad.Oracle
{
	public class OracleIdentifierValidationRule : ValidationRule
	{
		public override ValidationResult Validate(object value, CultureInfo cultureInfo)
		{
			return new ValidationResult(OracleSqlParser.IsValidIdentifier((string)value), "Identifier contains characters that are not allowed, starts with number or has more than 30 characters");
		}
	}
}