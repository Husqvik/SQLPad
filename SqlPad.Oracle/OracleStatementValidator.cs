using System;
using System.Collections.Generic;
using System.Linq;

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

			var oracleDatabaseModel = (OracleDatabaseModelBase)semanticModel.DatabaseModel;
			var oracleSemanticModel = (OracleStatementSemanticModel)semanticModel;

			var validationModel = new OracleValidationModel { SemanticModel = oracleSemanticModel };

			var mainObjectReferences = oracleSemanticModel.MainObjectReferenceContainer.MainObjectReference == null
				? Enumerable.Empty<OracleDataObjectReference>()
				: Enumerable.Repeat(oracleSemanticModel.MainObjectReferenceContainer.MainObjectReference, 1);

			var objectReferences = oracleSemanticModel.QueryBlocks.SelectMany(qb => qb.ObjectReferences)
				.Concat(mainObjectReferences)
				.Where(tr => tr.Type != ReferenceType.InlineView);
			
			foreach (var objectReference in objectReferences)
			{
				if (objectReference.Type == ReferenceType.CommonTableExpression)
				{
					validationModel.ObjectNodeValidity[objectReference.ObjectNode] = new NodeValidationData { IsRecognized = true };
					continue;
				}

				if (objectReference.OwnerNode != null)
				{
					validationModel.ObjectNodeValidity[objectReference.OwnerNode] = new NodeValidationData { IsRecognized = oracleDatabaseModel.ExistsSchema(objectReference.OwnerNode.Token.Value) };
				}

				if (objectReference.DatabaseLinkNode == null)
				{
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

			return validationModel;
		}

		private void ResolveContainerValidities(OracleValidationModel validationModel, OracleReferenceContainer referenceContainer)
		{
			ResolveColumnNodeValidities(validationModel, referenceContainer.ColumnReferences);

			foreach (var functionReference in referenceContainer.ProgramReferences)
			{
				if (functionReference.DatabaseLinkNode == null)
				{
					ValidateLocalFunctionReference(functionReference, validationModel);
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
					var semanticError = GetCompilationEror(typeReference);
					validationModel.ProgramNodeValidity[typeReference.ObjectNode] = new ProgramValidationData(semanticError) { IsRecognized = true, Node = typeReference.ObjectNode };
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
					var semanticError = sequenceReference.SelectListColumn == null ? OracleSemanticErrorType.ObjectCannotBeUsed : OracleSemanticErrorType.None;
					validationModel.ObjectNodeValidity[sequenceReference.ObjectNode] = new ProgramValidationData(semanticError) { IsRecognized = true, Node = sequenceReference.ObjectNode };
				}
				else
				{
					ValidateDatabaseLinkReference(validationModel.ObjectNodeValidity, sequenceReference);
				}
			}
		}

		private static void ValidateDatabaseLinkReference(IDictionary<StatementGrammarNode, INodeValidationData> nodeValidityDictionary, OracleReference databaseLinkReference)
		{
			nodeValidityDictionary[databaseLinkReference.DatabaseLinkNode] = new ProgramValidationData { IsRecognized = databaseLinkReference.DatabaseLink != null, Node = databaseLinkReference.DatabaseLinkNode };
		}

		private void ValidateLocalFunctionReference(OracleProgramReference functionReference, OracleValidationModel validationModel)
		{
			var metadataFound = functionReference.Metadata != null;
			var semanticError = OracleSemanticErrorType.None;
			var isRecognized = false;
			if (metadataFound)
			{
				isRecognized = true;
				if (functionReference.ParameterListNode != null)
				{
					var maximumParameterCount = functionReference.Metadata.MinimumArguments > 0 && functionReference.Metadata.MaximumArguments == 0
						? Int32.MaxValue
						: functionReference.Metadata.MaximumArguments;

					// TODO: Handle optional parameters
					var parameterListSemanticError = OracleSemanticErrorType.None;
					if ((functionReference.ParameterNodes.Count < functionReference.Metadata.MinimumArguments) ||
					    (functionReference.ParameterNodes.Count > maximumParameterCount))
					{
						parameterListSemanticError = OracleSemanticErrorType.InvalidParameterCount;
					}
					else if (functionReference.Metadata.DisplayType == OracleFunctionMetadata.DisplayTypeNoParenthesis)
					{
						parameterListSemanticError = OracleSemanticErrorType.NonParenthesisFunction;
					}

					if (parameterListSemanticError != OracleSemanticErrorType.None)
					{
						validationModel.ProgramNodeValidity[functionReference.ParameterListNode] = new ProgramValidationData(parameterListSemanticError) { IsRecognized = true };
					}
				}
				else if (functionReference.Metadata.MinimumArguments > 0)
				{
					semanticError = OracleSemanticErrorType.InvalidParameterCount;
				}
				else if (functionReference.Metadata.DisplayType == OracleFunctionMetadata.DisplayTypeParenthesis)
				{
					semanticError = OracleSemanticErrorType.MissingParenthesis;
				}

				if (functionReference.AnalyticClauseNode != null && !functionReference.Metadata.IsAnalytic)
				{
					validationModel.ProgramNodeValidity[functionReference.AnalyticClauseNode] = new ProgramValidationData(OracleSemanticErrorType.AnalyticClauseNotSupported) { IsRecognized = true, Node = functionReference.AnalyticClauseNode };
				}
			}

			if (functionReference.ObjectNode != null)
			{
				var packageSemanticError = GetCompilationEror(functionReference);
				validationModel.ProgramNodeValidity[functionReference.ObjectNode] = new ProgramValidationData(packageSemanticError) { IsRecognized = functionReference.SchemaObject != null, Node = functionReference.ObjectNode };
			}

			if (semanticError == OracleSemanticErrorType.None && isRecognized && !functionReference.Metadata.IsPackageFunction && functionReference.SchemaObject != null && !functionReference.SchemaObject.IsValid)
			{
				semanticError = OracleSemanticErrorType.ObjectStatusInvalid;
			}

			validationModel.ProgramNodeValidity[functionReference.FunctionIdentifierNode] = new ProgramValidationData(semanticError) { IsRecognized = isRecognized, Node = functionReference.FunctionIdentifierNode };
		}

		private string GetCompilationEror(OracleProgramReferenceBase reference)
		{
			return reference.SchemaObject != null && reference.SchemaObject.IsValid
				? OracleSemanticErrorType.None
				: OracleSemanticErrorType.ObjectStatusInvalid;
		}

		private INodeValidationData GetInvalidIdentifierValidationData(StatementGrammarNode node)
		{
			if (!node.Id.IsIdentifierOrAlias())
				return null;

			var trimmedIdentifier = node.Token.Value.Trim('"');

			var errorMessage = String.Empty;
			if (node.Id == OracleGrammarDescription.Terminals.BindVariableIdentifier && trimmedIdentifier == node.Token.Value)
			{
				int bindVariableNumberIdentifier;
				if (Int32.TryParse(trimmedIdentifier.Substring(0, trimmedIdentifier.Length > 5 ? 5 : trimmedIdentifier.Length), out bindVariableNumberIdentifier) && bindVariableNumberIdentifier > 65535)
				{
					errorMessage = "Numeric bind variable identifier must be between 0 and 65535. ";
				}
			}

			if (String.IsNullOrEmpty(errorMessage) && trimmedIdentifier.Length > 0 && trimmedIdentifier.Length <= 30)
			{
				return null;
			}

			if (String.IsNullOrEmpty(errorMessage))
			{
				errorMessage = "Identifier length must be between one and 30 characters excluding quotes. ";
			}

			return new InvalidIdentifierNodeValidationData(errorMessage) { IsRecognized = true, Node = node };
		}

		private void ResolveColumnNodeValidities(OracleValidationModel validationModel, IEnumerable<OracleColumnReference> columnReferences)
		{
			foreach (var columnReference in columnReferences.Where(columnReference => columnReference.SelectListColumn == null || columnReference.SelectListColumn.ExplicitDefinition))
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
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _objectNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _columnNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _programNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();
		private readonly Dictionary<StatementGrammarNode, INodeValidationData> _identifierNodeValidity = new Dictionary<StatementGrammarNode, INodeValidationData>();

		public IStatementSemanticModel SemanticModel { get; set; }

		public StatementBase Statement { get { return SemanticModel.Statement; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> ObjectNodeValidity { get { return _objectNodeValidity; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> ColumnNodeValidity { get { return _columnNodeValidity; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> ProgramNodeValidity { get { return _programNodeValidity; } }

		public IDictionary<StatementGrammarNode, INodeValidationData> IdentifierNodeValidity { get { return _identifierNodeValidity; } }

		public IEnumerable<KeyValuePair<StatementGrammarNode, INodeValidationData>> GetNodesWithSemanticErrors()
		{
			return ColumnNodeValidity
				.Concat(ObjectNodeValidity)
				.Concat(ProgramNodeValidity)
				.Concat(IdentifierNodeValidity)
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

	public class ProgramValidationData : NodeValidationData
	{
		private readonly string _semanticError;

		public ProgramValidationData(string semanticError = OracleSemanticErrorType.None)
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
					.Columns.Where(c => !c.ExplicitDefinition)
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

	public static class OracleSemanticErrorType
	{
		public const string None = null;
		public const string AmbiguousReference = "Ambiguous reference";
		public const string InvalidParameterCount = "Invalid parameter count";
		public const string MissingParenthesis = "Missing parenthesis";
		public const string NonParenthesisFunction = "Non-parenthesis function";
		public const string InvalidIdentifier = "Invalid identifier";
		public const string AnalyticClauseNotSupported = "Analytic clause not supported";
		public const string ObjectStatusInvalid = "Object is invalid or unusable";
		public const string ObjectCannotBeUsed = "Object cannot be used here";
	}
}
