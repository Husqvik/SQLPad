using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle.SemanticModel
{
	public class OraclePlSqlStatementSemanticModel : OracleStatementSemanticModel
	{
		private readonly List<OraclePlSqlProgram> _programs = new List<OraclePlSqlProgram>();

		private readonly Lazy<ICollection<RedundantTerminalGroup>> _redundantSymbolGroup;

		public IReadOnlyList<OraclePlSqlProgram> Programs => _programs.AsReadOnly();

		public IEnumerable<OraclePlSqlProgram> AllPrograms => Programs.Concat(Programs.SelectMany(p => p.AllSubPrograms));

		public override OracleQueryBlock MainQueryBlock { get; } = null;

		public override IEnumerable<OracleReferenceContainer> AllReferenceContainers
		{
			get
			{
				var allPlSqlPrograms = AllPrograms.ToArray();
				var allSSqlContainers = allPlSqlPrograms
					.SelectMany(p => p.SqlModels)
					.SelectMany(m => m.AllReferenceContainers);
				return allPlSqlPrograms.Concat(allSSqlContainers);
			}
		}

		public override ICollection<RedundantTerminalGroup> RedundantSymbolGroups => _redundantSymbolGroup.Value;

		internal OraclePlSqlStatementSemanticModel(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel)
			: base(statementText, statement, databaseModel)
		{
			if (!statement.IsPlSql)
			{
				throw new ArgumentException("Statement is not PL/SQL statement. ", nameof(statement));
			}

			_redundantSymbolGroup =
				new Lazy<ICollection<RedundantTerminalGroup>>(
					() => AllPrograms.SelectMany(p => p.SqlModels).SelectMany(m => m.RedundantSymbolGroups).ToArray());
		}

		internal new OraclePlSqlStatementSemanticModel Build(CancellationToken cancellationToken)
		{
			return (OraclePlSqlStatementSemanticModel)base.Build(cancellationToken);
		}

		protected override void Build()
		{
			ResolveProgramDefinitions();
			ResolveProgramBodies();
		}

		private void ResolveProgramBodies()
		{
			FindPlSqlReferences(Programs);
			ResolveSubProgramReferences(Programs);
		}

		private void FindPlSqlReferences(IEnumerable<OraclePlSqlProgram> programs)
		{
			foreach (var program in programs)
			{
				var declarationReferenceSourceNodes = program.RootNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.ItemList2) && !String.Equals(n.Id, NonTerminals.ProgramBody) && !String.Equals(n.Id, NonTerminals.CursorDefinition), NonTerminals.PlSqlExpression, NonTerminals.PlSqlDataType);
				foreach (var sourceNode in declarationReferenceSourceNodes)
				{
					switch (sourceNode.Id)
					{
						case NonTerminals.PlSqlExpression:
							FindPlSqlReferences(program, sourceNode);
							break;
						case NonTerminals.PlSqlDataType:
							OracleDataTypeReference dataTypeReference;
							OracleReferenceBuilder.TryCreatePlSqlDataTypeReference(program, sourceNode, out dataTypeReference);
							break;
					}
				}

				var programBodyNode = program.RootNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.ProgramDeclareSection), NonTerminals.ProgramBody).FirstOrDefault();
				if (programBodyNode != null)
				{
					foreach (var statementTypeNode in GetPlSqlStatements(programBodyNode))
					{
						FindPlSqlReferences(program, statementTypeNode[0]);
					}

					var exceptionHandlerListNode = programBodyNode[NonTerminals.PlSqlExceptionClause, NonTerminals.PlSqlExceptionHandlerList];
					if (exceptionHandlerListNode != null)
					{
						foreach (var prefixedExceptionIdentifierNode in exceptionHandlerListNode.GetPathFilterDescendants(NodeFilters.BreakAtPlSqlSubProgramOrSqlCommand, NonTerminals.PrefixedExceptionIdentifier))
						{
							CreatePlSqlExceptionReference(program, prefixedExceptionIdentifierNode);
						}

						var exceptionHandlerStatementLists = StatementGrammarNode.GetAllChainedClausesByPath(exceptionHandlerListNode[NonTerminals.PlSqlExceptionHandler, NonTerminals.PlSqlStatementList], null, NonTerminals.PlSqlExceptionHandlerList, NonTerminals.PlSqlExceptionHandler, NonTerminals.PlSqlStatementList);
						foreach (var statementList in exceptionHandlerStatementLists)
						{
							foreach (var statementTypeNode in GetPlSqlStatements(statementList))
							{
								FindPlSqlReferences(program, statementTypeNode[0]);
							}
						}
					}
				}

				FindPlSqlReferences(program.SubPrograms);
			}
		}

		private static IEnumerable<StatementGrammarNode> GetPlSqlStatements(StatementGrammarNode sourceNode)
		{
			return sourceNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.PlSqlSqlStatement) && !String.Equals(n.Id, NonTerminals.PlSqlStatementList) && !(String.Equals(n.Id, NonTerminals.PlSqlStatementType) && n[NonTerminals.PlSqlBlock] != null), NonTerminals.PlSqlStatementType);
		}

		private void FindPlSqlReferences(OraclePlSqlProgram program, StatementGrammarNode node)
		{
			if (node == null)
			{
				return;
			}

			var excludedIdentifiers = new HashSet<StatementGrammarNode>();

			switch (node.Id)
			{
				case NonTerminals.PlSqlRaiseStatement:
					var prefixedExceptionIdentifierNode = node[NonTerminals.PrefixedExceptionIdentifier];
					CreatePlSqlExceptionReference(program, prefixedExceptionIdentifierNode);
					break;

				case NonTerminals.PlSqlCursorForLoopStatement:
					var cursorIdentifier = node[Terminals.PlSqlIdentifier];
					var cursorScopeNode = node[NonTerminals.PlSqlBasicLoopStatement];
					if (cursorIdentifier != null && cursorScopeNode != null)
					{
						excludedIdentifiers.Add(cursorIdentifier);

						var cursor =
							new OraclePlSqlCursorVariable
							{
								Name = cursorIdentifier.Token.Value.ToQuotedIdentifier(),
								IsImplicit = true,
								Owner = program,
								DefinitionNode = cursorIdentifier,
								ScopeNode = cursorScopeNode
							};

						SetStatementModelIfFound(cursor, node[NonTerminals.CursorSource, NonTerminals.SelectStatement]);

						program.Variables.Add(cursor);

						var cursorReferenceIdentifier = node[NonTerminals.CursorSource, Terminals.CursorIdentifier];
						if (cursorReferenceIdentifier != null)
						{
							var cursorReference =
								new OraclePlSqlVariableReference
								{
									RootNode = cursorReferenceIdentifier.ParentNode,
									IdentifierNode = cursorReferenceIdentifier,
									//ObjectNode = cursorIdentifierNode.ParentNode[NonTerminals.ObjectPrefix, Terminals.ObjectIdentifier],
									//OwnerNode = cursorIdentifierNode.ParentNode[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier],
									Container = program,
									PlSqlProgram = program
								};

							program.PlSqlVariableReferences.Add(cursorReference);
						}
					}

					goto default;

				case NonTerminals.PlSqlForLoopStatement:
					var identifier = node[Terminals.PlSqlIdentifier];
					var scopeNode = node[NonTerminals.PlSqlBasicLoopStatement];
					if (identifier != null && scopeNode != null)
					{
						excludedIdentifiers.Add(identifier);

						var variable =
							new OraclePlSqlVariable
							{
								Name = identifier.Token.Value.ToQuotedIdentifier(),
								Owner = program,
								DefinitionNode = identifier,
								DataType = OracleDataType.BinaryIntegerType,
								ScopeNode = scopeNode
							};

						program.Variables.Add(variable);
					}

					goto default;

				default:
					var identifiers = node.GetPathFilterDescendants(NodeFilters.BreakAtPlSqlSubProgramOrSqlCommand, Terminals.Identifier, Terminals.PlSqlIdentifier, Terminals.RowIdPseudocolumn, Terminals.Level, Terminals.RowNumberPseudocolumn, Terminals.User)
						.Where(i => !excludedIdentifiers.Contains(i));

					ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, program, identifiers, StatementPlacement.None, null, null, GetFunctionCallNodes);

					var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(node);
					CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, program, null, StatementPlacement.None, null);
					break;
			}
		}

		private static void CreatePlSqlExceptionReference(OraclePlSqlProgram program, StatementGrammarNode prefixedExceptionIdentifierNode)
		{
			var identifierNode = prefixedExceptionIdentifierNode?[Terminals.ExceptionIdentifier];
			if (identifierNode == null)
			{
				return;
			}

			var exceptionReference =
				new OraclePlSqlExceptionReference
				{
					RootNode = prefixedExceptionIdentifierNode,
					IdentifierNode = identifierNode,
					OwnerNode = prefixedExceptionIdentifierNode[NonTerminals.Prefix, NonTerminals.SchemaPrefix, Terminals.ObjectIdentifier],
					ObjectNode = prefixedExceptionIdentifierNode[NonTerminals.Prefix, NonTerminals.ObjectPrefix, Terminals.ObjectIdentifier],
					Container = program,
					PlSqlProgram = program
				};

			program.PlSqlExceptionReferences.Add(exceptionReference);
		}

		private static IEnumerable<StatementGrammarNode> GetFunctionCallNodes(StatementGrammarNode identifier)
		{
			var parenthesisEnclosedFunctionParametersNode = identifier.ParentNode.ParentNode[NonTerminals.ParenthesisEnclosedFunctionParameters];
			if (parenthesisEnclosedFunctionParametersNode != null)
			{
				yield return parenthesisEnclosedFunctionParametersNode;
			}
		}

		private void ResolvePlSqlReferences(OraclePlSqlProgram program)
		{
			ResolveProgramReferences(program.ProgramReferences, true);

			foreach (var variableReference in program.PlSqlVariableReferences.Concat(program.SqlModels.SelectMany(m => m.AllReferenceContainers).SelectMany(c => c.PlSqlVariableReferences)))
			{
				TryResolveLocalReference(variableReference, program.AccessibleVariables, variableReference.Variables);
			}

			var sourceColumnReferences = program.ColumnReferences
				.Concat(
					program.SqlModels
						.SelectMany(m => m.AllReferenceContainers)
						.SelectMany(c => c.ColumnReferences)
						.Where(c => !c.ReferencesAllColumns && c.ColumnNodeColumnReferences.Count == 0 && c.ObjectNodeObjectReferences.Count == 0))
				.ToArray();

			foreach (var columnReference in sourceColumnReferences)
			{
				var variableReference =
					new OraclePlSqlVariableReference
					{
						PlSqlProgram = program,
						IdentifierNode = columnReference.ColumnNode
					};

				variableReference.CopyPropertiesFrom(columnReference);

				var elementSource = variableReference.PlSqlProgram.AccessibleVariables.Concat(variableReference.PlSqlProgram.Parameters);
				var variableResolved = TryResolveLocalReference(variableReference, elementSource, variableReference.Variables);
				if (variableResolved)
				{
					columnReference.Container.ColumnReferences.Remove(columnReference);
				}
				else
				{
					variableResolved = !TryColumnReferenceAsProgramOrSequenceReference(columnReference, true);
				}

				if (variableResolved && !IsBooleanLiteral(columnReference))
				{
					program.PlSqlVariableReferences.Add(variableReference);
				}
			}

			program.ColumnReferences.Clear();

			foreach (var exceptionReference in program.PlSqlExceptionReferences)
			{
				TryResolveLocalReference(exceptionReference, program.Exceptions, exceptionReference.Exceptions);
			}

			foreach (var dataTypeReference in program.DataTypeReferences)
			{
				OracleReferenceBuilder.ResolveSchemaType(dataTypeReference);
			}

			ResolveSubProgramReferences(program.SubPrograms);
		}

		private static bool IsBooleanLiteral(OracleReference columnReference)
		{
			if (columnReference.ObjectNode != null)
			{
				return false;
			}

			return String.Equals(columnReference.NormalizedName, "\"TRUE\"") || String.Equals(columnReference.NormalizedName, "\"FALSE\"");
		}

		private static bool TryResolveLocalReference<TElement>(OraclePlSqlReference plSqlReference, IEnumerable<TElement> elements, ICollection<TElement> resolvedCollection) where TElement : OraclePlSqlElement
		{
			if (plSqlReference.OwnerNode != null)
			{
				return false;
			}

			var program = plSqlReference.PlSqlProgram;

			do
			{
				foreach (var element in elements)
				{
					var isLocal = program == element.Owner;
					if (resolvedCollection.Count > 0 && !isLocal)
					{
						break;
					}

					var matchVisitor = new OraclePlSqlReferenceElementMatchVisitor(plSqlReference, program);
					element.Accept(matchVisitor);

					if (matchVisitor.IsMatch)
					{
						resolvedCollection.Add(element);
					}
				}

				if (resolvedCollection.Count > 0)
				{
					return true;
				}

				program = program.Owner;
			} while (resolvedCollection.Count == 0 && program != null);


			return false;
		}

		private void ResolveSubProgramReferences(IEnumerable<OraclePlSqlProgram> programs)
		{
			foreach (var subProgram in programs)
			{
				ResolvePlSqlReferences(subProgram);
			}
		}

		private void ResolveProgramDefinitions()
		{
			var anonymousPlSqlBlock = String.Equals(Statement.RootNode.Id, NonTerminals.PlSqlBlockStatement);
			var objectNode = Statement.RootNode[NonTerminals.CreatePlSqlObjectClause]?[0];
			var isSchemaProcedure = String.Equals(objectNode?.Id, NonTerminals.CreateProcedure);
			var isSchemaFunction = String.Equals(objectNode?.Id, NonTerminals.CreateFunction);
			var isPackage = String.Equals(objectNode?.Id, NonTerminals.CreatePackageBody);
			var schemaObjectNode = objectNode?[NonTerminals.SchemaObject];
			if (isSchemaProcedure || isSchemaFunction || anonymousPlSqlBlock || isPackage)
			{
				if (isSchemaFunction)
				{
					schemaObjectNode = objectNode[NonTerminals.PlSqlFunctionSource, NonTerminals.SchemaObject];
				}
				else if (anonymousPlSqlBlock)
				{
					objectNode = Statement.RootNode[NonTerminals.PlSqlBlock];
				}

				var identifier = CreateObjectIdentifierFromSchemaObjectNode(schemaObjectNode);

				var programType = anonymousPlSqlBlock
					? PlSqlProgramType.PlSqlBlock
					: isPackage
						? PlSqlProgramType.PackageProgram
						: PlSqlProgramType.StandaloneProgram;

				var program =
					new OraclePlSqlProgram(identifier.NormalizedName, isSchemaFunction ? ProgramType.Function : ProgramType.Procedure, programType, this)
					{
						RootNode = objectNode,
						ObjectIdentifier = identifier
					};

				ResolveSqlStatements(program);
				ResolveParameterDeclarations(program);
				ResolveLocalVariableTypeAndLabelDeclarations(program);

				_programs.Add(program);

				ResolveSubProgramDefinitions(program);
			}
			else
			{
				// TODO: triggers/types
				//var programDefinitionNodes = Statement.RootNode.GetDescendants(NonTerminals.FunctionDefinition, NonTerminals.ProcedureDefinition);
				//_programs.AddRange(programDefinitionNodes.Select(n => new OraclePlSqlProgram { RootNode = n }));
				Initialize();
			}
		}

		private OracleObjectIdentifier CreateObjectIdentifierFromSchemaObjectNode(StatementGrammarNode schemaObjectNode)
		{
			if (schemaObjectNode == null)
			{
				return OracleObjectIdentifier.Empty;
			}

			var owner = schemaObjectNode[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier]?.Token.Value ?? DatabaseModel.CurrentSchema;
			var name = schemaObjectNode[Terminals.ObjectIdentifier]?.Token.Value;
			return OracleObjectIdentifier.Create(owner, name);
		}

		private void ResolveSqlStatements(OraclePlSqlProgram program)
		{
			var sqlStatementNodes = program.RootNode.GetPathFilterDescendants(
				n => !String.Equals(n.Id, NonTerminals.ItemList2) && !(String.Equals(n.Id, NonTerminals.PlSqlBlock) && String.Equals(n.ParentNode.Id, NonTerminals.PlSqlStatementType)),
				NonTerminals.SelectStatement, NonTerminals.InsertStatement, NonTerminals.UpdateStatement, NonTerminals.MergeStatement);

			foreach (var sqlStatementNode in sqlStatementNodes)
			{
				var childStatement =
					new OracleStatement
					{
						RootNode = sqlStatementNode,
						ParseStatus = Statement.ParseStatus,
						SourcePosition = sqlStatementNode.SourcePosition
					};

				var sqlStatementSemanticModel = new OracleStatementSemanticModel(sqlStatementNode.GetText(StatementText), childStatement, DatabaseModel).Build(CancellationToken);
				program.SqlModels.Add(sqlStatementSemanticModel);

				foreach (var queryBlock in sqlStatementSemanticModel.QueryBlocks)
				{
					QueryBlockNodes.Add(queryBlock.RootNode, queryBlock);
				}

				foreach (var plSqlVariableReference in sqlStatementSemanticModel.AllReferenceContainers.SelectMany(c => c.PlSqlVariableReferences))
				{
					plSqlVariableReference.PlSqlProgram = program;
				}
			}
		}

		private void ResolveSubProgramDefinitions(OraclePlSqlProgram program)
		{
			foreach (var childNode in program.RootNode.ChildNodes)
			{
				ResolveSubProgramDefinitions(program, childNode);
			}
		}

		private void ResolveSubProgramDefinitions(OraclePlSqlProgram program, StatementGrammarNode node)
		{
			foreach (var childNode in node.ChildNodes)
			{
				var subProgram = program;
				var isPlSqlBlock = String.Equals(childNode.Id, NonTerminals.PlSqlBlock);
				var isFunction = String.Equals(childNode.Id, NonTerminals.FunctionDefinition);
				if (isPlSqlBlock || String.Equals(childNode.Id, NonTerminals.ProcedureDefinition) || isFunction)
				{
					var nameTerminal = childNode[0]?[Terminals.Identifier];
					var programType = isPlSqlBlock ? PlSqlProgramType.PlSqlBlock : PlSqlProgramType.NestedProgram;

					subProgram =
						new OraclePlSqlProgram(nameTerminal?.Token.Value.ToQuotedIdentifier(), isFunction ? ProgramType.PackageFunction : ProgramType.PackageProcedure, programType, this)
						{
							Owner = program,
							RootNode = childNode,
							ObjectIdentifier = program.ObjectIdentifier
						};

					ResolveSqlStatements(subProgram);
					ResolveParameterDeclarations(subProgram);
					ResolveLocalVariableTypeAndLabelDeclarations(subProgram);

					program.SubPrograms.Add(subProgram);
				}

				ResolveSubProgramDefinitions(subProgram, childNode);
			}
		}

		private void ResolveLocalVariableTypeAndLabelDeclarations(OraclePlSqlProgram program)
		{
			StatementGrammarNode programSourceNode;
			switch (program.RootNode.Id)
			{
				case NonTerminals.CreateFunction:
					programSourceNode = program.RootNode[NonTerminals.PlSqlFunctionSource, NonTerminals.FunctionTypeDefinition, NonTerminals.ProgramImplentationDeclaration];
					break;
				case NonTerminals.PlSqlBlock:
					programSourceNode = program.RootNode[NonTerminals.PlSqlBlockDeclareSection];
					break;
				case NonTerminals.CreatePackageBody:
					programSourceNode = program.RootNode[NonTerminals.PackageBodyDefinition];
					break;
				default:
					programSourceNode = program.RootNode[NonTerminals.ProgramImplentationDeclaration];
					break;
			}

			ResolveProgramDeclarationLabels(program);

			var firstItemNode = programSourceNode?[NonTerminals.ProgramDeclareSection, NonTerminals.ItemList1, NonTerminals.Item1OrPragmaDefinition];
			if (firstItemNode == null)
			{
				return;
			}

			var itemOrPragmaNodes = StatementGrammarNode.GetAllChainedClausesByPath(firstItemNode, n => n.ParentNode, NonTerminals.ItemList1Chained, NonTerminals.Item1OrPragmaDefinition);
			foreach (var itemOrPragmaSwitchNode in itemOrPragmaNodes)
			{
				var itemOrPragmaNode = itemOrPragmaSwitchNode[0];
				if (String.Equals(itemOrPragmaNode.Id, NonTerminals.Item1))
				{
					var declarationRoot = itemOrPragmaNode[0];
					switch (declarationRoot?.Id)
					{
						case NonTerminals.ItemDeclaration:
							var specificNode = declarationRoot[0];
							if (specificNode != null)
							{
								OraclePlSqlVariable variable;
								switch (specificNode.Id)
								{
									case NonTerminals.ConstantDeclaration:
										variable =
											new OraclePlSqlVariable
											{
												Owner = program,
												DefinitionNode = specificNode,
												IsConstant = true,
												DefaultExpressionNode = specificNode[NonTerminals.PlSqlExpression]
											};

										SetDataTypeAndNullablePropertiesAndAddIfIdentifierFound(variable, specificNode, program);
										break;

									case NonTerminals.ExceptionDeclaration:
										var exceptionName = specificNode[Terminals.ExceptionIdentifier]?.Token.Value.ToQuotedIdentifier();
										if (exceptionName != null)
										{
											var exception =
												new OraclePlSqlException
												{
													Owner = program,
													Name = exceptionName,
													DefinitionNode = specificNode
												};

											program.Exceptions.Add(exception);
										}

										break;

									case NonTerminals.VariableDeclaration:
										specificNode = specificNode[NonTerminals.FieldDefinition];
										if (specificNode != null)
										{
											variable =
												new OraclePlSqlVariable
												{
													Owner = program,
													DefinitionNode = specificNode,
													DefaultExpressionNode = specificNode[NonTerminals.VariableDeclarationDefaultValue, NonTerminals.PlSqlExpression],
												};

											SetDataTypeAndNullablePropertiesAndAddIfIdentifierFound(variable, specificNode, program);
										}

										break;
								}
							}

							break;

						case NonTerminals.TypeDefinition:
							var typeIdentifierNode = declarationRoot[Terminals.TypeIdentifier];
							var associativeArrayTypeDefinitionNode = declarationRoot[NonTerminals.TypeDefinitionSpecification, NonTerminals.CollectionTypeDefinition, NonTerminals.AssociativeArrayTypeDefinition];
							if (typeIdentifierNode != null)
							{
								var type =
									new OraclePlSqlType
									{
										Owner = program,
										DefinitionNode = declarationRoot,
										Name = typeIdentifierNode.Token.Value.ToQuotedIdentifier(),
										IsAssociativeArray = associativeArrayTypeDefinitionNode?[Terminals.Index] != null
									};

								program.Types.Add(type);

								OracleDataTypeReference dataTypeReference;
								var associativeArrayIndexTypeNode = associativeArrayTypeDefinitionNode?[NonTerminals.AssociativeArrayIndexType];
								if (associativeArrayIndexTypeNode != null && OracleReferenceBuilder.TryCreatePlSqlDataTypeReference(program, associativeArrayIndexTypeNode, out dataTypeReference))
								{
									type.AssociativeArrayIndexDataTypeReference = dataTypeReference;
								}
							}

							break;

						case NonTerminals.CursorDefinition:
							var identifierNode = declarationRoot[Terminals.CursorIdentifier];
							if (identifierNode != null)
							{
								var cursor =
									new OraclePlSqlCursorVariable
									{
										Owner = program,
										DefinitionNode = declarationRoot,
										Name = identifierNode.Token.Value.ToQuotedIdentifier()
									};

								SetStatementModelIfFound(cursor, declarationRoot[NonTerminals.SelectStatement]);

								program.Variables.Add(cursor);
							}
							break;
					}
				}
				else
				{
					var exceptionInit = itemOrPragmaNode[NonTerminals.PragmaType, Terminals.ExceptionInit];
					if (exceptionInit != null)
					{
						var exceptionIdentifier = exceptionInit.ParentNode[Terminals.ExceptionIdentifier];
						var errorCodeModifier = exceptionInit.ParentNode[Terminals.MathMinus] == null ? 1 : -1;
						var integerLiteral = exceptionInit.ParentNode[Terminals.IntegerLiteral];

						int errorCode;
						if (exceptionIdentifier != null && integerLiteral != null && Int32.TryParse(integerLiteral.Token.Value, out errorCode))
						{
							var exceptionName = exceptionIdentifier.Token.Value.ToQuotedIdentifier();
							foreach (var exception in program.Exceptions)
							{
								if (String.Equals(exception.Name, exceptionName))
								{
									exception.ErrorCode = errorCodeModifier * errorCode;
								}
							}

							CreatePlSqlExceptionReference(program, integerLiteral.ParentNode);
						}
					}
				}
			}
		}

		private static void SetStatementModelIfFound(OraclePlSqlCursorVariable cursor, StatementGrammarNode selectStatementNode)
		{
			if (selectStatementNode != null)
			{
				cursor.SemanticModel = cursor.Owner.SqlModels.SingleOrDefault(m => m.Statement.RootNode == selectStatementNode);
			}
		}

		private static void SetDataTypeAndNullablePropertiesAndAddIfIdentifierFound(OraclePlSqlVariable variable, StatementGrammarNode variableNode, OraclePlSqlProgram program)
		{
			var identifierNode = variableNode[Terminals.PlSqlIdentifier];
			if (identifierNode == null)
			{
				return;
			}

			variable.Name = identifierNode.Token.Value.ToQuotedIdentifier();
			variable.Nullable = variableNode[NonTerminals.NotNull, Terminals.Not] == null;
			variable.DataTypeNode = variableNode[NonTerminals.PlSqlDataType];
			program.Variables.Add(variable);
		}

		private static void ResolveProgramDeclarationLabels(OraclePlSqlProgram program)
		{
			var labelListNode = program.RootNode[NonTerminals.PlSqlLabelList];
			if (String.Equals(program.RootNode.ParentNode.Id, NonTerminals.PlSqlStatementType))
			{
				labelListNode = program.RootNode.ParentNode.ParentNode[NonTerminals.PlSqlLabelList];
			}

			if (labelListNode != null)
			{
				foreach (var labelIdentifier in labelListNode.GetDescendants(Terminals.LabelIdentifier))
				{
					var label =
						new OraclePlSqlLabel
						{
							DefinitionNode = labelIdentifier.ParentNode,
							Name = labelIdentifier.Token.Value.ToQuotedIdentifier(),
							Node = labelIdentifier.ParentNode
						};

					program.Labels.Add(label);
				}
			}
		}

		private void ResolveParameterDeclarations(OraclePlSqlProgram program)
		{
			StatementGrammarNode parameterSourceNode;
			switch (program.RootNode.Id)
			{
				case NonTerminals.CreateFunction:
					parameterSourceNode = program.RootNode[NonTerminals.PlSqlFunctionSource];
					break;
				case NonTerminals.CreateProcedure:
					parameterSourceNode = program.RootNode;
					break;
				default:
					parameterSourceNode = program.RootNode[0];
					break;
			}

			var returnParameterNode = parameterSourceNode?[NonTerminals.PlSqlDataTypeWithoutConstraint];
			var parameterPosition = 0;
			if (returnParameterNode != null)
			{
				program.ReturnParameter =
					new OraclePlSqlParameter
					{
						DefinitionNode = parameterSourceNode,
						Direction = ParameterDirection.ReturnValue,
						Nullable = true,
						DataTypeNode = returnParameterNode
					};

				var returnTypeName = String.Concat(returnParameterNode.Terminals.Select(t => t.Token.Value));
				program.Metadata.AddParameter(new OracleProgramParameterMetadata(null, parameterPosition, parameterPosition, 0, ParameterDirection.ReturnValue, returnTypeName, OracleObjectIdentifier.Empty, false));
				parameterPosition++;
			}

			var parameterDeclarationList = parameterSourceNode?[NonTerminals.ParenthesisEnclosedParameterDeclarationList, NonTerminals.ParameterDeclarationList];
			if (parameterDeclarationList == null)
			{
				return;
			}

			var parameterDeclarations = StatementGrammarNode.GetAllChainedClausesByPath(parameterDeclarationList, null, NonTerminals.ParameterDeclarationListChained, NonTerminals.ParameterDeclarationList)
				.Select(n => n[NonTerminals.ParameterDeclaration]);
			foreach (var parameterDeclaration in parameterDeclarations)
			{
				var parameter = ResolveParameter(parameterDeclaration);
				program.Parameters.Add(parameter);

				var parameterMetadata = new OracleProgramParameterMetadata(parameter.Name, parameterPosition, parameterPosition, 0, parameter.Direction, parameter.DataType.IsPrimitive ? parameter.DataType.FullyQualifiedName.NormalizedName : null, parameter.DataType.IsPrimitive ? OracleObjectIdentifier.Empty : parameter.DataType.FullyQualifiedName, parameter.DefaultExpressionNode != null);
				program.Metadata.AddParameter(parameterMetadata);

				parameterPosition++;
			}
		}

		private static OraclePlSqlParameter ResolveParameter(StatementGrammarNode parameterDeclaration)
		{
			var parameter =
				new OraclePlSqlParameter
				{
					DefinitionNode = parameterDeclaration,
					Name = parameterDeclaration[Terminals.ParameterIdentifier].Token.Value.ToQuotedIdentifier()
				};

			var direction = ParameterDirection.Input;
			var parameterDirectionDeclaration = parameterDeclaration[NonTerminals.ParameterDirectionDeclaration];
			if (parameterDirectionDeclaration != null)
			{
				if (parameterDirectionDeclaration[Terminals.Out] != null)
				{
					direction = parameterDeclaration[Terminals.In] == null ? ParameterDirection.Output : ParameterDirection.InputOutput;
				}

				parameter.DataTypeNode = parameterDirectionDeclaration[NonTerminals.PlSqlDataTypeWithoutConstraint];
				parameter.DataType = OracleReferenceBuilder.ResolveDataTypeFromNode(parameter.DataTypeNode);
				parameter.DefaultExpressionNode = parameterDirectionDeclaration[NonTerminals.VariableDeclarationDefaultValue];
			}
			else
			{
				parameter.DataType = OracleDataType.Empty;
			}

			parameter.Direction = direction;

			return parameter;
		}
	}

	public abstract class OraclePlSqlReference : OracleReference
	{
		public StatementGrammarNode IdentifierNode { get; set; }

		public OraclePlSqlProgram PlSqlProgram { get; set; }

		public override string Name => IdentifierNode.Token.Value;

		protected override IEnumerable<StatementGrammarNode> GetAdditionalIdentifierTerminals()
		{
			if (IdentifierNode != null)
			{
				yield return IdentifierNode;
			}
		}
	}

	[DebuggerDisplay("OraclePlSqlVariableReference (Name={Name}; Variables={Variables.Count})")]
	public class OraclePlSqlVariableReference : OraclePlSqlReference
	{
		public ICollection<OraclePlSqlElement> Variables { get; } = new List<OraclePlSqlElement>();

		public override void Accept(IOracleReferenceVisitor visitor)
		{
			visitor.VisitPlSqlVariableReference(this);
		}
	}

	[DebuggerDisplay("OraclePlSqlExceptionReference (Name={Name}; Exceptions={Exceptions.Count})")]
	public class OraclePlSqlExceptionReference : OraclePlSqlReference
	{
		public ICollection<OraclePlSqlException> Exceptions { get; } = new List<OraclePlSqlException>();

		public override void Accept(IOracleReferenceVisitor visitor)
		{
			visitor.VisitPlSqlExceptionReference(this);
		}
	}

	public abstract class OraclePlSqlElement
	{
		public string Name { get; set; }

		public StatementGrammarNode DefinitionNode { get; set; }

		public OraclePlSqlProgram Owner { get; set; }

		public StatementGrammarNode ScopeNode { get; set; }

		public bool HasLocalScopeOnly => ScopeNode != null;

		public virtual void Accept(IOraclePlSqlElementVisitor visitor)
		{
			visitor.VisitPlSqlElement(this);
		}
	}

	public interface IOraclePlSqlElementVisitor
	{
		void VisitPlSqlElement(OraclePlSqlElement element);

		void VisitPlSqlCursorVariable(OraclePlSqlCursorVariable cursorVariable);
	}

	public class OraclePlSqlReferenceElementMatchVisitor : IOraclePlSqlElementVisitor
	{
		private readonly OraclePlSqlReference _plSqlReference;
		private readonly OraclePlSqlProgram _currentProgram;

		public bool IsMatch { get; private set; }

		public OraclePlSqlReferenceElementMatchVisitor(OraclePlSqlReference plSqlReference, OraclePlSqlProgram currentProgram)
		{
			_plSqlReference = plSqlReference;
			_currentProgram = currentProgram;
		}

		public void VisitPlSqlElement(OraclePlSqlElement element)
		{
			var variablePrefix = _plSqlReference.ObjectNode == null ? null : _plSqlReference.FullyQualifiedObjectName.NormalizedName;
			var isPrefixValid = variablePrefix == null || GetScopeNames(_currentProgram).Any(n => String.Equals(variablePrefix, n));
			IsMatch = isPrefixValid && String.Equals(element.Name, _plSqlReference.NormalizedName) && IsWithinScope(element);
		}

		public void VisitPlSqlCursorVariable(OraclePlSqlCursorVariable cursorVariable)
		{
			if (_plSqlReference.OwnerNode != null || _plSqlReference.IdentifierNode == null)
			{
				return;
			}

			var bindVariableExpressionOrPlSqlTargetNode = _plSqlReference.RootNode.GetPathFilterAncestor(n => !String.Equals(n.Id, NonTerminals.PlSqlStatementType), NonTerminals.BindVariableExpressionOrPlSqlTarget);
			if (bindVariableExpressionOrPlSqlTargetNode != null && bindVariableExpressionOrPlSqlTargetNode.ParentNode.Id.In(NonTerminals.PlSqlOpenStatement, NonTerminals.PlSqlOpenForStatement, NonTerminals.PlSqlFetchStatement, NonTerminals.PlSqlCloseStatement))
			{
				VisitPlSqlElement(cursorVariable);
				return;
			}

			var cursorName = _plSqlReference.ObjectNode?.Token.Value.ToQuotedIdentifier();
			IsMatch =
				IsWithinScope(cursorVariable) && String.Equals(cursorVariable.Name, cursorName) &&
				(cursorVariable.SemanticModel == null || cursorVariable.SemanticModel.MainQueryBlock?.NamedColumns.Contains(_plSqlReference.NormalizedName) == true);
		}

		private bool IsWithinScope(OraclePlSqlElement element)
		{
			var isWithinScope = element.ScopeNode?.SourcePosition.Contains(_plSqlReference.IdentifierNode.SourcePosition) ?? true;
			var elementDefinitionIndex = element.DefinitionNode?.SourcePosition.IndexStart;
			return isWithinScope && elementDefinitionIndex < _plSqlReference.IdentifierNode.SourcePosition.IndexStart;
		}

		private static IEnumerable<string> GetScopeNames(OraclePlSqlProgram program)
		{
			return String.IsNullOrEmpty(program.Name)
				? program.Labels.Select(l => l.Name)
				: Enumerable.Repeat(program.Name, 1);
		}
	}

	[DebuggerDisplay("OraclePlSqlVariable (Name={Name}; ErrorCode={ErrorCode})")]
	public class OraclePlSqlException : OraclePlSqlElement
	{
		public int? ErrorCode { get; set; }
	}

	[DebuggerDisplay("OraclePlSqlVariable (Name={Name}; Nullable={Nullable}; IsConstant={IsConstant})")]
	public class OraclePlSqlVariable : OraclePlSqlElement
	{
		public bool IsConstant { get; set; }

		public bool Nullable { get; set; }

		public OracleDataType DataType { get; set; }

		public StatementGrammarNode DefaultExpressionNode { get; set; }

		public StatementGrammarNode DataTypeNode { get; set; }

		public virtual bool IsReadOnly => IsConstant;
	}

	[DebuggerDisplay("OraclePlSqlCursorVariable (Name={Name})")]
	public class OraclePlSqlCursorVariable : OraclePlSqlVariable
	{
		public bool IsImplicit { get; set; }

		public OracleStatementSemanticModel SemanticModel { get; set; }

		public override bool IsReadOnly { get; } = true;

		public override void Accept(IOraclePlSqlElementVisitor visitor)
		{
			visitor.VisitPlSqlCursorVariable(this);
		}
	}

	[DebuggerDisplay("OraclePlSqlParameter (Name={Name})")]
	public class OraclePlSqlParameter : OraclePlSqlVariable
	{
		public ParameterDirection Direction { get; set; }

		public override bool IsReadOnly => Direction == ParameterDirection.Input;
	}

	[DebuggerDisplay("OraclePlSqlType (Name={Name})")]
	public class OraclePlSqlType : OraclePlSqlElement
	{
		public bool IsAssociativeArray { get; set; }

		public OracleDataTypeReference AssociativeArrayIndexDataTypeReference { get; set; }
	}

	[DebuggerDisplay("OraclePlSqlLabel (Name={Name})")]
	public class OraclePlSqlLabel : OraclePlSqlElement
	{
		public StatementGrammarNode Node { get; set; }
	}
}
