using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	public static class OracleBindVariable
	{
		public const string DataTypeUnicodeClob = "NCLOB";

		public static readonly ReadOnlyDictionary<string, Type> DataTypes =
			new ReadOnlyDictionary<string, Type>(
				new Dictionary<string, Type>
				{
					{ TerminalValues.Char, typeof (string) },
					{ TerminalValues.Clob, typeof (string) },
					{ TerminalValues.Date, typeof (DateTime) },
					{ TerminalValues.Timestamp, typeof (DateTime) },
					{ TerminalValues.NChar, typeof (string) },
					{ DataTypeUnicodeClob, typeof (string) },
					{ TerminalValues.Number, typeof (string) },
					{ TerminalValues.NVarchar2, typeof (string) },
					{ TerminalValues.Varchar2, typeof (string) }
				});
	}
}
