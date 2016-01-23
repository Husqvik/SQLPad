using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using SqlPad.Commands;
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

					var dataDictionaryObjects = documentation.DataDictionary.ToDictionary(o => OracleObjectIdentifier.Create(OracleObjectIdentifier.SchemaSys, o.Name));
					_dataDictionaryObjects = dataDictionaryObjects.AsReadOnly();

					var packageDocumentations = new Dictionary<OracleObjectIdentifier, DocumentationPackage>();
					var packageProgramDocumentations = new Dictionary<OracleProgramIdentifier, DocumentationPackageSubProgram>();
					foreach (var packageDocumentation in documentation.Packages)
					{
						var packageIdentifier = OracleObjectIdentifier.Create(OracleObjectIdentifier.SchemaSys, packageDocumentation.Name);
						packageDocumentations.Add(packageIdentifier, packageDocumentation);

						var dataDictionaryObjectDocumentation =
							new DocumentationDataDictionaryObject
							{
								Name = packageDocumentation.Name,
								Value = packageDocumentation.Description,
								Url = packageDocumentation.Url
							};

						dataDictionaryObjects.Add(packageIdentifier, dataDictionaryObjectDocumentation);

						if (packageDocumentation.SubPrograms == null)
						{
							continue;
						}

						foreach (var subProgramDocumentation in packageDocumentation.SubPrograms)
						{
							subProgramDocumentation.PackageDocumentation = packageDocumentation;
							packageProgramDocumentations[OracleProgramIdentifier.CreateFromValues(OracleObjectIdentifier.SchemaSys, packageDocumentation.Name, subProgramDocumentation.Name)] = subProgramDocumentation;
						}
					}

					_packageProgramDocumentations = packageProgramDocumentations.AsReadOnly();
				}
			}
		}

		internal static string GetBuiltInSqlFunctionDocumentation(string sqlFunctionNormalizedName)
		{
			var documentationBuilder = new StringBuilder();

			foreach (var documentationFunction in SqlFunctionDocumentation[sqlFunctionNormalizedName])
			{
				if (documentationBuilder.Length > 0)
				{
					documentationBuilder.AppendLine();
				}

				documentationBuilder.AppendLine(documentationFunction.Value);
			}

			return documentationBuilder.ToString();
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
				DocumentationDataDictionaryObject dataDictionaryObject;
				var dataDictionaryObjectDocumentationFound = _dataDictionaryObjects.TryGetValue(targetObject.FullyQualifiedName, out dataDictionaryObject);
				if (dataDictionaryObjectDocumentationFound)
				{
					Process.Start(dataDictionaryObject.Url);
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
			var isBuiltInSqlFunction = (String.IsNullOrEmpty(identifier.Package) || String.Equals(identifier.Package, OracleObjectIdentifier.PackageBuiltInFunction));
			if (isBuiltInSqlFunction)
			{
				foreach (var documentation in _sqlFunctionDocumentation[identifier.Name])
				{
					Process.Start(documentation.Url);
				}
			}

			DocumentationPackageSubProgram programDocumentation;
			var packageProgramDocumentationExists = _packageProgramDocumentations.TryGetValue(identifier, out programDocumentation);
			if (packageProgramDocumentationExists)
			{
				Process.Start($"{programDocumentation.PackageDocumentation.Url}{programDocumentation.ElementId}");
			}
		}
	}
}