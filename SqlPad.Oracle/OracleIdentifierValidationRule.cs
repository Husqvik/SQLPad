using System.Globalization;
using System.Windows.Controls;

namespace SqlPad.Oracle
{
	public class OracleIdentifierValidationRule : ValidationRule
	{
		public override ValidationResult Validate(object value, CultureInfo cultureInfo)
		{
			return new ValidationResult(OracleSqlParser.IsValidIdentifier((string)value), "Identifier contains characters that are not allowed, starts with a number, has more than 30 characters or matches a keyword. ");
		}
	}

	public class OracleBindVariableIdentifierValidationRule : ValidationRule
	{
		public override ValidationResult Validate(object value, CultureInfo cultureInfo)
		{
			return new ValidationResult(OracleStatementValidator.IsValidBindVariableIdentifier((string)value), "Bind variable identifier contains characters that are not allowed, has more than 30 characters, matches a keyword or is a number value between 0 and 65535. ");
		}
	}
}