using System;
using System.Globalization;
using System.Linq;
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
				return null;

			var tip = node.Type == NodeType.Terminal ? node.Id : null;

			var validationModel = (OracleValidationModel)sqlDocumentRepository.ValidationModels[node.Statement];

			var nodeSemanticError = validationModel.GetNodesWithSemanticErrors().FirstOrDefault(n => node.HasAncestor(n.Key, true));
			if (nodeSemanticError.Key != null)
			{
				tip = nodeSemanticError.Value.ToolTipText;
			}
			else
			{
				var queryBlock = ((OracleStatementSemanticModel)validationModel.SemanticModel).GetQueryBlock(node);

				switch (node.Id)
				{
					case Terminals.ObjectIdentifier:
						var objectReference = GetObjectReference(queryBlock, node);
						var schemaObjectReference = GetSchemaObjectReference(queryBlock, node);
						if (objectReference != null)
						{
							tip = GetObjectToolTip(objectReference);
						}
						else if (schemaObjectReference != null)
						{
							tip = GetFullSchemaObjectToolTip(schemaObjectReference);
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
					case Terminals.RowIdPseudoColumn:
					case Terminals.Identifier:
						var columnReference = GetColumnDescription(queryBlock, node);
						if (columnReference == null)
						{
							var functionToolTip = GetFunctionToolTip(queryBlock, node);
							var typeToolTip = GetTypeToolTip(queryBlock, node);
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
							var objectPrefix = columnReference.ObjectNode == null
								? String.Format("{0}.{1} ", columnReference.ValidObjectReference.FullyQualifiedObjectName, columnReference.Name.ToSimpleIdentifier())
								: null;
							
							tip = String.Format("{0}{1} {2}{3}", objectPrefix, columnReference.ColumnDescription.FullTypeName, columnReference.ColumnDescription.Nullable ? null : "NOT ", "NULL");
						}
						
						break;
				}
			}

			return String.IsNullOrEmpty(tip) ? null : new ToolTipObject { DataContext = tip };
		}

		private static string GetTypeToolTip(OracleQueryBlock queryBlock, StatementDescriptionNode node)
		{
			var typeReference = queryBlock.AllTypeReferences.SingleOrDefault(t => t.ObjectNode == node);
			return typeReference == null ? null : GetFullSchemaObjectToolTip(typeReference.SchemaObject);
		}

		private static string GetObjectToolTip(OracleObjectWithColumnsReference objectReference)
		{
			if (objectReference.Type == ReferenceType.SchemaObject)
			{
				return GetFullSchemaObjectToolTip(objectReference.SchemaObject);
			}
			
			return objectReference.FullyQualifiedObjectName + " (" + objectReference.Type.ToCategoryLabel() + ")";
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

		private string GetFunctionToolTip(OracleQueryBlock queryBlock, StatementDescriptionNode terminal)
		{
			var functionReference = queryBlock.AllFunctionReferences.SingleOrDefault(f => f.FunctionIdentifierNode == terminal);
			return functionReference == null || functionReference.Metadata == null ? null : functionReference.Metadata.Identifier.FullyQualifiedIdentifier;
		}

		private OracleColumnReference GetColumnDescription(OracleQueryBlock queryBlock, StatementDescriptionNode terminal)
		{
			return queryBlock.AllColumnReferences
				.FirstOrDefault(c => c.ColumnNode == terminal);
		}

		private OracleSchemaObject GetSchemaObjectReference(OracleQueryBlock queryBlock, StatementDescriptionNode terminal)
		{
			return queryBlock.AllFunctionReferences
				.Cast<OracleReference>()
				.Concat(queryBlock.AllSequenceReferences)
				.Where(f => f.ObjectNode == terminal)
				.Select(f => f.SchemaObject)
				.FirstOrDefault();
		}

		private OracleObjectWithColumnsReference GetObjectReference(OracleQueryBlock queryBlock, StatementDescriptionNode terminal)
		{
			var objectReference = queryBlock.AllColumnReferences
				.Where(c => c.ObjectNode == terminal && c.ObjectNodeObjectReferences.Count == 1)
				.Select(c => c.ObjectNodeObjectReferences.First())
				.FirstOrDefault();

			return objectReference ?? queryBlock.ObjectReferences.FirstOrDefault(o => o.ObjectNode == terminal);
		}
	}
}
