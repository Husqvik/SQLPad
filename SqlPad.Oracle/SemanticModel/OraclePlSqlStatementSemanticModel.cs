using System;
using System.Collections.Generic;
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
				return Programs
					.SelectMany(p => p.ChildModels)
					.SelectMany(m => m.AllReferenceContainers)
					.Concat(Programs);
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
			foreach (var program in Programs)
			{
				foreach (var statementTypeNode in program.RootNode.GetPathFilterDescendants(n => !String.Equals(n.Id, NonTerminals.PlSqlSqlStatement) && (!String.Equals(n.Id, NonTerminals.PlSqlBlock) || !String.Equals(n.ParentNode.Id, NonTerminals.PlSqlStatementType)), NonTerminals.PlSqlStatementType))
				{
					var statementNode = statementTypeNode[0];
					if (statementNode == null)
					{
						continue;
					}

					switch (statementNode.Id)
					{
						case NonTerminals.PlSqlProcedureCall:
							var node = statementNode[NonTerminals.PrefixedProgramIdentifier];
							var parameterListNode = statementNode[NonTerminals.ParenthesisEnclosedFunctionParameters];
							var programReference =
								new OracleProgramReference
								{
									RootNode = statementNode,
									ProgramIdentifierNode = node[Terminals.Identifier],
									ObjectNode = node[NonTerminals.Prefix, NonTerminals.ObjectPrefix, Terminals.ObjectIdentifier],
									OwnerNode = node[NonTerminals.Prefix, NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier],
									DatabaseLinkNode = null,
									ParameterListNode = parameterListNode,
									ParameterReferences = ResolvedParameterReferences(parameterListNode)
								};

							FindPlSqlReferences(program, parameterListNode);
							program.ProgramReferences.Add(programReference);

							break;

						default:
							FindPlSqlReferences(program, statementNode);
							break;
					}
				}
			}

			ResolveSubProgramReferences(Programs);
		}

		private void FindPlSqlReferences(OraclePlSqlProgram program, StatementGrammarNode node)
		{
			if (node == null)
			{
				return;
			}

			var identifiers = node.GetPathFilterDescendants(NodeFilters.BreakAtPlSqlSubProgramOrSqlCommand, Terminals.Identifier, Terminals.RowIdPseudoColumn, Terminals.Level, Terminals.RowNumberPseudoColumn);
			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, program, identifiers, StatementPlacement.None, null);

			var grammarSpecificFunctions = GetGrammarSpecificFunctionNodes(node);
			CreateGrammarSpecificFunctionReferences(grammarSpecificFunctions, null, program.ProgramReferences, StatementPlacement.None, null);

			var assignmentTargetIdentifiers = node
				.GetPathFilterDescendants(NodeFilters.BreakAtPlSqlSubProgramOrSqlCommand, NonTerminals.BindVariableExpressionOrPlSqlTarget)
				.SelectMany(t => t.GetDescendants(Terminals.PlSqlIdentifier));

			ResolveColumnFunctionOrDataTypeReferencesFromIdentifiers(null, program, assignmentTargetIdentifiers, StatementPlacement.None, null);
		}

		private void ResolvePlSqlReferences(OraclePlSqlProgram program)
		{
			foreach (var columnReference in program.ColumnReferences)
			{
				var variableReference =
					new OraclePlSqlVariableReference
					{
						PlSqlProgram = program,
						IdentifierNode = columnReference.ColumnNode
					};

				variableReference.CopyPropertiesFrom(columnReference);
				TryResolveLocalVariableReference(variableReference);

				program.PlSqlVariableReferences.Add(variableReference);
			}

			program.ColumnReferences.Clear();

			ResolveFunctionReferences(program.ProgramReferences);

			ResolveSubProgramReferences(program.SubPrograms);
		}

		private bool TryResolveLocalVariableReference(OraclePlSqlVariableReference variableReference)
		{
			if (variableReference.ObjectNode != null)
			{
				return false;
			}

			var program = variableReference.PlSqlProgram;

			do
			{
				foreach (var variable in variableReference.PlSqlProgram.Variables)
				{
					if (String.Equals(variable.Name, variableReference.NormalizedName))
					{
						variableReference.Variables.Add(variable);
					}
				}

				foreach (var parameter in variableReference.PlSqlProgram.Parameters)
				{
					if (String.Equals(parameter.Name, variableReference.NormalizedName))
					{
						variableReference.Variables.Add(parameter);
					}
				}

				if (variableReference.Variables.Count > 0)
				{
					return true;
				}

				program = program.Owner;
			} while (variableReference.Variables.Count == 0 && program != null);


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
			var identifier = OracleObjectIdentifier.Empty;

			var anonymousPlSqlBlock = String.Equals(Statement.RootNode.Id, NonTerminals.PlSqlBlockStatement);
			var functionOrProcedure = Statement.RootNode[NonTerminals.CreatePlSqlObjectClause]?[0];
			var isSchemaProcedure = String.Equals(functionOrProcedure?.Id, NonTerminals.CreateProcedure);
			var isSchemaFunction = String.Equals(functionOrProcedure?.Id, NonTerminals.CreateFunction);
			if (isSchemaProcedure || isSchemaFunction || anonymousPlSqlBlock)
			{
				StatementGrammarNode schemaObjectNode = null;
				if (isSchemaFunction)
				{
					schemaObjectNode = functionOrProcedure[NonTerminals.PlSqlFunctionSource, NonTerminals.SchemaObject];
				}
				else if (isSchemaProcedure)
				{
					schemaObjectNode = functionOrProcedure[NonTerminals.SchemaObject];
				}
				else
				{
					functionOrProcedure = Statement.RootNode;
				}

				if (schemaObjectNode != null)
				{
					var owner = schemaObjectNode[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier]?.Token.Value ?? DatabaseModel.CurrentSchema;
					var name = schemaObjectNode[Terminals.ObjectIdentifier]?.Token.Value;
					identifier = OracleObjectIdentifier.Create(owner, name);
				}

				var program =
					new OraclePlSqlProgram(this)
					{
						RootNode = functionOrProcedure,
						ObjectIdentifier = identifier,
						Name = identifier.NormalizedName
					};

				ResolveParameterDeclarations(program);
				ResolveLocalVariableAndTypeDeclarations(program);
				ResolveSqlStatements(program);

				_programs.Add(program);

				ResolveSubProgramDefinitions(program);
			}
			else
			{
				// TODO: packages
				//var programDefinitionNodes = Statement.RootNode.GetDescendants(NonTerminals.FunctionDefinition, NonTerminals.ProcedureDefinition);
				//_programs.AddRange(programDefinitionNodes.Select(n => new OraclePlSqlProgram { RootNode = n }));
			}
		}

		private void ResolveSqlStatements(OraclePlSqlProgram program)
		{
			var sqlStatementNodes = program.RootNode.GetPathFilterDescendants(
				n => !String.Equals(n.Id, NonTerminals.ItemList2) && (!String.Equals(n.Id, NonTerminals.PlSqlBlock) || !String.Equals(n.ParentNode.Id, NonTerminals.PlSqlStatementType)),
				NonTerminals.SelectStatement, NonTerminals.InsertStatement, NonTerminals.UpdateStatement, NonTerminals.MergeStatement);

			foreach (var sqlStatementNode in sqlStatementNodes)
			{
				var childStatement = new OracleStatement { RootNode = sqlStatementNode, ParseStatus = Statement.ParseStatus, SourcePosition = sqlStatementNode.SourcePosition };
				var childStatementSemanticModel = new OracleStatementSemanticModel(sqlStatementNode.GetText(StatementText), childStatement, DatabaseModel).Build(CancellationToken);
				program.ChildModels.Add(childStatementSemanticModel);

				foreach (var queryBlock in childStatementSemanticModel.QueryBlocks)
				{
					QueryBlockNodes.Add(queryBlock.RootNode, queryBlock);
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
				if (String.Equals(childNode.Id, NonTerminals.ProcedureDefinition) || String.Equals(childNode.Id, NonTerminals.FunctionDefinition) || String.Equals(childNode.Id, NonTerminals.PlSqlBlock))
				{
					var nameTerminal = childNode[0]?[Terminals.Identifier];

					subProgram =
						new OraclePlSqlProgram(this)
						{
							Owner = program,
							RootNode = childNode,
							ObjectIdentifier = program.ObjectIdentifier
						};

					ResolveParameterDeclarations(subProgram);
					ResolveLocalVariableAndTypeDeclarations(subProgram);
					ResolveSqlStatements(subProgram);

					if (nameTerminal != null)
					{
						subProgram.Name = nameTerminal.Token.Value.ToQuotedIdentifier();
					}

					program.SubPrograms.Add(subProgram);
				}

				ResolveSubProgramDefinitions(subProgram, childNode);
			}
		}

		private void ResolveLocalVariableAndTypeDeclarations(OraclePlSqlProgram program)
		{
			StatementGrammarNode programSourceNode;
			switch (program.RootNode.Id)
			{
				case NonTerminals.CreateFunction:
					programSourceNode = program.RootNode[NonTerminals.PlSqlFunctionSource, NonTerminals.FunctionTypeDefinition, NonTerminals.ProgramImplentationDeclaration];
					break;
				case NonTerminals.PlSqlBlockStatement:
					programSourceNode = program.RootNode[NonTerminals.PlSqlBlock, NonTerminals.PlSqlBlockDeclareSection];
					break;
				case NonTerminals.PlSqlBlock:
					programSourceNode = program.RootNode[NonTerminals.PlSqlBlockDeclareSection];
					break;
				default:
					programSourceNode = program.RootNode[NonTerminals.ProgramImplentationDeclaration];
					break;
			}

			var item1 = programSourceNode?[NonTerminals.ProgramDeclareSection, NonTerminals.ItemList1, NonTerminals.Item1];
			if (item1 == null)
			{
				return;
			}

			var itemDeclarations = StatementGrammarNode.GetAllChainedClausesByPath(item1, n => String.Equals(n.ParentNode.Id, NonTerminals.Item1OrPragmaDefinition) ? n.ParentNode.ParentNode : n.ParentNode, NonTerminals.ItemList1Chained, NonTerminals.Item1OrPragmaDefinition, NonTerminals.Item1).ToArray();
			foreach (var itemDeclaration in itemDeclarations)
			{
				var variable = new OraclePlSqlVariable();
				var declarationRoot = itemDeclaration[0];
				StatementGrammarNode identifierNode = null;
				switch (declarationRoot?.Id)
				{
					case NonTerminals.ItemDeclaration:
						var specificNode = declarationRoot[0];
						if (specificNode != null)
						{
							switch (specificNode.Id)
							{
								case NonTerminals.ConstantDeclaration:
									variable.IsConstant = true;
									identifierNode = specificNode[Terminals.Identifier];
									break;

								case NonTerminals.ExceptionDeclaration:
									variable.IsException = true;
									identifierNode = specificNode[Terminals.ExceptionIdentifier];
									break;

								case NonTerminals.VariableDeclaration:
									identifierNode = specificNode[NonTerminals.FieldDefinition, Terminals.Identifier];
									break;
							}
						}

						break;

					case NonTerminals.TypeDefinition:
						var typeIdentifierNode = declarationRoot[Terminals.TypeIdentifier];
						if (typeIdentifierNode != null)
						{
							program.Types.Add(new OraclePlSqlType { Name = typeIdentifierNode.Token.Value.ToQuotedIdentifier() });
						}

						break;
				}

				if (identifierNode != null)
				{
					variable.Name = identifierNode.Token.Value.ToQuotedIdentifier();
					program.Variables.Add(variable);
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
						Direction = ParameterDirection.ReturnValue,
						//DataType = null
					};
			}
		}

		private static OraclePlSqlParameter ResolveParameter(StatementGrammarNode parameterDeclaration)
		{
			var direction = ParameterDirection.Input;
			if (parameterDeclaration[NonTerminals.ParameterDirectionDeclaration, Terminals.Out] != null)
			{
				direction = parameterDeclaration[Terminals.In] == null ? ParameterDirection.Output : ParameterDirection.InputOutput;
			}

			return
				new OraclePlSqlParameter
				{
					Name = parameterDeclaration[Terminals.ParameterIdentifier].Token.Value.ToQuotedIdentifier(),
					Direction = direction
				};
		}
	}

	public class OraclePlSqlProgram : OracleReferenceContainer
	{
		public OraclePlSqlProgram(OraclePlSqlStatementSemanticModel semanticModel) : base(semanticModel)
		{
		}

		public StatementGrammarNode RootNode { get; set; }

		public OracleObjectIdentifier ObjectIdentifier { get; set; }

		public string Name { get; set; }

		public IList<OracleStatementSemanticModel> ChildModels { get; } = new List<OracleStatementSemanticModel>();

		public IList<OraclePlSqlProgram> SubPrograms { get; } = new List<OraclePlSqlProgram>();

		public OraclePlSqlProgram Owner { get; set; }

		public IList<OraclePlSqlParameter> Parameters { get; } = new List<OraclePlSqlParameter>();

		public IList<OraclePlSqlType> Types { get; } = new List<OraclePlSqlType>();

		public IList<OraclePlSqlVariable> Variables { get; } = new List<OraclePlSqlVariable>();

		public OraclePlSqlParameter ReturnParameter { get; set; }
	}

	public class OraclePlSqlVariableReference : OracleReference
	{
		public StatementGrammarNode IdentifierNode { get; set; }

		public OraclePlSqlProgram PlSqlProgram { get; set; }

		public OraclePlSqlVariableReference ChainedVariableReference { get; set; }

		public override string Name => IdentifierNode.Token.Value;

		public ICollection<OraclePlSqlElement> Variables { get; } = new List<OraclePlSqlElement>();
	}

	public abstract class OraclePlSqlElement
	{
		public string Name { get; set; }
	}

	public class OraclePlSqlVariable : OraclePlSqlElement
	{
		public bool IsConstant { get; set; }

		public bool IsException { get; set; }

		//public OracleDataType DataType { get; set; }
	}

	public class OraclePlSqlType : OraclePlSqlElement
	{
	}

	public class OraclePlSqlParameter : OraclePlSqlElement
	{
		public ParameterDirection Direction { get; set; }

		//public OracleDataType DataType { get; set; }
	}
}
