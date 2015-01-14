using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using SqlPad.Oracle.ToolTips;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleToolTipProvider : IToolTipProvider
	{
		public IToolTip GetToolTip(SqlDocumentRepository sqlDocumentRepository, int cursorPosition)
		{
			if (sqlDocumentRepository == null)
				throw new ArgumentNullException("sqlDocumentRepository");

			var node = sqlDocumentRepository.Statements.GetNodeAtPosition(cursorPosition);
			if (node == null)
			{
				return null;
			}

			var tip = node.Type == NodeType.Terminal ? node.Id : null;

			var validationModel = (OracleValidationModel)sqlDocumentRepository.ValidationModels[node.Statement];

			var nodeSemanticError = validationModel.SemanticErrors
				.Concat(validationModel.Suggestions)
				.FirstOrDefault(v => node.HasAncestor(v.Node, true));
			
			if (nodeSemanticError != null)
			{
				tip = nodeSemanticError.ToolTipText;
			}
			else
			{
				var semanticModel = validationModel.SemanticModel;
				var queryBlock = semanticModel.GetQueryBlock(node);

				switch (node.Id)
				{
					case Terminals.ObjectIdentifier:
						var objectReference = GetObjectReference(semanticModel, node);
						return objectReference == null
							? null
							: BuildObjectTooltip(semanticModel.DatabaseModel, objectReference);

					case Terminals.Min:
					case Terminals.Max:
					case Terminals.Sum:
					case Terminals.Avg:
					case Terminals.FirstValue:
					case Terminals.Count:
					case Terminals.Variance:
					case Terminals.StandardDeviation:
					case Terminals.LastValue:
					case Terminals.Lead:
					case Terminals.Lag:
					case Terminals.ListAggregation:
					case Terminals.RowIdPseudoColumn:
					case Terminals.Identifier:
						var columnReference = semanticModel.GetColumnReference(node);
						if (columnReference == null)
						{
							var functionToolTip = GetFunctionToolTip(semanticModel, node);
							if (functionToolTip != null)
							{
								return functionToolTip;
							}

							var typeToolTip = GetTypeToolTip(semanticModel, node);
							if (typeToolTip != null)
							{
								tip = typeToolTip;
							}
							else
							{
								return null;
							}
						}
						else if (columnReference.ColumnDescription != null)
						{
							return BuildColumnToolTip(semanticModel.DatabaseModel, columnReference);
						}
						
						break;
					case Terminals.DatabaseLinkIdentifier:
						var databaseLink = GetDatabaseLink(queryBlock, node);
						if (databaseLink == null)
							return null;

						tip = databaseLink.FullyQualifiedName + " (" + databaseLink.Host + ")";

						break;
				}
			}

			return String.IsNullOrEmpty(tip) ? null : new ToolTipObject { DataContext = tip };
		}

		private static IToolTip BuildColumnToolTip(OracleDatabaseModelBase databaseModel, OracleColumnReference columnReference)
		{
			var validObjectReference = columnReference.ValidObjectReference;
			var isSchemaObject = validObjectReference.Type == ReferenceType.SchemaObject;
			var targetSchemaObject = isSchemaObject ? validObjectReference.SchemaObject.GetTargetSchemaObject() : null;
			if (!isSchemaObject || !targetSchemaObject.Type.In(OracleSchemaObjectType.Table, OracleSchemaObjectType.MaterializedView))
			{
				var objectPrefix = columnReference.ObjectNode == null && !String.IsNullOrEmpty(validObjectReference.FullyQualifiedObjectName.Name)
					? String.Format("{0}.", validObjectReference.FullyQualifiedObjectName)
					: null;

				var qualifiedColumnName = isSchemaObject && targetSchemaObject.Type == OracleSchemaObjectType.Sequence
					? null
					: String.Format("{0}{1} ", objectPrefix, columnReference.Name.ToSimpleIdentifier());
				
				var tip = String.Format("{0}{1} {2}{3}", qualifiedColumnName, columnReference.ColumnDescription.FullTypeName, columnReference.ColumnDescription.Nullable ? null : "NOT ", "NULL");
				return new ToolTipObject { DataContext = tip };
			}

			var tableOwner = targetSchemaObject.FullyQualifiedName;
			var dataModel =
				new ColumnDetailsModel
				{
					Owner = tableOwner.ToString(),
					Name = columnReference.Name.ToSimpleIdentifier(),
					Nullable = columnReference.ColumnDescription.Nullable,
					DataType = columnReference.ColumnDescription.FullTypeName,
				};

			databaseModel.UpdateColumnDetailsAsync(tableOwner, columnReference.ColumnDescription.Name, dataModel, CancellationToken.None);

			return new ToolTipColumn(dataModel);
		}

		private static string GetTypeToolTip(OracleStatementSemanticModel semanticModel, StatementGrammarNode node)
		{
			var typeReference = semanticModel.GetTypeReference(node);
			return typeReference == null || typeReference.DatabaseLinkNode != null ? null : GetFullSchemaObjectToolTip(typeReference.SchemaObject);
		}

		private IToolTip BuildObjectTooltip(OracleDatabaseModelBase databaseModel, OracleReference reference)
		{
			var simpleToolTip = GetFullSchemaObjectToolTip(reference.SchemaObject);
			var objectReference = reference as OracleObjectWithColumnsReference;
			if (objectReference != null)
			{
				if (objectReference.Type == ReferenceType.SchemaObject)
				{
					var schemaObject = objectReference.SchemaObject.GetTargetSchemaObject();
					if (schemaObject != null)
					{
						TableDetailsModel dataModel;

						switch (schemaObject.Type)
						{
							case OracleSchemaObjectType.MaterializedView:
								var materializedView = (OracleMaterializedView)schemaObject;
								dataModel = new MaterializedViewDetailsModel { MaterializedViewTitle = simpleToolTip, Title = GetObjectTitle(OracleObjectIdentifier.Create(materializedView.Owner, materializedView.TableName), OracleSchemaObjectType.Table), MaterializedView = materializedView };
								databaseModel.UpdateTableDetailsAsync(schemaObject.FullyQualifiedName, dataModel, CancellationToken.None);
								return new ToolTipMaterializedView { DataContext = dataModel };
							case OracleSchemaObjectType.Table:
								dataModel = new TableDetailsModel { Title = simpleToolTip };
								databaseModel.UpdateTableDetailsAsync(schemaObject.FullyQualifiedName, dataModel, CancellationToken.None);
								return new ToolTipTable { DataContext = dataModel };
							case OracleSchemaObjectType.Sequence:
								return new ToolTipSequence(simpleToolTip, (OracleSequence)schemaObject);
						}
					}
				}
				else if (objectReference.Type == ReferenceType.TableCollection)
				{
					simpleToolTip = GetFullSchemaObjectToolTip(objectReference.SchemaObject);
				}
				else
				{
					simpleToolTip = objectReference.FullyQualifiedObjectName + " (" + objectReference.Type.ToCategoryLabel() + ")";
				}
			}

			return String.IsNullOrEmpty(simpleToolTip)
				? null
				: new ToolTipObject {DataContext = simpleToolTip};
		}

		private static string GetFullSchemaObjectToolTip(OracleSchemaObject schemaObject)
		{
			string tip = null;
			var synonym = schemaObject as OracleSynonym;
			if (synonym != null)
			{
				tip = GetSchemaObjectToolTip(synonym) + " => ";
				schemaObject = synonym.SchemaObject;
			}

			return tip + GetSchemaObjectToolTip(schemaObject);
		}

		private static string GetSchemaObjectToolTip(OracleSchemaObject schemaObject)
		{
			if (schemaObject == null)
				return null;

			return GetObjectTitle(schemaObject.FullyQualifiedName, schemaObject.Type);
		}

		private static string GetObjectTitle(OracleObjectIdentifier schemaObjectIdentifier, string objectType)
		{
			return schemaObjectIdentifier + " (" + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(objectType.ToLower()) + ")";
		}

		private ToolTipProgram GetFunctionToolTip(OracleStatementSemanticModel semanticModel, StatementGrammarNode terminal)
		{
			var functionReference = semanticModel.GetProgramReference(terminal);
			return functionReference == null || functionReference.DatabaseLinkNode != null || functionReference.Metadata == null
				? null
				: new ToolTipProgram(functionReference.Metadata.Identifier.FullyQualifiedIdentifier, functionReference.Metadata);
		}

		private OracleReference GetObjectReference(OracleStatementSemanticModel semanticModel, StatementGrammarNode terminal)
		{
			var objectReference = semanticModel.GetReference<OracleReference>(terminal);
			var columnReference = objectReference as OracleColumnReference;
			if (columnReference != null)
			{
				objectReference = columnReference.ValidObjectReference;
			}

			return objectReference;
		}

		private OracleDatabaseLink GetDatabaseLink(OracleQueryBlock queryBlock, StatementGrammarNode terminal)
		{
			if (queryBlock == null)
			{
				return null;
			}

			var databaseLinkReference = queryBlock.DatabaseLinkReferences.SingleOrDefault(l => l.DatabaseLinkNode.Terminals.Contains(terminal));
			return databaseLinkReference == null ? null : databaseLinkReference.DatabaseLink;
		}
	}
}
