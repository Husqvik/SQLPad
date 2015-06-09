using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using SqlPad.Oracle.SemanticModel;
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

					case Terminals.Asterisk:
						return BuildAsteriskToolTip(queryBlock, node);
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
					case Terminals.CumulativeDistribution:
					case Terminals.Rank:
					case Terminals.DenseRank:
					case Terminals.PercentileDiscreteDistribution:
					case Terminals.PercentileContinuousDistribution:
					case Terminals.NegationOrNull:
					case Terminals.RowIdPseudoColumn:
					case Terminals.JsonQuery:
					case Terminals.JsonExists:
					case Terminals.JsonValue:
					case Terminals.XmlElement:
					case Terminals.XmlSerialize:
					case Terminals.XmlParse:
					case Terminals.XmlQuery:
					case Terminals.XmlRoot:
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

		private IToolTip BuildAsteriskToolTip(OracleQueryBlock queryBlock, StatementGrammarNode asteriskTerminal)
		{
			var asteriskColumn = queryBlock.AsteriskColumns.SingleOrDefault(c => c.RootNode.LastTerminalNode == asteriskTerminal);
			if (asteriskColumn == null)
			{
				return null;
			}

			var columns = queryBlock.Columns.Where(c => c.AsteriskColumn == asteriskColumn)
				.Select((c, i) =>
				{
					var validObjectReference = c.ColumnReferences.Single().ValidObjectReference;

					return
						new OracleColumnModel
						{
							Name = String.IsNullOrEmpty(c.ColumnDescription.Name)
								? OracleSelectListColumn.BuildNonAliasedColumnName(c.RootNode.Terminals)
								: c.ColumnDescription.Name,
							FullTypeName = c.ColumnDescription.FullTypeName,
							ColumnIndex = i + 1,
							RowSourceName = validObjectReference == null ? String.Empty : validObjectReference.FullyQualifiedObjectName.ToString()
						};
				}).ToArray();

			return columns.Length == 0
				? null
				: new ToolTipAsterisk
				{
					Columns = columns
				};
		}

		private static IToolTip BuildColumnToolTip(OracleDatabaseModelBase databaseModel, OracleColumnReference columnReference)
		{
			var validObjectReference = columnReference.ValidObjectReference;
			var isSchemaObject = validObjectReference.Type == ReferenceType.SchemaObject;
			var targetSchemaObject = isSchemaObject ? validObjectReference.SchemaObject.GetTargetSchemaObject() : null;

			if (isSchemaObject)
			{
				ColumnDetailsModel dataModel;
				switch (targetSchemaObject.Type)
				{
					case OracleSchemaObjectType.Table:
					case OracleSchemaObjectType.MaterializedView:
						dataModel = BuildColumnDetailsModel(databaseModel, columnReference);
						return columnReference.ColumnDescription.IsPseudoColumn
							? (IToolTip)new ToolTipViewColumn(dataModel)
							: new ToolTipColumn(dataModel);
							
					case OracleSchemaObjectType.View:
						dataModel = BuildColumnDetailsModel(databaseModel, columnReference);
						return new ToolTipViewColumn(dataModel);
				}
			}

			var objectPrefix = columnReference.ObjectNode == null && !String.IsNullOrEmpty(validObjectReference.FullyQualifiedObjectName.Name)
				? String.Format("{0}.", validObjectReference.FullyQualifiedObjectName)
				: null;

			var qualifiedColumnName = isSchemaObject && targetSchemaObject.Type == OracleSchemaObjectType.Sequence
				? null
				: String.Format("{0}{1} ", objectPrefix, columnReference.Name.ToSimpleIdentifier());

			var tip = String.Format("{0}{1} {2}{3}", qualifiedColumnName, columnReference.ColumnDescription.FullTypeName, columnReference.ColumnDescription.Nullable ? null : "NOT ", "NULL");
			return new ToolTipObject {DataContext = tip};
		}

		private static ColumnDetailsModel BuildColumnDetailsModel(OracleDatabaseModelBase databaseModel, OracleColumnReference columnReference)
		{
			var columnOwner = columnReference.ValidObjectReference.SchemaObject.GetTargetSchemaObject().FullyQualifiedName;

			var dataModel =
				new ColumnDetailsModel
				{
					Owner = columnOwner.ToString(),
					Name = columnReference.Name.ToSimpleIdentifier(),
					Nullable = columnReference.ColumnDescription.Nullable,
					Virtual = columnReference.ColumnDescription.Virtual,
					DataType = columnReference.ColumnDescription.FullTypeName,
					DefaultValue = BuildDefaultValuePreview(columnReference.ColumnDescription.DefaultValue)
				};

			databaseModel.UpdateColumnDetailsAsync(columnOwner, columnReference.ColumnDescription.Name, dataModel, CancellationToken.None);

			return dataModel;
		}

		private static string BuildDefaultValuePreview(string defaultValue)
		{
			if (String.IsNullOrEmpty(defaultValue))
			{
				return ConfigurationProvider.Configuration.ResultGrid.NullPlaceholder;
			}

			return defaultValue.Length < 256
				? defaultValue
				: String.Format("{0}{1}", defaultValue.Substring(0, 255), OracleLargeTextValue.Ellipsis);
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
								dataModel =
									new MaterializedViewDetailsModel
									{
										MaterializedViewTitle = simpleToolTip,
										Title = GetObjectTitle(OracleObjectIdentifier.Create(materializedView.Owner, materializedView.TableName), OracleSchemaObjectType.Table.ToLower()),
										MaterializedView = materializedView
									};

								SetPartitionKeys(dataModel, materializedView);
								
								databaseModel.UpdateTableDetailsAsync(schemaObject.FullyQualifiedName, dataModel, CancellationToken.None);
								return new ToolTipMaterializedView { DataContext = dataModel };
							case OracleSchemaObjectType.Table:
								dataModel = new TableDetailsModel { Title = simpleToolTip };
								SetPartitionKeys(dataModel, (OracleTable)schemaObject);
								
								databaseModel.UpdateTableDetailsAsync(schemaObject.FullyQualifiedName, dataModel, CancellationToken.None);
								return new ToolTipTable { DataContext = dataModel };
							case OracleSchemaObjectType.View:
								var viewDetailModel = new ViewDetailsModel { Title = simpleToolTip };
								databaseModel.UpdateViewDetailsAsync(schemaObject.FullyQualifiedName, viewDetailModel, CancellationToken.None);
								return new ToolTipView { DataContext = viewDetailModel };
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

			var partitionReference = reference as OraclePartitionReference;
			if (partitionReference != null && partitionReference.Partition != null)
			{
				var subPartition = partitionReference.Partition as OracleSubPartition;
				if (subPartition != null)
				{
					var subPartitionDetail = new SubPartitionDetailsModel();

					SetBasePartitionData(subPartitionDetail, partitionReference);

					databaseModel.UpdateSubPartitionDetailsAsync(subPartitionDetail, CancellationToken.None);
					return new ToolTipPartition(subPartitionDetail);
				}
				
				var partitionDetail = new PartitionDetailsModel(16);

				SetBasePartitionData(partitionDetail, partitionReference);
					
				databaseModel.UpdatePartitionDetailsAsync(partitionDetail, CancellationToken.None);
				return new ToolTipPartition(partitionDetail);
			}

			return String.IsNullOrEmpty(simpleToolTip)
				? null
				: new ToolTipObject { DataContext = simpleToolTip };
		}

		private static void SetBasePartitionData(PartitionDetailsModelBase dataModel, OraclePartitionReference partitionReference)
		{
			dataModel.Owner = partitionReference.DataObjectReference.SchemaObject.FullyQualifiedName;
			dataModel.Name = partitionReference.NormalizedName.Trim('"');
		}

		private static void SetPartitionKeys(TableDetailsModel tableDetails, OracleTable table)
		{
			tableDetails.PartitionKeys = String.Join(", ", table.PartitionKeyColumns.Select(c => c.ToSimpleIdentifier()));
			tableDetails.SubPartitionKeys = String.Join(", ", table.SubPartitionKeyColumns.Select(c => c.ToSimpleIdentifier()));
		}

		private static string GetFullSchemaObjectToolTip(OracleSchemaObject schemaObject)
		{
			string tip = null;
			var synonym = schemaObject as OracleSynonym;
			if (synonym != null)
			{
				tip = String.Format("{0} => ", GetSchemaObjectToolTip(synonym));
				schemaObject = synonym.SchemaObject;
			}

			return String.Format("{0}{1}", tip, GetSchemaObjectToolTip(schemaObject));
		}

		private static string GetSchemaObjectToolTip(OracleSchemaObject schemaObject)
		{
			return schemaObject == null
				? null
				: GetObjectTitle(schemaObject.FullyQualifiedName, GetObjectTypeLabel(schemaObject));
		}

		private static string GetObjectTypeLabel(OracleSchemaObject schemaObject)
		{
			switch (schemaObject.Type)
			{
				case OracleSchemaObjectType.Type:
					if (schemaObject is OracleTypeObject)
					{
						return "Object type";
					}

					var collection = (OracleTypeCollection)schemaObject;
					return collection.CollectionType == OracleCollectionType.Table
						? "Object table"
						: "Object varrying array";
				
				default:
					return schemaObject.Type.ToLower();
			}
		}

		private static string GetObjectTitle(OracleObjectIdentifier schemaObjectIdentifier, string objectType)
		{
			return String.Format("{0} ({1})", schemaObjectIdentifier, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(objectType));
		}

		private ToolTipProgram GetFunctionToolTip(OracleStatementSemanticModel semanticModel, StatementGrammarNode terminal)
		{
			var functionReference = semanticModel.GetProgramReference(terminal);
			if (functionReference == null || functionReference.DatabaseLinkNode != null || functionReference.Metadata == null)
			{
				return null;
			}

			var documentationBuilder = new StringBuilder();
			DocumentationFunction documentationFunction;
			DocumentationPackage documentationPackage;
			if ((String.IsNullOrEmpty(functionReference.Metadata.Identifier.Owner) || String.Equals(functionReference.Metadata.Identifier.Package, OracleDatabaseModelBase.PackageBuiltInFunction)) &&
				functionReference.Metadata.Type != ProgramType.StatementFunction && OracleHelpProvider.SqlFunctionDocumentation.TryGetValue(functionReference.Metadata.Identifier.Name, out documentationFunction))
			{
				documentationBuilder.AppendLine(documentationFunction.Value);
			}
			else if (!String.IsNullOrEmpty(functionReference.Metadata.Identifier.Package) && functionReference.Metadata.Owner.GetTargetSchemaObject() != null &&
			         OracleHelpProvider.PackageDocumentation.TryGetValue(functionReference.Metadata.Owner.GetTargetSchemaObject().FullyQualifiedName, out documentationPackage) &&
					 documentationPackage.SubPrograms != null)
			{
				var program = documentationPackage.SubPrograms.SingleOrDefault(sp => String.Equals(sp.Name, functionReference.Metadata.Identifier.Name));
				if (program != null)
				{
					documentationBuilder.AppendLine(program.Value);
				}
			}

			return new ToolTipProgram(functionReference.Metadata.Identifier.FullyQualifiedIdentifier, documentationBuilder.ToString(), functionReference.Metadata);
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
