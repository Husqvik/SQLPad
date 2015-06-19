using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using SqlPad.Oracle.DataDictionary;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

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

				bool isConstant;
				var dataType = OracleDataType.FromExpression(expressionNode, out isConstant);
				if (dataType != null)
				{
					_columnDescription.DataType = dataType;
					_columnDescription.Nullable = !isConstant;

					if (dataType.FullyQualifiedName.Name.EndsWith("CHAR"))
					{
						_columnDescription.CharacterSize = dataType.Length;
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
	}
}
