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

		public IReadOnlyList<OraclePlSqlProgram> Programs => _programs.AsReadOnly();

		public override OracleQueryBlock MainQueryBlock { get; } = null;

		public override IEnumerable<OracleReferenceContainer> AllReferenceContainers
		{
			get
			{
				var allPlSqlPrograms = Programs.Concat(Programs.SelectMany(p => p.AllSubPrograms)).ToArray();
				var allSSqlContainers = allPlSqlPrograms
					.SelectMany(p => p.ChildModels)
					.SelectMany(m => m.AllReferenceContainers);
				return allPlSqlPrograms.Concat(allSSqlContainers);
			}
		}

		internal OraclePlSqlStatementSemanticModel(string statementText, OracleStatement statement, OracleDatabaseModelBase databaseModel)
			: base(statementText, statement, databaseModel)
		{
			if (!statement.IsPlSql)
			{
				throw new ArgumentException("Statement is not PL/SQL statement. ", nameof(statement));
			}
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
				var declarationReferenceSourceNodes = program.RootNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.ItemList2) && !String.Equals(n.Id, NonTerminals.ProgramBody) && !String.Equals(n.Id, NonTerminals.CursorDefinition), NonTerminals.PlSqlExpression);
				foreach (var sourceNode in declarationReferenceSourceNodes)
				{
					FindPlSqlReferences(program, sourceNode);
				}

				var programBodyNode = program.RootNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.ProgramDeclareSection), NonTerminals.ProgramBody).FirstOrDefault();
				if (programBodyNode != null)
				{
					foreach (var statementTypeNode in programBodyNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.PlSqlSqlStatement) && !String.Equals(n.Id, NonTerminals.PlSqlStatementList) && !(String.Equals(n.Id, NonTerminals.PlSqlStatementType) && n[NonTerminals.PlSqlBlock] != null), NonTerminals.PlSqlStatementType))
					{
						var statementNode = statementTypeNode[0];
						FindPlSqlReferences(program, statementNode);
					}

					var exceptionHandlerListNode = programBodyNode[NonTerminals.PlSqlExceptionClause, NonTerminals.PlSqlExceptionHandlerList];
					if (exceptionHandlerListNode != null)
					{
						foreach (var prefixedExceptionIdentifierNode in exceptionHandlerListNode.GetPathFilterDescendants(NodeFilters.BreakAtPlSqlSubProgramOrSqlCommand, NonTerminals.PrefixedExceptionIdentifier))
						{
							CreatePlSqlExceptionReference(program, prefixedExceptionIdentifierNode);
						}
					}
				}

				FindPlSqlReferences(program.SubPrograms);
			}
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

				case NonTerminals.PlSqlOpenStatement:
					var cursorIdentifierNode = node[NonTerminals.PrefixedCursorIdentifier, Terminals.CursorIdentifier];

					if (cursorIdentifierNode != null)
					{
						var cursorReference =
							new OraclePlSqlVariableReference
							{
								RootNode = cursorIdentifierNode.ParentNode,
								IdentifierNode = cursorIdentifierNode,
								ObjectNode = cursorIdentifierNode.ParentNode[NonTerminals.ObjectPrefix, Terminals.ObjectIdentifier],
								OwnerNode = cursorIdentifierNode.ParentNode[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier],
								Container = program,
								PlSqlProgram = program
							};

						program.PlSqlVariableReferences.Add(cursorReference);
					}

					goto default;

				default:
					var identifiers = node.GetPathFilterDescendants(NodeFilters.BreakAtPlSqlSubProgramOrSqlCommand, Terminals.Identifier, Terminals.PlSqlIdentifier, Terminals.RowIdPseudoColumn, Terminals.Level, Terminals.RowNumberPseudoColumn)
						.Where(i => !excludedIdentifiers.Contains(i));

					ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, program, identifiers, StatementPlacement.None, null, null, GetFunctionCallNodes);

					var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(node);
					CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, null, program.ProgramReferences, StatementPlacement.None, null);
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
			ResolveFunctionReferences(program.ProgramReferences, true);

			foreach (var variableReference in program.PlSqlVariableReferences.Concat(program.ChildModels.SelectMany(m => m.AllReferenceContainers).SelectMany(c => c.PlSqlVariableReferences)))
			{
				TryResolveLocalReference(variableReference, program.AccessibleVariables, variableReference.Variables);
			}

			var sourceColumnReferences = program.ColumnReferences
				.Concat(
					program.ChildModels
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

				if (variableResolved)
				{
					program.PlSqlVariableReferences.Add(variableReference);
				}
			}

			program.ColumnReferences.Clear();

			foreach (var exceptionReference in program.PlSqlExceptionReferences)
			{
				TryResolveLocalReference(exceptionReference, exceptionReference.PlSqlProgram.Exceptions, exceptionReference.Exceptions);
			}

			ResolveSubProgramReferences(program.SubPrograms);
		}

		private static bool TryResolveLocalReference<TElement>(OraclePlSqlReference plSqlReference, IEnumerable<TElement> elements, ICollection<TElement> resolvedCollection) where TElement : OraclePlSqlElement
		{
			if (plSqlReference.OwnerNode != null)
			{
				return false;
			}

			var variablePrefix = plSqlReference.ObjectNode == null ? null : plSqlReference.FullyQualifiedObjectName.NormalizedName;
			var program = plSqlReference.PlSqlProgram;

			do
			{
				if (variablePrefix == null || GetScopeNames(program).Any(n => String.Equals(variablePrefix, n)))
				{
					foreach (var element in elements)
					{
						var isLocal = program == element.Owner;
						if (resolvedCollection.Count > 0 && !isLocal)
						{
							break;
						}

						var isWithinScope = element.ScopeNode?.SourcePosition.Contains(plSqlReference.IdentifierNode.SourcePosition) ?? true;
						var elementDefinitionIndex = element.DefinitionNode?.SourcePosition.IndexStart;
						if (isWithinScope && String.Equals(element.Name, plSqlReference.NormalizedName) && elementDefinitionIndex < plSqlReference.IdentifierNode.SourcePosition.IndexStart)
						{
							resolvedCollection.Add(element);
						}
					}

					if (resolvedCollection.Count > 0)
					{
						return true;
					}
				}

				program = program.Owner;
			} while (resolvedCollection.Count == 0 && program != null);


			return false;
		}

		private static IEnumerable<string> GetScopeNames(OraclePlSqlProgram program)
		{
			return String.IsNullOrEmpty(program.Name)
				? program.Labels.Select(l => l.Name)
				: Enumerable.Repeat(program.Name, 1);
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
					new OraclePlSqlProgram(programType, this)
					{
						RootNode = objectNode,
						ObjectIdentifier = identifier,
						Name = identifier.NormalizedName
					};

				ResolveSqlStatements(program);
				ResolveParameterDeclarations(program);
				ResolveLocalVariableTypeAndLabelDeclarations(program);

				_programs.Add(program);

				ResolveSubProgramDefinitions(program);
			}
			else
			{
				// TODO: packages/triggers/types
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

				var childStatementSemanticModel = new OracleStatementSemanticModel(sqlStatementNode.GetText(StatementText), childStatement, DatabaseModel).Build(CancellationToken);
				program.ChildModels.Add(childStatementSemanticModel);

				foreach (var queryBlock in childStatementSemanticModel.QueryBlocks)
				{
					QueryBlockNodes.Add(queryBlock.RootNode, queryBlock);
				}

				foreach (var plSqlVariableReference in childStatementSemanticModel.AllReferenceContainers.SelectMany(c => c.PlSqlVariableReferences))
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
				if (isPlSqlBlock || String.Equals(childNode.Id, NonTerminals.ProcedureDefinition) || String.Equals(childNode.Id, NonTerminals.FunctionDefinition))
				{
					var nameTerminal = childNode[0]?[Terminals.Identifier];

					subProgram =
						new OraclePlSqlProgram(isPlSqlBlock ? PlSqlProgramType.PlSqlBlock : PlSqlProgramType.NestedProgram, this)
						{
							Owner = program,
							RootNode = childNode,
							ObjectIdentifier = program.ObjectIdentifier
						};

					ResolveSqlStatements(subProgram);
					ResolveParameterDeclarations(subProgram);
					ResolveLocalVariableTypeAndLabelDeclarations(subProgram);

					if (nameTerminal != null)
					{
						subProgram.Name = nameTerminal.Token.Value.ToQuotedIdentifier();
					}

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
							if (typeIdentifierNode != null)
							{
								var type =
									new OraclePlSqlType
									{
										Owner = program,
										DefinitionNode = declarationRoot,
										Name = typeIdentifierNode.Token.Value.ToQuotedIdentifier()
									};

								program.Types.Add(type);
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
				cursor.SemanticModel = cursor.Owner.ChildModels.SingleOrDefault(m => m.Statement.RootNode == selectStatementNode);
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

			var parameterDeclarationList = parameterSourceNode?[NonTerminals.ParenthesisEnclosedParameterDeclarationList, NonTerminals.ParameterDeclarationList];
			if (parameterDeclarationList == null)
			{
				return;
			}

			var parameterDeclarations = StatementGrammarNode.GetAllChainedClausesByPath(parameterDeclarationList, null, NonTerminals.ParameterDeclarationListChained, NonTerminals.ParameterDeclarationList)
				.Select(n => n[NonTerminals.ParameterDeclaration]);
			foreach (var parameterDeclaration in parameterDeclarations)
			{
				program.Parameters.Add(ResolveParameter(parameterDeclaration));
			}

			var returnParameterNode = parameterSourceNode[NonTerminals.PlSqlDataTypeWithoutConstraint];
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
				parameter.DefaultExpressionNode = parameterDirectionDeclaration[NonTerminals.VariableDeclarationDefaultValue];
			}

			parameter.Direction = direction;

			return parameter;
		}
	}

	public enum PlSqlProgramType
	{
		PlSqlBlock,
		StandaloneProgram,
		PackageProgram,
		NestedProgram
	}

	[DebuggerDisplay("OraclePlSqlProgram (Name={Name}; ObjectOwner={ObjectIdentifier.Owner}; ObjectName={ObjectIdentifier.Name})")]
	public class OraclePlSqlProgram : OracleReferenceContainer
	{
		public OraclePlSqlProgram(PlSqlProgramType type, OraclePlSqlStatementSemanticModel semanticModel) : base(semanticModel)
		{
			Type = type;
		}

		public PlSqlProgramType Type { get; }

		public StatementGrammarNode RootNode { get; set; }

		public OracleObjectIdentifier ObjectIdentifier { get; set; }

		public string Name { get; set; }

		public IList<OracleStatementSemanticModel> ChildModels { get; } = new List<OracleStatementSemanticModel>();

		public IList<OraclePlSqlProgram> SubPrograms { get; } = new List<OraclePlSqlProgram>();

		public OraclePlSqlProgram Owner { get; set; }

		public IList<OraclePlSqlParameter> Parameters { get; } = new List<OraclePlSqlParameter>();

		public IList<OraclePlSqlType> Types { get; } = new List<OraclePlSqlType>();

		public IList<OraclePlSqlVariable> Variables { get; } = new List<OraclePlSqlVariable>();

		public IList<OraclePlSqlException> Exceptions { get; } = new List<OraclePlSqlException>();

		public IList<OraclePlSqlLabel> Labels { get; } = new List<OraclePlSqlLabel>();

		public OraclePlSqlParameter ReturnParameter { get; set; }

		public IEnumerable<OraclePlSqlVariable> AccessibleVariables
		{
			get
			{
				var variables = (IEnumerable<OraclePlSqlVariable>)Variables;
				if (Owner != null)
				{
					variables = variables.Concat(Owner.AccessibleVariables);
				}

				return variables;
			}
		}

		public IEnumerable<OraclePlSqlProgram> AllSubPrograms
		{
			get
			{
				foreach (var program in SubPrograms)
				{
					yield return program;

					if (program.SubPrograms.Count == 0)
					{
						continue;
					}

					foreach (var subProgram in program.AllSubPrograms)
					{
						yield return subProgram;
					}
				}
			}
		}
	}

	public abstract class OraclePlSqlReference : OracleReference
	{
		public StatementGrammarNode IdentifierNode { get; set; }

		public OraclePlSqlProgram PlSqlProgram { get; set; }

		public override string Name => IdentifierNode.Token.Value;
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
	}

	public class OraclePlSqlException : OraclePlSqlElement
	{
		public int? ErrorCode { get; set; }
	}

	public class OraclePlSqlVariable : OraclePlSqlElement
	{
		public bool IsConstant { get; set; }

		public bool Nullable { get; set; }

		public OracleDataType DataType { get; set; }

		public StatementGrammarNode DefaultExpressionNode { get; set; }

		public StatementGrammarNode DataTypeNode { get; set; }
	}

	public class OraclePlSqlCursorVariable : OraclePlSqlVariable
	{
		public OracleStatementSemanticModel SemanticModel { get; set; }
	}

	public class OraclePlSqlParameter : OraclePlSqlVariable
	{
		public ParameterDirection Direction { get; set; }

		public bool IsReadOnly => Direction == ParameterDirection.Input;
	}

	public class OraclePlSqlType : OraclePlSqlElement
	{
	}

	public class OraclePlSqlLabel : OraclePlSqlElement
	{
		public StatementGrammarNode Node { get; set; }
	}
}
