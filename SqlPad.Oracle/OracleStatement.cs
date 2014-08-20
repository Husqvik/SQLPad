using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (ChildNodes={RootNode == null ? 0 : RootNode.ChildNodes.Count})")]
	public class OracleStatement : StatementBase
	{
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

		private ICollection<BindVariableConfiguration> BuildBindVariableCollection()
		{
			return BuildBindVariableIdentifierTerminalLookup()
				.OrderBy(g => g.Key)
				.Select(g => new BindVariableConfiguration { Name = g.Key, DataType = OracleBindVariable.DataTypeVarchar2, Nodes = g.ToList().AsReadOnly(), DataTypes = OracleBindVariable.DataTypes })
				.ToArray();
		}

		private IEnumerable<IGrouping<string, StatementGrammarNode>> BuildBindVariableIdentifierTerminalLookup()
		{
			var sourceTerminals = RootNode == null
				? new StatementGrammarNode[0]
				: RootNode.Terminals.Where(t => t.Id == OracleGrammarDescription.Terminals.BindVariableIdentifier);

			return sourceTerminals.ToLookup(t => t.Token.Value.Trim('"'));
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
