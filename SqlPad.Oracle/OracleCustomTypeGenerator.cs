using System;
using System.Collections.Generic;
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

			var collectionTypes = dataDictionary.AllObjects.Values
				.OfType<OracleTypeCollection>()
				.Select(c => new KeyValuePair<string, string>(c.FullyQualifiedName.ToString(), c.ElementTypeIdentifier.Name.Trim('"')));

			var types = new List<Type>();
			foreach (var typeName in collectionTypes)
			{
				types.Add(CreateOracleCustomType(customTypeModuleBuilder, typeName));
			}

			customTypeAssemblyBuilder.Save(_customTypeAssemblyFile.Name);
		}

		private static Type CreateOracleCustomType(ModuleBuilder customTypeModuleBuilder, KeyValuePair<string, string> fullyQualifiedCollectionTypeNameDataTypePair)
		{
			var fullyQualifiedCollectionTypeName = fullyQualifiedCollectionTypeNameDataTypePair.Key;
			
			var targetType = typeof (string);
			var enclosingCharacter = "'";
			switch (fullyQualifiedCollectionTypeNameDataTypePair.Value)
			{
				case "NUMBER":
					targetType = typeof (decimal);
					enclosingCharacter = String.Empty;
					break;
				case "DATE":
					targetType = typeof(DateTime);
					enclosingCharacter = String.Empty;
					break;
				case "RAW":
					targetType = typeof(byte[]);
					enclosingCharacter = String.Empty;
					break;
			}
			
			var baseFactoryType = typeof(OracleTableValueFactoryBase<>);
			baseFactoryType = baseFactoryType.MakeGenericType(targetType);
			var wrapperTypeName = String.Format("{0}.{1}", DynamicAssemblyNameBase, fullyQualifiedCollectionTypeName.Replace('.', '_'));

			var customTypeBuilder = customTypeModuleBuilder.DefineType(wrapperTypeName, TypeAttributes.Public | TypeAttributes.Class, baseFactoryType);
			var attributeType = typeof (OracleCustomTypeMappingAttribute);
			var attribute = new CustomAttributeBuilder(attributeType.GetConstructor(new[] {typeof (string)}), new[] {fullyQualifiedCollectionTypeName});
			customTypeBuilder.SetCustomAttribute(attribute);

			var baseConstructor = baseFactoryType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
			var constructor = customTypeBuilder.DefineConstructor(MethodAttributes.Public | MethodAttributes.HideBySig, CallingConventions.HasThis, null);
			var ilGenerator = constructor.GetILGenerator();
			ilGenerator.Emit(OpCodes.Ldarg_0);
			ilGenerator.Emit(OpCodes.Call, baseConstructor);
			ilGenerator.Emit(OpCodes.Ret);

			ImplementAbstractStringValueProperty(customTypeBuilder, "FullyQualifiedName", fullyQualifiedCollectionTypeName);
			ImplementAbstractStringValueProperty(customTypeBuilder, "EnclosingCharacter", enclosingCharacter);

			return customTypeBuilder.CreateType();
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
	}
}
