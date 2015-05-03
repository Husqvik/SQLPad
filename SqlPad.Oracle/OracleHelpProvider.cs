using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using SqlPad.Commands;

namespace SqlPad.Oracle
{
	public class OracleHelpProvider : IHelpProvider
	{
		private static IReadOnlyDictionary<string, DocumentationFunction> _sqlFunctionDocumentation;
		private static IReadOnlyDictionary<string, DocumentationStatement> _statementDocumentation;

		internal static IReadOnlyDictionary<string, DocumentationFunction> SqlFunctionDocumentation
		{
			get
			{
				EnsureDocumentationDictionaries();

				return _sqlFunctionDocumentation;
			}
		}

		private static void EnsureDocumentationDictionaries()
		{
			if (_sqlFunctionDocumentation != null)
			{
				return;
			}

			var folder = new Uri(Path.GetDirectoryName(typeof (OracleHelpProvider).Assembly.CodeBase)).LocalPath;
			using (var reader = XmlReader.Create(Path.Combine(folder, "OracleSqlFunctionDocumentation.xml")))
			{
				var documentation = (Documentation)new XmlSerializer(typeof (Documentation)).Deserialize(reader);
				var sqlFunctionDocumentation = new Dictionary<string, DocumentationFunction>();

				foreach (var function in documentation.Functions)
				{
					DocumentationFunction existingFunction;
					var identifier = function.Name.ToQuotedIdentifier();
					if (!sqlFunctionDocumentation.TryGetValue(identifier, out existingFunction) || function.Value.Length > existingFunction.Value.Length)
					{
						sqlFunctionDocumentation[identifier] = function;
					}
					else
					{
						Trace.WriteLine(String.Format("Function documentation skipped: {0}", function.Value));
					}
				}

				_sqlFunctionDocumentation = new ReadOnlyDictionary<string, DocumentationFunction>(sqlFunctionDocumentation);

				var records = new HashSet<string>();
				var statementDocumentation = documentation.Statements
					.Where(s => records.Add(s.Name))
					.ToDictionary(s => s.Name);

				_statementDocumentation = new ReadOnlyDictionary<string, DocumentationStatement>(statementDocumentation);
			}
		}

		public void ShowHelp(CommandExecutionContext executionContext)
		{
			var statement = executionContext.DocumentRepository.Statements.GetStatementAtPosition(executionContext.CaretOffset);
			if (statement == null)
			{
				return;
			}

			var semanticModel = (OracleStatementSemanticModel)executionContext.DocumentRepository.ValidationModels[statement].SemanticModel;
			var terminal = statement.GetTerminalAtPosition(executionContext.CaretOffset);
			if (terminal == null)
			{
				return;
			}

			EnsureDocumentationDictionaries();

			var programReference = semanticModel.GetProgramReference(terminal);
			if (programReference != null && programReference.Metadata != null && programReference.Metadata.Type != ProgramType.StatementFunction)
			{
				ShowSqlFunctionDocumentation(programReference.Metadata.Identifier);
				return;
			}

			var firstThreeTerminals = terminal.RootNode.Terminals.Where(t => t.IsRequiredIncludingParent).Take(3).ToList();
			if (!terminal.Id.IsIdentifierOrAlias() && firstThreeTerminals.IndexOf(terminal) != -1)
			{
				for (var i = 3; i > 0; i--)
				{
					var statementDocumentationKey = String.Join(" ", firstThreeTerminals.Take(i).Select(t => ((OracleToken)t.Token).UpperInvariantValue));
					DocumentationStatement documentation;
					if (_statementDocumentation.TryGetValue(statementDocumentationKey, out documentation))
					{
						Process.Start(documentation.Url);
						return;
					}
				}
			}
		}

		private static void ShowSqlFunctionDocumentation(OracleProgramIdentifier identifier)
		{
			DocumentationFunction documentation;
			if ((String.IsNullOrEmpty(identifier.Package) || String.Equals(identifier.Package, OracleDatabaseModelBase.PackageBuiltInFunction)) && OracleHelpProvider.SqlFunctionDocumentation.TryGetValue(identifier.Name, out documentation))
			{
				Process.Start(documentation.Url);
			}
		}
	}
}