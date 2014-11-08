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

		public OracleSelectListColumn(OracleStatementSemanticModel semanticModel, OracleSelectListColumn asteriskColumn)
			: base(semanticModel)
		{
			AsteriskColumn = asteriskColumn;
		}

		public void RegisterOuterReference()
		{
			OuterReferenceCount++;

			if (AsteriskColumn != null)
			{
				AsteriskColumn.RegisterOuterReference();
			}
		}

		public int OuterReferenceCount { get; private set; }

		public bool IsReferenced { get { return OuterReferenceCount > 0; } }
		
		public bool IsDirectReference { get; set; }
		
		public bool IsAsterisk { get; set; }
		
		public OracleSelectListColumn AsteriskColumn { get; private set; }

		public bool HasExplicitDefinition { get { return AsteriskColumn == null; } }

		public string NormalizedName
		{
			get
			{
				if (!String.IsNullOrEmpty(ExplicitNormalizedName))
					return ExplicitNormalizedName;

				if (AliasNode != null)
					return AliasNode.Token.Value.ToQuotedIdentifier();

				return _columnDescription == null
					? null
					: _columnDescription.Name;
			}
		}

		public string ExplicitNormalizedName { get; set; }

		public bool HasExplicitAlias
		{
			get { return !IsAsterisk && HasExplicitDefinition && RootNode.TerminalCount > 1 && AliasNode != null; }
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
				? ColumnReferences[0].ColumnDescription
				: null;

			_columnDescription =
				new OracleColumn
				{
					Name = NormalizedName,
					Nullable = columnDescription == null || columnDescription.Nullable,
					DataType = columnDescription == null ? OracleDataType.Empty : columnDescription.DataType,
					CharacterSize = columnDescription == null ? Int32.MinValue : columnDescription.CharacterSize
				};

			if (columnDescription == null && RootNode.TerminalCount > 0)
			{
				var expectedTerminalCountOffset = RootNode.TerminalCount > 0 && RootNode.LastTerminalNode.Id == Terminals.ColumnAlias ? 1 : 0;
				var tokenValue = RootNode.FirstTerminalNode.Token.Value;
				string literalInferredDataTypeName = null;
				var literalInferredDataType = new OracleDataType();
				switch (RootNode.FirstTerminalNode.Id)
				{
					case Terminals.StringLiteral:
						if (RootNode.TerminalCount != 1 + expectedTerminalCountOffset)
						{
							break;
						}

						if (tokenValue[0] == 'n' || tokenValue[0] == 'N')
						{
							literalInferredDataTypeName = "NCHAR";
						}
						else
						{
							literalInferredDataTypeName = "CHAR";
							literalInferredDataType.Unit = DataUnit.Character;
						}

						_columnDescription.CharacterSize = tokenValue.ToPlainString().Length;
						_columnDescription.Nullable = false;
						break;
					case Terminals.NumberLiteral:
						if (RootNode.TerminalCount != 1 + expectedTerminalCountOffset)
						{
							break;
						}

						literalInferredDataTypeName = "NUMBER";
						literalInferredDataType.Precision = GetNumberPrecision(tokenValue);
						int? scale = null;
						if (literalInferredDataType.Precision.HasValue)
						{
							var indexDecimalDigit = tokenValue.IndexOf('.');
							if (indexDecimalDigit != -1)
							{
								scale = tokenValue.Length - indexDecimalDigit - 1;
							}
						}

						literalInferredDataType.Scale = scale;
						_columnDescription.Nullable = false;
						break;
					case Terminals.Date:
						if (RootNode.TerminalCount != 2 + expectedTerminalCountOffset)
						{
							break;
						}

						literalInferredDataTypeName = "DATE";
						_columnDescription.Nullable = false;
						break;
					case Terminals.Timestamp:
						if (RootNode.TerminalCount != 2 + expectedTerminalCountOffset)
						{
							break;
						}

						literalInferredDataTypeName = "TIMESTAMP";
						_columnDescription.Nullable = false;
						break;
				}

				if (literalInferredDataTypeName != null)
				{
					literalInferredDataType.FullyQualifiedName = OracleObjectIdentifier.Create(null, literalInferredDataTypeName);
					_columnDescription.DataType = literalInferredDataType;
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

		public OracleSelectListColumn AsImplicit(OracleSelectListColumn asteriskColumn)
		{
			return
				new OracleSelectListColumn(SemanticModel, asteriskColumn)
				{
					AliasNode = AliasNode,
					RootNode = RootNode,
					IsDirectReference = true,
					_columnDescription = _columnDescription
				};
		}
	}
}
