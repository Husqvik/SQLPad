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

	public abstract class OracleSchemaObject : OracleObject
	{
		private HashSet<OracleSynonym> _synonyms; 

		public DateTime Created { get; set; }

		public DateTime LastDdl { get; set; }

		public bool IsValid { get; set; }

		public bool IsTemporary { get; set; }

		public abstract string Type { get; }

		public string Name { get { return FullyQualifiedName.NormalizedName; } }

		public string Owner { get { return FullyQualifiedName.NormalizedOwner; } }

		public ICollection<OracleSynonym> Synonyms { get { return _synonyms ?? (_synonyms = new HashSet<OracleSynonym>()); } }
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
						  DataType =
							new OracleDataType
							{
								FullyQualifiedName = OracleObjectIdentifier.Create(null, Organization == OrganizationType.Index ? "UROWID" : OracleColumn.RowId)
							}
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
					DataType =
						new OracleDataType
						{
							FullyQualifiedName = OracleObjectIdentifier.Create(null, "INTEGER"),
							Scale = 0
						}
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
		private readonly List<OracleFunctionMetadata> _functions = new List<OracleFunctionMetadata>();

		public ICollection<OracleFunctionMetadata> Functions { get { return _functions; } } 

		public override string Type { get { return OracleSchemaObjectType.Package; } }
	}

	[DebuggerDisplay("OracleFunction (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleFunction : OracleSchemaObject, IFunctionCollection
	{
		private OracleFunctionMetadata _metadata;

		public OracleFunctionMetadata Metadata
		{
			get { return _metadata; }
			set { _metadata = value; }
		}

		ICollection<OracleFunctionMetadata> IFunctionCollection.Functions { get { return new [] { _metadata }; } }

		public override string Type { get { return OracleSchemaObjectType.Function; } }
	}

	public abstract class OracleTypeBase : OracleSchemaObject
	{
		public const string TypeCodeObject = "OBJECT";
		public const string TypeCodeXml = "XMLTYPE";
		public const string TypeCodeCollection = "COLLECTION";

		private OracleFunctionMetadata _constructorMetadata;
		
		public override string Type { get { return OracleSchemaObjectType.Type; } }

		public abstract string TypeCode { get; }

		public OracleFunctionMetadata GetConstructorMetadata()
		{
			return _constructorMetadata ?? (_constructorMetadata = BuildConstructorMetadata());
		}

		protected abstract OracleFunctionMetadata BuildConstructorMetadata();
	}

	[DebuggerDisplay("OracleTypeObject (Owner={FullyQualifiedName.NormalizedOwner}; Name={FullyQualifiedName.NormalizedName})")]
	public class OracleTypeObject : OracleTypeBase
	{
		public OracleTypeObject()
		{
			Attributes = new List<OracleTypeAttribute>();
		}

		public override string TypeCode { get { return TypeCodeObject; } }

		public IList<OracleTypeAttribute> Attributes { get; set; }

		protected override OracleFunctionMetadata BuildConstructorMetadata()
		{
			var constructorMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(FullyQualifiedName.Owner, null, FullyQualifiedName.Name), false, false, false, false, false, false, null, null, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeParenthesis, false);
			var constructorParameters = Attributes.Select(
				(a, i) => new OracleFunctionParameterMetadata(a.Name.ToSimpleIdentifier(), i + 1, ParameterDirection.Input, OracleDataType.ResolveFullTypeName(a.DataType), false));

			constructorParameters = Enumerable.Repeat(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, FullyQualifiedName.ToString(), false), 1)
				.Concat(constructorParameters);
			
			constructorMetadata.Parameters.AddRange(constructorParameters);
			return constructorMetadata;
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
	public class OracleTypeCollection : OracleTypeBase
	{
		public override string TypeCode { get { return TypeCodeCollection; } }

		public OracleObjectIdentifier ElementTypeIdentifier { get; set; }

		public OracleCollectionType CollectionType { get; set; }

		public int? UpperBound { get; set; }

		protected override OracleFunctionMetadata BuildConstructorMetadata()
		{
			var elementTypeLabel = String.IsNullOrEmpty(ElementTypeIdentifier.Owner) ? ElementTypeIdentifier.Name.Trim('"') : ElementTypeIdentifier.ToString();
			var constructorMetadata = new OracleFunctionMetadata(OracleFunctionIdentifier.CreateFromValues(FullyQualifiedName.Owner, null, FullyQualifiedName.Name), false, false, false, false, false, false, 0, 0, AuthId.CurrentUser, OracleFunctionMetadata.DisplayTypeParenthesis, false);
			constructorMetadata.Parameters.Add(new OracleFunctionParameterMetadata(null, 0, ParameterDirection.ReturnValue, FullyQualifiedName.ToString(), false));
			constructorMetadata.Parameters.Add(new OracleFunctionParameterMetadata(String.Format("array of {0}", elementTypeLabel), 1, ParameterDirection.Input, null, true));
			
			return constructorMetadata;
		}
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
		public const string Synonym = "SYNONYM";
		public const string Sequence = "SEQUENCE";
		public const string Function = "FUNCTION";
		public const string Package = "PACKAGE";
		public const string Type = "TYPE";
	}
}
