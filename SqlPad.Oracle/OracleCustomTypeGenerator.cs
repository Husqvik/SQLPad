using System;
using System.Collections.Generic;
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
	internal static class OracleCustomTypeGenerator
	{
		public static void GenerateCustomTypeAssembly(OracleDataDictionary dataDictionary)
		{
			var baseFactoryType = typeof(OracleTableValueFactoryBase);
			var assembly = baseFactoryType.Assembly;
			var fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();
			var targetFrameworkAttribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
			var companyAttribute = assembly.GetCustomAttribute<AssemblyCompanyAttribute>();
			var productAttribute = assembly.GetCustomAttribute<AssemblyProductAttribute>();
			const string dynamicAssemblyName = "SqlPad.Oracle.CustomTypes";
			const string dynamicAssemblyFileName = "SqlPad.Oracle.CustomTypes.dll";

			var assemblyVersion = assembly.GetName().Version;
			var constructorStringParameters = new[] { typeof(string) };

			var customAttributeBuilders = new[]
			{
				new CustomAttributeBuilder(typeof (AssemblyTitleAttribute).GetConstructor(constructorStringParameters), GetParameterAsObjectArray(dynamicAssemblyName)),
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

			var assemblyName = new AssemblyName(dynamicAssemblyName) { Version = assemblyVersion };
			var customTypeAssemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave, null, true, customAttributeBuilders);

			customTypeAssemblyBuilder.DefineVersionInfoResource(); // Makes attributes readable by unmanaged environment like Windows Explorer.
			var customTypeModuleBuilder = customTypeAssemblyBuilder.DefineDynamicModule(dynamicAssemblyFileName, dynamicAssemblyFileName, true);

			var collectionTypes = dataDictionary.AllObjects.Values
				.OfType<OracleTypeCollection>()
				.Where(c => c.ElementTypeIdentifier.Owner == "\"\"")
				.Select(c => c.FullyQualifiedName.ToString());

			var types = new List<Type>();
			foreach (var typeName in collectionTypes)
			{
				types.Add(CreateOracleCustomType(customTypeModuleBuilder, typeName));
			}

			customTypeAssemblyBuilder.Save(dynamicAssemblyFileName);
		}

		private static Type CreateOracleCustomType(ModuleBuilder customTypeModuleBuilder, string fullyQualifiedCollectionTypeName)
		{
			var baseFactoryType = typeof(OracleTableValueFactoryBase);
			var wrapperTypeName = "SqlPad.Oracle.CustomTypes" + "." + fullyQualifiedCollectionTypeName.Replace('.', '_');

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

			var items = Array.Select(i => i.ToString());

			if (!String.IsNullOrEmpty(_enclosingCharacter))
			{
				items = items.Select(i => String.Format("{0}{1}{0}", _enclosingCharacter, i));
			}

			return String.Format("{0}({1})", FullTypeName, String.Join(", ", items));
		}
	}

	public abstract class OracleTableValueFactoryBase : IOracleCustomTypeFactory, IOracleArrayTypeFactory
	{
		protected abstract string FullyQualifiedName { get; }

		public IOracleCustomType CreateObject()
		{
			return new OracleValueArray<string>(FullyQualifiedName, "'");
		}

		public Array CreateArray(int elementCount)
		{
			return new string[elementCount];
		}

		public Array CreateStatusArray(int elementCount)
		{
			return new OracleUdtStatus[elementCount];
		}
	}
}
