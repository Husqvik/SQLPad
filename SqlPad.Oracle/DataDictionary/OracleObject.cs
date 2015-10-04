using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DatabaseConnection;

namespace SqlPad.Oracle.DataDictionary
{
	public abstract class OracleObject
	{
		public OracleObjectIdentifier FullyQualifiedName { get; set; }
	}

	public abstract class OracleSchemaObject : OracleObject
	{
		private HashSet<OracleSynonym> _synonyms; 

		public DateTime Created { get; set; }

		public DateTime LastDdl { get; set; }

		public bool IsValid { get; set; }

		public bool IsTemporary { get; set; }

		public abstract string Type { get; }

		public string Name => FullyQualifiedName.NormalizedName;

	    public string Owner => FullyQualifiedName.NormalizedOwner;

	    public ICollection<OracleSynonym> Synonyms => _synonyms ?? (_synonyms = new HashSet<OracleSynonym>());
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

		public IEnumerable<OracleReferenceConstraint> ReferenceConstraints => Constraints.OfType<OracleReferenceConstraint>();
	}

	[DebuggerDisplay("OracleMaterializedView (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleMaterializedView : OracleTable
	{
		public string TableName { get; set; }

		public bool IsUpdatable { get; set; }
		
		public bool IsPrebuilt { get; set; }

		public string Query { get; set; }
		
		public string RefreshGroup { get; set; }

		public MaterializedViewRefreshMode RefreshMode { get; set; }

		// TODO: Make proper enum
		public string RefreshMethod { get; set; }

		public MaterializedViewRefreshType RefreshType { get; set; }
		
		public DateTime? StartWith { get; set; }

		public string Next { get; set; }
		
		public DateTime? LastRefresh { get; set; }

		public override string Type => OracleSchemaObjectType.MaterializedView;
	}

	public enum MaterializedViewRefreshMode
	{
		OnDemand,
		OnCommit
	}

	public enum MaterializedViewRefreshType
	{
		Fast,
		Complete,
		Force
	}

	[DebuggerDisplay("OracleView (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleView : OracleDataObject
	{
		public string StatementText { get; set; }

		public override string Type => OracleSchemaObjectType.View;
	}

	[DebuggerDisplay("OracleSynonym (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleSynonym : OracleSchemaObject
	{
		public OracleSchemaObject SchemaObject { get; set; }

		public bool IsPublic => String.Equals(FullyQualifiedName.NormalizedOwner, OracleDatabaseModelBase.SchemaPublic);

	    public override string Type => OracleSchemaObjectType.Synonym;
	}

	[DebuggerDisplay("OracleTable (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleTable : OracleDataObject
	{
		public OracleTable()
		{
			Partitions = new Dictionary<string, OraclePartition>();
			PartitionKeyColumns = new List<string>();
			SubPartitionKeyColumns = new List<string>();
		}

		public bool IsInternal { get; set; }

		public override string Type => OracleSchemaObjectType.Table;

	    public IDictionary<string, OraclePartition> Partitions { get; set; }

		public ICollection<string> PartitionKeyColumns { get; set; }

		public ICollection<string> SubPartitionKeyColumns { get; set; }
	}

	public abstract class OraclePartitionBase : OracleObject
	{
		public string Name { get; set; }

		public int Position { get; set; }
	}

	[DebuggerDisplay("OraclePartition (Name={Name}; Position={Position})")]
	public class OraclePartition : OraclePartitionBase
	{
		private Dictionary<string, OracleSubPartition> _subPartitions;

		public IDictionary<string, OracleSubPartition> SubPartitions => _subPartitions ?? (_subPartitions = new Dictionary<string, OracleSubPartition>());
	}

	[DebuggerDisplay("OracleSubPartition (Name={Name}; Position={Position})")]
	public class OracleSubPartition : OraclePartitionBase
	{
	}

	[DebuggerDisplay("OracleSequence (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleSequence : OracleSchemaObject
	{
		public const string NormalizedColumnNameCurrentValue = "\"CURRVAL\"";
		public const string NormalizedColumnNameNextValue = "\"NEXTVAL\"";

	    public OracleSequence()
		{
			var nextValueColumn =
				new OracleColumn
				{
					Name = NormalizedColumnNameNextValue,
					DataType =
						new OracleDataType
						{
							FullyQualifiedName = OracleObjectIdentifier.Create(null, "INTEGER"),
							Scale = 0
						}
				};

			var currentValueColumn =
				new OracleColumn
				{
					Name = NormalizedColumnNameCurrentValue,
					DataType =
						new OracleDataType
						{
							FullyQualifiedName = OracleObjectIdentifier.Create(null, "INTEGER"),
							Scale = 0
						}
				};

			Columns =
				new List<OracleColumn>
				{
					nextValueColumn,
					currentValueColumn
				}.AsReadOnly();
		}

		public IReadOnlyList<OracleColumn> Columns { get; }

	    public decimal CurrentValue { get; set; }

		public decimal Increment { get; set; }

		public decimal MinimumValue { get; set; }

		public decimal MaximumValue { get; set; }

		public decimal CacheSize { get; set; }

		public bool IsOrdered { get; set; }

		public bool CanCycle { get; set; }

		public override string Type => OracleSchemaObjectType.Sequence;
	}

	public interface IFunctionCollection
	{
		ICollection<OracleProgramMetadata> Functions { get; }

		OracleObjectIdentifier FullyQualifiedName { get; }
	}

	[DebuggerDisplay("OraclePackage (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OraclePackage : OracleSchemaObject, IFunctionCollection
	{
		private readonly List<OracleProgramMetadata> _functions = new List<OracleProgramMetadata>();

		public ICollection<OracleProgramMetadata> Functions => _functions;

	    public override string Type => OracleSchemaObjectType.Package;
	}

	[DebuggerDisplay("OracleFunction (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleFunction : OracleSchemaObject, IFunctionCollection
	{
		public OracleProgramMetadata Metadata { get; set; }

		ICollection<OracleProgramMetadata> IFunctionCollection.Functions => new [] { Metadata };

	    public override string Type => OracleSchemaObjectType.Function;
	}

	[DebuggerDisplay("OracleProcedure (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleProcedure : OracleSchemaObject
	{
		public OracleProgramMetadata Metadata { get; set; }

		public override string Type => OracleSchemaObjectType.Procedure;
	}

	public abstract class OracleTypeBase : OracleSchemaObject
	{
		public const string TypeCodeAnyData = "ANYDATA";
		public const string TypeCodeCollection = "COLLECTION";
		public const string TypeCodeObject = "OBJECT";
		public const string TypeCodeXml = "XMLTYPE";

		private OracleProgramMetadata _constructorMetadata;
		
		public override string Type => OracleSchemaObjectType.Type;

	    public abstract string TypeCode { get; }

		public OracleProgramMetadata GetConstructorMetadata()
		{
			return _constructorMetadata ?? (_constructorMetadata = BuildConstructorMetadata());
		}

		protected abstract OracleProgramMetadata BuildConstructorMetadata();
	}

	[DebuggerDisplay("OracleTypeObject (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleTypeObject : OracleTypeBase
	{
		private string _typeCode = TypeCodeObject;

		public OracleTypeObject()
		{
			Attributes = new List<OracleTypeAttribute>();
		}

		internal OracleTypeObject WithTypeCode(string typeCode)
		{
			_typeCode = typeCode;
			return this;
		}

		public override string TypeCode => _typeCode;

	    public IList<OracleTypeAttribute> Attributes { get; set; }

		protected override OracleProgramMetadata BuildConstructorMetadata()
		{
			var constructorMetadata = new OracleProgramMetadata(ProgramType.ObjectConstructor, OracleProgramIdentifier.CreateFromValues(FullyQualifiedName.Owner, null, FullyQualifiedName.Name), false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeParenthesis, false);
			var constructorParameters = Attributes.Select(
				(a, i) => new OracleProgramParameterMetadata(a.Name.ToSimpleIdentifier(), i + 1, i + 1, 0, ParameterDirection.Input, GetFunctionParameterTypeName(a.DataType), GetFunctionParameterCustomTypeIdentifier(a.DataType), false));

			var returnParameter = new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, TypeCodeObject, FullyQualifiedName, false);
			constructorMetadata.AddParameter(returnParameter);
			constructorMetadata.AddParameters(constructorParameters);
			return constructorMetadata;
		}

		private static OracleObjectIdentifier GetFunctionParameterCustomTypeIdentifier(OracleDataType dataType)
		{
			return dataType.IsPrimitive ? OracleObjectIdentifier.Empty : dataType.FullyQualifiedName;
		}

		private static string GetFunctionParameterTypeName(OracleDataType dataType)
		{
			return dataType.IsPrimitive ? dataType.FullyQualifiedName.NormalizedName : TypeCodeObject;
		}
	}

	[DebuggerDisplay("OracleTypeAttribute (Name={Name}; DataType={DataType}; IsInherited={IsInherited})")]
	public class OracleTypeAttribute
	{
		public string Name { get; set; }
		
		public OracleDataType DataType { get; set; }

		public bool IsInherited { get; set; }
	}

	[DebuggerDisplay("OracleTypeCollection (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleTypeCollection : OracleTypeBase, IFunctionCollection
	{
		public const string OracleCollectionTypeNestedTable = "TABLE";
		public const string OracleCollectionTypeVarryingArray = "VARRAY";

		public override string TypeCode => TypeCodeCollection;

	    public OracleCollectionType CollectionType { get; set; }

		public int? UpperBound { get; set; }

		protected override OracleProgramMetadata BuildConstructorMetadata()
		{
			var elementTypeLabel = ElementDataType.IsPrimitive
				? ElementDataType.FullyQualifiedName.Name.Trim('"')
				: ElementDataType.FullyQualifiedName.ToString();

			var returnParameterType = CollectionType == OracleCollectionType.Table ? OracleCollectionTypeNestedTable : OracleCollectionTypeVarryingArray;
			var constructorMetadata = new OracleProgramMetadata(ProgramType.CollectionConstructor, OracleProgramIdentifier.CreateFromValues(FullyQualifiedName.Owner, null, FullyQualifiedName.Name), false, false, false, false, false, false, 0, UpperBound ?? Int32.MaxValue, AuthId.CurrentUser, OracleProgramMetadata.DisplayTypeParenthesis, false);
			constructorMetadata.AddParameter(new OracleProgramParameterMetadata(null, 0, 0, 0, ParameterDirection.ReturnValue, returnParameterType, FullyQualifiedName, false));
			constructorMetadata.AddParameter(new OracleProgramParameterMetadata($"array of {elementTypeLabel}", 1, 1, 0, ParameterDirection.Input, String.Empty, OracleObjectIdentifier.Empty, true));
			constructorMetadata.Owner = this;
			
			return constructorMetadata;
		}

		public ICollection<OracleProgramMetadata> Functions => new [] { BuildConstructorMetadata() };

	    public OracleDataType ElementDataType { get; set; }
	}

	[DebuggerDisplay("OracleSchema (Name={Name}; Created={Created}; IsOracleMaintained={IsOracleMaintained}; IsCommon={IsCommon})")]
	public struct OracleSchema
	{
		public static readonly OracleSchema Public = new OracleSchema { Name = OracleDatabaseModelBase.SchemaPublic, IsOracleMaintained = true };

		public string Name { get; set; }

		public DateTime Created { get; set; }

		public bool IsOracleMaintained { get; set; }
		
		public bool IsCommon { get; set; }
	}

	public enum OracleCollectionType
	{
		Table,
		VarryingArray
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
		public const string MaterializedView = "MATERIALIZED VIEW";
		public const string Synonym = "SYNONYM";
		public const string Sequence = "SEQUENCE";
		public const string Function = "FUNCTION";
		public const string Package = "PACKAGE";
		public const string Procedure = "PROCEDURE";
		public const string Type = "TYPE";
	}
}
