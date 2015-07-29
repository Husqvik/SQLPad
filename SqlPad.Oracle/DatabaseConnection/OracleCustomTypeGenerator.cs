#if !ORACLE_MANAGED_DATA_ACCESS_CLIENT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Resources;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.DatabaseConnection
{
	internal class OracleCustomTypeGenerator
	{
		private const string DynamicAssemblyNameBase = "SqlPad.Oracle.CustomTypes";
		private const MethodAttributes InterfaceMethodAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
		private static readonly Assembly CurrentAssembly = typeof(OracleCustomTypeGenerator).Assembly;
		private static readonly Dictionary<string, OracleCustomTypeGenerator> Generators = new Dictionary<string, OracleCustomTypeGenerator>();

		private readonly string _customTypeAssemblyName;	
		private readonly FileInfo _customTypeAssemblyFile;	
		private readonly Assembly _customTypeAssembly;
		//private readonly AppDomain _customTypeHostDomain;

		private OracleCustomTypeGenerator(string connectionStringName)
		{
			_customTypeAssemblyName = $"{DynamicAssemblyNameBase}.{connectionStringName}";
			var customTypeAssemblyFileName = $"{_customTypeAssemblyName}.dll";
			var directoryName = Path.GetDirectoryName(CurrentAssembly.Location);
			var customTypeAssemblyFullFileName = Path.Combine(directoryName, customTypeAssemblyFileName);

			if (File.Exists(customTypeAssemblyFullFileName))
			{
				_customTypeAssembly = Assembly.Load(File.ReadAllBytes(customTypeAssemblyFullFileName));
				//_customTypeHostDomain = AppDomain.CreateDomain(String.Format("{0}.{1}", "CustomTypeHostDomain", connectionStringName));
				//_customTypeHostDomain.Load(DynamicAssemblyNameBase);

				Trace.WriteLine($"{DateTime.Now} - Custom object and collection types assembly '{customTypeAssemblyFileName}' has been found and loaded. ");
			}

			_customTypeAssemblyFile = new FileInfo(customTypeAssemblyFullFileName);
		}

		public static OracleCustomTypeGenerator GetCustomTypeGenerator(string connectionStringName)
		{
			lock (Generators)
			{
				OracleCustomTypeGenerator generator;
				if (!Generators.TryGetValue(connectionStringName, out generator))
				{
					generator = new OracleCustomTypeGenerator(connectionStringName);
					Generators.Add(connectionStringName, generator);
				}

				return generator;
			}
		}

		public void GenerateCustomTypeAssembly(OracleDataDictionary dataDictionary)
		{
			if (_customTypeAssembly != null)
			{
				return;
			}

			Trace.WriteLine($"{DateTime.Now} - Custom object and collection types generation started. ");

			var stopwatch = Stopwatch.StartNew();

			var fileVersionAttribute = CurrentAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
			var targetFrameworkAttribute = CurrentAssembly.GetCustomAttribute<TargetFrameworkAttribute>();
			var companyAttribute = CurrentAssembly.GetCustomAttribute<AssemblyCompanyAttribute>();
			var productAttribute = CurrentAssembly.GetCustomAttribute<AssemblyProductAttribute>();

			var assemblyVersion = CurrentAssembly.GetName().Version;
			var constructorStringParameters = new[] { typeof(string) };

			var customAttributeBuilders = new[]
			{
				new CustomAttributeBuilder(typeof (AssemblyTitleAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(DynamicAssemblyNameBase)),
				new CustomAttributeBuilder(typeof (NeutralResourcesLanguageAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(String.Empty)),
				new CustomAttributeBuilder(typeof (GuidAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(Guid.NewGuid().ToString())),
				new CustomAttributeBuilder(typeof (AssemblyCompanyAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(companyAttribute.Company)),
				new CustomAttributeBuilder(typeof (AssemblyProductAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(productAttribute.Product)),
				new CustomAttributeBuilder(typeof (AssemblyDescriptionAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray("This assembly contains dynamically generated Oracle custom data types. ")),
				new CustomAttributeBuilder(typeof (AssemblyCopyrightAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray("Copyright © " + DateTime.UtcNow.Year)),
				new CustomAttributeBuilder(typeof (AssemblyFileVersionAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(fileVersionAttribute.Version)),
				new CustomAttributeBuilder(typeof (AssemblyInformationalVersionAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(fileVersionAttribute.Version)), // Adds Product Version
				new CustomAttributeBuilder(typeof (ComVisibleAttribute).GetConstructor(new[] {typeof (bool)}), GetParameterAsObjectArray(false)),
				new CustomAttributeBuilder(typeof (AssemblyConfigurationAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(String.Empty)),
				new CustomAttributeBuilder(typeof (AssemblyTrademarkAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(String.Empty)),
				new CustomAttributeBuilder(typeof (TargetFrameworkAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(targetFrameworkAttribute.FrameworkName))
			};

			var assemblyName = new AssemblyName(_customTypeAssemblyName) { Version = assemblyVersion };
			var customTypeAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, _customTypeAssemblyFile.DirectoryName, true, customAttributeBuilders);

			customTypeAssemblyBuilder.DefineVersionInfoResource(); // Makes attributes readable by unmanaged environment like Windows Explorer.
			var customTypeModuleBuilder = customTypeAssemblyBuilder.DefineDynamicModule(_customTypeAssemblyName, _customTypeAssemblyFile.Name, true);

			var customTypes = new Dictionary<string, Type>();
			foreach (var objectType in dataDictionary.AllObjects.Values.OfType<OracleTypeObject>().Where(t => t.TypeCode != OracleTypeBase.TypeCodeXml))
			{
				CreateOracleObjectType(customTypeModuleBuilder, objectType, customTypes);
			}
			
			var objectTypeCount = customTypes.Count;
			foreach (var collectionType in dataDictionary.AllObjects.Values.OfType<OracleTypeCollection>())
			{
				CreateOracleCollectionType(customTypeModuleBuilder, collectionType, customTypes);
			}

			customTypeAssemblyBuilder.Save(_customTypeAssemblyFile.Name);

			//var mappingTableField = typeof(OracleUdt).GetField("s_mapUdtNameToMappingObj", BindingFlags.Static | BindingFlags.NonPublic);
			//mappingTableField.SetValue(null, mappingTable);

			stopwatch.Stop();

			var collectionTypeCount = customTypes.Count - objectTypeCount;
			Trace.WriteLine($"{DateTime.Now} - {objectTypeCount} Custom object types and {collectionTypeCount} collection types generated into {_customTypeAssemblyFile.Name} in {stopwatch.Elapsed}. ");
		}

		private static void CreateOracleObjectType(ModuleBuilder customTypeModuleBuilder, OracleTypeObject objectType, IDictionary<string, Type> customTypes)
		{
			var fullyQualifiedObjectTypeName = objectType.FullyQualifiedName.ToString().Replace("\"", null);
			var customTypeClassName = $"{DynamicAssemblyNameBase}.ObjectTypes.{MakeValidMemberName(fullyQualifiedObjectTypeName)}";
			var customTypeBuilder = customTypeModuleBuilder.DefineType(customTypeClassName, TypeAttributes.Public | TypeAttributes.Class, typeof(OracleCustomTypeBase), new[] { typeof(IOracleCustomType), typeof(IOracleCustomTypeFactory), typeof(IValue) });
			AddOracleCustomTypeMappingAttribute(customTypeBuilder, fullyQualifiedObjectTypeName);

			var constructorBuilder = AddConstructor(customTypeBuilder, typeof(object).GetConstructor(Type.EmptyTypes));

			ImplementAbstractStringValueProperty(customTypeBuilder, "DataTypeName", fullyQualifiedObjectTypeName, MethodAttributes.Public);

			var createObjectMethodBuilder = customTypeBuilder.DefineMethod("CreateObject", InterfaceMethodAttributes, typeof(IOracleCustomType), null);

			var ilGenerator = createObjectMethodBuilder.GetILGenerator();
			ilGenerator.Emit(OpCodes.Newobj, constructorBuilder);
			ilGenerator.Emit(OpCodes.Ret);

			var fields = new Dictionary<string, FieldInfo>();
			foreach (var objectAttribute in objectType.Attributes)
			{
				var targetType = MapOracleTypeToNetType(objectAttribute.DataType.FullyQualifiedName);

				var fieldName = MakeValidMemberName(objectAttribute.Name);
				var fieldBuilder = customTypeBuilder.DefineField(fieldName, targetType, FieldAttributes.Public);

				var attributeType = typeof (OracleObjectMappingAttribute);
				var attribute = new CustomAttributeBuilder(attributeType.GetConstructor(new[] { typeof(string) }), new[] { objectAttribute.Name.Replace("\"", null) });
				fieldBuilder.SetCustomAttribute(attribute);

				fields.Add(fieldName, fieldBuilder);
			}

			ImplementNullableInterface(customTypeBuilder, constructorBuilder);

			ilGenerator = BuildOracleCustomTypeInterfaceMethod(customTypeBuilder, "FromCustomObject");

			foreach (var field in fields)
			{
				ilGenerator.Emit(OpCodes.Ldarg_1);
				ilGenerator.Emit(OpCodes.Ldarg_2);
				ilGenerator.Emit(OpCodes.Ldstr, field.Key);
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Ldfld, field.Value);

				if (field.Value.FieldType.IsValueType)
				{
					ilGenerator.Emit(OpCodes.Box, field.Value.FieldType);
				}

				ilGenerator.Emit(OpCodes.Call, typeof(OracleUdt).GetMethod("SetValue", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(OracleConnection), typeof(IntPtr), typeof(string), typeof(object) }, null));
			}

			ilGenerator.Emit(OpCodes.Ret);

			ilGenerator = BuildOracleCustomTypeInterfaceMethod(customTypeBuilder, "ToCustomObject"); ;

			foreach (var field in fields)
			{
				ilGenerator.Emit(OpCodes.Ldarg_0);
				ilGenerator.Emit(OpCodes.Ldarg_1);
				ilGenerator.Emit(OpCodes.Ldarg_2);
				ilGenerator.Emit(OpCodes.Ldstr, field.Key);

				ilGenerator.Emit(OpCodes.Call, typeof(OracleUdt).GetMethod("GetValue", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(OracleConnection), typeof(IntPtr), typeof(string) }, null));

				if (field.Value.FieldType.IsValueType)
				{
					ilGenerator.Emit(OpCodes.Unbox_Any, field.Value.FieldType);
				}

				ilGenerator.Emit(OpCodes.Stfld, field.Value);
			}

			ilGenerator.Emit(OpCodes.Ret);

			var customType = customTypeBuilder.CreateType();
			customTypes.Add(fullyQualifiedObjectTypeName, customType);

			/*var schemaName = objectType.FullyQualifiedName.Owner.Trim('"');
			var typeName = objectType.FullyQualifiedName.Name.Trim('"');
			var mappingTableKey = String.Format("schemaName='{0}' typeName='{1}'", schemaName, typeName);
			var mappingTableValue =
				new NameValueCollection
				{
					{ "schemaName", schemaName },
					{ "typeName", typeName },
					{ "factoryName", customType.FullName }
				};

			mappingTable.Add(mappingTableKey, mappingTableValue);*/
		}

		private static void ImplementNullableInterface(TypeBuilder customTypeBuilder, ConstructorInfo constructorInfo)
		{
			var isNullValueFieldBuilder = customTypeBuilder.DefineField("_isNull", typeof (bool), FieldAttributes.Private);
			var isNullPropertyBuilder = customTypeBuilder.DefineProperty("IsNull", PropertyAttributes.None, CallingConventions.HasThis, typeof (bool), null);

			const MethodAttributes isNullAttributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName;
			var isNullPropertyGetterBuilder = customTypeBuilder.DefineMethod("get_IsNull", isNullAttributes, typeof (bool), null);

			var ilGenerator = isNullPropertyGetterBuilder.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Ldfld, isNullValueFieldBuilder);
			ilGenerator.Emit(OpCodes.Ret);

			isNullPropertyBuilder.SetGetMethod(isNullPropertyGetterBuilder);

			var nullPropertyBuilder = customTypeBuilder.DefineProperty("Null", PropertyAttributes.None, customTypeBuilder, null);

			var nullPropertyGetterBuilder = customTypeBuilder.DefineMethod("get_Null", MethodAttributes.Public | MethodAttributes.Static, customTypeBuilder, null);
			ilGenerator = nullPropertyGetterBuilder.GetILGenerator();
			ilGenerator.DeclareLocal(customTypeBuilder);
			ilGenerator.Emit(OpCodes.Newobj, constructorInfo);
			ilGenerator.Emit(OpCodes.Stloc_0);
			ilGenerator.Emit(OpCodes.Ldloc_0);
			ilGenerator.Emit(OpCodes.Ldc_I4_1);
			ilGenerator.Emit(OpCodes.Stfld, isNullValueFieldBuilder);
			ilGenerator.Emit(OpCodes.Ldloc_0);
			ilGenerator.Emit(OpCodes.Ret);

			nullPropertyBuilder.SetGetMethod(nullPropertyGetterBuilder);
		}

		private static Type MapOracleTypeToNetType(OracleObjectIdentifier typeIdentifier)
		{
			var targetType = typeof(string);
			switch (typeIdentifier.ToString().Trim('"'))
			{
				case "NUMBER":
				case "INTEGER":
					targetType = typeof(OracleDecimal);
					break;
				case "DATE":
					targetType = typeof(DateTime?);
					break;
				case "CLOB":
				case "NCLOB":
					targetType = typeof(OracleClob);
					break;
				case "BLOB":
					targetType = typeof(OracleBlob);
					break;
				case "RAW":
				case "LONG RAW":
					targetType = typeof(OracleBinary);
					break;
				case "BFILE":
					targetType = typeof(OracleBFile);
					break;
				case "BINARY_DOUBLE":
				case "DOUBLE PRECISION":
				case "BINARY_FLOAT":
					targetType = typeof(decimal?);
					break;
				case "TIMESTAMP":
					targetType = typeof(OracleTimeStamp);
					break;
				case "TIMESTAMP WITH TZ":
					targetType = typeof(OracleTimeStampTZ);
					break;
			}

			return targetType;
		}

		private static ILGenerator BuildOracleCustomTypeInterfaceMethod(TypeBuilder typeBuilder, string methodName)
		{
			var methodBuilder = typeBuilder.DefineMethod(methodName, InterfaceMethodAttributes, null, new[] { typeof(OracleConnection), typeof(IntPtr) });
			methodBuilder.DefineParameter(1, ParameterAttributes.None, "connection");
			methodBuilder.DefineParameter(2, ParameterAttributes.None, "pointerUdt");
			return methodBuilder.GetILGenerator();
		}

		private static void CreateOracleCollectionType(ModuleBuilder customTypeModuleBuilder, OracleTypeCollection collectionType, IDictionary<string, Type> customTypes)
		{
			var targetType = typeof(object);
			var enclosingCharacter = String.Empty;
			if (collectionType.ElementDataType != null)
			{
				if (collectionType.ElementDataType.FullyQualifiedName.HasOwner)
				{
					Type targetCustomType;
					if (customTypes.TryGetValue(collectionType.ElementDataType.FullyQualifiedName.ToString().Replace("\"", null), out targetCustomType))
					{
						targetType = targetCustomType;
					}
				}
				else
				{
					targetType = MapOracleTypeToNetType(collectionType.ElementDataType.FullyQualifiedName);

					if (targetType == typeof(string))
					{
						enclosingCharacter = "'";
					}
				}
			}

			var baseFactoryType = typeof(OracleTableValueFactoryBase<>);
			baseFactoryType = baseFactoryType.MakeGenericType(targetType);
			var fullyQualifiedCollectionTypeName = collectionType.FullyQualifiedName.ToString().Replace("\"", null); ;
			var customTypeClassName = $"{DynamicAssemblyNameBase}.CollectionTypes.{MakeValidMemberName(fullyQualifiedCollectionTypeName)}";

			var customTypeBuilder = customTypeModuleBuilder.DefineType(customTypeClassName, TypeAttributes.Public | TypeAttributes.Class, baseFactoryType);
			AddOracleCustomTypeMappingAttribute(customTypeBuilder, fullyQualifiedCollectionTypeName);

			var baseConstructor = baseFactoryType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
			AddConstructor(customTypeBuilder, baseConstructor);

			ImplementAbstractStringValueProperty(customTypeBuilder, "FullyQualifiedName", fullyQualifiedCollectionTypeName);
			ImplementAbstractStringValueProperty(customTypeBuilder, "EnclosingCharacter", enclosingCharacter);
			ImplementAbstractStringValueProperty(customTypeBuilder, "ElementTypeName", collectionType.ElementDataType.FullyQualifiedName.ToString());

			customTypes.Add(fullyQualifiedCollectionTypeName, customTypeBuilder.CreateType());
		}

		private static void AddOracleCustomTypeMappingAttribute(TypeBuilder customTypeBuilder, string value)
		{
			var attributeType = typeof(OracleCustomTypeMappingAttribute);
			var attribute = new CustomAttributeBuilder(attributeType.GetConstructor(new[] { typeof(string) }), new[] { value.Replace("\"", null) });
			customTypeBuilder.SetCustomAttribute(attribute);
		}

		private static ConstructorBuilder AddConstructor(TypeBuilder customTypeBuilder, ConstructorInfo baseConstructor)
		{
			var constructor = customTypeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis, null);
			var ilGenerator = constructor.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Call, baseConstructor);
			ilGenerator.Emit(OpCodes.Ret);

			return constructor;
		}

		private static string MakeValidMemberName(string typeName)
		{
			return new String(typeName.Replace('.', '_').Where(c => c == '_' || Char.IsLetterOrDigit(c)).ToArray());
		}

		private static void ImplementAbstractStringValueProperty(TypeBuilder typeBuilder, string propertyName, string value, MethodAttributes methodAttributes = MethodAttributes.Family)
		{
			var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.None, CallingConventions.HasThis, typeof(string), null);

			var attributes = methodAttributes | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName;
			var propertyGetterBuilder = typeBuilder.DefineMethod("get_" + propertyName, attributes, typeof(string), null);

			var ilGenerator = propertyGetterBuilder.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldstr, value);
			ilGenerator.Emit(OpCodes.Ret);

			propertyBuilder.SetGetMethod(propertyGetterBuilder);
		}

		private static object[] GetParameterAsObjectArray(params object[] parameters)
		{
			return parameters;
		}
	}

	public class OracleValueArray<T> : IOracleCustomType, ICollectionValue
	{
		private const int PreviewMaxItemCount = 10;
		private const int LargeValuePreviewLength = 16;
		
		private static readonly Type ArrayItemType = typeof (T);

		private readonly string _enclosingCharacter;
		private ColumnHeader _columnHeader;

		[OracleArrayMapping] public T[] Array;

		public OracleValueArray(string dataTypeName, string elementTypeName, string enclosingCharacter)
		{
			DataTypeName = dataTypeName;
			ElementTypeName = elementTypeName;
			_enclosingCharacter = enclosingCharacter;
		}

		public bool IsNull => Array == null;

		public object RawValue
		{
			get { throw new NotImplementedException(); }
		}

		public string ToSqlLiteral()
		{
			throw new NotImplementedException();
		}

		public string ToXml()
		{
			throw new NotImplementedException();
		}

		public string ToJson()
		{
			throw new NotImplementedException();
		}

		public void FromCustomObject(OracleConnection connection, IntPtr pointerUdt)
		{
			OracleUdt.SetValue(connection, pointerUdt, 0, Array);
		}

		public void ToCustomObject(OracleConnection connection, IntPtr pointerUdt)
		{
			Array = (T[])OracleUdt.GetValue(connection, pointerUdt, 0);
		}

		public string DataTypeName { get; private set; }

		public string ElementTypeName { get; private set; }
		
		public bool IsEditable => false;

	    public long Length => Array?.Length ?? 0;

	    public void Prefetch() { }

		public IList Records
		{
			get { return Array.Select(i => CustomTypeValueConverter.ConvertItem(i, OracleLargeTextValue.DefaultPreviewLength)).ToArray(); }
		}

		public ColumnHeader ColumnHeader => _columnHeader ?? BuildColumnHeader();

	    private ColumnHeader BuildColumnHeader()
		{
			_columnHeader =
				new ColumnHeader
				{
					DataType = ArrayItemType,
					DatabaseDataType = CustomTypeValueConverter.ConvertDatabaseTypeNameToReaderTypeName(ElementTypeName),
					Name = "COLUMN_VALUE",
				};

			return _columnHeader;
		}

		public override string ToString()
		{
			return BuildPreview();
		}

		private string BuildPreview()
		{
			if (IsNull)
			{
				return String.Empty;
			}

			var items = Array.Take(PreviewMaxItemCount).Select(i => ToPrintable(CustomTypeValueConverter.ConvertItem(i, LargeValuePreviewLength)));

			if (!String.IsNullOrEmpty(_enclosingCharacter))
			{
				items = items.Select(i => String.Format("{0}{1}{0}", _enclosingCharacter, i));
			}

			return $"{DataTypeName}({String.Join(", ", items)}{(Array.Length > PreviewMaxItemCount ? $", {OracleLargeTextValue.Ellipsis} ({Array.Length} items)" : String.Empty)})";
		}

		private static object ToPrintable(object value)
		{
			var oracleRaw = value as OracleRawValue;
			var oracleLongRaw = value as OracleLongRawValue;
			if (oracleRaw == null && oracleLongRaw == null)
			{
				return value;
			}

			var largeBinaryValue = (ILargeBinaryValue)oracleRaw ?? oracleLongRaw;

			var hexValue = largeBinaryValue.IsNull
				? String.Empty
				: largeBinaryValue.Value.ToHexString();
			
			return hexValue.Length > LargeValuePreviewLength
				? $"{hexValue.Substring(0, LargeValuePreviewLength)}{OracleLargeTextValue.Ellipsis}"
			    : hexValue;
		}
	}

	public abstract class OracleCustomTypeBase : IComplexType
	{
		private IReadOnlyList<CustomTypeAttributeValue> _attributes;
		private string _preview;

		public abstract string DataTypeName { get; }

		public abstract bool IsNull { get; }

		public object RawValue
		{
			get { throw new NotImplementedException(); }
		}

		public string ToSqlLiteral()
		{
			throw new NotImplementedException();
		}

		public string ToXml()
		{
			throw new NotImplementedException();
		}

		public string ToJson()
		{
			throw new NotImplementedException();
		}

		public bool IsEditable => false;

	    public long Length { get { throw new NotSupportedException(); } }
		
		public void Prefetch() { }

		public IReadOnlyList<CustomTypeAttributeValue> Attributes => _attributes ?? BuildAttributeCollection();

	    public override string ToString()
		{
			return _preview ?? BuildPreview();
		}

		private string BuildPreview()
		{
			var attributeValues = BuildAttributeCollection();
			var attributeValuesPreview = String.Join(", ", attributeValues.Select(FormatAttributeLabel));
			return _preview = $"{DataTypeName}({attributeValuesPreview})";
		}

		private string FormatAttributeLabel(CustomTypeAttributeValue attribute)
		{
			var stringValue = Convert.ToString(attribute.Value);
			return $"{attribute.ColumnHeader.Name}={(String.IsNullOrEmpty(stringValue) ? "NULL" : stringValue)}";
		}

		private IReadOnlyList<CustomTypeAttributeValue> BuildAttributeCollection()
		{
			var attributeSource = GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
				.Select(f =>
					new
					{
						Field = f,
						Attribute = f.GetCustomAttribute<OracleObjectMappingAttribute>()
					})
				.Where(fa => fa.Attribute != null)
				.Select((fa, i) =>
					new CustomTypeAttributeValue
					{
						ColumnHeader =
							new ColumnHeader
							{
								ColumnIndex = i,
								Name = fa.Attribute.AttributeName
							},
						Value = CustomTypeValueConverter.ConvertItem(fa.Field.GetValue(this), OracleLargeTextValue.DefaultPreviewLength)
					})
				.ToList()
				.AsReadOnly();

			return _attributes = attributeSource;
		}
	}

	internal static class CustomTypeValueConverter
	{
		public static string ConvertDatabaseTypeNameToReaderTypeName(string databaseTypeName)
		{
			switch (databaseTypeName)
			{
				case "\"RAW\"":
					return "Raw";
				default:
					return databaseTypeName;
			}
		}

		public static object ConvertItem(object value, int largeValuePreviewLength)
		{
			var oracleBlob = value as OracleBlob;
			if (oracleBlob != null)
			{
				return new OracleBlobValue(oracleBlob);
			}
			
			var oracleClob = value as OracleClob;
			if (oracleClob != null)
			{
				var typeName = oracleClob.IsNClob ? "NCLOB" : "CLOB";
				return new OracleClobValue(typeName, oracleClob, largeValuePreviewLength);
			}

			var oracleBinary = value as OracleBinary?;
			if (oracleBinary != null)
			{
				return new OracleRawValue(oracleBinary.Value);
			}

			var oracleTimestamp = value as OracleTimeStamp?;
			if (oracleTimestamp != null)
			{
				return new OracleTimestamp(oracleTimestamp.Value);
			}
			
			var oracleTimestampWithTimeZone = value as OracleTimeStampTZ?;
			if (oracleTimestampWithTimeZone != null)
			{
				return new OracleTimestampWithTimeZone(oracleTimestampWithTimeZone.Value);
			}

			var oracleTimestampWithLocalTimeZone = value as OracleTimeStampLTZ?;
			if (oracleTimestampWithLocalTimeZone != null)
			{
				return new OracleTimestampWithLocalTimeZone(oracleTimestampWithLocalTimeZone.Value);
			}

			var oracleDecimal = value as OracleDecimal?;
			if (oracleDecimal != null)
			{
				return new OracleNumber(oracleDecimal.Value);
			}

			var oracleXmlType = value as OracleXmlType;
			if (oracleXmlType != null)
			{
				return new OracleXmlValue((OracleXmlType)value, largeValuePreviewLength);
			}

			return value;
		}
	}

	public abstract class OracleTableValueFactoryBase<T> : IOracleCustomTypeFactory, IOracleArrayTypeFactory
	{
		protected abstract string FullyQualifiedName { get; }

		protected abstract string ElementTypeName { get; }
		
		protected abstract string EnclosingCharacter { get; }

		public IOracleCustomType CreateObject()
		{
			return new OracleValueArray<T>(FullyQualifiedName, ElementTypeName, EnclosingCharacter);
		}

		public Array CreateArray(int elementCount)
		{
			return new T[elementCount];
		}

		public Array CreateStatusArray(int elementCount)
		{
			return new OracleUdtStatus[elementCount];
		}
	}
}
#endif
