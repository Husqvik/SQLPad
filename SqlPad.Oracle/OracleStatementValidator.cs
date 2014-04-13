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

			foreach (var tableReference in semanticModel.QueryBlocks.SelectMany(qb => qb.TableReferences).Where(tr => tr.Type != TableReferenceType.NestedQuery))
			{
				if (tableReference.Type == TableReferenceType.CommonTableExpression)
				{
					validationModel.TableNodeValidity[tableReference.TableNode] = true;
					continue;
				}

				if (tableReference.OwnerNode != null)
				{
					validationModel.TableNodeValidity[tableReference.OwnerNode] = tableReference.SearchResult.SchemaFound;
				}

				validationModel.TableNodeValidity[tableReference.TableNode] = tableReference.SearchResult.SchemaObject != null;
			}

			foreach (var queryBlock in semanticModel.QueryBlocks)
			{
				foreach (var cr in queryBlock.Columns
					.SelectMany(c => c.ColumnReferences.Select(r => new { Column = c, ColumnReference = r }))
					.Concat(queryBlock.ColumnReferences.Select(r => new { Column = (OracleSelectListColumn)null, ColumnReference = r })))
				{
					// Schema
					if (cr.ColumnReference.OwnerNode != null)
						validationModel.TableNodeValidity[cr.ColumnReference.OwnerNode] = cr.ColumnReference.ObjectNodeReferences.Count == 1;

					// Object
					if (cr.ColumnReference.ObjectNode != null)
						validationModel.TableNodeValidity[cr.ColumnReference.ObjectNode] = cr.ColumnReference.ObjectNodeReferences.Count == 1;

					// Column
					var columnReferences = cr.Column != null && cr.Column.IsAsterisk
						? 1
						: cr.ColumnReference.ColumnNodeReferences.Count;

					validationModel.ColumnNodeValidity[cr.ColumnReference.ColumnNode] = new ColumnValidationData(cr.ColumnReference.ColumnNodeReferences) { IsRecognized = columnReferences > 0 };
				}
			}

			return validationModel;
		}
	}

	public class OracleValidationModel : IValidationModel
	{
		private readonly Dictionary<StatementDescriptionNode, bool> _tableNodeValidity = new Dictionary<StatementDescriptionNode, bool>();
		private readonly Dictionary<StatementDescriptionNode, IColumnValidationData> _columnNodeValidity = new Dictionary<StatementDescriptionNode, IColumnValidationData>();

		public IDictionary<StatementDescriptionNode, bool> TableNodeValidity { get { return _tableNodeValidity; } }

		public IDictionary<StatementDescriptionNode, IColumnValidationData> ColumnNodeValidity { get { return _columnNodeValidity; } }
	}

	public class ColumnValidationData : IColumnValidationData
	{
		private readonly HashSet<OracleTableReference> _tableReferences;

		public ColumnValidationData(IEnumerable<OracleTableReference> tableReferences = null)
		{
			_tableReferences = new HashSet<OracleTableReference>(tableReferences ?? Enumerable.Empty<OracleTableReference>());
		}

		public StatementDescriptionNode ColumnNode { get; set; }
		
		public bool IsRecognized { get; set; }

		public SemanticError SemanticError
		{
			get { return _tableReferences.Count >= 2 ? SemanticError.AmbiguousTableReference : SemanticError.None; }
		}

		public ICollection<OracleTableReference> TableReferences { get { return _tableReferences; } }

		public ICollection<string> TableNames { get { return _tableReferences.Select(t => t.FullyQualifiedName.Name).OrderByDescending(n => n).ToArray(); } }
	}
}