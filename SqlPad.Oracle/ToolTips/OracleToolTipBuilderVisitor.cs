using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;

namespace SqlPad.Oracle.ToolTips
{
	public class OracleToolTipBuilderVisitor : IOracleReferenceVisitor
	{
		private readonly StatementGrammarNode _terminal;

		public OracleToolTipBuilderVisitor(StatementGrammarNode terminal)
		{
			_terminal = terminal;
		}

		public IToolTip ToolTip { get; private set; }

		public void VisitColumnReference(OracleColumnReference columnReference)
		{
			if (columnReference.ColumnDescription == null)
			{
				return;
			}

			var validObjectReference = columnReference.ValidObjectReference;
			var isSchemaObject = validObjectReference.Type == ReferenceType.SchemaObject;
			var targetSchemaObject = isSchemaObject ? validObjectReference.SchemaObject.GetTargetSchemaObject() : null;
			var databaseModel = columnReference.Container.SemanticModel.DatabaseModel;

			if (isSchemaObject)
			{
				ColumnDetailsModel dataModel;
				switch (targetSchemaObject.Type)
				{
					case OracleSchemaObjectType.Table:
					case OracleSchemaObjectType.MaterializedView:
						dataModel = BuildColumnDetailsModel(databaseModel, columnReference);
						ToolTip = columnReference.ColumnDescription.IsPseudoColumn
							? (IToolTip)new ToolTipViewColumn(dataModel)
							: new ToolTipColumn(dataModel);

						return;

					case OracleSchemaObjectType.View:
						dataModel = BuildColumnDetailsModel(databaseModel, columnReference);
						ToolTip = new ToolTipViewColumn(dataModel);
						return;
				}
			}

			var objectPrefix = columnReference.ObjectNode == null && !String.IsNullOrEmpty(validObjectReference.FullyQualifiedObjectName.Name)
				? $"{validObjectReference.FullyQualifiedObjectName}."
				: null;

			var qualifiedColumnName = isSchemaObject && targetSchemaObject.Type == OracleSchemaObjectType.Sequence
				? null
				: $"{objectPrefix}{columnReference.Name.ToSimpleIdentifier()} ";

			var tip = $"{qualifiedColumnName}{columnReference.ColumnDescription.FullTypeName} {(columnReference.ColumnDescription.Nullable ? null : "NOT ")}{"NULL"}";
			ToolTip = new ToolTipObject { DataContext = tip };
		}

		public void VisitProgramReference(OracleProgramReference programReference)
		{
			if (programReference.DatabaseLinkNode != null || programReference.Metadata == null)
			{
				return;
			}

			if (programReference.ObjectNode == _terminal)
			{
				BuildSimpleToolTip(programReference.SchemaObject);
				return;
			}

			var documentationBuilder = new StringBuilder();
			DocumentationPackage documentationPackage;
			if ((String.IsNullOrEmpty(programReference.Metadata.Identifier.Owner) || String.Equals(programReference.Metadata.Identifier.Package, OracleDatabaseModelBase.PackageBuiltInFunction)) &&
			    programReference.Metadata.Type != ProgramType.StatementFunction && OracleHelpProvider.SqlFunctionDocumentation[programReference.Metadata.Identifier.Name].Any())
			{
				foreach (var documentationFunction in OracleHelpProvider.SqlFunctionDocumentation[programReference.Metadata.Identifier.Name])
				{
					if (documentationBuilder.Length > 0)
					{
						documentationBuilder.AppendLine();
					}

					documentationBuilder.AppendLine(documentationFunction.Value);
				}
			}
			else if (!String.IsNullOrEmpty(programReference.Metadata.Identifier.Package) && programReference.Metadata.Owner.GetTargetSchemaObject() != null &&
			         OracleHelpProvider.PackageDocumentation.TryGetValue(programReference.Metadata.Owner.GetTargetSchemaObject().FullyQualifiedName, out documentationPackage) &&
			         documentationPackage.SubPrograms != null)
			{
				var program = documentationPackage.SubPrograms.SingleOrDefault(sp => String.Equals(sp.Name, programReference.Metadata.Identifier.Name));
				if (program != null)
				{
					documentationBuilder.AppendLine(program.Value);
				}
			}

			ToolTip = new ToolTipProgram(programReference.Metadata.Identifier.FullyQualifiedIdentifier, documentationBuilder.ToString(), programReference.Metadata);
		}

