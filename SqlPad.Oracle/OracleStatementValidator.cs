using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		public IValidationModel ResolveReferences(string sqlText, IStatement statement, IDatabaseModel databaseModel)
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
				foreach (var columnReference in queryBlock.Columns.SelectMany(c => c.ColumnReferences))
				{
					// Schema
					if (columnReference.OwnerNode != null)
						validationModel.TableNodeValidity.Add(columnReference.OwnerNode, columnReference.TableNodeReferences.Count == 1);

					// Object
					if (columnReference.TableNode != null)
						validationModel.TableNodeValidity.Add(columnReference.TableNode, columnReference.TableNodeReferences.Count == 1);

					// Column
					var columnReferences = columnReference.Owner.IsAsterisk
						? 1
						: columnReference.ColumnNodeReferences.Count;

					validationModel.ColumnNodeValidity.Add(columnReference.ColumnNode, new ColumnValidationData(columnReference.ColumnNodeReferences) { IsValid = columnReferences == 1 });
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
		
		public bool IsValid { get; set; }

		public ICollection<OracleTableReference> TableReferences { get { return _tableReferences; } }

		public ICollection<string> TableNames { get { return _tableReferences.Select(t => t.FullyQualifiedName.Name).OrderByDescending(n => n).ToArray(); } }
	}
}