using System;
using System.Collections.Generic;
using System.Diagnostics;

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
	}

	[DebuggerDisplay("OracleDataObject (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName}; Type={Type})")]
	public abstract class OracleDataObject : OracleSchemaObject
	{
		protected OracleDataObject()
		{
			Columns = new Dictionary<string, OracleColumn>();
			ForeignKeys = new List<OracleForeignKeyConstraint>();
		}

		public IDictionary<string, OracleColumn> Columns { get; set; }

		public OrganizationType Organization { get; set; }
		
		public ICollection<OracleForeignKeyConstraint> ForeignKeys { get; set; }
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
			get { return FullyQualifiedName.NormalizedOwner == OracleDatabaseModel.SchemaPublic; }
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

	public enum OrganizationType
	{
		NotApplicable,
		Heap,
		Index,
		External
	}
}
