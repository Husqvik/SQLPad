using System;
using System.Diagnostics;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleDataType (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleDataType : OracleObject
	{
		public static readonly OracleDataType Empty = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, String.Empty) };

		public int? Length { get; set; }

		public int? Precision { get; set; }
		
		public int? Scale { get; set; }

		public DataUnit Unit { get; set; }

		public bool IsPrimitive { get { return String.IsNullOrEmpty(FullyQualifiedName.Owner); } }

		public static string ResolveFullTypeName(OracleDataType dataType, int? characterSize = null)
		{
			if (!dataType.IsPrimitive)
			{
				return dataType.FullyQualifiedName.ToString();
			}

			var name = dataType.FullyQualifiedName.Name.Trim('"');
			var effectiveSize = String.Empty;
			switch (name)
			{
				case "NVARCHAR2":
				case "NVARCHAR":
				case "VARCHAR2":
				case "VARCHAR":
				case "NCHAR":
				case "CHAR":
					var effectiveLength = characterSize ?? dataType.Length;
					if (effectiveLength.HasValue)
					{
						var unit = dataType.Unit == DataUnit.Byte ? " BYTE" : " CHAR";
						effectiveSize = String.Format("({0}{1})", effectiveLength, dataType.Unit == DataUnit.NotApplicable ? null : unit);
					}

					name = String.Format("{0}{1}", name, effectiveSize);
					break;
				case "FLOAT":
				case "NUMBER":
					var decimalScale = dataType.Scale > 0 ? String.Format(", {0}", dataType.Scale) : null;
					if (dataType.Precision > 0 || dataType.Scale > 0)
					{
						name = String.Format("{0}({1}{2})", name, dataType.Precision == null ? "*" : Convert.ToString(dataType.Precision), decimalScale);
					}
					break;
				case "RAW":
					name = String.Format("{0}({1})", name, dataType.Length);
					break;
			}

			return name;
		}

		public static OracleDataType FromJsonReturnTypeNode(StatementGrammarNode jsonReturnTypeNode)
		{
			if (jsonReturnTypeNode == null)
			{
				return Empty;
			}

			var isVarchar = false;
			if (jsonReturnTypeNode.Id == NonTerminals.JsonValueReturnType)
			{
				switch (jsonReturnTypeNode.FirstTerminalNode.Id)
				{
					case Terminals.Varchar2:
						isVarchar = true;
						break;
					case Terminals.Number:
						var dataType = new OracleDataType
						{
							FullyQualifiedName = OracleObjectIdentifier.Create(null, "NUMBER")
						};

						TryResolveNumericPrecisionAndScale(jsonReturnTypeNode, dataType);
						return dataType;
					default:
						return Empty;
				}
			}

			if (jsonReturnTypeNode.Id == NonTerminals.JsonQueryReturnType || isVarchar)
			{
				var dataType = new OracleDataType
				{
					FullyQualifiedName = OracleObjectIdentifier.Create(null, "VARCHAR2")
				};

				TryResolveVarcharDetails(dataType, jsonReturnTypeNode);

				return dataType;
			}

			throw new ArgumentException("Node ID must be 'JsonValueReturnType' or 'JsonQueryReturnType'. ", "jsonReturnTypeNode");
		}

		public static OracleDataType FromDataTypeNode(StatementGrammarNode dataTypeNode)
		{
			if (dataTypeNode == null)
			{
				throw new ArgumentNullException("dataTypeNode");
			}
			
			if (dataTypeNode.Id != NonTerminals.DataType)
			{
				throw new ArgumentException("Node ID must be 'DataType'. ", "dataTypeNode");
			}

			var owner = dataTypeNode.FirstTerminalNode.Id == Terminals.SchemaIdentifier
				? dataTypeNode.FirstTerminalNode.Token.Value
				: String.Empty;
			
			var definitionNode = dataTypeNode.GetDescendantByPath(NonTerminals.DataTypeDefinition);
			if (definitionNode == null)
			{
				return Empty;
			}

			var dataType = new OracleDataType();

			var isVarying = definitionNode.GetDescendantByPath(Terminals.Varying) != null;

			string name;
			switch (definitionNode.FirstTerminalNode.Id)
			{
				case Terminals.Double:
					name = "BINARY_DOUBLE";
					break;
				case Terminals.Long:
					name = definitionNode.ChildNodes.Count > 1 && definitionNode.ChildNodes[1].Id == Terminals.Raw
						? "LONG RAW"
						: "LONG";
					break;
				case Terminals.Interval:
					var yearToMonthNode = definitionNode.GetDescendantByPath(NonTerminals.YearToMonthOrDayToSecond, NonTerminals.YearOrMonth);
					if (yearToMonthNode == null)
					{
						var dayToSecondNode = definitionNode.GetDescendantByPath(NonTerminals.YearToMonthOrDayToSecond, NonTerminals.YearOrMonth);
						if (dayToSecondNode == null)
						{
							name = String.Empty;
						}
						else
						{
							name = "INTERVAL DAY TO SECOND";
						}
					}
					else
					{
						name = "INTERVAL YEAR TO MONTH";
					}

					break;
				case Terminals.National:
					name = isVarying ? "NVARCHAR2" : "NCHAR";
					break;
				case Terminals.Character:
					name = isVarying ? "VARCHAR2" : "CHAR";
					break;
				default:
					name = definitionNode.FirstTerminalNode.Token.Value.ToUpperInvariant();
					break;
			}

			dataType.FullyQualifiedName = OracleObjectIdentifier.Create(owner, name);

			var simplePrecisionNode = definitionNode.GetSingleDescendant(NonTerminals.DataTypeSimplePrecision);
			if (simplePrecisionNode != null)
			{
				var simplePrecisionValueTerminal = simplePrecisionNode.GetDescendantByPath(Terminals.IntegerLiteral);
				if (simplePrecisionValueTerminal != null)
				{
					dataType.Length = Convert.ToInt32(simplePrecisionValueTerminal.Token.Value);
				}
			}

			TryResolveVarcharDetails(dataType, definitionNode);

			TryResolveNumericPrecisionAndScale(definitionNode, dataType);

			return dataType;
		}

		private static void TryResolveNumericPrecisionAndScale(StatementGrammarNode definitionNode, OracleDataType dataType)
		{
			var numericPrecisionScaleNode = definitionNode.GetDescendantByPath(NonTerminals.DataTypeNumericPrecisionAndScale);
			if (numericPrecisionScaleNode == null)
			{
				return;
			}
			
			var precisionValueTerminal = numericPrecisionScaleNode.GetDescendantByPath(NonTerminals.IntegerOrAsterisk, Terminals.IntegerLiteral);
			if (precisionValueTerminal == null)
			{
				return;
			}
			
			dataType.Precision = Convert.ToInt32(precisionValueTerminal.Token.Value);

			var scaleValueTerminal = numericPrecisionScaleNode.GetDescendantByPath(NonTerminals.Scale, Terminals.IntegerLiteral);
			if (scaleValueTerminal == null)
			{
				return;
			}
			
			dataType.Scale = Convert.ToInt32(precisionValueTerminal.Token.Value);

			if (scaleValueTerminal.PrecedingTerminal.Id == Terminals.MathMinus)
			{
				dataType.Scale = -dataType.Scale;
			}
		}

		private static void TryResolveVarcharDetails(OracleDataType dataType, StatementGrammarNode definitionNode)
		{
			var varyingCharacterSimplePrecisionNode = definitionNode.GetSingleDescendant(NonTerminals.DataTypeVarcharSimplePrecision);
			if (varyingCharacterSimplePrecisionNode == null)
			{
				return;
			}
			
			var valueTerminal = varyingCharacterSimplePrecisionNode.GetDescendantByPath(Terminals.IntegerLiteral);
			if (valueTerminal == null)
			{
				return;
			}
			
			dataType.Length = Convert.ToInt32(valueTerminal.Token.Value);

			var byteOrCharNode = varyingCharacterSimplePrecisionNode.GetDescendantByPath(NonTerminals.ByteOrChar);
			if (byteOrCharNode != null)
			{
				dataType.Unit = byteOrCharNode.FirstTerminalNode.Id == Terminals.Byte ? DataUnit.Byte : DataUnit.Character;
			}
		}
	}
}
