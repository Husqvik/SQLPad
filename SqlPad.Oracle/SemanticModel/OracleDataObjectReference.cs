using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.SemanticModel
{
	public abstract class OracleObjectWithColumnsReference : OracleReference
	{
		private readonly List<OracleQueryBlock> _queryBlocks = new List<OracleQueryBlock>();

		public abstract IReadOnlyList<OracleColumn> Columns { get; }

		public abstract IReadOnlyList<OracleColumn> PseudoColumns { get; }

		public virtual ICollection<OracleQueryBlock> QueryBlocks => _queryBlocks;

		public abstract ReferenceType Type { get; }
	}

	public class OracleHierarchicalClauseReference : OracleObjectWithColumnsReference
	{
		private static readonly OracleColumn[] EmptyArray = new OracleColumn[0];

		public const string ColumnConnectByIsLeaf = "\"CONNECT_BY_ISLEAF\"";
		public const string ColumnConnectByIsCycle = "\"CONNECT_BY_ISCYCLE\"";

		public OracleColumn ConnectByIsLeafColumn { get; } =
				new OracleColumn(true)
				{
					Name = ColumnConnectByIsLeaf,
					DataType = OracleDataType.NumberType
				};

		public OracleColumn ConnectByIsCycleColumn { get; } =
			new OracleColumn(true)
			{
				Name = ColumnConnectByIsCycle,
				DataType = OracleDataType.NumberType
			};

		public override string Name { get; } = String.Empty;

		public override IReadOnlyList<OracleColumn> Columns { get; } = EmptyArray;

		public override IReadOnlyList<OracleColumn> PseudoColumns { get; }

		public override ReferenceType Type { get; } = ReferenceType.HierarchicalClause;

		public OracleHierarchicalClauseReference(bool includeIsCycleColumn)
		{
			var pseudoColumns = new List<OracleColumn>(2) { ConnectByIsLeafColumn };
			if (includeIsCycleColumn)
			{
				pseudoColumns.Add(ConnectByIsCycleColumn);
			}

			PseudoColumns = pseudoColumns;
		}
	}

	public class OraclePartitionReference : OracleReference
	{
		public override string Name => ObjectNode.Token.Value;

		public OraclePartitionBase Partition { get; set; }
		
		public OracleDataObjectReference DataObjectReference { get; set; }

		public override void Accept(IOracleReferenceVisitor visitor)
		{
			visitor.VisitPartitionReference(this);
		}
	}

	[DebuggerDisplay("OracleDataObjectReference (Owner={OwnerNode == null ? null : OwnerNode.Token.Value}; Table={Type != SqlPad.Oracle.SemanticModel.ReferenceType.InlineView ? ObjectNode.Token.Value : \"<Nested subquery>\"}; Alias={AliasNode == null ? null : AliasNode.Token.Value}; Type={Type}; IsOuterJoined={IsOuterJoined})")]
	public class OracleDataObjectReference : OracleObjectWithColumnsReference
	{
		private IReadOnlyList<OracleColumn> _columns;
		private IReadOnlyList<OracleColumn> _pseudoColumns;

		internal static readonly string RowIdNormalizedName = TerminalValues.RowIdPseudoColumn.ToQuotedIdentifier();

		public static readonly OracleDataObjectReference[] EmptyArray = new OracleDataObjectReference[0];

		public bool IsOuterJoined { get; set; }

		public OracleDataObjectReference(ReferenceType referenceType)
		{
			Type = referenceType;
		}

		public override string Name { get { throw new NotSupportedException(); } }

		public OraclePartitionReference PartitionReference { get; set; }

		protected override OracleObjectIdentifier BuildFullyQualifiedObjectName()
		{
			return OracleObjectIdentifier.Create(
					AliasNode == null ? OwnerNode : null,
					Type == ReferenceType.InlineView ? null : ObjectNode, AliasNode);
		}

		public virtual IEnumerable<OracleDataObjectReference> IncludeInnerReferences => Enumerable.Repeat(this, 1);

		public override IReadOnlyList<OracleColumn> PseudoColumns
		{
			get
			{
				if (_pseudoColumns != null)
				{
					return _pseudoColumns;
				}

				var pseudoColumns = new List<OracleColumn>();
				var table = SchemaObject.GetTargetSchemaObject() as OracleTable;
				if (Type == ReferenceType.SchemaObject && table != null)
				{
					if (table.Organization == OrganizationType.Heap || table.Organization == OrganizationType.Index)
					{
						var rowIdPseudoColumn =
							new OracleColumn(true)
							{
								Name = RowIdNormalizedName,
								DataType =
									new OracleDataType
									{
										FullyQualifiedName = OracleObjectIdentifier.Create(null, table.Organization == OrganizationType.Index ? TerminalValues.UniversalRowId : TerminalValues.RowIdDataType)
									}
							};

						pseudoColumns.Add(rowIdPseudoColumn);
					}

					if (FlashbackOption == FlashbackOption.None || FlashbackOption == FlashbackOption.AsOf)
					{
						var rowSystemChangeNumberPseudoColumn =
							new OracleColumn(true)
							{
								Name = "\"ORA_ROWSCN\"",
								DataType = OracleDataType.NumberType
							};

						pseudoColumns.Add(rowSystemChangeNumberPseudoColumn);
					}
					else if ((FlashbackOption & FlashbackOption.Versions) == FlashbackOption.Versions)
					{
						var flashbackVersionColumns =
							new[]
								{
									new OracleColumn(true)
									{
										Name = "\"VERSIONS_STARTTIME\"",
										DataType = OracleDataType.CreateTimestampDataType(0)
									},
									new OracleColumn(true)
									{
										Name = "\"VERSIONS_ENDTIME\"",
										DataType = OracleDataType.CreateTimestampDataType(0)
									},
									new OracleColumn(true)
									{
										Name = "\"VERSIONS_STARTSCN\"",
										DataType = OracleDataType.NumberType
									},
									new OracleColumn(true)
									{
										Name = "\"VERSIONS_ENDSCN\"",
										DataType = OracleDataType.NumberType
									},
									new OracleColumn(true)
									{
										Name = "\"VERSIONS_OPERATION\"",
										DataType = new OracleDataType {FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Varchar2), Unit = DataUnit.Byte, Length = 1}
									},
									new OracleColumn(true)
									{
										Name = "\"VERSIONS_XID\"",
										DataType = new OracleDataType {FullyQualifiedName = OracleObjectIdentifier.Create(null, TerminalValues.Raw), Length = 8}
									}
								};

						pseudoColumns.AddRange(flashbackVersionColumns);
					}
				}

				return _pseudoColumns = pseudoColumns.AsReadOnly();
			}
		}

		public override IReadOnlyList<OracleColumn> Columns
		{
			get
			{
				if (_columns != null)
				{
					return _columns;
				}

				var columns = new List<OracleColumn>();
				if (Type == ReferenceType.SchemaObject)
				{
					var dataObject = SchemaObject.GetTargetSchemaObject() as OracleDataObject;
					if (dataObject != null)
					{
						columns.AddRange(dataObject.Columns.Values);
					}
				}
				else
				{
					var queryColumns = QueryBlocks.SelectMany(qb => qb.Columns)
						.Where(c => !c.IsAsterisk)
						.Select(c => c.ColumnDescription);

					columns.AddRange(queryColumns);
				}

				return _columns = columns.AsReadOnly();
			}
		}
		
		public StatementGrammarNode AliasNode { get; set; }

		public FlashbackOption FlashbackOption { get; set; }
		
		public override ReferenceType Type { get; }

		public override void Accept(IOracleReferenceVisitor visitor)
		{
			visitor.VisitDataObjectReference(this);
		}
	}

	[Flags]
	public enum FlashbackOption
	{
		None,
		AsOf,
		Versions
	}
}
