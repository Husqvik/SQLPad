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
			if (programReference.ObjectNode == _terminal)
			{
				BuildSimpleToolTip(programReference.SchemaObject);
				return;
			}

			if (programReference.DatabaseLinkNode != null || programReference.Metadata == null)
			{
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

		public void VisitPlSqlVariableReference(OraclePlSqlVariableReference variableReference)
		{
			if (variableReference.Variables.Count != 1)
			{
				return;
			}

			var element = variableReference.Variables.First();
			var labelBuilder = new ToolTipLabelBuilder();

			var elementTypeName = element.GetType().Name;
			var elementName = $"{element.Name.ToSimpleIdentifier()}:";
			switch (elementTypeName)
			{
				case nameof(OraclePlSqlParameter):
					var parameter = (OraclePlSqlParameter)element;
					labelBuilder.AddElement("Parameter");
					labelBuilder.AddElement(elementName);
					labelBuilder.AddElement(GetDataTypeFromNode(parameter));
					labelBuilder.AddElement("NULL");
					labelBuilder.AddElement(GetDefaultExpression(variableReference.PlSqlProgram, parameter), "= ");
					break;

				case nameof(OraclePlSqlVariable):
					var variable = (OraclePlSqlVariable)element;
					labelBuilder.AddElement(variable.IsConstant ? "Constant" : "Variable");
					labelBuilder.AddElement(elementName);
					var dataTypeName = variable.DataType == null ? GetDataTypeFromNode(variable) : OracleDataType.ResolveFullTypeName(variable.DataType);
					labelBuilder.AddElement(dataTypeName);
					labelBuilder.AddElement(variable.Nullable ? "NULL" : "NOT NULL");
					labelBuilder.AddElement(GetDefaultExpression(variableReference.PlSqlProgram, variable), "= ");
					break;

				case nameof(OraclePlSqlType):
					labelBuilder.AddElement("Type");
					labelBuilder.AddElement(elementName);
					break;

				case nameof(OraclePlSqlCursorVariable):
					labelBuilder.AddElement("Cursor");
					labelBuilder.AddElement(elementName);

					var cursor = (OraclePlSqlCursorVariable)element;
					if (cursor.SemanticModel != null)
					{
						var queryText = cursor.SemanticModel.Statement.RootNode.GetText(variableReference.Container.SemanticModel.StatementText);
						labelBuilder.AddElement(queryText);
					}

					break;
			}

			ToolTip = new ToolTipObject { DataContext = labelBuilder.ToString() };
		}

		public void VisitPlSqlExceptionReference(OraclePlSqlExceptionReference exceptionReference)
		{
			if (exceptionReference.Exceptions.Count != 1)
			{
				return;
			}

			var exception = exceptionReference.Exceptions.First();
			var initializationPostfix = exception.ErrorCode.HasValue ? $"(Error code {exception.ErrorCode})" : null;

			ToolTip =
				new ToolTipObject
				{
					DataContext = $"Exception {exception.Name.ToSimpleIdentifier()} {initializationPostfix}".TrimEnd()
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
			return variable.DefaultExpressionNode == null || !variable.IsConstant
				? String.Empty
				: variable.DefaultExpressionNode.GetText(program.SemanticModel.StatementText);
		}

		private static ColumnDetailsModel BuildColumnDetailsModel(OracleDatabaseModelBase databaseModel, OracleColumnReference columnReference)
		{
			var columnOwner = columnReference.ValidObjectReference.SchemaObject.GetTargetSchemaObject().FullyQualifiedName;

			var dataModel =
				new ColumnDetailsModel
				{
					Owner = columnOwner.ToString(),
					Name = OracleCodeCompletionProvider.GetPrettyColumnName(columnReference.ColumnDescription.Name),
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

		private class ToolTipLabelBuilder
		{
			private readonly StringBuilder _builder = new StringBuilder();

			public void AddElement(string labelElement, string prefix = null)
			{
				if (String.IsNullOrEmpty(labelElement))
				{
					return;
				}

				if (_builder.Length > 0)
				{
					_builder.Append(" ");
				}

				_builder.Append(prefix);
				_builder.Append(labelElement);
			}

			public override string ToString()
			{
				return _builder.ToString();
			}
		}
	}
}