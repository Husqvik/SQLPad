using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle
{
	public abstract class OracleObject
	{
		public OracleObjectIdentifier FullyQualifiedName { get; set; }
	}

	public abstract class OracleSchemaObject : OracleObject, IDatabaseObject
	{
		public DateTime Created { get; set; }

		public DateTime LastDdl { get; set; }

		public bool IsValid { get; set; }

		public bool IsTemporary { get; set; }

		public abstract string Type { get; }

		public string Name { get { return FullyQualifiedName.NormalizedName; } }

		public string Owner { get { return FullyQualifiedName.NormalizedOwner; } }

		public OracleSynonym Synonym { get; set; }
	}

	public abstract class OracleDataObject : OracleSchemaObject
	{
		protected OracleDataObject()
		{
			Columns = new Dictionary<string, OracleColumn>();
			Constraints = new List<OracleConstraint>();
		}

		public IDictionary<string, OracleColumn> Columns { get; set; }

		public OrganizationType Organization { get; set; }

		public ICollection<OracleConstraint> Constraints { get; set; }
		
		public IEnumerable<OracleForeignKeyConstraint> ForeignKeys { get { return Constraints.OfType<OracleForeignKeyConstraint>(); } }
	}

	[DebuggerDisplay("OracleView (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleView : OracleDataObject
	{
		public string StatementText { get; set; }

		public override string Type { get { return "VIEW"; } }
	}

	[DebuggerDisplay("OracleSynonym (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleSynonym : OracleDataObject
	{
		public OracleSchemaObject SchemaObject { get; set; }

		public bool IsPublic
		{
			get { return FullyQualifiedName.NormalizedOwner == OracleDatabaseModelBase.SchemaPublic; }
		}

		public override string Type { get { return "SYNONYM"; } }
	}

	[DebuggerDisplay("OracleTable (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleTable : OracleDataObject
	{
		public OracleTable()
		{
		}

		public bool IsInternal { get; set; }

		public OracleColumn RowIdPseudoColumn
		{
			get
			{
				return Organization.In(OrganizationType.Heap, OrganizationType.Index)
					? new OracleColumn
					{
						Name = OracleColumn.RowId.ToQuotedIdentifier(),
						Type = Organization == OrganizationType.Index ? "UROWID" : OracleColumn.RowId
					}
					: null;
			}
		}

		public override string Type { get { return "TABLE"; } }
	}

	public class OracleSequence : OracleSchemaObject
	{
		public decimal CurrentValue { get; set; }

		public decimal Increment { get; set; }

		public decimal MinimumValue { get; set; }

		public decimal MaximumValue { get; set; }

		public decimal CacheSize { get; set; }

		public bool IsOrdered { get; set; }

		public bool CanCycle { get; set; }

		public override string Type { get { return "SEQUENCE"; } }
	}

	public class OraclePackage : OracleSchemaObject
	{
		public OraclePackage()
		{
			Functions = new HashSet<OracleFunctionMetadata>();
		}

		public ICollection<OracleFunctionMetadata> Functions { get; private set; } 

		public override string Type { get { return "PACKAGE"; } }
	}

	public class OracleFunction : OracleSchemaObject
	{
		public OracleFunctionMetadata Metadata { get; set; }

		public override string Type { get { return "FUNCTION"; } }
	}

	public enum OrganizationType
	{
		NotApplicable,
		Heap,
		Index,
		External
	}
}