		public void VisitTypeReference(OracleTypeReference typeReference)
		{
			BuildSimpleToolTip(typeReference.SchemaObject);
		}

		public void VisitSequenceReference(OracleSequenceReference sequenceReference)
		{
			var schemaObject = (OracleSequence)sequenceReference.SchemaObject.GetTargetSchemaObject();
			if (schemaObject == null)
			{
				return;
			}

			var toolTipText = GetFullSchemaObjectToolTip(sequenceReference.SchemaObject);
			ToolTip = new ToolTipSequence(toolTipText, schemaObject);
		}

		public void VisitTableCollectionReference(OracleTableCollectionReference tableCollectionReference)
		{
			BuildSimpleToolTip(tableCollectionReference.SchemaObject);
		}

		private void BuildSimpleToolTip(OracleSchemaObject schemaObject)
		{
			if (schemaObject == null)
			{
				return;
			}

			ToolTip =
				new ToolTipObject
				{
					DataContext = GetFullSchemaObjectToolTip(schemaObject)
				};
		}

		public void VisitPartitionReference(OraclePartitionReference partitionReference)
		{
			if (partitionReference.Partition == null)
			{
				return;
			}

			var databaseModel = partitionReference.Container.SemanticModel.DatabaseModel;
			var subPartition = partitionReference.Partition as OracleSubPartition;
			if (subPartition != null)
			{
				var subPartitionDetail = new SubPartitionDetailsModel();

				SetBasePartitionData(subPartitionDetail, partitionReference);

				databaseModel.UpdateSubPartitionDetailsAsync(subPartitionDetail, CancellationToken.None);
				ToolTip = new ToolTipPartition(subPartitionDetail);
			}
			else
			{
				var partitionDetail = new PartitionDetailsModel(16);

				SetBasePartitionData(partitionDetail, partitionReference);

				databaseModel.UpdatePartitionDetailsAsync(partitionDetail, CancellationToken.None);
				ToolTip = new ToolTipPartition(partitionDetail);
			}
		}

		public void VisitDataObjectReference(OracleDataObjectReference objectReference)
		{
			if (objectReference.Type == ReferenceType.SchemaObject)
			{
				var schemaObject = objectReference.SchemaObject.GetTargetSchemaObject();
				if (schemaObject == null)
				{
					return;
				}

				TableDetailsModel dataModel;

				var databaseModel = objectReference.Container.SemanticModel.DatabaseModel;
				var toolTipText = GetFullSchemaObjectToolTip(objectReference.SchemaObject);

				switch (schemaObject.Type)
				{
					case OracleSchemaObjectType.MaterializedView:
						var materializedView = (OracleMaterializedView)schemaObject;
						dataModel =
							new MaterializedViewDetailsModel
							{
								MaterializedViewTitle = toolTipText,
								Title = GetObjectTitle(OracleObjectIdentifier.Create(materializedView.Owner, materializedView.TableName), OracleSchemaObjectType.Table.ToLower()),
								MaterializedView = materializedView
							};

						SetPartitionKeys(dataModel, materializedView);

						databaseModel.UpdateTableDetailsAsync(schemaObject.FullyQualifiedName, dataModel, CancellationToken.None);
						ToolTip = new ToolTipMaterializedView { DataContext = dataModel };
						break;

					case OracleSchemaObjectType.Table:
						dataModel = new TableDetailsModel { Title = toolTipText };
						SetPartitionKeys(dataModel, (OracleTable)schemaObject);

						databaseModel.UpdateTableDetailsAsync(schemaObject.FullyQualifiedName, dataModel, CancellationToken.None);
						ToolTip = new ToolTipTable { DataContext = dataModel };
						break;

					case OracleSchemaObjectType.View:
						var viewDetailModel = new ViewDetailsModel { Title = toolTipText };
						databaseModel.UpdateViewDetailsAsync(schemaObject.FullyQualifiedName, viewDetailModel, CancellationToken.None);
						ToolTip = new ToolTipView { DataContext = viewDetailModel };
						break;

					case OracleSchemaObjectType.Sequence:
						ToolTip = new ToolTipSequence(toolTipText, (OracleSequence)schemaObject);
						break;
				}
			}
			else
			{
				ToolTip =
					new ToolTipObject
					{
						DataContext = objectReference.FullyQualifiedObjectName + " (" + objectReference.Type.ToCategoryLabel() + ")"
					};
			}
		}

