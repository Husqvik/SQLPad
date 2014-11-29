using System;
using System.Collections.Generic;
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

			var nodeSemanticError = validationModel.GetNodesWithSemanticError()
				.Concat(validationModel.GetNodesWithSuggestion())
				.FirstOrDefault(n => node.HasAncestor(n.Key, true));
			
			if (nodeSemanticError.Key != null)
			{
				tip = nodeSemanticError.Value.ToolTipText;
			}
			else
			{
				var semanticModel = validationModel.SemanticModel;
				var queryBlock = semanticModel.GetQueryBlock(node);

				switch (node.Id)
				{
					case Terminals.ObjectIdentifier:
						var objectReference = GetObjectReference(queryBlock, semanticModel, node);
						var schemaObject = GetSchemaObject(queryBlock, node);
						if (objectReference != null)
						{
							return BuildObjectTooltip(semanticModel.DatabaseModel, objectReference);
						}
						
						if (schemaObject != null)
						{
							tip = GetFullSchemaObjectToolTip(schemaObject);
						}
						else
						{
							return null;
						}

						break;
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
							var typeToolTip = GetTypeToolTip(semanticModel, node);
							if (functionToolTip != null)
							{
								tip = functionToolTip;
							}
							else if (typeToolTip != null)
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
			if (!isSchemaObject || targetSchemaObject.Type != OracleSchemaObjectType.Table)
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

		private IToolTip BuildObjectTooltip(OracleDatabaseModelBase databaseModel, OracleObjectWithColumnsReference objectReference)
		{
			string simpleToolTip;
			if (objectReference.Type == ReferenceType.SchemaObject)
			{
				simpleToolTip = GetFullSchemaObjectToolTip(objectReference.SchemaObject);
				if (String.IsNullOrEmpty(simpleToolTip))
				{
					return null;
				}

				IToolTip toolTip = new ToolTipObject { DataContext = simpleToolTip };
				var schemaObject = objectReference.SchemaObject.GetTargetSchemaObject();
				if (schemaObject != null)
				{
					switch (schemaObject.Type)
					{
						case OracleSchemaObjectType.Table:
							var dataModel = new TableDetailsModel { Title = simpleToolTip };
							databaseModel.UpdateTableDetailsAsync(schemaObject.FullyQualifiedName, dataModel, CancellationToken.None);
							return new ToolTipTable(dataModel);
						case OracleSchemaObjectType.Sequence:
							return new ToolTipSequence(simpleToolTip, (OracleSequence)schemaObject);
					}
				}

				return toolTip;
			}

			if (objectReference.Type == ReferenceType.TableCollection)
			{
				simpleToolTip = GetFullSchemaObjectToolTip(objectReference.SchemaObject);
			}
			else
			{
				simpleToolTip = objectReference.FullyQualifiedObjectName + " (" + objectReference.Type.ToCategoryLabel() + ")";
			}
			
			return new ToolTipObject { DataContext = simpleToolTip };
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

			return schemaObject.FullyQualifiedName + " (" + CultureInfo.InvariantCulture.TextInfo.ToTitleCase(schemaObject.Type.ToLower()) + ")";
		}

		private string GetFunctionToolTip(OracleStatementSemanticModel semanticModel, StatementGrammarNode terminal)
		{
			var functionReference = semanticModel.GetProgramReference(terminal);
			return functionReference == null || functionReference.DatabaseLinkNode != null || functionReference.Metadata == null ? null : functionReference.Metadata.Identifier.FullyQualifiedIdentifier;
		}

		private OracleSchemaObject GetSchemaObject(OracleQueryBlock queryBlock, StatementGrammarNode terminal)
		{
			if (queryBlock == null)
			{
				return null;
			}

			return queryBlock.AllProgramReferences
				.Cast<OracleReference>()
				.Concat(queryBlock.AllSequenceReferences)
				.Where(f => f.ObjectNode == terminal)
				.Select(f => f.SchemaObject)
				.FirstOrDefault();
		}

		private OracleObjectWithColumnsReference GetObjectReference(OracleQueryBlock queryBlock, OracleStatementSemanticModel semanticModel, StatementGrammarNode terminal)
		{
			if (queryBlock == null)
			{
				if (semanticModel.MainObjectReferenceContainer.MainObjectReference != null && semanticModel.MainObjectReferenceContainer.MainObjectReference.ObjectNode == terminal)
				{
					return semanticModel.MainObjectReferenceContainer.MainObjectReference;
				}

				var insertValuesObjectReference = GetObjectReferenceFromColumn(semanticModel.InsertTargets.SelectMany(t => t.ColumnReferences), terminal);

				return insertValuesObjectReference ?? semanticModel.InsertTargets
					.Select(t => t.DataObjectReference)
					.SingleOrDefault(o => o != null && o.ObjectNode == terminal);
			}

			var objectReference = GetObjectReferenceFromColumn(queryBlock.AllColumnReferences, terminal);

			objectReference = objectReference ?? queryBlock.ObjectReferences.FirstOrDefault(o => o.ObjectNode == terminal);
			return objectReference == null || objectReference.DatabaseLinkNode != null
				? null
				: objectReference;
		}

		private OracleObjectWithColumnsReference GetObjectReferenceFromColumn(IEnumerable<OracleColumnReference> columnReferences, StatementGrammarNode objectIdentifier)
		{
			return columnReferences.Where(c => c.ObjectNode == objectIdentifier && c.ValidObjectReference != null)
					.Select(c => c.ValidObjectReference)
					.FirstOrDefault();
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
