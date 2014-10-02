using System;
using System.Diagnostics;
using System.Linq;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleSelectListColumn (Alias={AliasNode == null ? null : AliasNode.Token.Value}; IsDirectReference={IsDirectReference})")]
	public class OracleSelectListColumn : OracleReferenceContainer
	{
		private OracleColumn _columnDescription;

		public OracleSelectListColumn(OracleStatementSemanticModel semanticModel)
			: base(semanticModel)
		{
		}

		public bool IsDirectReference { get; set; }
		
		public bool IsAsterisk { get; set; }
		
		public bool IsGrouped { get; set; }

		public bool ExplicitDefinition { get; set; }

		public string NormalizedName
		{
			get
			{
				if (AliasNode != null)
					return AliasNode.Token.Value.ToQuotedIdentifier();

				return _columnDescription == null ? null : _columnDescription.Name;
			}
		}

		public StatementGrammarNode AliasNode { get; set; }

		public StatementGrammarNode RootNode { get; set; }
		
		public OracleQueryBlock Owner { get; set; }

		public OracleColumn ColumnDescription
		{
			get
			{
				return _columnDescription ?? BuildColumnDescription();
			}
			set { _columnDescription = value; }
		}

		private OracleColumn BuildColumnDescription()
		{
			var columnDescription = IsDirectReference && ColumnReferences.Count == 1
				? ColumnReferences.First().ColumnDescription
				: null;

			_columnDescription =
				new OracleColumn
				{
					Name = AliasNode == null ? null : AliasNode.Token.Value.ToQuotedIdentifier(),
					Nullable = columnDescription == null || columnDescription.Nullable,
					Type = columnDescription == null ? null : columnDescription.Type,
					Precision = columnDescription == null ? null : columnDescription.Precision,
					Scale = columnDescription == null ? null : columnDescription.Scale,
					Size = columnDescription == null ? Int32.MinValue : columnDescription.Size,
					CharacterSize = columnDescription == null ? Int32.MinValue : columnDescription.CharacterSize,
					Unit = columnDescription == null ? DataUnit.NotApplicable : columnDescription.Unit
				};

			if (columnDescription == null && RootNode.TerminalCount == 1)
			{
				var tokenValue = RootNode.FirstTerminalNode.Token.Value;
				switch (RootNode.FirstTerminalNode.Id)
				{
					case Terminals.StringLiteral:
						if (tokenValue[0] == 'n' || tokenValue[0] == 'N')
						{
							_columnDescription.Type = "NVARCHAR2";
						}
						else
						{
							_columnDescription.Type = "VARCHAR2";
							_columnDescription.Unit = DataUnit.Character;
						}

						_columnDescription.CharacterSize = tokenValue.ToSimpleString().Length;
						_columnDescription.Nullable = false;
						break;
					case Terminals.NumberLiteral:
						_columnDescription.Type = "NUMBER";
						_columnDescription.Precision = GetNumberPrecision(tokenValue);
						int? scale = null;
						if (_columnDescription.Precision.HasValue)
						{
							var indexDecimalDigit = tokenValue.IndexOf('.');
							if (indexDecimalDigit != -1)
							{
								scale = tokenValue.Length - indexDecimalDigit - 1;
							}
						}

						_columnDescription.Scale = scale;
						_columnDescription.Nullable = false;
						break;
					case Terminals.IntegerLiteral:
						_columnDescription.Type = "INTEGER";
						_columnDescription.Precision = GetNumberPrecision(tokenValue);
						_columnDescription.Scale = 0;
						_columnDescription.Nullable = false;
						break;
				}
			}

			return _columnDescription;
		}

		private static int? GetNumberPrecision(string value)
		{
			if (value.Any(c => c.In('e', 'E')))
			{
				return null;
			}

			return value.Count(Char.IsDigit);
		}

		public OracleSelectListColumn AsImplicit()
		{
			return
				new OracleSelectListColumn(SemanticModel)
				{
					ExplicitDefinition = false,
					AliasNode = AliasNode,
					RootNode = RootNode,
					IsDirectReference = true,
					_columnDescription = _columnDescription
				};
		}
	}
}