		public void VisitDataTypeReference(OracleDataTypeReference dataTypeReference)
		{
			BuildSimpleToolTip(dataTypeReference.SchemaObject);
		}

		public void VisitPlSqlVariableReference(OraclePlSqlVariableReference plSqlVariableReference)
		{
			if (plSqlVariableReference.Variables.Count != 1)
			{
				return;
			}

			var element = plSqlVariableReference.Variables.First();
			var labelNullable = String.Empty;
			var labelVariableType = String.Empty;
			var labelDataType = String.Empty;
			var labelDefaultExpression = String.Empty;

			var elementTypeName = element.GetType().Name;
			switch (elementTypeName)
			{
				case nameof(OraclePlSqlParameter):
					labelVariableType = "Parameter";

					var parameter = (OraclePlSqlParameter)element;
					labelNullable = "NULL";
					labelDataType = GetDataTypeFromNode(parameter);
					labelDefaultExpression = GetDefaultExpression(plSqlVariableReference.PlSqlProgram, parameter);
					break;

				case nameof(OraclePlSqlVariable):
					var variable = (OraclePlSqlVariable)element;
					labelVariableType = variable.IsConstant ? "Constant" : "Variable";
					labelNullable = variable.Nullable ? "NULL" : "NOT NULL";
					labelDataType = GetDataTypeFromNode(variable);
					labelDefaultExpression = GetDefaultExpression(plSqlVariableReference.PlSqlProgram, variable);
					break;

				case nameof(OraclePlSqlType):
					labelVariableType = "Type";
					break;

				case nameof(OraclePlSqlException):
					labelVariableType = "Exception";
					break;
			}

			var defaultExpressionPostFix = String.IsNullOrEmpty(labelDefaultExpression)
				? String.Empty
				: $"= {labelDefaultExpression}";

			ToolTip =
				new ToolTipObject
				{
					DataContext = $"{labelVariableType} {element.Name.ToSimpleIdentifier()}: {labelDataType} {labelNullable} {defaultExpressionPostFix}".Trim()
				};
		}

		// TODO: Make proper resolution using metadata
		private static string GetDataTypeFromNode(OraclePlSqlVariable variable)
		{
			return variable.DataTypeNode == null
				? String.Empty
				: String.Join(null, variable.DataTypeNode.Terminals.Select(t => t.Token.Value));
		}

		private static string GetDefaultExpression(OracleReferenceContainer program, OraclePlSqlVariable variable)
		{
			return variable.DefaultExpression == null || !variable.IsConstant
				? String.Empty
				: variable.DefaultExpression.GetText(program.SemanticModel.StatementText);
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
					Invisible = columnReference.ColumnDescription.Hidden,
					Virtual = columnReference.ColumnDescription.Virtual,
					IsSystemGenerated = columnReference.ColumnDescription.UserGenerated == false,
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
				: $"{defaultValue.Substring(0, 255)}{OracleLargeTextValue.Ellipsis}";
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
				tip = $"{GetSchemaObjectToolTip(synonym)} => ";
				schemaObject = synonym.SchemaObject;
			}

			return $"{tip}{GetSchemaObjectToolTip(schemaObject)}";
		}

		private static string GetSchemaObjectToolTip(OracleSchemaObject schemaObject)
		{
			return schemaObject == null
				? null
				: GetObjectTitle(schemaObject.FullyQualifiedName, GetObjectTypeLabel(schemaObject));
		}

		private static string GetObjectTitle(OracleObjectIdentifier schemaObjectIdentifier, string objectType)
		{
			return $"{schemaObjectIdentifier} ({CultureInfo.InvariantCulture.TextInfo.ToTitleCase(objectType)})";
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
	}
}