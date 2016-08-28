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
			else if (!IsAsterisk && RootNode.TerminalCount > 0)
			{
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
