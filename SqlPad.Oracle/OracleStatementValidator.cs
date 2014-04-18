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
				foreach (var cr in queryBlock.Columns
					.SelectMany(c => c.ColumnReferences.Select(r => new { Column = c, ColumnReference = r }))
					.Concat(queryBlock.ColumnReferences.Select(r => new { Column = (OracleSelectListColumn)null, ColumnReference = r })))
				{
					// Schema
					if (cr.ColumnReference.OwnerNode != null)
						validationModel.TableNodeValidity[cr.ColumnReference.OwnerNode] = new NodeValidationData(cr.ColumnReference.ObjectNodeObjectReferences) { IsRecognized = cr.ColumnReference.ObjectNodeObjectReferences.Count > 0 };

					// Object
					if (cr.ColumnReference.ObjectNode != null)
						validationModel.TableNodeValidity[cr.ColumnReference.ObjectNode] = new NodeValidationData(cr.ColumnReference.ObjectNodeObjectReferences) { IsRecognized = cr.ColumnReference.ObjectNodeObjectReferences.Count > 0 };

					// Column
					var columnReferences = cr.Column != null && cr.Column.IsAsterisk
						? 1
						: cr.ColumnReference.ColumnNodeObjectReferences.Count;

					validationModel.ColumnNodeValidity[cr.ColumnReference.ColumnNode] = new NodeValidationData(cr.ColumnReference.ColumnNodeObjectReferences) { IsRecognized = columnReferences > 0 };
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
}