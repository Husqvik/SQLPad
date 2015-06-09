using System;
using System.Diagnostics;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;

namespace SqlPad.Oracle.DataDictionary
{
	[DebuggerDisplay("OracleDataType (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleDataType : OracleObject
	{
		public static readonly OracleDataType Empty = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, String.Empty) };
		public static readonly OracleDataType NumberType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, TerminalValues.Number) };
		public static readonly OracleDataType XmlType = new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(OracleDatabaseModelBase.SchemaSys, OracleTypeBase.TypeCodeXml) };

		public int? Length { get; set; }

		public int? Precision { get; set; }
		
		public int? Scale { get; set; }

		public DataUnit Unit { get; set; }

		public bool IsPrimitive { get { return !FullyQualifiedName.HasOwner; } }

		public static OracleDataType CreateTimestampDataType(int precision)
		{
			return new OracleDataType { FullyQualifiedName = OracleObjectIdentifier.Create(String.Empty, TerminalValues.Timestamp), Precision = precision };
		}

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
				case TerminalValues.NVarchar2:
				case TerminalValues.NVarchar:
				case TerminalValues.Varchar2:
				case TerminalValues.Varchar:
				case TerminalValues.NChar:
				case TerminalValues.Char:
					var effectiveLength = characterSize ?? dataType.Length;
					if (effectiveLength.HasValue)
					{
						var unit = dataType.Unit == DataUnit.Byte ? " BYTE" : " CHAR";
						effectiveSize = String.Format("({0}{1})", effectiveLength, dataType.Unit == DataUnit.NotApplicable ? null : unit);
					}

					name = String.Format("{0}{1}", name, effectiveSize);
					break;
				case TerminalValues.Float:
				case TerminalValues.Number:
					var decimalScale = dataType.Scale > 0 ? String.Format(", {0}", dataType.Scale) : null;
					if (dataType.Precision > 0 || dataType.Scale > 0)
					{
						name = String.Format("{0}({1}{2})", name, dataType.Precision == null ? "*" : Convert.ToString(dataType.Precision), decimalScale);
					}
					
					break;
				case TerminalValues.Raw:
					name = String.Format("{0}({1})", name, dataType.Length);
					break;
				case TerminalValues.Timestamp:
					if (dataType.Scale.HasValue)
					{
						name = String.Format("{0}({1})", name, dataType.Scale);
					}
					
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
							FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Number)
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
					FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Varchar2)
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

			var dataType = new OracleDataType();

			var isVarying = dataTypeNode[Terminals.Varying] != null;

			string name;
			switch (dataTypeNode.FirstTerminalNode.Id)
			{
				case Terminals.Double:
					name = TerminalValues.BinaryDouble;
					break;
				case Terminals.Long:
					name = dataTypeNode.ChildNodes.Count > 1 && dataTypeNode.ChildNodes[1].Id == Terminals.Raw
						? "LONG RAW"
						: TerminalValues.Long;
					break;
				case Terminals.Interval:
					var yearToMonthNode = dataTypeNode[NonTerminals.YearToMonthOrDayToSecond, NonTerminals.YearOrMonth];
					if (yearToMonthNode == null)
					{
						var dayToSecondNode = dataTypeNode[NonTerminals.YearToMonthOrDayToSecond, NonTerminals.YearOrMonth];
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
					name = isVarying ? TerminalValues.NVarchar2 : TerminalValues.NChar;
					break;
				case Terminals.Character:
					name = isVarying ? TerminalValues.Varchar2 : TerminalValues.Char;
					break;
				default:
					name = dataTypeNode.FirstTerminalNode.Token.Value.ToUpperInvariant();
					break;
			}

			dataType.FullyQualifiedName = OracleObjectIdentifier.Create(owner, name);

			var simplePrecisionNode = dataTypeNode.GetSingleDescendant(NonTerminals.DataTypeSimplePrecision);
			if (simplePrecisionNode != null)
			{
				var simplePrecisionValueTerminal = simplePrecisionNode[Terminals.IntegerLiteral];
				if (simplePrecisionValueTerminal != null)
				{
					dataType.Length = Convert.ToInt32(simplePrecisionValueTerminal.Token.Value);
				}
			}

			TryResolveVarcharDetails(dataType, dataTypeNode);

			TryResolveNumericPrecisionAndScale(dataTypeNode, dataType);

			return dataType;
		}

		private static void TryResolveNumericPrecisionAndScale(StatementGrammarNode definitionNode, OracleDataType dataType)
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
			
			dataType.Precision = Convert.ToInt32(precisionValueTerminal.Token.Value);

			var scaleValueTerminal = numericPrecisionScaleNode[NonTerminals.Scale, Terminals.IntegerLiteral];
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
			
			var valueTerminal = varyingCharacterSimplePrecisionNode[Terminals.IntegerLiteral];
			if (valueTerminal == null)
			{
				return;
			}
			
			dataType.Length = Convert.ToInt32(valueTerminal.Token.Value);

			var byteOrCharNode = varyingCharacterSimplePrecisionNode[NonTerminals.ByteOrChar];
			if (byteOrCharNode != null)
			{
				dataType.Unit = byteOrCharNode.FirstTerminalNode.Id == Terminals.Byte ? DataUnit.Byte : DataUnit.Character;
			}
		}
	}
}
