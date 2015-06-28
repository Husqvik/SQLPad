using System;
using System.Linq;
using SqlPad.Oracle.SemanticModel;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.DataDictionary
{
	internal class OracleReferenceBuilder
	{
		private readonly OracleStatementSemanticModel _semanticModel;

		public OracleReferenceBuilder(OracleStatementSemanticModel semanticModel)
		{
			_semanticModel = semanticModel;
		}

		public OracleDataTypeReference CreateDataTypeReference(OracleQueryBlock queryBlock, OracleSelectListColumn selectListColumn, StatementPlacement placement, StatementGrammarNode typeIdentifier)
		{
			var dataTypeNode = typeIdentifier.ParentNode;
			var dataTypeReference =
				new OracleDataTypeReference
				{
					RootNode = dataTypeNode,
					OwnerNode = dataTypeNode[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier],
					ObjectNode = typeIdentifier,
					DatabaseLinkNode = String.Equals(typeIdentifier.Id, Terminals.DataTypeIdentifier) ? GetDatabaseLinkFromIdentifier(typeIdentifier) : null,
					Placement = placement,
					Owner = queryBlock,
					SelectListColumn = selectListColumn
				};

			ResolveTypeMetadata(dataTypeReference);

			if (dataTypeReference.DatabaseLinkNode == null && _semanticModel.HasDatabaseModel)
			{
				dataTypeReference.SchemaObject = _semanticModel.DatabaseModel.GetFirstSchemaObject<OracleTypeBase>(_semanticModel.DatabaseModel.GetPotentialSchemaObjectIdentifiers(dataTypeReference.FullyQualifiedObjectName));
			}

			return dataTypeReference;
		}

		public static StatementGrammarNode GetDatabaseLinkFromIdentifier(StatementGrammarNode identifier)
		{
			return GetDatabaseLinkFromNode(identifier.ParentNode);
		}

		public static StatementGrammarNode GetDatabaseLinkFromNode(StatementGrammarNode node)
		{
			return node[NonTerminals.DatabaseLink, NonTerminals.DatabaseLinkName];
		}

		public static OracleDataType ResolveDataTypeFromNode(StatementGrammarNode dataType)
		{
			var dataTypeReference = new OracleDataTypeReference { RootNode = dataType };
			ResolveTypeMetadata(dataTypeReference);
			return dataTypeReference.ResolvedDataType;
		}

		public static OracleDataType ResolveDataTypeFromJsonReturnTypeNode(StatementGrammarNode jsonReturnTypeNode)
		{
			if (jsonReturnTypeNode == null)
			{
				return OracleDataType.Empty;
			}

			var isVarchar = false;
			if (String.Equals(jsonReturnTypeNode.Id, NonTerminals.JsonValueReturnType))
			{
				switch (jsonReturnTypeNode.FirstTerminalNode.Id)
				{
					case Terminals.Varchar2:
						isVarchar = true;
						break;
					case Terminals.Number:
						var dataType = new OracleDataType
						{
							FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Number)
						};

						TryResolveNumericPrecisionAndScale(new OracleDataTypeReference { ResolvedDataType = dataType }, jsonReturnTypeNode);
						return dataType;
					default:
						return OracleDataType.Empty;
				}
			}

			if (isVarchar || String.Equals(jsonReturnTypeNode.Id, NonTerminals.JsonQueryReturnType))
			{
				var dataType =
					new OracleDataType
					{
						FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Varchar2)
					};

				TryResolveVarcharDetails(new OracleDataTypeReference { ResolvedDataType = dataType }, jsonReturnTypeNode);

				return dataType;
			}

			throw new ArgumentException("Node ID must be 'JsonValueReturnType' or 'JsonQueryReturnType'. ", "jsonReturnTypeNode");
		}

		private static void ResolveTypeMetadata(OracleDataTypeReference dataTypeReference)
		{
			var dataTypeNode = dataTypeReference.RootNode;

			if (!String.Equals(dataTypeNode.Id, NonTerminals.DataType))
			{
				throw new ArgumentException("Node ID must be 'DataType'. ", "dataTypeNode");
			}

			var owner = String.Equals(dataTypeNode.FirstTerminalNode.Id, Terminals.SchemaIdentifier)
				? dataTypeNode.FirstTerminalNode.Token.Value
				: String.Empty;

			var dataType = dataTypeReference.ResolvedDataType = new OracleDataType();

			string name;
			if (String.IsNullOrEmpty(owner))
			{
				var isVarying = dataTypeNode[Terminals.Varying] != null;

				switch (dataTypeNode.FirstTerminalNode.Id)
				{
					case Terminals.Double:
						name = TerminalValues.BinaryDouble;
						break;
					case Terminals.Long:
						name = dataTypeNode.ChildNodes.Count > 1 && String.Equals(dataTypeNode.ChildNodes[1].Id, Terminals.Raw)
							? "LONG RAW"
							: TerminalValues.Long;
						break;
					case Terminals.Interval:
						var yearToMonthNode = dataTypeNode[NonTerminals.YearToMonthOrDayToSecond, NonTerminals.IntervalYearToMonth];
						if (yearToMonthNode == null)
						{
							var dayToSecondNode = dataTypeNode[NonTerminals.YearToMonthOrDayToSecond, NonTerminals.IntervalDayToSecond];
							name = dayToSecondNode == null ? String.Empty : OracleDatabaseModelBase.BuiltInDataTypeIntervalDayToSecond;
						}
						else
						{
							name = OracleDatabaseModelBase.BuiltInDataTypeIntervalYearToMonth;
						}

						break;
					case Terminals.National:
						name = isVarying ? TerminalValues.NVarchar2 : TerminalValues.NChar;
						break;
					case Terminals.Character:
						name = isVarying ? TerminalValues.Varchar2 : TerminalValues.Char;
						break;
					default:
						name = ((OracleToken)dataTypeNode.FirstTerminalNode.Token).UpperInvariantValue;
						break;
				}

				StatementGrammarNode precisionNode;
				if (String.Equals(name, OracleDatabaseModelBase.BuiltInDataTypeIntervalDayToSecond) ||
				    String.Equals(name, OracleDatabaseModelBase.BuiltInDataTypeIntervalYearToMonth))
				{
					var intervalPrecisions = dataTypeNode.GetDescendants(NonTerminals.DataTypeSimplePrecision).ToArray();
					if (intervalPrecisions.Length > 0)
					{
						dataType.Precision = GetSimplePrecisionValue(intervalPrecisions[0], out precisionNode);
						dataTypeReference.PrecisionNode = precisionNode;

						if (intervalPrecisions.Length == 2)
						{
							dataType.Scale = GetSimplePrecisionValue(intervalPrecisions[1], out precisionNode);
							dataTypeReference.ScaleNode = precisionNode;
						}
					}
				}
				else
				{
					var simplePrecisionNode = dataTypeNode.GetSingleDescendant(NonTerminals.DataTypeSimplePrecision);
					var precisionValue = GetSimplePrecisionValue(simplePrecisionNode, out precisionNode);

					switch (name)
					{
						case TerminalValues.Float:
						case TerminalValues.Timestamp:
							dataType.Precision = precisionValue;
							dataTypeReference.PrecisionNode = precisionNode;
							break;
						default:
							dataType.Length = precisionValue;
							dataTypeReference.LengthNode = precisionNode;
							break;
					}

					TryResolveVarcharDetails(dataTypeReference, dataTypeNode);

					TryResolveNumericPrecisionAndScale(dataTypeReference, dataTypeNode);
				}
			}
			else
			{
				var identifier = dataTypeNode[Terminals.DataTypeIdentifier];
				name = identifier == null ? String.Empty : identifier.Token.Value;
			}

			dataType.FullyQualifiedName = OracleObjectIdentifier.Create(owner, name);

			dataTypeReference.ResolvedDataType = dataType;
		}

		private static int? GetSimplePrecisionValue(StatementGrammarNode simplePrecisionNode, out StatementGrammarNode node)
		{
			if (simplePrecisionNode == null)
			{
				node = null;
				return null;
			}

			node = simplePrecisionNode[Terminals.IntegerLiteral];
			return node == null
				? (int?)null
				: Convert.ToInt32(node.Token.Value);
		}

		private static void TryResolveNumericPrecisionAndScale(OracleDataTypeReference dataTypeReference, StatementGrammarNode definitionNode)
		{
			var numericPrecisionScaleNode = definitionNode[NonTerminals.DataTypeNumericPrecisionAndScale];
			if (numericPrecisionScaleNode == null)
			{
				return;
			}

			var precisionValueTerminal = numericPrecisionScaleNode[NonTerminals.IntegerOrAsterisk, Terminals.IntegerLiteral];
			if (precisionValueTerminal == null)
			{
				return;
			}

			dataTypeReference.ResolvedDataType.Precision = Convert.ToInt32(precisionValueTerminal.Token.Value);
			dataTypeReference.PrecisionNode = precisionValueTerminal;

			var negativeIntegerNonTerminal = numericPrecisionScaleNode[NonTerminals.Scale, NonTerminals.NegativeInteger];
			if (negativeIntegerNonTerminal == null)
			{
				return;
			}

			dataTypeReference.ResolvedDataType.Scale = Convert.ToInt32(negativeIntegerNonTerminal.LastTerminalNode.Token.Value);
			dataTypeReference.ScaleNode = negativeIntegerNonTerminal;

			if (String.Equals(negativeIntegerNonTerminal.FirstTerminalNode.Id, Terminals.MathMinus))
			{
				dataTypeReference.ResolvedDataType.Scale = -dataTypeReference.ResolvedDataType.Scale;
			}
		}

		private static void TryResolveVarcharDetails(OracleDataTypeReference dataTypeReference, StatementGrammarNode definitionNode)
		{
			var varyingCharacterSimplePrecisionNode = definitionNode.GetSingleDescendant(NonTerminals.DataTypeVarcharSimplePrecision);
			if (varyingCharacterSimplePrecisionNode == null)
			{
				return;
			}

			var valueTerminal = varyingCharacterSimplePrecisionNode[Terminals.IntegerLiteral];
			if (valueTerminal == null)
			{
				return;
			}

			dataTypeReference.ResolvedDataType.Length = Convert.ToInt32(valueTerminal.Token.Value);
			dataTypeReference.LengthNode = valueTerminal;

			var byteOrCharNode = varyingCharacterSimplePrecisionNode[NonTerminals.ByteOrChar];
			if (byteOrCharNode != null)
			{
				dataTypeReference.ResolvedDataType.Unit = byteOrCharNode.FirstTerminalNode.Id == Terminals.Byte ? DataUnit.Byte : DataUnit.Character;
			}
		}
	}
}
