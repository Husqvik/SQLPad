using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using SqlPad.Oracle.DataDictionary;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OracleSelectListColumn (Alias={AliasNode == null ? null : AliasNode.Token.Value}; IsDirectReference={IsDirectReference}; DataType={_columnDescription == null ? null : _columnDescription.FullTypeName})")]
	public class OracleSelectListColumn : OracleReferenceContainer
	{
		private static readonly Regex WhitespaceRegex = new Regex(@"\s", RegexOptions.Compiled);

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
			get { return !IsAsterisk && HasExplicitDefinition && String.Equals(RootNode.LastTerminalNode.Id, Terminals.ColumnAlias); }
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
				var expressionNode = RootNode[0];
				if (!String.Equals(expressionNode.Id, NonTerminals.Expression))
				{
					expressionNode = expressionNode[0];
				}

				if (TryResolveDataTypeFromExpression(expressionNode, _columnDescription) && !_columnDescription.DataType.IsDynamicCollection)
				{
					if (_columnDescription.DataType.FullyQualifiedName.Name.EndsWith("CHAR"))
					{
						_columnDescription.CharacterSize = _columnDescription.DataType.Length;
					}

					if (SemanticModel.HasDatabaseModel)
					{
						var oracleType = SemanticModel.DatabaseModel.GetFirstSchemaObject<OracleTypeBase>(_columnDescription.DataType.FullyQualifiedName);
						if (oracleType == null)
						{
							_columnDescription.DataType = OracleDataType.Empty;
						}
					}
				}
			}

			return _columnDescription;
		}

		internal static string BuildNonAliasedColumnName(IEnumerable<StatementGrammarNode> terminals)
		{
			return String.Concat(terminals.Select(t => WhitespaceRegex.Replace(((OracleToken)t.Token).UpperInvariantValue, String.Empty)));
		}

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

		private static bool TryResolveDataTypeFromExpression(StatementGrammarNode expressionNode, OracleColumn column)
		{
			if (expressionNode == null || expressionNode.TerminalCount == 0)
			{
				return false;
			}

			if (!String.Equals(expressionNode.Id, NonTerminals.Expression))
			{
				throw new ArgumentException("Node ID must be 'Expression'. ", "expressionNode");
			}

			StatementGrammarNode analyzedNode;
			var isChainedExpression = expressionNode[NonTerminals.ExpressionMathOperatorChainedList] != null;
			if (isChainedExpression)
			{
				return false;
			}

			if (expressionNode.ChildNodes.Count >= 2 && String.Equals(expressionNode.ChildNodes[0].Id, Terminals.LeftParenthesis) &&
				String.Equals((analyzedNode = expressionNode.ChildNodes[1]).Id, NonTerminals.Expression))
			{
				return TryResolveDataTypeFromExpression(analyzedNode, column);
			}

			analyzedNode = expressionNode[NonTerminals.CastOrXmlCastFunction, NonTerminals.CastFunctionParameterClause, NonTerminals.AsDataType, NonTerminals.DataType];
			if (analyzedNode != null)
			{
				column.DataType = OracleDataType.FromDataTypeNode(analyzedNode);
				column.Nullable = true;
				return true;
			}

			if (String.Equals(expressionNode.FirstTerminalNode.Id, Terminals.Collect))
			{
				column.DataType = OracleDataType.DynamicCollectionType;
				column.Nullable = true;
				return true;
			}

			var tokenValue = expressionNode.FirstTerminalNode.Token.Value;
			string literalInferredDataTypeName = null;
			var literalInferredDataType = new OracleDataType();
			switch (expressionNode.FirstTerminalNode.Id)
			{
				case Terminals.StringLiteral:
					if (expressionNode.TerminalCount != 1)
					{
						break;
					}

					if (tokenValue[0] == 'n' || tokenValue[0] == 'N')
					{
						literalInferredDataTypeName = TerminalValues.NChar;
					}
					else
					{
						literalInferredDataTypeName = TerminalValues.Char;
					}

					literalInferredDataType.Length = tokenValue.ToPlainString().Length;

					break;
				case Terminals.NumberLiteral:
					if (expressionNode.TerminalCount != 1)
					{
						break;
					}

					literalInferredDataTypeName = TerminalValues.Number;

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
					if (expressionNode.TerminalCount == 2)
					{
						literalInferredDataTypeName = TerminalValues.Date;
					}

					break;
				case Terminals.Timestamp:
					if (expressionNode.TerminalCount == 2)
					{
						literalInferredDataTypeName = TerminalValues.Timestamp;
						literalInferredDataType.Scale = 9;
					}

					break;
			}

			if (literalInferredDataTypeName != null)
			{
				literalInferredDataType.FullyQualifiedName = OracleObjectIdentifier.Create(null, literalInferredDataTypeName);
				column.DataType = literalInferredDataType;
				column.Nullable = false;
				return true;
			}

			return false;
		}

		/*private static int? GetNumberPrecision(string value)
		{
			if (value.Any(c => c.In('e', 'E')))
			{
				return null;
			}

			return value.Count(Char.IsDigit);
		}*/
	}
}
