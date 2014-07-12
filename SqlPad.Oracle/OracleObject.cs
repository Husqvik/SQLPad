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

		public OrganizationType Organization { get; set; }

		public ICollection<OracleConstraint> Constraints { get; set; }

		public IDictionary<string, OracleColumn> Columns { get; set; }
		
		public IEnumerable<OracleForeignKeyConstraint> ForeignKeys { get { return Constraints.OfType<OracleForeignKeyConstraint>(); } }
	}

	[DebuggerDisplay("OracleView (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleView : OracleDataObject
	{
		public string StatementText { get; set; }

		public override string Type { get { return OracleSchemaObjectType.View; } }
	}

	[DebuggerDisplay("OracleSynonym (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleSynonym : OracleSchemaObject
	{
		public OracleSchemaObject SchemaObject { get; set; }

		public bool IsPublic
		{
			get { return FullyQualifiedName.NormalizedOwner == OracleDatabaseModelBase.SchemaPublic; }
		}

		public override string Type { get { return OracleSchemaObjectType.Synonym; } }
	}

	[DebuggerDisplay("OracleTable (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleTable : OracleDataObject
	{
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

		public override string Type { get { return OracleSchemaObjectType.Table; } }
	}

	[DebuggerDisplay("OracleSequence (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleSequence : OracleSchemaObject
	{
		public const string NormalizedColumnNameCurrentValue = "\"CURRVAL\"";
		public const string NormalizedColumnNameNextValue = "\"NEXTVAL\"";
		private readonly Dictionary<string, OracleColumn> _columns = new Dictionary<string, OracleColumn>();

		public OracleSequence()
		{
			var nextValueColumn =
				new OracleColumn
				{
					Name = NormalizedColumnNameNextValue,
					Type = "INTEGER",
					Scale = 0
				};

			_columns.Add(NormalizedColumnNameNextValue, nextValueColumn);
			_columns.Add(NormalizedColumnNameCurrentValue, nextValueColumn.Clone(NormalizedColumnNameCurrentValue));
		}

		public ICollection<OracleColumn> Columns { get { return _columns.Values; } }

		public decimal CurrentValue { get; set; }

		public decimal Increment { get; set; }

		public decimal MinimumValue { get; set; }

		public decimal MaximumValue { get; set; }

		public decimal CacheSize { get; set; }

		public bool IsOrdered { get; set; }

		public bool CanCycle { get; set; }

		public override string Type { get { return OracleSchemaObjectType.Sequence; } }
	}

	public interface IFunctionCollection
	{
		ICollection<OracleFunctionMetadata> Functions { get; }

		OracleObjectIdentifier FullyQualifiedName { get; }
	}

	[DebuggerDisplay("OraclePackage (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OraclePackage : OracleSchemaObject, IFunctionCollection
	{
		public OraclePackage()
		{
			Functions = new HashSet<OracleFunctionMetadata>();
		}

		public ICollection<OracleFunctionMetadata> Functions { get; private set; } 

		public override string Type { get { return OracleSchemaObjectType.Package; } }
	}

	[DebuggerDisplay("OracleFunction (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleFunction : OracleSchemaObject, IFunctionCollection
	{
		private readonly OracleFunctionMetadata[] _metadata = new OracleFunctionMetadata[1];

		public OracleFunctionMetadata Metadata
		{
			get { return _metadata[0]; }
			set { _metadata[0] = value; }
		}

		ICollection<OracleFunctionMetadata> IFunctionCollection.Functions { get { return _metadata; } }

		public override string Type { get { return OracleSchemaObjectType.Function; } }
	}

	public abstract class OracleTypeBase : OracleSchemaObject
	{
		public const string ObjectType = "OBJECT";
		public const string XmlType = "XMLTYPE";
		public const string CollectionType = "COLLECTION";
		public override string Type { get { return OracleSchemaObjectType.Type; } }

		public abstract string TypeCode { get; }
	}

	[DebuggerDisplay("OracleObjectType (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleObjectType : OracleTypeBase
	{
		public override string TypeCode { get { return ObjectType; } }
	}

	[DebuggerDisplay("OracleCollectionType (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleCollectionType : OracleTypeBase
	{
		public override string TypeCode { get { return CollectionType; } }
	}

	public enum OrganizationType
	{
		NotApplicable,
		Heap,
		Index,
		External
	}

	public static class OracleSchemaObjectType
	{
		public const string Table = "TABLE";
		public const string View = "VIEW";
		public const string Synonym = "SYNONYM";
		public const string Sequence = "SEQUENCE";
		public const string Function = "FUNCTION";
		public const string Package = "PACKAGE";
		public const string Type = "TYPE";
	}
}
