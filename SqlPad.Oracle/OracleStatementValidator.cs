using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		public IValidationModel BuildValidationModel(string sqlText, StatementBase statement, IDatabaseModel databaseModel)
		{
			var oracleDatabaseModel = (OracleDatabaseModel)databaseModel;
			var semanticModel = new OracleStatementSemanticModel(sqlText, (OracleStatement)statement, oracleDatabaseModel);

			var validationModel = new OracleValidationModel { SemanticModel = semanticModel };

			foreach (var tableReference in semanticModel.QueryBlocks.SelectMany(qb => qb.ObjectReferences).Where(tr => tr.Type != TableReferenceType.InlineView))
			{
				if (tableReference.Type == TableReferenceType.CommonTableExpression)
				{
					validationModel.ObjectNodeValidity[tableReference.ObjectNode] = new NodeValidationData { IsRecognized = true };
					continue;
				}

				if (tableReference.OwnerNode != null)
				{
					validationModel.ObjectNodeValidity[tableReference.OwnerNode] = new NodeValidationData { IsRecognized = tableReference.SearchResult.SchemaFound };
				}

				validationModel.ObjectNodeValidity[tableReference.ObjectNode] = new NodeValidationData { IsRecognized = tableReference.SearchResult.SchemaObject != null };
			}

			foreach (var queryBlock in semanticModel.QueryBlocks)
			{
				foreach (var columnReference in queryBlock.AllColumnReferences)
				{
					// Schema
					if (columnReference.OwnerNode != null)
						validationModel.ObjectNodeValidity[columnReference.OwnerNode] = new NodeValidationData(columnReference.ObjectNodeObjectReferences) { IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0, Node = columnReference.OwnerNode };

					// Object
					if (columnReference.ObjectNode != null)
						validationModel.ObjectNodeValidity[columnReference.ObjectNode] = new NodeValidationData(columnReference.ObjectNodeObjectReferences) { IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0, Node = columnReference.ObjectNode };

					// Column
					var columnReferences = columnReference.SelectListColumn != null && columnReference.SelectListColumn.IsAsterisk
						? 1
						: columnReference.ColumnNodeObjectReferences.Count;

					validationModel.ColumnNodeValidity[columnReference.ColumnNode] = new ColumnNodeValidationData(columnReference.ColumnNodeObjectReferences, columnReference.ColumnNodeColumnReferences) { IsRecognized = columnReferences > 0, Node = columnReference.ColumnNode };
				}

				foreach (var functionReference in queryBlock.AllFunctionReferences)
				{
					var metadataFound = functionReference.Metadata != null;
					var semanticError = SemanticError.None;
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
							if ((functionReference.ParameterNodes.Count < functionReference.Metadata.MinimumArguments) ||
								(functionReference.ParameterNodes.Count > maximumParameterCount))
							{
								validationModel.FunctionNodeValidity[functionReference.ParameterListNode] = new FunctionValidationData(SemanticError.InvalidParameterCount) { IsRecognized = isRecognized };
							}
						}
						else if (functionReference.Metadata.MinimumArguments > 0)
						{
							semanticError = SemanticError.InvalidParameterCount;
						}
						else if (functionReference.Metadata.DisplayType == OracleFunctionMetadata.DisplayTypeParenthesis)
						{
							semanticError = SemanticError.MissingParenthesis;
						}
					}

					validationModel.FunctionNodeValidity[functionReference.FunctionIdentifierNode] = new FunctionValidationData(semanticError) { IsRecognized = isRecognized, Node = functionReference.FunctionIdentifierNode };
				}
			}

			return validationModel;
		}
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _objectNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _columnNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _functionNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();

		public OracleStatementSemanticModel SemanticModel { get; set; }

		public StatementBase Statement { get { return SemanticModel.Statement; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> ObjectNodeValidity { get { return _objectNodeValidity; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> ColumnNodeValidity { get { return _columnNodeValidity; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> FunctionNodeValidity { get { return _functionNodeValidity; } }

		public IEnumerable<KeyValuePair<StatementDescriptionNode, INodeValidationData>> GetNodesWithSemanticErrors()
		{
			return ColumnNodeValidity
				.Concat(ObjectNodeValidity)
				.Concat(FunctionNodeValidity)
				.Where(nv => nv.Value.SemanticError != SemanticError.None)
				.Select(nv => new KeyValuePair<StatementDescriptionNode, INodeValidationData>(nv.Key, nv.Value));
		}
	}

	public class NodeValidationData : INodeValidationData
	{
		private readonly HashSet<OracleObjectReference> _objectReferences;

		public NodeValidationData(OracleObjectReference objectReference) : this(Enumerable.Repeat(objectReference, 1))
		{
		}

		public NodeValidationData(IEnumerable<OracleObjectReference> objectReferences = null)
		{
			_objectReferences = new HashSet<OracleObjectReference>(objectReferences ?? Enumerable.Empty<OracleObjectReference>());
		}

		public bool IsRecognized { get; set; }

		public virtual SemanticError SemanticError
		{
			get { return _objectReferences.Count >= 2 ? SemanticError.AmbiguousReference : SemanticError.None; }
		}

		public ICollection<OracleObjectReference> ObjectReferences { get { return _objectReferences; } }

		public ICollection<string> ObjectNames { get { return _objectReferences.Select(t => t.FullyQualifiedName.ToString()).OrderByDescending(n => n).ToArray(); } }	
		
		public StatementDescriptionNode Node { get; set; }

		public virtual string ToolTipText
		{
			get
			{
				return SemanticError == SemanticError.None
					? Node.Type == NodeType.NonTerminal
						? null
						: Node.Id
					: SemanticError.ToToolTipText() + String.Format(" ({0})", String.Join(", ", ObjectNames));
			}
		}
	}

	public class FunctionValidationData : NodeValidationData
	{
		private readonly SemanticError _semanticError;

		public FunctionValidationData(SemanticError semanticError = SemanticError.None)
		{
			_semanticError = semanticError;
		}

		public override SemanticError SemanticError { get { return _semanticError; } }

		public override string ToolTipText
		{
			get { return _semanticError.ToToolTipText(); }
		}
	}

	public class ColumnNodeValidationData : NodeValidationData
	{
		public ColumnNodeValidationData(IEnumerable<OracleObjectReference> objectReferences, int columnNodeColumnReferences = 0)
			:base(objectReferences)
		{
			ColumnNodeColumnReferences = columnNodeColumnReferences;
		}

		public int ColumnNodeColumnReferences { get; private set; }

		public override SemanticError SemanticError
		{
			get { return ColumnNodeColumnReferences > 1 ? SemanticError.AmbiguousReference : base.SemanticError; }
		}
	}
}
