﻿using System;
using System.Globalization;
using System.Windows.Controls;

namespace SqlPad.Oracle
{
	public class OracleIdentifierValidationRule : ValidationRule
	{
		public override ValidationResult Validate(object value, CultureInfo cultureInfo)
		{
			var identifier = (string)value;
			return new ValidationResult(AllowEmpty && String.IsNullOrEmpty(identifier) || OracleSqlParser.IsValidIdentifier(identifier), "Identifier contains characters that are not allowed, starts with a number, has more than 30 characters or matches a reserved word. ");
		}

		public bool AllowEmpty { get; set; }
	}

	public class OracleBindVariableIdentifierValidationRule : ValidationRule
	{
	    private readonly Version _databaseVersion;

	    public OracleBindVariableIdentifierValidationRule(Version databaseVersion)
	    {
	        _databaseVersion = databaseVersion;
	    }

		public override ValidationResult Validate(object value, CultureInfo cultureInfo)
		{
			return new ValidationResult(OracleStatementValidator.IsValidBindVariableIdentifier((string)value, _databaseVersion), "Bind variable identifier contains characters that are not allowed, has more than 30 characters, matches a reserved word or is a number value not between 0 and 65535. ");
		}
	}
}