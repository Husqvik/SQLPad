using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	public class OracleStatementValidator : IStatementValidator
	{
		public IValidationModel ResolveReferences(string sqlText, IStatement statement, IDatabaseModel databaseModel)
		{
			var semanticModel = new OracleStatementSemanticModel(sqlText, (OracleStatement)statement);

			var validationModel = new OracleValidationModel();

			var physicalTables = new Dictionary<OracleTableReference, IDatabaseObject>();

			foreach (var tableReference in semanticModel.QueryBlocks.SelectMany(qb => qb.TableReferences).Where(tr => tr.Type != TableReferenceType.NestedQuery))
			{
				if (tableReference.Type == TableReferenceType.CommonTableExpression)
				{
					validationModel.TableNodeValidity[tableReference.TableNode] = true;
					continue;
				}

				var objectName = tableReference.TableNode.Token.Value;
				bool objectValid;
				if (tableReference.OwnerNode != null)
				{
					var owner = tableReference.OwnerNode.Token.Value;
					validationModel.TableNodeValidity[tableReference.OwnerNode] = databaseModel.Schemas.Any(s => s == owner.ToOracleIdentifier());
					objectValid = databaseModel.AllObjects.ContainsKey(OracleObjectIdentifier.Create(owner, objectName));
					
					if (objectValid)
						physicalTables[tableReference] = databaseModel.AllObjects[OracleObjectIdentifier.Create(owner, objectName)];
				}
				else
				{
					var currentSchemaObject = OracleObjectIdentifier.Create(databaseModel.CurrentSchema, objectName);
					var publicSchemaObject = OracleObjectIdentifier.Create(DatabaseModelFake.SchemaPublic, objectName);
					objectValid = databaseModel.AllObjects.ContainsKey(currentSchemaObject) || databaseModel.AllObjects.ContainsKey(publicSchemaObject);

					if (objectValid)
					{
						if (databaseModel.AllObjects.ContainsKey(currentSchemaObject))
							physicalTables[tableReference] = databaseModel.AllObjects[currentSchemaObject];
						else if (databaseModel.AllObjects.ContainsKey(publicSchemaObject))
							physicalTables[tableReference] = databaseModel.AllObjects[publicSchemaObject];
					}
				}

				validationModel.TableNodeValidity[tableReference.TableNode] = objectValid;
			}

			foreach (var queryBlock in semanticModel.QueryBlocks)
			{
				foreach (var columnReference in queryBlock.Columns.SelectMany(c => c.ColumnReferences))
				{
					if (columnReference.ReferencesAllColumns)
					{
						foreach (var exposedTableReference in queryBlock.ExposedTableReferences)
						{
							if (exposedTableReference.Type == TableReferenceType.PhysicalTable)
							{

							}
							else
							{

							}
						}
					}

					var tableReferences = queryBlock.TableReferences.Where(tr => tr.FullyQualifiedName == columnReference.FullyQualifiedObjectName).ToArray();
					if (tableReferences.Length == 0 && String.IsNullOrEmpty(columnReference.FullyQualifiedObjectName.Owner))
					{
						tableReferences = queryBlock.TableReferences.Where(tr => tr.Type == TableReferenceType.PhysicalTable && tr.FullyQualifiedName.NormalizedName == columnReference.FullyQualifiedObjectName.NormalizedName).ToArray();
					}

					var tableReferenceValid = tableReferences.Length == 1;

					if (columnReference.TableNode != null)
						validationModel.TableNodeValidity.Add(columnReference.TableNode, tableReferenceValid);

					if (columnReference.OwnerNode != null)
						validationModel.TableNodeValidity.Add(columnReference.OwnerNode, tableReferenceValid);

					// Column names
					var columnReferences = 0;
					var columnTableReferences = new List<OracleTableReference>();

					foreach (var tableReference in queryBlock.TableReferences)
					{
						if (columnReference.HasTableReference && !tableReferenceValid)
							continue;

						int newTableReferences = 0;
						if (tableReference.Type == TableReferenceType.PhysicalTable)
						{
							if (!physicalTables.ContainsKey(tableReference))
								continue;

							newTableReferences = physicalTables[tableReference].Columns
								.Count(c => c.Name == columnReference.Name && (!columnReference.HasTableReference || columnReference.TableName == tableReference.FullyQualifiedName.NormalizedName));

						}
						else
						{
							newTableReferences = tableReference.QueryBlocks.Single().Columns
								.Count(c => c.Name == columnReference.Name && (!columnReference.HasTableReference || columnReference.TableName == tableReference.FullyQualifiedName.NormalizedName));
						}

						if (newTableReferences > 0)
						{
							columnTableReferences.Add(tableReference);
							columnReferences += newTableReferences;
						}
					}

					validationModel.ColumnNodeValidity.Add(columnReference.ColumnNode, new ColumnValidationData(columnTableReferences) { IsValid = columnReferences == 1 });
				}
			}

			return validationModel;
		}

		private ICollection<OracleColumnReference> ResolveTableColumn(OracleTableReference tableReference)
		{
			return null;
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