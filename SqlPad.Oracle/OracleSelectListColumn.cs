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

		public void RegisterOuterReference()
		{
			OuterReferenceCount++;
		}

		public int OuterReferenceCount { get; private set; }
		
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

		public bool HasExplicitAlias
		{
			get { return !IsAsterisk && ExplicitDefinition && RootNode.TerminalCount > 1 && AliasNode != null; }
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

			if (columnDescription == null && RootNode.TerminalCount > 0)
			{
				var expectedTerminalCountOffset = RootNode.TerminalCount > 0 && RootNode.LastTerminalNode.Id == Terminals.ColumnAlias ? 1 : 0;
				var tokenValue = RootNode.FirstTerminalNode.Token.Value;
				switch (RootNode.FirstTerminalNode.Id)
				{
					case Terminals.StringLiteral:
						if (RootNode.TerminalCount != 1 + expectedTerminalCountOffset)
						{
							break;
						}

						if (tokenValue[0] == 'n' || tokenValue[0] == 'N')
						{
							_columnDescription.Type = "NCHAR";
						}
						else
						{
							_columnDescription.Type = "CHAR";
							_columnDescription.Unit = DataUnit.Character;
						}

						_columnDescription.CharacterSize = tokenValue.ToPlainString().Length;
						_columnDescription.Nullable = false;
						break;
					case Terminals.NumberLiteral:
						if (RootNode.TerminalCount != 1 + expectedTerminalCountOffset)
						{
							break;
						}
						
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
					case Terminals.Date:
						if (RootNode.TerminalCount != 2 + expectedTerminalCountOffset)
						{
							break;
						}

						_columnDescription.Type = "DATE";
						_columnDescription.Nullable = false;
						break;
					case Terminals.Timestamp:
						if (RootNode.TerminalCount != 2 + expectedTerminalCountOffset)
						{
							break;
						}

						_columnDescription.Type = "TIMESTAMP";
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
