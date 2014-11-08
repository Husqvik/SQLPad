using System;
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

namespace SqlPad.Oracle
{
	internal class OracleCustomTypeGenerator
	{
		private const string DynamicAssemblyNameBase = "SqlPad.Oracle.CustomTypes";
		private static readonly Assembly CurrentAssembly = typeof(OracleCustomTypeGenerator).Assembly;
		private static readonly Dictionary<string, OracleCustomTypeGenerator> Generators = new Dictionary<string, OracleCustomTypeGenerator>();

		private readonly string _customTypeAssemblyName;	
		private readonly FileInfo _customTypeAssemblyFile;	
		private readonly Assembly _customTypeAssembly;
		//private readonly AppDomain _customTypeHostDomain;

		private OracleCustomTypeGenerator(string connectionStringName)
		{
			_customTypeAssemblyName = String.Format("{0}.{1}", DynamicAssemblyNameBase, connectionStringName);
			var customTypeAssemblyFileName = String.Format("{0}.dll", _customTypeAssemblyName);
			var directoryName = Path.GetDirectoryName(CurrentAssembly.Location);
			var customTypeAssemblyFullFileName = Path.Combine(directoryName, customTypeAssemblyFileName);

			if (File.Exists(customTypeAssemblyFullFileName))
			{
				_customTypeAssembly = Assembly.LoadFile(customTypeAssemblyFullFileName);
				//_customTypeHostDomain = AppDomain.CreateDomain(String.Format("{0}.{1}", "CustomTypeHostDomain", connectionStringName));
				//_customTypeHostDomain.Load(DynamicAssemblyNameBase);

				Trace.WriteLine(String.Format("{0} - Custom object and collection types assembly '{1}' has been found and loaded. ", DateTime.Now, customTypeAssemblyFileName));
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

			Trace.WriteLine(String.Format("{0} - Custom object and collection types generation started. ", DateTime.Now));

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

			stopwatch.Stop();

			var collectionTypeCount = customTypes.Count - objectTypeCount;
			Trace.WriteLine(String.Format("{0} - {1} Custom object types and {2} collection types generated into {3} in {4}. ", DateTime.Now, objectTypeCount, collectionTypeCount, _customTypeAssemblyFile.Name, stopwatch.Elapsed));
		}

		private static void CreateOracleObjectType(ModuleBuilder customTypeModuleBuilder, OracleTypeObject objectType, IDictionary<string, Type> customTypes)
		{
			var fullyQualifiedObjectTypeName = objectType.FullyQualifiedName.ToString().Replace("\"", null);
			var customTypeClassName = String.Format("{0}.ObjectTypes.{1}", DynamicAssemblyNameBase, MakeValidMemberName(fullyQualifiedObjectTypeName));
			var customTypeBuilder = customTypeModuleBuilder.DefineType(customTypeClassName, TypeAttributes.Public | TypeAttributes.Class, typeof(object), new[] { typeof(IOracleCustomType), typeof(IOracleCustomTypeFactory) });
			AddOracleCustomTypeMappingAttribute(customTypeBuilder, fullyQualifiedObjectTypeName);

			var constructorBuilder = AddConstructor(customTypeBuilder, typeof(object).GetConstructor(Type.EmptyTypes));

			const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;
			var propertyGetterBuilder = customTypeBuilder.DefineMethod("CreateObject", attributes, typeof(IOracleCustomType), null);

			var ilGenerator = propertyGetterBuilder.GetILGenerator();
			ilGenerator.Emit(OpCodes.Newobj, constructorBuilder);
			ilGenerator.Emit(OpCodes.Ret);

			var fields = new Dictionary<string, FieldInfo>();
			foreach (var objectAttribute in objectType.Attributes)
			{
				var targetType = typeof (string);
				switch (objectAttribute.DataType.FullyQualifiedName.ToString().Trim('"'))
				{
					case "NUMBER":
						targetType = typeof(decimal?);
						break;
					case "DATE":
						targetType = typeof(DateTime?);
						break;
					case "BLOB":
					case "LONG RAW":
					case "RAW":
						targetType = typeof(byte[]);
						break;
				}

				var fieldName = MakeValidMemberName(objectAttribute.Name);
				var fieldBuilder = customTypeBuilder.DefineField(fieldName, targetType, FieldAttributes.Public);

				var attributeType = typeof (OracleObjectMappingAttribute);
				var attribute = new CustomAttributeBuilder(attributeType.GetConstructor(new[] { typeof(string) }), new[] { objectAttribute.Name.Replace("\"", null) });
				fieldBuilder.SetCustomAttribute(attribute);

				fields.Add(fieldName, fieldBuilder);
			}

			var fromCustomObjectBuilder = customTypeBuilder.DefineMethod("FromCustomObject", attributes, null, new []{ typeof(OracleConnection), typeof(IntPtr) });
			fromCustomObjectBuilder.DefineParameter(1, ParameterAttributes.None, "connection");
			fromCustomObjectBuilder.DefineParameter(2, ParameterAttributes.None, "pointerUdt");
			ilGenerator = fromCustomObjectBuilder.GetILGenerator();

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

			var toCustomObjectBuilder = customTypeBuilder.DefineMethod("ToCustomObject", attributes, null, new[] { typeof(OracleConnection), typeof(IntPtr) });
			toCustomObjectBuilder.DefineParameter(1, ParameterAttributes.None, "connection");
			toCustomObjectBuilder.DefineParameter(2, ParameterAttributes.None, "pointerUdt");
			ilGenerator = toCustomObjectBuilder.GetILGenerator();

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

			customTypes.Add(fullyQualifiedObjectTypeName, customTypeBuilder.CreateType());
		}

		private static void CreateOracleCollectionType(ModuleBuilder customTypeModuleBuilder, OracleTypeCollection collectionType, IDictionary<string, Type> customTypes)
		{
			Type targetType;
			var enclosingCharacter = String.Empty;
			if (String.IsNullOrEmpty(collectionType.ElementTypeIdentifier.Owner))
			{
				var dataType = collectionType.ElementTypeIdentifier.Name.Trim('"');
				switch (dataType)
				{
					case "NUMBER":
						targetType = typeof (decimal);
						break;
					case "DATE":
						targetType = typeof (DateTime);
						break;
					case "BLOB":
					case "LONG RAW":
					case "RAW":
						targetType = typeof (byte[]);
						break;
					default:
						enclosingCharacter = "'";
						targetType = typeof(string);
						break;
				}
			}
			else if (customTypes.TryGetValue(collectionType.ElementTypeIdentifier.ToString().Replace("\"", null), out targetType))
			{

			}
			else
			{
				targetType = typeof(object);
			}

			var baseFactoryType = typeof(OracleTableValueFactoryBase<>);
			baseFactoryType = baseFactoryType.MakeGenericType(targetType);
			var fullyQualifiedCollectionTypeName = collectionType.FullyQualifiedName.ToString().Replace("\"", null); ;
			var customTypeClassName = String.Format("{0}.CollectionTypes.{1}", DynamicAssemblyNameBase, MakeValidMemberName(fullyQualifiedCollectionTypeName));

			var customTypeBuilder = customTypeModuleBuilder.DefineType(customTypeClassName, TypeAttributes.Public | TypeAttributes.Class, baseFactoryType);
			AddOracleCustomTypeMappingAttribute(customTypeBuilder, fullyQualifiedCollectionTypeName);

			var baseConstructor = baseFactoryType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
			AddConstructor(customTypeBuilder, baseConstructor);

			ImplementAbstractStringValueProperty(customTypeBuilder, "FullyQualifiedName", fullyQualifiedCollectionTypeName);
			ImplementAbstractStringValueProperty(customTypeBuilder, "EnclosingCharacter", enclosingCharacter);

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

		private static void ImplementAbstractStringValueProperty(TypeBuilder typeBuilder, string propertyName, string value)
		{
			var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.None, CallingConventions.HasThis, typeof(string), null);

			const MethodAttributes attributes = MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.SpecialName;
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

	public class OracleValueArray<T> : IOracleCustomType, INullable
	{
		private readonly string _enclosingCharacter;

		public string FullTypeName { get; private set; }

		[OracleArrayMapping]
		public T[] Array { get; set; }

		public OracleValueArray(string fullTypeName, string enclosingCharacter = null, T[] array = null)
		{
			FullTypeName = fullTypeName;
			_enclosingCharacter = enclosingCharacter;
			Array = array;
		}

		public bool IsNull { get { return Array == null; } }

		public void FromCustomObject(OracleConnection connection, IntPtr pointerUdt)
		{
			OracleUdt.SetValue(connection, pointerUdt, 0, Array);
		}

		public void ToCustomObject(OracleConnection connection, IntPtr pointerUdt)
		{
			Array = (T[])OracleUdt.GetValue(connection, pointerUdt, 0);
		}

		public override string ToString()
		{
			if (IsNull)
			{
				return String.Empty;
			}

			var items = Array.Select(i => Convert.ToString(i));

			if (!String.IsNullOrEmpty(_enclosingCharacter))
			{
				items = items.Select(i => String.Format("{0}{1}{0}", _enclosingCharacter, i));
			}

			return String.Format("{0}({1})", FullTypeName, String.Join(", ", items));
		}
	}

	public abstract class OracleTableValueFactoryBase<T> : IOracleCustomTypeFactory, IOracleArrayTypeFactory
	{
		protected abstract string FullyQualifiedName { get; }
		
		protected abstract string EnclosingCharacter { get; }

		public IOracleCustomType CreateObject()
		{
			return new OracleValueArray<T>(FullyQualifiedName, EnclosingCharacter);
		}

		public Array CreateArray(int elementCount)
		{
			return new T[elementCount];
		}

		public Array CreateStatusArray(int elementCount)
		{
			return new OracleUdtStatus[elementCount];
		}
	}/*
		public override string ToString()
		{
			return String.Format("{0}(ARGTYPE={1}, TABLENAME={2}, TABLESCHEMA={3}, COLNAME={4}, TABLEPARTITIONLOWER={5}, TABLEPARTITIONUPPER={6}, CARDINALITY={7})", "SYS.ODCIARGDESC", ARGTYPE, TABLENAME, TABLESCHEMA, COLNAME, TABLEPARTITIONLOWER, TABLEPARTITIONUPPER, CARDINALITY);
		}
	}*/
}
