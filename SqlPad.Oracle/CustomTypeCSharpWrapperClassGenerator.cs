using System.IO;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle
{
	public class CustomTypeCSharpWrapperClassGenerator
	{
		private static readonly string UsingClause =
$@"using {OracleToNetTypeMapper.OracleDataAccessAssemblyName}.Client;
using {OracleToNetTypeMapper.OracleDataAccessAssemblyName}.Types;
";

		private const string CustomTypeBase =
@"public abstract class CustomTypeBase<T> : IOracleCustomType, IOracleCustomTypeFactory, INullable where T : CustomTypeBase<T>, new()
{
	private bool _isNull;
	
	public IOracleCustomType CreateObject()
	{
		return new T();
	}

	public abstract void FromCustomObject(OracleConnection connection, IntPtr pointerUdt);

	public abstract void ToCustomObject(OracleConnection connection, IntPtr pointerUdt);

	public bool IsNull
	{
		get { return this._isNull; }
	}

	public static T Null
	{
		get { return new T { _isNull = true }; }
	}
}";

		private const string CustomCollectionTypeBase =
@"public abstract class CustomCollectionTypeBase<TType, TValue> : CustomTypeBase<TType>, IOracleArrayTypeFactory where TType : CustomTypeBase<TType>, new()
{
	[OracleArrayMapping()]
	public TValue[] Values;

	public override void FromCustomObject(OracleConnection connection, IntPtr pointerUdt)
	{
		OracleUdt.SetValue(connection, pointerUdt, 0, Values);
	}

	public override void ToCustomObject(OracleConnection connection, IntPtr pointerUdt)
	{
		Values = (TValue[])OracleUdt.GetValue(connection, pointerUdt, 0);
	}

	public Array CreateArray(int numElems)
	{
		return new TValue[numElems];
	}

	public Array CreateStatusArray(int numElems)
	{
		return null;
	}
}";

		public static void Generate(OracleTypeBase type, TextWriter writer)
		{
			writer.WriteLine(UsingClause);

			if (type is OracleTypeObject objectType)
			{
				Generate(objectType, writer);
			}
			else
			{
				Generate((OracleTypeCollection)type, writer);
			}

			writer.WriteLine(CustomTypeBase);
		}

		private static void Generate(OracleTypeObject type, TextWriter writer)
		{
			var owner = type.FullyQualifiedName.NormalizedOwner.Trim('"');
			var name = type.FullyQualifiedName.NormalizedName.Trim('"');
			writer.WriteLine($"[OracleCustomTypeMapping(\"{owner}.{name}\")]");
			writer.WriteLine($"public class {owner}_{name} : CustomTypeBase<{owner}_{name}>");
			writer.WriteLine("{");

			foreach (var attribute in type.Attributes)
			{
				var attributeName = attribute.Name.Trim('"');
				writer.WriteLine($"	[OracleObjectMapping(\"{attributeName}\")]");
				writer.WriteLine($"	public {OracleToNetTypeMapper.MapOracleTypeToNetType(attribute.DataType.FullyQualifiedName).Name} {attributeName};");
			}

			writer.WriteLine();
			writer.WriteLine("	public override void FromCustomObject(OracleConnection connection, IntPtr pointerUdt)");
			writer.WriteLine("	{");

			foreach (var attribute in type.Attributes)
			{
				var attributeName = attribute.Name.Trim('"');
				writer.WriteLine($"		OracleUdt.SetValue(connection, pointerUdt, \"{attributeName}\", {attributeName});");
			}

			writer.WriteLine("	}");
			writer.WriteLine();
			writer.WriteLine("	public override void ToCustomObject(OracleConnection connection, IntPtr pointerUdt)");
			writer.WriteLine("	{");

			foreach (var attribute in type.Attributes)
			{
				var attributeName = attribute.Name.Trim('"');
				writer.WriteLine($"		{attributeName} = ({OracleToNetTypeMapper.MapOracleTypeToNetType(attribute.DataType.FullyQualifiedName).Name})OracleUdt.GetValue(connection, pointerUdt, \"{attributeName}\");");
			}

			writer.WriteLine("	}");
			writer.WriteLine("}");
			writer.WriteLine();
		}

		private static void Generate(OracleTypeCollection collectionType, TextWriter writer)
		{
			var owner = collectionType.FullyQualifiedName.NormalizedOwner.Trim('"');
			var name = collectionType.FullyQualifiedName.NormalizedName.Trim('"');

			var elementFullyQualifiedName = collectionType.ElementDataType.FullyQualifiedName;
			var elementClassName = elementFullyQualifiedName.HasOwner ? $"{elementFullyQualifiedName.NormalizedOwner.Trim('"')}_" : null;
			elementClassName = $"{elementClassName}{elementFullyQualifiedName.NormalizedName.Trim('"')}";

			writer.WriteLine($"[OracleCustomTypeMapping(\"{owner}.{name}\")]");
			writer.WriteLine($"public class {owner}_{name} : CustomCollectionTypeBase<{owner}_{name}, {elementClassName}>");
			writer.WriteLine("{");
			writer.WriteLine("}");
			writer.WriteLine();

			writer.WriteLine(CustomCollectionTypeBase);
			writer.WriteLine();
		}
	}
}
