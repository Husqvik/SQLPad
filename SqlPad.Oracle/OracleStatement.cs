using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleStatement (ParseStatus={ParseStatus}; ChildNodes={RootNode == null ? 0 : RootNode.ChildNodes.Count})")]
	public class OracleStatement : StatementBase
	{
		public const string DefaultMessageCommandExecutedSuccessfully = "Command executed successfully. ";

		private ICollection<BindVariableConfiguration> _bindVariables;

		public static readonly OracleStatement EmptyStatement =
			new OracleStatement
			{
				ParseStatus = ParseStatus.Success,
				SourcePosition = SourcePosition.Empty
			};

		public override ICollection<BindVariableConfiguration> BindVariables => _bindVariables ?? (_bindVariables = BuildBindVariableCollection());

		public override bool IsDataManipulation
		{
			get
			{
				var statement = RootNode[NonTerminals.Statement];
				return statement != null && statement[0].Id.In(NonTerminals.InsertStatement, NonTerminals.UpdateStatement, NonTerminals.DeleteStatement, NonTerminals.MergeStatement);
			}
		}

		public bool IsPlSql => !String.Equals(RootNode.Id, NonTerminals.StandaloneStatement);

		public bool IsSelect => RootNode.GetAncestor(NonTerminals.SelectStatement) != null;

		public string BuildExecutionFeedbackMessage(int? affectedRows, bool hasCompilationError)
		{
			if (String.Equals(RootNode.Id, NonTerminals.PlSqlBlockStatement))
			{
				return "PL/SQL procedure successfully completed. ";
			}

			if (String.Equals(RootNode.Id, NonTerminals.CreatePlSqlStatement))
			{
				var compilationErrorPostfix = hasCompilationError ? " with compilation errors" : null;
				switch (RootNode[NonTerminals.CreatePlSqlObjectClause]?[0].Id)
				{
					case NonTerminals.CreateFunction:
						return $"Function created{compilationErrorPostfix}. ";
					case NonTerminals.CreateProcedure:
						return $"Procedure created{compilationErrorPostfix}. ";
					case NonTerminals.CreatePackageBody:
						return $"Package body created{compilationErrorPostfix}. ";
					case NonTerminals.CreatePackage:
						return $"Package created{compilationErrorPostfix}. ";
					case NonTerminals.CreateTrigger:
						return $"Trigger created{compilationErrorPostfix}. ";
					case NonTerminals.CreateTypeBody:
						return $"Type body created{compilationErrorPostfix}. ";
					case NonTerminals.CreateType:
						return $"Type created{compilationErrorPostfix}. ";
				}
			}

			var statementNode = RootNode[0, 0];
			var pluralPostfix = affectedRows == 1 ? null : "s";
			switch (statementNode?.Id)
			{
				case NonTerminals.InsertStatement:
					return $"{affectedRows} row{pluralPostfix} created. ";

				case NonTerminals.UpdateStatement:
					return $"{affectedRows} row{pluralPostfix} updated. ";

				case NonTerminals.DeleteStatement:
					return $"{affectedRows} row{pluralPostfix} deleted. ";

				case NonTerminals.MergeStatement:
					return "Rows merged. ";

				case NonTerminals.CreateSqlStatement:
					switch (statementNode[1, 0]?.Id)
					{
						case NonTerminals.CreateTable:
							return "Table created. ";
						case NonTerminals.CreateIndex:
							return "Index created. ";
						case NonTerminals.CreateSynonym:
							return "Synonym created. ";
						case NonTerminals.CreateSequence:
							return "Sequence created. ";
						case NonTerminals.CreateView:
							return "View created. ";
						case NonTerminals.CreateCluster:
							return "Cluster created. ";
						case NonTerminals.CreateMaterializedViewLog:
							return "Materialized view log created. ";
						case NonTerminals.CreateDirectory:
							return "Directory created. ";
						case NonTerminals.CreateProfile:
							return "Profile created. ";
						case NonTerminals.CreateTablespace:
							return "Tablespace created. ";
						case NonTerminals.CreateUser:
							return "User created. ";
						case NonTerminals.CreateRole:
							return "Role created. ";
						case NonTerminals.CreateRollbackSegment:
							return "Rollback segment created. ";
						case NonTerminals.CreateRestorePoint:
							return "Restore point created. ";
						case NonTerminals.CreateSchemaAuthorization:
							return "Schema created. ";
						case NonTerminals.CreateDatabaseLink:
							return "Database link created. ";
						case NonTerminals.CreateParameterFile:
						case NonTerminals.CreateServerParameterFile:
							return "File created. ";
						default:
							return DefaultMessageCommandExecutedSuccessfully;
					}

				case NonTerminals.AlterStatement:
					switch (statementNode[1, 0]?.Id)
					{
						case NonTerminals.AlterSession:
							return "Session altered. ";
						case NonTerminals.AlterProcedure:
							return "Procedure altered. ";
						case NonTerminals.AlterFunction:
							return "Function altered. ";
						case NonTerminals.AlterPackage:
							return "Package altered. ";
						case NonTerminals.AlterTable:
							return "Table altered. ";
						case NonTerminals.AlterProfile:
							return "Profile altered. ";
						case NonTerminals.AlterTablespace:
							return "Tablespace altered. ";
						case NonTerminals.AlterView:
							return "View altered. ";
						case NonTerminals.AlterLibrary:
							return "Library altered. ";
						case NonTerminals.AlterRole:
							return "Role altered. ";
						case NonTerminals.AlterRollbackSegment:
							return "Rollback segment altered. ";
						case NonTerminals.AlterResourceCost:
							return "Resource cost altered. ";
						case NonTerminals.AlterUser:
							return "User altered. ";
						case NonTerminals.AlterMaterializedView:
							return "Materialized view altered. ";
						case NonTerminals.AlterSystem:
							return "System altered. ";
						case NonTerminals.AlterTrigger:
							return "Trigger altered. ";
						case NonTerminals.AlterSynonym:
							return "Synonym altered. ";
						default:
							return DefaultMessageCommandExecutedSuccessfully;
					}
				case NonTerminals.DropStatement:
					var rootDropClause = statementNode[1, 0];
					switch (rootDropClause?.Id)
					{
						case NonTerminals.DropTable:
							return "Table dropped. ";
						case NonTerminals.DropView:
							return "View dropped. ";
						case NonTerminals.DropIndex:
							return "Index dropped. ";
						case NonTerminals.DropPackage:
							return "Package dropped. ";
						case NonTerminals.DropContext:
							return "Context dropped. ";
						case NonTerminals.DropCluster:
							return "Cluster dropped. ";
						case NonTerminals.DropMaterializedViewLog:
							return "Materialized view log dropped. ";
						case NonTerminals.DropMaterializedView:
							return "Materialized view dropped. ";
						case NonTerminals.DropSynonym:
							return "Synonym dropped. ";
						case NonTerminals.DropType:
							return "Type dropped. ";
						case NonTerminals.DropDatabaseLink:
							return "Database link dropped. ";
						case NonTerminals.DropTablespace:
							return "Tablespace dropped. ";
						case NonTerminals.DropUser:
							return "User dropped. ";
						case NonTerminals.DropOther:
							switch (rootDropClause?[0, 0]?.Id)
							{
								case Terminals.Function:
									return "Function dropped. ";
								case Terminals.Procedure:
									return "Procedure dropped. ";
								case Terminals.Sequence:
									return "Sequence dropped. ";
								case Terminals.Directory:
									return "Directory dropped. ";
								case Terminals.Trigger:
									return "Trigger dropped. ";
								case Terminals.Restore:
									return "Restore point dropped. ";
								default:
									return DefaultMessageCommandExecutedSuccessfully;
							}

						default:
							return DefaultMessageCommandExecutedSuccessfully;
					}
				case NonTerminals.CallStatement:
					return "Call completed. ";
				case NonTerminals.CommentStatement:
					return "Comment created. ";
				case NonTerminals.CommitStatement:
					return "Commit complete. ";
				case NonTerminals.SavepointStatement:
					return "Savepoint created.";
				case NonTerminals.RollbackStatement:
					return "Rollback complete. ";
				case NonTerminals.RenameStatement:
					return "Object renamed. ";
				case NonTerminals.ExplainPlanStatement:
					return "Explained. ";
				case NonTerminals.SetConstraintStatement:
					return "Constraint set. ";
				case NonTerminals.GrantStatement:
					return "Grant succeeded. ";
				case NonTerminals.TruncateStatement:
					return "Table truncated. ";
				case NonTerminals.SetTransactionStatement:
					return "Transaction set. ";
				case NonTerminals.FlashbackStatement:
					return "Flashback complete. ";
				case NonTerminals.PurgeStatement:
					switch (statementNode[NonTerminals.PurgeOption]?.FirstTerminalNode?.Id)
					{
						case Terminals.Table:
							return "Table purged. ";
						case Terminals.Index:
							return "Index purged. ";
						case Terminals.RecycleBin:
							return "Recyclebin purged. ";
						case Terminals.DbaRecycleBin:
							return "DBA Recyclebin purged. ";
						case Terminals.Tablespace:
							return "Tablespace purged. ";
						default:
							return DefaultMessageCommandExecutedSuccessfully;
					}

				case NonTerminals.AnalyzeStatement:
					switch (statementNode[NonTerminals.AnalyzedObject]?.FirstTerminalNode?.Id)
					{
						case Terminals.Table:
							return "Table analyzed. ";
						case Terminals.Index:
							return "Index analyzed. ";
						case Terminals.Cluster:
							return "Cluster analyzed. ";
						default:
							return DefaultMessageCommandExecutedSuccessfully;
					}

				default:
					return DefaultMessageCommandExecutedSuccessfully;
			}
		}

		private ICollection<BindVariableConfiguration> BuildBindVariableCollection()
		{
			return BuildBindVariableIdentifierTerminalLookup()
				.OrderBy(g => g.Key)
				.Select(g =>
					new BindVariableConfiguration
					{
						Name = g.Key,
						DataType = TerminalValues.Varchar2,
						Nodes = g.ToList().AsReadOnly(),
						DataTypes = OracleBindVariable.DataTypes
					})
				.ToArray();
		}

		private IEnumerable<IGrouping<string, StatementGrammarNode>> BuildBindVariableIdentifierTerminalLookup()
		{
			return AllTerminals.Where(t => t.Id == Terminals.BindVariableIdentifier)
				.ToLookup(t => t.Token.Value.ToNormalizedBindVariableIdentifier());
		}

		public static bool TryGetPlSqlUnitName(StatementBase statement, out OracleObjectIdentifier objectIdentifier)
		{
			objectIdentifier = OracleObjectIdentifier.Empty;
			
			switch (statement.RootNode.Id)
			{
				case NonTerminals.CreatePlSqlStatement:
					var createPlSqlObjectClause = statement.RootNode[NonTerminals.CreatePlSqlObjectClause];
					if (createPlSqlObjectClause != null && createPlSqlObjectClause.ChildNodes.Count == 1)
					{
						var createFunctionNode = createPlSqlObjectClause[NonTerminals.CreateFunction, NonTerminals.PlSqlFunctionSource];
						createPlSqlObjectClause = createFunctionNode ?? createPlSqlObjectClause.ChildNodes[0];
						
						objectIdentifier = GetObjectIdentifierFromNode(createPlSqlObjectClause[NonTerminals.SchemaObject]);
					}

					break;
				case NonTerminals.StandaloneStatement:
					var alterObjectClause = statement.RootNode[NonTerminals.Statement, NonTerminals.AlterStatement, NonTerminals.AlterObjectClause];
					if (alterObjectClause != null && alterObjectClause.ChildNodes.Count == 1 && alterObjectClause.ChildNodes[0].Id.In(NonTerminals.AlterProcedure, NonTerminals.AlterFunction, NonTerminals.AlterPackage))
					{
						objectIdentifier = GetObjectIdentifierFromNode(alterObjectClause.ChildNodes[0][NonTerminals.SchemaObject]);
					}

					break;
			}

			return !String.IsNullOrEmpty(objectIdentifier.Name);
		}

		private static OracleObjectIdentifier GetObjectIdentifierFromNode(StatementGrammarNode node)
		{
			if (node == null)
			{
				return OracleObjectIdentifier.Empty;
			}

			var schemaIdentifierTerminal = node[NonTerminals.SchemaPrefix, Terminals.SchemaIdentifier];
			var objectIdentifierTerminal = node[Terminals.ObjectIdentifier];
			return objectIdentifierTerminal == null
				? OracleObjectIdentifier.Empty
				: OracleObjectIdentifier.Create(schemaIdentifierTerminal, objectIdentifierTerminal, null);
		}
	}

	public class OracleStatementCollection : StatementCollection
	{
		private static readonly OracleFoldingSectionProvider FoldingSectionProvider = new OracleFoldingSectionProvider();

		public OracleStatementCollection(IList<StatementBase> statements, IReadOnlyList<IToken> tokens, IEnumerable<StatementCommentNode> comments)
			: base(statements, tokens, comments)
		{
		}

		public override IEnumerable<FoldingSection> FoldingSections => FoldingSectionProvider.GetFoldingSections(Tokens);
	}
}
