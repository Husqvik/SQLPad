using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	public abstract class OracleObject: IDatabaseObject
	{
		public OracleObjectIdentifier FullyQualifiedName { get; set; }

		public string Type { get; set; }

		public string Name { get { return FullyQualifiedName.NormalizedName; } }

		public string Owner { get { return FullyQualifiedName.NormalizedOwner; } }

	}

	[DebuggerDisplay("OracleDataObject (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName}; Type={Type})")]
	public abstract class OracleDataObject : OracleObject
	{
	}

	public abstract class OracleRowSourceObject : OracleDataObject
	{
		protected OracleRowSourceObject()
		{
			Columns = new List<OracleColumn>();
			ForeignKeys = new List<OracleForeignKeyConstraint>();
		}

		public ICollection<OracleColumn> Columns { get; set; }

		public OrganizationType Organization { get; set; }

		public ICollection<OracleForeignKeyConstraint> ForeignKeys { get; set; }
	}

	[DebuggerDisplay("OracleView (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleView : OracleRowSourceObject
	{
		public string StatementText { get; set; }
	}

	[DebuggerDisplay("OracleSynonym (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleSynonym : OracleDataObject
	{
		public OracleObject SchemaObject { get; set; }

		public bool IsPublic
		{
			get { return FullyQualifiedName.NormalizedOwner == OracleDatabaseModel.SchemaPublic; }
		}
	}

	[DebuggerDisplay("OracleTable (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleTable : OracleRowSourceObject
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
	}
}
