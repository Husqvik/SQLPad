using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;
using NonTerminals = SqlPad.Oracle.OracleGrammarDescription.NonTerminals;
using TerminalValues = SqlPad.Oracle.OracleGrammarDescription.TerminalValues;

namespace SqlPad.Oracle.SemanticModel
{
	public abstract class OracleObjectWithColumnsReference : OracleReference
	{
		private IReadOnlyList<OracleColumn> _columns;
		private IReadOnlyList<OracleColumn> _pseudoColumns;
		private readonly List<OracleQueryBlock> _queryBlocks = new List<OracleQueryBlock>();

		public IReadOnlyList<OracleColumn> Columns => _columns ?? (_columns = BuildColumns());

		public IReadOnlyList<OracleColumn> Pseudocolumns => _pseudoColumns ?? (_pseudoColumns = BuildPseudocolumns());

		public virtual ICollection<OracleQueryBlock> QueryBlocks => _queryBlocks;

		public abstract ReferenceType Type { get; }

		protected abstract IReadOnlyList<OracleColumn> BuildColumns();

		protected abstract IReadOnlyList<OracleColumn> BuildPseudocolumns();
	}

	public class OracleHierarchicalClauseReference : OracleObjectWithColumnsReference
	{
		private static readonly OracleColumn[] EmptyArray = new OracleColumn[0];

		public const string ColumnNameConnectByIsLeaf = "\"CONNECT_BY_ISLEAF\"";
		public const string ColumnNameConnectByIsCycle = "\"CONNECT_BY_ISCYCLE\"";

		public OracleColumn ConnectByIsLeafColumn { get; } =
				new OracleColumn(true)
				{
					Name = ColumnNameConnectByIsLeaf,
					DataType = OracleDataType.NumberType
				};

		public OracleColumn ConnectByIsCycleColumn { get; } =
			new OracleColumn(true)
			{
				Name = ColumnNameConnectByIsCycle,
				DataType = OracleDataType.NumberType
			};

		public override string Name { get; } = String.Empty;

		public override ReferenceType Type { get; } = ReferenceType.HierarchicalClause;

		public bool HasNoCycleSupport { get; }

		public OracleHierarchicalClauseReference(bool hasNoCycleSupport)
		{
			HasNoCycleSupport = hasNoCycleSupport;
		}

		protected override IReadOnlyList<OracleColumn> BuildColumns()
		{
			return EmptyArray;
		}

		protected override IReadOnlyList<OracleColumn> BuildPseudocolumns()
		{
			var pseudoColumns = new List<OracleColumn>(2) { ConnectByIsLeafColumn };
			if (HasNoCycleSupport)
			{
				pseudoColumns.Add(ConnectByIsCycleColumn);
			}

			return pseudoColumns.AsReadOnly();
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
		internal static readonly string RowIdNormalizedName = TerminalValues.RowIdPseudocolumn.ToQuotedIdentifier();

		public static readonly OracleDataObjectReference[] EmptyArray = new OracleDataObjectReference[0];

		public bool IsOuterJoined { get; set; }

		public bool IsLateral { get; set; }

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

		protected override IReadOnlyList<OracleColumn> BuildPseudocolumns()
		{
			var pseudocolumns = new List<OracleColumn>();
			var table = SchemaObject.GetTargetSchemaObject() as OracleTable;
			if (Type != ReferenceType.SchemaObject || table == null)
			{
				return pseudocolumns.AsReadOnly();
			}

			if (table.Organization == OrganizationType.Heap || table.Organization == OrganizationType.Index)
			{
				var rowIdPseudocolumn =
					new OracleColumn(true)
					{
						Name = RowIdNormalizedName,
						DataType =
							new OracleDataType
							{
								FullyQualifiedName = OracleObjectIdentifier.Create(null, table.Organization == OrganizationType.Index ? TerminalValues.UniversalRowId : TerminalValues.RowIdDataType)
							}
					};

				pseudocolumns.Add(rowIdPseudocolumn);
			}

			if (FlashbackOption == FlashbackOption.None || FlashbackOption == FlashbackOption.AsOf)
			{
				var rowSystemChangeNumberPseudocolumn =
					new OracleColumn(true)
					{
						Name = "\"ORA_ROWSCN\"",
						DataType = OracleDataType.NumberType
					};

				pseudocolumns.Add(rowSystemChangeNumberPseudocolumn);
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

				pseudocolumns.AddRange(flashbackVersionColumns);
			}

			return pseudocolumns.AsReadOnly();
		}

		protected override IReadOnlyList<OracleColumn> BuildColumns()
		{
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

			return columns.AsReadOnly();
		}

		public StatementGrammarNode AliasNode { get; set; }

		public StatementGrammarNode FlashbackClauseNode { get; set; }

		public FlashbackOption FlashbackOption
		{
			get
			{
				var flashbackOption = FlashbackOption.None;
				var table = SchemaObject.GetTargetSchemaObject() as OracleTable;
				if (table == null || FlashbackClauseNode == null)
				{
					return flashbackOption;
				}

				if (FlashbackClauseNode[NonTerminals.FlashbackVersionsClause] != null)
				{
					flashbackOption |= FlashbackOption.Versions;
				}

				if (FlashbackClauseNode[NonTerminals.FlashbackAsOfClause] != null)
				{
					flashbackOption |= FlashbackOption.AsOf;
				}

				return flashbackOption;
			}
		}

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
