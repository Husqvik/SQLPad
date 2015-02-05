using System;
using System.Diagnostics;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleSelectListColumn (Alias={AliasNode == null ? null : AliasNode.Token.Value}; IsDirectReference={IsDirectReference})")]
	public class OracleSelectListColumn : OracleReferenceContainer
	{
		private OracleColumn _columnDescription;
		private StatementGrammarNode _aliasNode;
		private string _normalizedName;

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

				if (_aliasNode != null)
					return _normalizedName;

				return _columnDescription == null
					? null
					: _columnDescription.Name;
			}
		}

		public string ExplicitNormalizedName { get; set; }

		public bool HasExplicitAlias
		{
			get { return !IsAsterisk && HasExplicitDefinition && RootNode.LastTerminalNode.Id == Terminals.ColumnAlias; }
		}

		public StatementGrammarNode AliasNode
		{
			get { return _aliasNode; }
			set
			{
				if (value == _aliasNode)
				{
					return;
				}

				_aliasNode = value;
				_normalizedName = _aliasNode == null ? null : _aliasNode.Token.Value.ToQuotedIdentifier();
			}
		}

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

			if (columnDescription == null)
			{
				var literalInferredDataType = TryResolveDataTypeFromAliasedExpression(RootNode);
				if (literalInferredDataType != null)
				{
					_columnDescription.DataType = literalInferredDataType;
					_columnDescription.Nullable = false;

					if (literalInferredDataType.FullyQualifiedName.Name.EndsWith("CHAR"))
					{
						_columnDescription.CharacterSize = literalInferredDataType.Length;
					}
				}
			}

			return _columnDescription;
		}

		internal static OracleDataType TryResolveDataTypeFromAliasedExpression(StatementGrammarNode aliasedExpressionNode)
		{
			if (aliasedExpressionNode == null || aliasedExpressionNode.TerminalCount == 0)
			{
				return null;
			}

			var expectedTerminalCountOffset = aliasedExpressionNode.LastTerminalNode.Id == Terminals.ColumnAlias
				? aliasedExpressionNode.LastTerminalNode.ParentNode.ChildNodes.Count
				: 0;
			
			var tokenValue = aliasedExpressionNode.FirstTerminalNode.Token.Value;
			string literalInferredDataTypeName = null;
			var literalInferredDataType = new OracleDataType();
			switch (aliasedExpressionNode.FirstTerminalNode.Id)
			{
				case Terminals.StringLiteral:
					if (aliasedExpressionNode.TerminalCount != 1 + expectedTerminalCountOffset)
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
					}

					literalInferredDataType.Length = tokenValue.ToPlainString().Length;

					break;
				case Terminals.NumberLiteral:
					if (aliasedExpressionNode.TerminalCount != 1 + expectedTerminalCountOffset)
					{
						break;
					}

					literalInferredDataTypeName = "NUMBER";

					/*if (includeLengthPrecisionAndScale)
					{
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
					}*/
					
					break;
				case Terminals.Date:
					if (aliasedExpressionNode.TerminalCount == 2 + expectedTerminalCountOffset)
					{
						literalInferredDataTypeName = "DATE";
					}

					break;
				case Terminals.Timestamp:
					if (aliasedExpressionNode.TerminalCount == 2 + expectedTerminalCountOffset)
					{
						literalInferredDataTypeName = "TIMESTAMP";
						literalInferredDataType.Scale = 9;
					}

					break;
			}

			if (literalInferredDataTypeName == null)
			{
				return null;
			}

			literalInferredDataType.FullyQualifiedName = OracleObjectIdentifier.Create(null, literalInferredDataTypeName);
			return literalInferredDataType;
		}

		/*private static int? GetNumberPrecision(string value)
		{
			if (value.Any(c => c.In('e', 'E')))
			{
				return null;
			}

			return value.Count(Char.IsDigit);
		}*/

		public OracleSelectListColumn AsImplicit(OracleSelectListColumn asteriskColumn)
		{
			return
				new OracleSelectListColumn(SemanticModel, asteriskColumn)
				{
					AliasNode = AliasNode,
					RootNode = RootNode,
					ExplicitNormalizedName = ExplicitNormalizedName,
					IsDirectReference = true,
					_columnDescription = _columnDescription,
					_normalizedName = _normalizedName
				};
		}
	}
}
