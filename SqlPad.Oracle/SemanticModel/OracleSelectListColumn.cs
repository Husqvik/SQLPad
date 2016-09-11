using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using SqlPad.Oracle.DatabaseConnection;
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

		    AsteriskColumn?.RegisterOuterReference();
		}

		public int OuterReferenceCount { get; private set; }

		public bool IsReferenced => OuterReferenceCount > 0;

	    public bool IsDirectReference { get; set; }
		
		public bool IsAsterisk { get; set; }
		
		public OracleSelectListColumn AsteriskColumn { get; }

		public bool HasExplicitDefinition => AsteriskColumn == null;

		public string NormalizedName => ColumnName ?? ColumnDescription?.Name;

		public string ExplicitNormalizedName { get; set; }

		public StatementGrammarNode ExplicitAliasNode { get; set; }

		public bool HasExplicitAlias => !IsAsterisk && HasExplicitDefinition && String.Equals(RootNode.LastTerminalNode.Id, Terminals.ColumnAlias);

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
				_normalizedName = _aliasNode?.Token.Value.ToQuotedIdentifier();
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

		private string ColumnName
		{
			get
			{
				if (!String.IsNullOrEmpty(ExplicitNormalizedName))
				{
					return ExplicitNormalizedName;
				}

				if (_aliasNode != null)
				{
					return _normalizedName;
				}

				if (HasExplicitDefinition && !IsAsterisk && RootNode != null)
				{
					return BuildNonAliasedColumnName(RootNode.Terminals);
				}

				return null;
			}
		}

		private OracleColumn BuildColumnDescription()
		{
			var columnReference = IsDirectReference && ColumnReferences.Count == 1
				? ColumnReferences[0]
				: null;

			var columnDescription = columnReference?.ColumnDescription;

			_columnDescription =
				new OracleColumn
				{
					Name = ColumnName,
					Nullable = columnDescription == null,
					DataType = OracleDataType.Empty
				};

			if (columnDescription != null)
			{
				_columnDescription.Nullable = columnDescription.Nullable;
				_columnDescription.DataType = columnDescription.DataType;
				_columnDescription.CharacterSize = columnDescription.CharacterSize;

				if (!_columnDescription.Nullable)
				{
					var objectReference = columnReference.ValidObjectReference as OracleDataObjectReference;
					if (objectReference != null)
					{
						_columnDescription.Nullable = objectReference.IsOuterJoined;
					}
				}
			}
			if (IsAsterisk || RootNode.TerminalCount == 0)
			{
				return _columnDescription;
			}

			var expressionNode = RootNode[0];
			if (String.Equals(expressionNode.Id, NonTerminals.AliasedExpression))
			{
				expressionNode = expressionNode[0];
			}

			if (OracleDataType.TryResolveDataTypeFromExpression(expressionNode, _columnDescription) && !_columnDescription.DataType.IsDynamicCollection)
			{
				if (_columnDescription.DataType.FullyQualifiedName.Name.EndsWith("CHAR"))
				{
					_columnDescription.CharacterSize = _columnDescription.DataType.Length;
				}

				var isBuiltInDataType = _columnDescription.DataType.IsPrimitive && OracleDatabaseModelBase.BuiltInDataTypes.Any(t => String.Equals(t, _columnDescription.DataType.FullyQualifiedName.Name));
				if (!isBuiltInDataType && SemanticModel.HasDatabaseModel)
				{
					var oracleType = SemanticModel.DatabaseModel.GetFirstSchemaObject<OracleTypeBase>(_columnDescription.DataType.FullyQualifiedName);
					if (oracleType == null)
					{
						_columnDescription.DataType = OracleDataType.Empty;
					}
				}
			}
			else if (columnDescription == null)
			{
				bool isChainedExpression;
				expressionNode = expressionNode.UnwrapIfNonChainedExpressionWithinParentheses(out isChainedExpression);
				if (!isChainedExpression)
				{
					var programReference = ProgramReferences.SingleOrDefault(r => r.RootNode == expressionNode);
					if (programReference == null)
					{
						var typeReference = TypeReferences.SingleOrDefault(r => r.RootNode == expressionNode);
					}
					else if (programReference.Metadata != null)
					{
						if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToNumber ||
							programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramBinaryToNumber)
						{
							_columnDescription.DataType = OracleDataType.NumberType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToChar)
						{
							_columnDescription.DataType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Varchar2), Length = SemanticModel.DatabaseModel.MaximumVarcharLength };
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToDate)
						{
							_columnDescription.DataType = OracleDataType.DateType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToTimestamp)
						{
							_columnDescription.DataType = OracleDataType.CreateTimestampDataType(9);
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToTimestampWithTimeZone)
						{
							_columnDescription.DataType = OracleDataType.CreateTimestampWithTimeZoneDataType(9);
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToBlob)
						{
							_columnDescription.DataType = OracleDataType.BlobType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramHexToRaw)
						{
							_columnDescription.DataType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Raw), Length = SemanticModel.DatabaseModel.MaximumRawLength };
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToClob)
						{
							_columnDescription.DataType = OracleDataType.ClobType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToNClob)
						{
							_columnDescription.DataType = OracleDataType.NClobType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToBinaryFloat)
						{
							_columnDescription.DataType = OracleDataType.BinaryFloatType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToBinaryDouble)
						{
							_columnDescription.DataType = OracleDataType.BinaryDoubleType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramBinaryFileName)
						{
							_columnDescription.DataType = OracleDataType.BinaryFileType;
						}
						else if (programReference.Metadata.Identifier == OracleProgramIdentifier.IdentifierBuiltInProgramToNChar)
						{
							_columnDescription.DataType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.NVarchar2), Length = SemanticModel.DatabaseModel.MaximumNVarcharLength };
						}
					}
				}
			}

			return _columnDescription;
		}

		internal static string BuildNonAliasedOutputColumnName(IEnumerable<StatementGrammarNode> terminals)
		{
			return String.Concat(terminals.Select(t => WhitespaceRegex.Replace(((OracleToken)t.Token).UpperInvariantValue, String.Empty)));
		}

		internal static string BuildNonAliasedColumnName(IEnumerable<StatementGrammarNode> terminals)
		{
			var outputColumnName = BuildNonAliasedOutputColumnName(terminals);
			return outputColumnName.Trim('"').IndexOf('"') == -1 ? outputColumnName.ToQuotedIdentifier() : null;
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
					_columnDescription = ColumnDescription,
					_normalizedName = _normalizedName
				};
		}
	}
}
