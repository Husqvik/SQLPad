using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	public static class OracleBindVariable
	{
		public const string DataTypeUnicodeClob = "NCLOB";
		public const string DataTypeRefCursor = "REF CURSOR";

		public static readonly ReadOnlyDictionary<string, BindVariableType> DataTypes =
			new ReadOnlyDictionary<string, BindVariableType>(
				new Dictionary<string, BindVariableType>
				{
					{ TerminalValues.Char, new BindVariableType(TerminalValues.Char, typeof(string), false) },
					{ TerminalValues.Clob, new BindVariableType(TerminalValues.Clob, typeof(string), true) },
					{ TerminalValues.Date, new BindVariableType(TerminalValues.Date, typeof(DateTime), false) },
					{ TerminalValues.Timestamp, new BindVariableType(TerminalValues.Timestamp, typeof(DateTime), false) },
					{ TerminalValues.NChar, new BindVariableType(TerminalValues.NChar, typeof(string), false) },
					{ DataTypeUnicodeClob, new BindVariableType(DataTypeUnicodeClob, typeof(string), true) },
					{ TerminalValues.Number, new BindVariableType(TerminalValues.Number, typeof(string), false) },
					{ TerminalValues.NVarchar2, new BindVariableType(TerminalValues.NVarchar2, typeof(string), false) },
					{ TerminalValues.Varchar2, new BindVariableType(TerminalValues.Varchar2, typeof(string), false) },
					{ DataTypeRefCursor, new BindVariableType(DataTypeRefCursor, typeof(string), false) },
					{ TerminalValues.Raw, new BindVariableType(TerminalValues.Raw, typeof(string), true) },
					{ TerminalValues.Blob, new BindVariableType(TerminalValues.Blob, typeof(string), true) }
				});
	}
}
