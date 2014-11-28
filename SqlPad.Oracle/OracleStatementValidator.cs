using System;
using System.Collections.Generic;
using System.Linq;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		public IStatementSemanticModel BuildSemanticModel(string statementText, StatementBase statementBase, IDatabaseModel databaseModel)
		{
			return new OracleStatementSemanticModel(statementText, (OracleStatement)statementBase, (OracleDatabaseModelBase)databaseModel);
		}

		public IValidationModel BuildValidationModel(IStatementSemanticModel semanticModel)
		{
			if (semanticModel == null)
				throw new ArgumentNullException("semanticModel");

			var oracleSemanticModel = (OracleStatementSemanticModel)semanticModel;

			var validationModel = new OracleValidationModel { SemanticModel = oracleSemanticModel };

			var mainObjectReference = oracleSemanticModel.MainObjectReferenceContainer.MainObjectReference;
			var mainObjectReferences = mainObjectReference == null
				? Enumerable.Empty<OracleDataObjectReference>()
				: Enumerable.Repeat(mainObjectReference, 1);

			var objectReferences = oracleSemanticModel.QueryBlocks.SelectMany(qb => qb.ObjectReferences)
				.Concat(mainObjectReferences)
				.Concat(oracleSemanticModel.InsertTargets.SelectMany(t => t.ObjectReferences))
				.Where(tr => tr.Type.In(ReferenceType.CommonTableExpression, ReferenceType.SchemaObject));
			
			foreach (var objectReference in objectReferences)
			{
				if (objectReference.Type == ReferenceType.CommonTableExpression)
				{
					validationModel.ObjectNodeValidity[objectReference.ObjectNode] = new NodeValidationData { IsRecognized = true };
					continue;
				}

				if (objectReference.DatabaseLinkNode == null)
				{
					if (objectReference.OwnerNode != null)
					{
						var isRecognized = !semanticModel.IsSimpleModel && ((OracleDatabaseModelBase)semanticModel.DatabaseModel).ExistsSchema(objectReference.OwnerNode.Token.Value);
						validationModel.ObjectNodeValidity[objectReference.OwnerNode] = new NodeValidationData { IsRecognized = isRecognized };
					}

					validationModel.ObjectNodeValidity[objectReference.ObjectNode] = new NodeValidationData { IsRecognized = objectReference.SchemaObject != null, Node = objectReference.ObjectNode };
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, objectReference);
				}
			}

			foreach (var referenceContainer in oracleSemanticModel.AllReferenceContainers)
			{
				ResolveContainerValidities(validationModel, referenceContainer);
			}

			var invalidIdentifiers = oracleSemanticModel.Statement.AllTerminals
				.Select(GetInvalidIdentifierValidationData)
				.Where(nv => nv != null);

			foreach (var nodeValidity in invalidIdentifiers)
			{
				validationModel.IdentifierNodeValidity[nodeValidity.Node] = nodeValidity;
			}

			foreach (var insertTarget in oracleSemanticModel.InsertTargets)
			{
				var dataObjectReference = insertTarget.DataObjectReference;
				var dataSourceSpecified = insertTarget.RowSource != null || insertTarget.ValueList != null;
				if (dataObjectReference != null && dataSourceSpecified &&
				    (dataObjectReference.Type == ReferenceType.InlineView ||
				     validationModel.ObjectNodeValidity[dataObjectReference.ObjectNode].IsRecognized))
				{
					var insertColumnCount = insertTarget.ColumnListNode == null
						? dataObjectReference.Columns.Count
						: insertTarget.ColumnListNode.GetDescendants(Terminals.Identifier).Count();

					var rowSourceColumnCount = insertTarget.RowSource == null
						? insertTarget.ValueList.GetDescendantsWithinSameQuery(NonTerminals.ExpressionOrOrDefaultValue).Count()
						: insertTarget.RowSource.Columns.Count(c => !c.IsAsterisk);

					if (insertColumnCount == rowSourceColumnCount)
					{
						continue;
					}

					if (insertTarget.ColumnListNode != null)
					{
						validationModel.ColumnNodeValidity[insertTarget.ColumnListNode] = new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) {IsRecognized = true, Node = insertTarget.ColumnListNode};
					}

					var sourceDataNode = insertTarget.ValueList ?? insertTarget.RowSource.SelectList;
					if (sourceDataNode != null)
					{
						validationModel.ColumnNodeValidity[sourceDataNode] = new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount) {IsRecognized = true, Node = sourceDataNode};
					}
				}
			}

			ValidateConcatenatedQueryBlocks(validationModel);

			return validationModel;
		}

		private void ValidateConcatenatedQueryBlocks(OracleValidationModel validationModel)
		{
			foreach (var queryBlock in validationModel.SemanticModel.QueryBlocks.Where(qb => qb.PrecedingConcatenatedQueryBlock == null && qb.FollowingConcatenatedQueryBlock != null))
			{
				var firstQueryBlockColumnCount = queryBlock.Columns.Count(c => !c.IsAsterisk);
				foreach (var concatenatedQueryBlock in queryBlock.AllFollowingConcatenatedQueryBlocks)
				{
					var concatenatedQueryBlockColumnCount = concatenatedQueryBlock.Columns.Count(c => !c.IsAsterisk);
					if (concatenatedQueryBlockColumnCount != firstQueryBlockColumnCount && concatenatedQueryBlockColumnCount > 0)
					{
						validationModel.InvalidNonTerminals[queryBlock.SelectList] = new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount);
						foreach (var invalidColumnCountQueryBlock in queryBlock.AllFollowingConcatenatedQueryBlocks)
						{
							validationModel.InvalidNonTerminals[invalidColumnCountQueryBlock.SelectList] = new InvalidNodeValidationData(OracleSemanticErrorType.InvalidColumnCount);
						}

						break;
					}
				}
			}
		}

		private void ResolveContainerValidities(OracleValidationModel validationModel, OracleReferenceContainer referenceContainer)
		{
			ResolveColumnNodeValidities(validationModel, referenceContainer.ColumnReferences);

			foreach (var functionReference in referenceContainer.ProgramReferences)
			{
				if (functionReference.DatabaseLinkNode == null)
				{
					ValidateLocalProgramReference(functionReference, validationModel);
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ProgramNodeValidity, functionReference);
				}
			}

			foreach (var typeReference in referenceContainer.TypeReferences)
			{
				if (typeReference.DatabaseLinkNode == null)
				{
					var semanticError = GetCompilationError(typeReference);
					var node = typeReference.ObjectNode;
					var targetTypeObject = typeReference.SchemaObject.GetTargetSchemaObject() as OracleTypeObject;
					if (semanticError == OracleSemanticErrorType.None && targetTypeObject != null &&
						targetTypeObject.TypeCode == OracleTypeBase.TypeCodeObject && targetTypeObject.Attributes.Count != typeReference.ParameterNodes.Count)
					{
						semanticError = OracleSemanticErrorType.InvalidParameterCount;
						node = typeReference.ParameterListNode;
					}

					validationModel.ProgramNodeValidity[node] = new InvalidNodeValidationData(semanticError) { IsRecognized = true, Node = node };
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ProgramNodeValidity, typeReference);
				}
			}

			foreach (var sequenceReference in referenceContainer.SequenceReferences)
			{
				if (sequenceReference.DatabaseLinkNode == null)
				{
					var semanticError = !sequenceReference.Placement.In(QueryBlockPlacement.None, QueryBlockPlacement.SelectList) ? OracleSemanticErrorType.ObjectCannotBeUsed : OracleSemanticErrorType.None;
					validationModel.ObjectNodeValidity[sequenceReference.ObjectNode] = new InvalidNodeValidationData(semanticError) { IsRecognized = true, Node = sequenceReference.ObjectNode };
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, sequenceReference);
				}
			}
		}

		private static void ValidateDatabaseLinkReference(IDictionary<StatementGrammarNode, INodeValidationData> nodeValidityDictionary, OracleReference databaseLinkReference)
		{
			var isRecognized = databaseLinkReference.DatabaseLink != null;
			foreach (var terminal in databaseLinkReference.DatabaseLinkNode.Terminals)
			{
				nodeValidityDictionary[terminal] = new InvalidNodeValidationData { IsRecognized = isRecognized, Node = terminal };	
			}
		}

		private void ValidateLocalProgramReference(OracleProgramReference programReference, OracleValidationModel validationModel)
		{
			var metadataFound = programReference.Metadata != null;
			var semanticError = OracleSemanticErrorType.None;
			var isRecognized = false;
			if (metadataFound)
			{
				isRecognized = true;
				if (programReference.ParameterListNode != null)
				{
					var maximumParameterCount = programReference.Metadata.MinimumArguments > 0 && programReference.Metadata.MaximumArguments == 0
						? Int32.MaxValue
						: programReference.Metadata.MaximumArguments;

					// TODO: Handle optional parameters
					var parameterListSemanticError = OracleSemanticErrorType.None;
					if ((programReference.ParameterNodes.Count < programReference.Metadata.MinimumArguments) ||
					    (programReference.ParameterNodes.Count > maximumParameterCount))
					{
						parameterListSemanticError = OracleSemanticErrorType.InvalidParameterCount;
					}
					else if (programReference.Metadata.DisplayType == OracleProgramMetadata.DisplayTypeNoParenthesis)
					{
						parameterListSemanticError = OracleSemanticErrorType.NonParenthesisFunction;
					}

					if (parameterListSemanticError != OracleSemanticErrorType.None)
					{
						validationModel.ProgramNodeValidity[programReference.ParameterListNode] = new InvalidNodeValidationData(parameterListSemanticError) { IsRecognized = true };
					}
				}
				else if (programReference.Metadata.MinimumArguments > 0)
				{
					semanticError = OracleSemanticErrorType.InvalidParameterCount;
				}
				else if (programReference.Metadata.DisplayType == OracleProgramMetadata.DisplayTypeParenthesis)
				{
					semanticError = OracleSemanticErrorType.MissingParenthesis;
				}

				if (programReference.AnalyticClauseNode != null && !programReference.Metadata.IsAnalytic)
				{
					validationModel.ProgramNodeValidity[programReference.AnalyticClauseNode] = new InvalidNodeValidationData(OracleSemanticErrorType.AnalyticClauseNotSupported) { IsRecognized = true, Node = programReference.AnalyticClauseNode };
				}
			}

			if (programReference.ObjectNode != null)
			{
				var packageSemanticError = GetCompilationError(programReference);
				validationModel.ProgramNodeValidity[programReference.ObjectNode] = new InvalidNodeValidationData(packageSemanticError) { IsRecognized = programReference.SchemaObject != null, Node = programReference.ObjectNode };
			}

			if (semanticError == OracleSemanticErrorType.None && isRecognized && !programReference.Metadata.IsPackageFunction && programReference.SchemaObject != null && !programReference.SchemaObject.IsValid)
			{
				semanticError = OracleSemanticErrorType.ObjectStatusInvalid;
			}

			validationModel.ProgramNodeValidity[programReference.FunctionIdentifierNode] = new InvalidNodeValidationData(semanticError) { IsRecognized = isRecognized, Node = programReference.FunctionIdentifierNode };
		}

		private string GetCompilationError(OracleProgramReferenceBase reference)
		{
			return reference.SchemaObject == null || reference.SchemaObject.IsValid
				? OracleSemanticErrorType.None
				: OracleSemanticErrorType.ObjectStatusInvalid;
		}

		private INodeValidationData GetInvalidIdentifierValidationData(StatementGrammarNode node)
		{
			if (!node.Id.IsIdentifierOrAlias())
				return null;

			var validationResult = ValidateIdentifier(node.Token.Value, node.Id == Terminals.BindVariableIdentifier);
			string errorMessage;
			if (node.Id == Terminals.XmlAlias)
			{
				errorMessage = validationResult.IsEmptyQuotedIdentifier
					? "XML alias length must be at least one character excluding quotes. "
					: null;
			}
			else
			{
				errorMessage = validationResult.ErrorMessage;
			}

			return String.IsNullOrEmpty(errorMessage)
				? null
				: new InvalidIdentifierNodeValidationData(errorMessage) { IsRecognized = true, Node = node };
		}

		public static bool IsValidBindVariableIdentifier(string identifier)
		{
			var validationResult = ValidateIdentifier(identifier, true);
			return validationResult.IsValid && (validationResult.IsNumericBindVariable || OracleSqlParser.IsValidIdentifier(identifier)) && !identifier.IsReservedWord();
		}

		private static IdentifierValidationResult ValidateIdentifier(string identifier, bool validateNumericBindVariable)
		{
			var trimmedIdentifier = identifier.Trim('"');
			var result = new IdentifierValidationResult();

			if (validateNumericBindVariable && trimmedIdentifier == identifier)
			{
				result.IsNumericBindVariable = trimmedIdentifier.All(Char.IsDigit);

				int bindVariableNumberIdentifier;
				if (result.IsNumericBindVariable && Int32.TryParse(trimmedIdentifier.Substring(0, trimmedIdentifier.Length > 5 ? 5 : trimmedIdentifier.Length), out bindVariableNumberIdentifier) && bindVariableNumberIdentifier > 65535)
				{
					result.ErrorMessage = "Numeric bind variable identifier must be between 0 and 65535. ";
				}
			}

			result.IsEmptyQuotedIdentifier = trimmedIdentifier.Length == 0;
			if (String.IsNullOrEmpty(result.ErrorMessage) && result.IsEmptyQuotedIdentifier || trimmedIdentifier.Length > 30)
			{
				result.ErrorMessage = "Identifier length must be between one and 30 characters excluding quotes. ";
			}

			return result;
		} 

		private void ResolveColumnNodeValidities(OracleValidationModel validationModel, IEnumerable<OracleColumnReference> columnReferences)
		{
			foreach (var columnReference in columnReferences.Where(columnReference => columnReference.SelectListColumn == null || columnReference.SelectListColumn.HasExplicitDefinition))
			{
				if (columnReference.DatabaseLinkNode == null)
				{
					// Schema
					if (columnReference.OwnerNode != null)
						validationModel.ObjectNodeValidity[columnReference.OwnerNode] =
							new NodeValidationData(columnReference.ObjectNodeObjectReferences)
							{
								IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0,
								Node = columnReference.OwnerNode
							};

					// Object
					if (columnReference.ObjectNode != null)
						validationModel.ObjectNodeValidity[columnReference.ObjectNode] =
							new NodeValidationData(columnReference.ObjectNodeObjectReferences)
							{
								IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0,
								Node = columnReference.ObjectNode
							};

					// Column
					validationModel.ColumnNodeValidity[columnReference.ColumnNode] =
						new ColumnNodeValidationData(columnReference)
						{
							IsRecognized = columnReference.SelectListColumn != null && columnReference.SelectListColumn.IsAsterisk || columnReference.ColumnNodeObjectReferences.Count > 0,
							Node = columnReference.ColumnNode
						};
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, columnReference);
				}
			}
		}

		private struct IdentifierValidationResult
		{
			public bool IsValid { get { return String.IsNullOrEmpty(ErrorMessage); } }
			
			public string ErrorMessage { get; set; }

			public bool IsNumericBindVariable { get; set; }
			
			public bool IsEmptyQuotedIdentifier { get; set; }
		}
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _objectNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _columnNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _programNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _identifierNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _invalidNonTerminals = new Dictionary<StatementGrammarNode, INodeValidationData>();

		IStatementSemanticModel IValidationModel.SemanticModel { get { return SemanticModel; } }

		public OracleStatementSemanticModel SemanticModel { get; set; }

		public StatementBase Statement { get { return SemanticModel.Statement; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> ObjectNodeValidity { get { return _objectNodeValidity; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> ColumnNodeValidity { get { return _columnNodeValidity; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> ProgramNodeValidity { get { return _programNodeValidity; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> IdentifierNodeValidity { get { return _identifierNodeValidity; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> InvalidNonTerminals { get { return _invalidNonTerminals; } }

		public IEnumerable<KeyValuePair<StatementGrammarNode, INodeValidationData>> GetNodesWithSemanticErrors()
		{
			return _columnNodeValidity
				.Concat(_objectNodeValidity)
				.Concat(_programNodeValidity)
				.Concat(_identifierNodeValidity)
				.Concat(_invalidNonTerminals)
				.Where(nv => nv.Value.SemanticErrorType != OracleSemanticErrorType.None)
				.Select(nv => new KeyValuePair<StatementGrammarNode, INodeValidationData>(nv.Key, nv.Value));
		}
	}

	public class NodeValidationData : INodeValidationData
	{
		private readonly HashSet<OracleObjectWithColumnsReference> _objectReferences;

		public NodeValidationData(OracleDataObjectReference objectReference) : this(Enumerable.Repeat(objectReference, 1))
		{
		}

		public NodeValidationData(IEnumerable<OracleObjectWithColumnsReference> objectReferences = null)
		{
			_objectReferences = new HashSet<OracleObjectWithColumnsReference>(objectReferences ?? Enumerable.Empty<OracleObjectWithColumnsReference>());
		}

		public bool IsRecognized { get; set; }

		public virtual string SemanticErrorType
		{
			get { return _objectReferences.Count >= 2 ? OracleSemanticErrorType.AmbiguousReference : OracleSemanticErrorType.None; }
		}

		public ICollection<OracleObjectWithColumnsReference> ObjectReferences { get { return _objectReferences; } }

		public ICollection<string> ObjectNames
		{
			get
			{
				return _objectReferences.Select(t => t.FullyQualifiedObjectName.ToString())
					.Where(n => !String.IsNullOrEmpty(n))
					.OrderByDescending(n => n)
					.ToArray();
			}
		}

		public StatementGrammarNode Node { get; set; }

		public virtual string ToolTipText
		{
			get
			{
				return SemanticErrorType == OracleSemanticErrorType.None
					? Node.Type == NodeType.NonTerminal
						? null
						: Node.Id
					: FormatToolTipWithObjectNames();
			}
		}

		private string FormatToolTipWithObjectNames()
		{
			var objectNames = ObjectNames;
			return String.Format("{0}{1}", SemanticErrorType, objectNames.Count == 0 ? null : String.Format(" ({0})", String.Join(", ", ObjectNames)));
		}
	}

	public class InvalidNodeValidationData : NodeValidationData
	{
		private readonly string _semanticError;

		public InvalidNodeValidationData(string semanticError = OracleSemanticErrorType.None)
		{
			_semanticError = semanticError;
		}

		public override string SemanticErrorType { get { return _semanticError; } }

		public override string ToolTipText
		{
			get { return _semanticError; }
		}
	}

	public class ColumnNodeValidationData : NodeValidationData
	{
		private readonly OracleColumnReference _columnReference;
		private readonly string[] _ambiguousColumnNames;

		public ColumnNodeValidationData(OracleColumnReference columnReference)
			: base(columnReference.ColumnNodeObjectReferences)
		{
			if (columnReference == null)
			{
				throw new ArgumentNullException("columnReference");
			}
			
			_columnReference = columnReference;

			if (_columnReference.SelectListColumn != null && _columnReference.SelectListColumn.IsAsterisk)
			{
				_ambiguousColumnNames = _columnReference.Owner
					.Columns.Where(c => !c.HasExplicitDefinition)
					.SelectMany(c => c.ColumnReferences)
					.Where(c => c.ColumnNodeColumnReferences.Count > 1 && ObjectReferencesEqual(_columnReference, c))
					.SelectMany(c => c.ColumnNodeColumnReferences)
					.Where(c => !String.IsNullOrEmpty(c.Name))
					.Select(c => c.Name.ToSimpleIdentifier())
					.Distinct()
					.ToArray();
			}
			else
			{
				_ambiguousColumnNames = new string[0];
			}
		}

		private static bool ObjectReferencesEqual(OracleReference asteriskColumnReference, OracleColumnReference implicitColumnReference)
		{
			return asteriskColumnReference.ObjectNodeObjectReferences.Count != 1 || implicitColumnReference.ColumnNodeObjectReferences.Count != 1 ||
				   asteriskColumnReference.ObjectNodeObjectReferences.First() == implicitColumnReference.ColumnNodeObjectReferences.First();
		}

		public ICollection<OracleColumn> ColumnNodeColumnReferences { get { return _columnReference.ColumnNodeColumnReferences; } }

		public override string SemanticErrorType
		{
			get
			{
				return _ambiguousColumnNames.Length > 0 || ColumnNodeColumnReferences.Count >= 2
					? OracleSemanticErrorType.AmbiguousReference
					: base.SemanticErrorType;
			}
		}

		public override string ToolTipText
		{
			get
			{
				var additionalInformation = _ambiguousColumnNames.Length > 0
					? String.Format(" ({0})", String.Join(", ", _ambiguousColumnNames))
					: String.Empty;

				return _ambiguousColumnNames.Length > 0 && ObjectReferences.Count <= 1
					? OracleSemanticErrorType.AmbiguousReference + additionalInformation
					: base.ToolTipText;
			}
		}
	}

	public class InvalidIdentifierNodeValidationData : NodeValidationData
	{
		private readonly string _toolTipText;

		public InvalidIdentifierNodeValidationData(string toolTipText)
		{
			_toolTipText = toolTipText;
		}

		public override string SemanticErrorType { get { return OracleSemanticErrorType.InvalidIdentifier; } }

		public override string ToolTipText
		{
			get { return _toolTipText; }
		}
	}
}
