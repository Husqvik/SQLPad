using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using SqlPad.Commands;
using SqlPad.Oracle.DatabaseConnection;
using SqlPad.Oracle.DataDictionary;
using SqlPad.Oracle.SemanticModel;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	public class OracleHelpProvider : IHelpProvider
	{
		private static readonly object LockObject = new object();
		private static ILookup<string, DocumentationFunction> _sqlFunctionDocumentation;
		private static ILookup<string, DocumentationStatement> _statementDocumentation;
		private static IReadOnlyDictionary<OracleObjectIdentifier, DocumentationPackage> _packageDocumentations;
		private static IReadOnlyDictionary<OracleProgramIdentifier, DocumentationPackageSubProgram> _packageProgramDocumentations;
		private static IReadOnlyDictionary<OracleObjectIdentifier, DocumentationDataDictionaryObject> _dataDictionaryObjects;

		internal static ILookup<string, DocumentationFunction> SqlFunctionDocumentation
		{
			get
			{
				EnsureDocumentationDictionaries();

				return _sqlFunctionDocumentation;
			}
		}

		internal static IReadOnlyDictionary<OracleObjectIdentifier, DocumentationPackage> PackageDocumentations
		{
			get
			{
				EnsureDocumentationDictionaries();

				return _packageDocumentations;
			}
		}

		internal static IReadOnlyDictionary<OracleObjectIdentifier, DocumentationDataDictionaryObject> DataDictionaryObjectDocumentation
		{
			get
			{
				EnsureDocumentationDictionaries();

				return _dataDictionaryObjects;
			}
		}

		internal static IReadOnlyDictionary<OracleProgramIdentifier, DocumentationPackageSubProgram> PackageProgramDocumentations
		{
			get
			{
				EnsureDocumentationDictionaries();

				return _packageProgramDocumentations;
			}
		}

		private static void EnsureDocumentationDictionaries()
		{
			if (_sqlFunctionDocumentation != null)
			{
				return;
			}

			lock (LockObject)
			{
				if (_sqlFunctionDocumentation != null)
				{
					return;
				}

				var folder = new Uri(Path.GetDirectoryName(typeof (OracleHelpProvider).Assembly.CodeBase)).LocalPath;
				using (var reader = XmlReader.Create(Path.Combine(folder, "OracleDocumentation.xml")))
				{
					var documentation = (Documentation)new XmlSerializer(typeof (Documentation)).Deserialize(reader);

					_sqlFunctionDocumentation = documentation.Functions.ToLookup(f => f.Name.ToQuotedIdentifier());

					_statementDocumentation = documentation.Statements.ToLookup(s => s.Name);

					_dataDictionaryObjects = documentation.DataDictionary.ToDictionary(o => OracleObjectIdentifier.Create(OracleDatabaseModelBase.SchemaSys, o.Name));

					var packageDocumentations = new Dictionary<OracleObjectIdentifier, DocumentationPackage>();
					var packageProgramDocumentations = new Dictionary<OracleProgramIdentifier, DocumentationPackageSubProgram>();
					foreach (var packageDocumentation in documentation.Packages)
					{
						packageDocumentations.Add(OracleObjectIdentifier.Create(OracleDatabaseModelBase.SchemaSys, packageDocumentation.Name), packageDocumentation);

						if (packageDocumentation.SubPrograms == null)
						{
							continue;
						}

						foreach (var subProgramDocumentation in packageDocumentation.SubPrograms)
						{
							packageProgramDocumentations[OracleProgramIdentifier.CreateFromValues(OracleDatabaseModelBase.SchemaSys, packageDocumentation.Name, subProgramDocumentation.Name)] = subProgramDocumentation;
						}
					}

					_packageDocumentations = packageDocumentations.AsReadOnly();
					_packageProgramDocumentations = packageProgramDocumentations.AsReadOnly();
				}
			}
		}

		internal static bool TryGetPackageDocumentation(OracleSchemaObject schemaObject, out DocumentationPackage documentationPackage)
		{
			documentationPackage = null;
			schemaObject = schemaObject.GetTargetSchemaObject();
			return schemaObject != null &&
				   PackageDocumentations.TryGetValue(schemaObject.FullyQualifiedName, out documentationPackage) &&
				   documentationPackage.SubPrograms != null;
		}

		public void ShowHelp(ActionExecutionContext executionContext)
		{
			var statement = executionContext.DocumentRepository.Statements.GetStatementAtPosition(executionContext.CaretOffset);
			if (statement == null)
			{
				return;
			}

			var semanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[statement].SemanticModel;
			var terminal = statement.GetTerminalAtPosition(executionContext.CaretOffset, n => !String.Equals(n.Id, Terminals.Comma) && !String.Equals(n.Id, Terminals.Dot) && !String.Equals(n.Id, Terminals.RightParenthesis));
			if (terminal == null)
			{
				return;
			}

			EnsureDocumentationDictionaries();

			var programReference = semanticModel.GetProgramReference(terminal);
			if (programReference?.Metadata != null && programReference.Metadata.Type != ProgramType.StatementFunction)
			{
				ShowSqlFunctionDocumentation(programReference.Metadata.Identifier);
				return;
			}

			var objectReference = semanticModel.GetReference<OracleReference>(terminal);
			if (objectReference != null)
			{
				var targetObject = objectReference.SchemaObject.GetTargetSchemaObject();
				var package = targetObject as OraclePackage;
				if (package != null)
				{
					DocumentationPackage packageDocumentation;
					var packageDocumentationExists = _packageDocumentations.TryGetValue(package.FullyQualifiedName, out packageDocumentation);
					if (packageDocumentationExists)
					{
						Process.Start(packageDocumentation.Url);
					}
				}

				if (targetObject == null && objectReference.ObjectNodeObjectReferences.Count == 1)
				{
					targetObject = objectReference.ObjectNodeObjectReferences.Single().SchemaObject.GetTargetSchemaObject();
				}

				var view = targetObject as OracleView;
				if (view != null)
				{
					DocumentationDataDictionaryObject dataDictionaryObject;
					var dataDictionaryObjectDocumentationExists = _dataDictionaryObjects.TryGetValue(view.FullyQualifiedName, out dataDictionaryObject);
					if (dataDictionaryObjectDocumentationExists)
					{
						Process.Start(dataDictionaryObject.Url);
					}
				}
			}

			var firstFourTerminals = terminal.RootNode.Terminals.Where(t => t.IsRequiredIncludingParent).Take(4).ToList();
			if (!terminal.Id.IsIdentifierOrAlias() && firstFourTerminals.IndexOf(terminal) != -1)
			{
				for (var i = 4; i > 0; i--)
				{
					var statementDocumentationKey = String.Join(" ", firstFourTerminals.Take(i).Select(t => ((OracleToken)t.Token).UpperInvariantValue));
					foreach (var documentation in _statementDocumentation[statementDocumentationKey])
					{
						Process.Start(documentation.Url);
						return;
					}
				}
			}
		}

		private static void ShowSqlFunctionDocumentation(OracleProgramIdentifier identifier)
		{
			var isBuiltInSqlFunction = (String.IsNullOrEmpty(identifier.Package) || String.Equals(identifier.Package, OracleDatabaseModelBase.PackageBuiltInFunction));
			if (isBuiltInSqlFunction)
			{
				foreach (var documentation in SqlFunctionDocumentation[identifier.Name])
				{
					Process.Start(documentation.Url);
				}
			}

			DocumentationPackage packageDocumentation;
			var packageDocumentationExists = _packageDocumentations.TryGetValue(OracleObjectIdentifier.Create(identifier.Owner, identifier.Package), out packageDocumentation);
			if (packageDocumentationExists)
			{
				var program = packageDocumentation.SubPrograms.SingleOrDefault(sp => String.Equals(sp.Name.ToQuotedIdentifier(), identifier.Name));
				if (program != null)
				{
					Process.Start($"{packageDocumentation.Url}{program.ElementId}");
				}
			}
		}
	}
}