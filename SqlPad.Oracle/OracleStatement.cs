using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (Count={RootNode == null ? 0 : RootNode.ChildNodes.Count})")]
	public class OracleStatement : StatementBase
	{
		private ICollection<StatementGrammarNode> _bindVariableIdentifierNodes;
		private ICollection<BindVariable> _bindVariables;

		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ProcessingStatus = ProcessingStatus.Success,
				SourcePosition = new SourcePosition { IndexStart = -1, IndexEnd = -1 }
			};

		public override bool ReturnDataset
		{
			get { return RootNode != null && RootNode.Id == OracleGrammarDescription.NonTerminals.SelectStatement; }
		}

		public override ICollection<BindVariable> BindVariables
		{
			get { return _bindVariables ?? (_bindVariables = BuildBindVariableCollection()); }
		}

		public ICollection<StatementGrammarNode> BindVariableIdentifierTerminals
		{
			get { return _bindVariableIdentifierNodes ?? (_bindVariableIdentifierNodes = BuildBindVariableIdentifierTerminalCollection()); }
		}

		private ICollection<BindVariable> BuildBindVariableCollection()
		{
			return BindVariableIdentifierTerminals
				.Select(n => n.Token.Value.Trim('"'))
				.Distinct()
				.Select(v => new OracleBindVariable(v) { DataType = OracleBindVariable.DataTypeVarchar2 })
				.ToArray();
		}

		private ICollection<StatementGrammarNode> BuildBindVariableIdentifierTerminalCollection()
		{
			return RootNode == null
				? new StatementGrammarNode[0]
				: RootNode.Terminals.Where(t => t.Id == OracleGrammarDescription.Terminals.BindVariableIdentifier).ToArray();
		}
	}

	public class OracleBindVariable : BindVariable
	{
		public const string DataTypeChar = "CHAR";
		public const string DataTypeClob = "CLOB";
		public const string DataTypeDate = "DATE";
		public const string DataTypeUnicodeChar = "NCHAR";
		public const string DataTypeUnicodeClob = "NCLOB";
		public const string DataTypeNumber = "NUMBER";
		public const string DataTypeUnicodeVarchar2 = "NVARCHAR2";
		public const string DataTypeVarchar2 = "VARCHAR2";

		private static readonly string[] BindDataTypesInternal =
		{
			DataTypeChar,
			DataTypeClob,
			DataTypeDate,
			DataTypeUnicodeChar,
			DataTypeUnicodeClob,
			DataTypeNumber,
			DataTypeUnicodeVarchar2,
			DataTypeVarchar2
		};

		public OracleBindVariable(string name) : base(name)
		{
		}

		public override ICollection<string> DataTypes
		{
			get { return BindDataTypesInternal; }
		}
	}
}
