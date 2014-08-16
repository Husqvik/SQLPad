using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (Count={RootNode == null ? 0 : RootNode.ChildNodes.Count})")]
	public class OracleStatement : StatementBase
	{
		private ICollection<StatementGrammarNode> _bindVariableIdentifierNodes;
		private ICollection<BindVariableConfiguration> _bindVariables;

		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ProcessingStatus = ProcessingStatus.Success,
				SourcePosition = new SourcePosition { IndexStart = -1, IndexEnd = -1 }
			};

		public override ICollection<BindVariableConfiguration> BindVariables
		{
			get { return _bindVariables ?? (_bindVariables = BuildBindVariableCollection()); }
		}

		public ICollection<StatementGrammarNode> BindVariableIdentifierTerminals
		{
			get { return _bindVariableIdentifierNodes ?? (_bindVariableIdentifierNodes = BuildBindVariableIdentifierTerminalCollection()); }
		}

		private ICollection<BindVariableConfiguration> BuildBindVariableCollection()
		{
			return BindVariableIdentifierTerminals
				.Select(n => n.Token.Value.Trim('"'))
				.Distinct()
				.OrderBy(v => v)
				.Select(v => new BindVariableConfiguration { Name = v, DataType = OracleBindVariable.DataTypeVarchar2, DataTypes = OracleBindVariable.DataTypes })
				.ToArray();
		}

		private ICollection<StatementGrammarNode> BuildBindVariableIdentifierTerminalCollection()
		{
			return RootNode == null
				? new StatementGrammarNode[0]
				: RootNode.Terminals.Where(t => t.Id == OracleGrammarDescription.Terminals.BindVariableIdentifier).ToArray();
		}
	}

	public static class OracleBindVariable
	{
		public const string DataTypeChar = "CHAR";
		public const string DataTypeClob = "CLOB";
		public const string DataTypeDate = "DATE";
		public const string DataTypeUnicodeChar = "NCHAR";
		public const string DataTypeUnicodeClob = "NCLOB";
		public const string DataTypeNumber = "NUMBER";
		public const string DataTypeUnicodeVarchar2 = "NVARCHAR2";
		public const string DataTypeVarchar2 = "VARCHAR2";

		private static readonly Dictionary<string, Type> BindDataTypesInternal =
			new Dictionary<string, Type>
			{
				{ DataTypeChar, typeof(string) },
				{ DataTypeClob, typeof(string) },
				{ DataTypeDate, typeof(DateTime) },
				{ DataTypeUnicodeChar, typeof(string) },
				{ DataTypeUnicodeClob, typeof(string) },
				{ DataTypeNumber, typeof(string) },
				{ DataTypeUnicodeVarchar2, typeof(string) },
				{ DataTypeVarchar2, typeof(string) }
			};

		private static readonly IDictionary<string, Type> BindDataTypesDictionary =
			new ReadOnlyDictionary<string, Type>(BindDataTypesInternal);

		public static IDictionary<string, Type> DataTypes
		{
			get { return BindDataTypesDictionary; }
		}
	}
}
