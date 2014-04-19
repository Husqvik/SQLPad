using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		public IValidationModel ResolveReferences(string sqlText, StatementBase statement, IDatabaseModel databaseModel)
		{
			var oracleDatabaseModel = (DatabaseModelFake)databaseModel;
			var semanticModel = new OracleStatementSemanticModel(sqlText, (OracleStatement)statement, oracleDatabaseModel);

			var validationModel = new OracleValidationModel();

			foreach (var tableReference in semanticModel.QueryBlocks.SelectMany(qb => qb.ObjectReferences).Where(tr => tr.Type != TableReferenceType.NestedQuery))
			{
				if (tableReference.Type == TableReferenceType.CommonTableExpression)
				{
					validationModel.TableNodeValidity[tableReference.ObjectNode] = new NodeValidationData { IsRecognized = true };
					continue;
				}

				if (tableReference.OwnerNode != null)
				{
					validationModel.TableNodeValidity[tableReference.OwnerNode] = new NodeValidationData { IsRecognized = tableReference.SearchResult.SchemaFound };
				}

				validationModel.TableNodeValidity[tableReference.ObjectNode] = new NodeValidationData { IsRecognized = tableReference.SearchResult.SchemaObject != null };
			}

			foreach (var queryBlock in semanticModel.QueryBlocks)
			{
				foreach (var columnReference in queryBlock.AllColumnReferences)
				{
					// Schema
					if (columnReference.OwnerNode != null)
						validationModel.TableNodeValidity[columnReference.OwnerNode] = new NodeValidationData(columnReference.ObjectNodeObjectReferences) { IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0 };

					// Object
					if (columnReference.ObjectNode != null)
						validationModel.TableNodeValidity[columnReference.ObjectNode] = new NodeValidationData(columnReference.ObjectNodeObjectReferences) { IsRecognized = columnReference.ObjectNodeObjectReferences.Count > 0 };

					// Column
					var columnReferences = columnReference.SelectListColumn != null && columnReference.SelectListColumn.IsAsterisk
						? 1
						: columnReference.ColumnNodeObjectReferences.Count;

					validationModel.ColumnNodeValidity[columnReference.ColumnNode] = new ColumnNodeValidationData(columnReference.ColumnNodeObjectReferences, columnReference.ColumnNodeColumnReferences) { IsRecognized = columnReferences > 0 };
				}
			}

			return validationModel;
		}
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _tableNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();
		private readonly Dictionary<StatementDescriptionNode, INodeValidationData> _columnNodeValidity = new Dictionary<StatementDescriptionNode, INodeValidationData>();

		public IDictionary<StatementDescriptionNode, INodeValidationData> TableNodeValidity { get { return _tableNodeValidity; } }

		public IDictionary<StatementDescriptionNode, INodeValidationData> ColumnNodeValidity { get { return _columnNodeValidity; } }
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

		public ICollection<string> TableNames { get { return _objectReferences.Select(t => t.FullyQualifiedName.Name).OrderByDescending(n => n).ToArray(); } }	
		
		public StatementDescriptionNode Node { get; set; }
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
			get { return ColumnNodeColumnReferences >= 2 ? SemanticError.AmbiguousReference : base.SemanticError; }
		}
	}
}