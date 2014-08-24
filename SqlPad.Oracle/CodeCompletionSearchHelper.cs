using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terminals = SqlPad.Oracle.OracleGrammarDescription.Terminals;

namespace SqlPad.Oracle
{
	internal static class CodeCompletionSearchHelper
	{
		private static readonly HashSet<OracleFunctionIdentifier> SpecificCodeCompletionFunctionIdentifiers =
				new HashSet<OracleFunctionIdentifier>
			{
				OracleDatabaseModelBase.IdentifierBuiltInFunctionRound,
				OracleDatabaseModelBase.IdentifierBuiltInFunctionToChar,
				OracleDatabaseModelBase.IdentifierBuiltInFunctionTrunc
			};

		public static IEnumerable<ICodeCompletionItem> ResolveSpecificFunctionParameterCodeCompletionItems(StatementGrammarNode currentNode, IEnumerable<OracleCodeCompletionFunctionOverload> functionOverloads)
		{
			var completionItems = new List<ICodeCompletionItem>();
			var specificFunctionOverloads = functionOverloads.Where(m => SpecificCodeCompletionFunctionIdentifiers.Contains(m.FunctionMetadata.Identifier)).ToArray();
			if (specificFunctionOverloads.Length == 0)
			{
				return completionItems;
			}

			var truncFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => o.CurrentParameterIndex == 1 && o.FunctionMetadata.Identifier.In(OracleDatabaseModelBase.IdentifierBuiltInFunctionTrunc, OracleDatabaseModelBase.IdentifierBuiltInFunctionRound) &&
				                      o.FunctionMetadata.Parameters[o.CurrentParameterIndex + 1].DataType == "VARCHAR2");
			
			if (currentNode.Id == Terminals.StringLiteral && truncFunctionOverload != null)
			{
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "CC", "CC - One greater than the first two digits of a four-digit year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "YYYY", "YYYY - Year (rounds up on July 1)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "YEAR", "YEAR - Year (rounds up on July 1)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "I", "I - ISO Year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "IYYY", "IYYY - ISO Year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "Q", "Q - Quarter (rounds up on the sixteenth day of the second month of the quarter)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MONTH", "MONTH - Month (rounds up on the sixteenth day)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MON", "MON - Month (rounds up on the sixteenth day)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MM", "MM - Month (rounds up on the sixteenth day)"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "WW", "WW - Same day of the week as the first day of the year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "IW", "IW - Same day of the week as the first day of the ISO year"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "W", "W - Same day of the week as the first day of the month"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "D", "D - Starting day of the week"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "DAY", "DAY - Starting day of the week"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "HH", "HH - Hour"));
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, "MI", "MI - Minute"));
			}

			var toCharFunctionOverload = specificFunctionOverloads
				.FirstOrDefault(o => o.CurrentParameterIndex == 2 && o.FunctionMetadata.Identifier == OracleDatabaseModelBase.IdentifierBuiltInFunctionToChar &&
				                      o.FunctionMetadata.Parameters[o.CurrentParameterIndex + 1].DataType == "VARCHAR2");
			if (currentNode.Id == Terminals.StringLiteral && toCharFunctionOverload != null)
			{
				const string itemText = "NLS_NUMERIC_CHARACTERS = ''<decimal separator><group separator>'' NLS_CURRENCY = ''currency_symbol'' NLS_ISO_CURRENCY = <territory>";
				const string itemDescription = "NLS_NUMERIC_CHARACTERS = '<decimal separator><group separator>' NLS_CURRENCY = 'currency_symbol' NLS_ISO_CURRENCY = <territory>";
				completionItems.Add(BuildRoundOrTruncFormatParameter(currentNode, itemText, itemDescription));
			}

			return completionItems;
		}

		private static OracleCodeCompletionItem BuildRoundOrTruncFormatParameter(StatementGrammarNode node, string parameterValue, string description)
		{
			return
				new OracleCodeCompletionItem
				{
					Category = OracleCodeCompletionCategory.FunctionParameter,
					Name = description,
					StatementNode = node,
					Text = String.Format("'{0}'", parameterValue)
				};
		}

		public static bool IsMatch(string fullyQualifiedName, string inputPhrase)
		{
			inputPhrase = inputPhrase == null ? null : inputPhrase.Trim('"');
			return String.IsNullOrWhiteSpace(inputPhrase) ||
			       ResolveSearchPhrases(inputPhrase).All(p => fullyQualifiedName.ToUpperInvariant().Contains(p));
		}

		private static IEnumerable<string> ResolveSearchPhrases(string inputPhrase)
		{
			var builder = new StringBuilder();
			var containsSmallLetter = false;
			foreach (var character in inputPhrase)
			{
				if (containsSmallLetter && Char.IsUpper(character) && builder.Length > 0)
				{
					yield return builder.ToString().ToUpperInvariant();
					containsSmallLetter = false;
					builder.Clear();
				}
				else if (Char.IsLower(character))
				{
					containsSmallLetter = true;
				}

				builder.Append(character);
			}

			if (builder.Length > 0)
			{
				yield return builder.ToString().ToUpperInvariant();
			}
		}
	}
}
