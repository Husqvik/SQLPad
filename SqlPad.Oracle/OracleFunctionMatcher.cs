using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlPad.Oracle
{
	internal class OracleFunctionMatcher
	{
		private readonly FunctionMatchElement _ownerMatch;
		private readonly FunctionMatchElement _packageMatch;
		private readonly FunctionMatchElement _identifierMatch;

		public OracleFunctionMatcher(FunctionMatchElement ownerMatch, FunctionMatchElement packageMatch, FunctionMatchElement identifierMatch)
		{
			_identifierMatch = identifierMatch;
			_packageMatch = packageMatch;
			_ownerMatch = ownerMatch;
		}

		public FunctionMatchResult GetMatchResult(OracleProgramMetadata programMetadata, string quotedCurrentSchema)
		{
			var isSchemaMatched = _ownerMatch == null || (_ownerMatch.Value != null && _ownerMatch.Value.Length == 0 && String.CompareOrdinal(programMetadata.Identifier.Owner, quotedCurrentSchema) == 0) ||
			                      _ownerMatch.IsMatch(programMetadata).Any();
			var matchResult =
				new FunctionMatchResult
				{
					Metadata = programMetadata,
					IsMatched = isSchemaMatched
				};

			if (_packageMatch != null && isSchemaMatched)
			{
				var matchedPackageNames = _packageMatch.IsMatch(programMetadata);
				if (_packageMatch.IsResultValue)
				{
					matchResult.Matches = matchedPackageNames;
				}

				matchResult.IsMatched &= matchedPackageNames.Any();
			}

			if (_identifierMatch != null && matchResult.IsMatched)
			{
				var matchedIdentifierNames = _identifierMatch.IsMatch(programMetadata);
				if (_identifierMatch.IsResultValue)
				{
					if (matchResult.Matches != null)
					{
						throw new InvalidOperationException("Only one MatchElement can be IsResultValue enabled. ");
					}

					matchResult.Matches = matchedIdentifierNames;
				}

				matchResult.IsMatched &= matchedIdentifierNames.Any();
			}

			return matchResult;
		}
	}

	internal struct FunctionMatchResult
	{
		public bool IsMatched { get; set; }

		public OracleProgramMetadata Metadata { get; set; }
		
		public IEnumerable<string> Matches { get; set; }
	}

	internal abstract class MatchElement<TElement>
	{
		private static readonly string[] EmptyArray = new string[0];
		private readonly string _quotedValue;
		private readonly string _rawUpperInvariantValue;
		private string _deniedValue;
		private string _quotedDeniedValue;

		protected MatchElement(string value)
		{
			Value = value;
			_quotedValue = value.ToQuotedIdentifier();
			_rawUpperInvariantValue = value.ToRawUpperInvariant();
		}

		public string Value { get; private set; }

		public bool AllowPartialMatch { get; set; }

		public bool IsResultValue { get; set; }

		public bool AllowStartWithMatch { get; set; }

		public bool DenyEqualMatch { get; set; }

		public string DeniedValue
		{
			get { return _deniedValue; }
			set
			{
				_deniedValue = value;
				_quotedDeniedValue = value.ToQuotedIdentifier();
			}
		}

		protected abstract Func<TElement, IEnumerable<string>> Selector { get; }

		public IEnumerable<string> IsMatch(TElement element)
		{
			var elements = Selector(element);
			return elements == null
				? EmptyArray
				: elements.Where(IsElementMatch);
		}

		private bool IsElementMatch(string elementValue)
		{
			var valueMatches = String.CompareOrdinal(elementValue, _quotedValue) == 0 ||
			                   (AllowStartWithMatch && elementValue.ToRawUpperInvariant().StartsWith(_rawUpperInvariantValue)) ||
			                   (AllowPartialMatch && CodeCompletionSearchHelper.IsMatch(elementValue, Value));

			return valueMatches && (String.IsNullOrEmpty(DeniedValue) || String.CompareOrdinal(elementValue, _quotedDeniedValue) != 0);
		}
	}

	internal class FunctionMatchElement : MatchElement<OracleProgramMetadata>
	{
		private static readonly Func<OracleProgramMetadata, IEnumerable<string>> OwnerSelector = metadata => Enumerable.Repeat(metadata.Identifier.Owner, 1);
		private static readonly Func<OracleProgramMetadata, IEnumerable<string>> PackageSelector = metadata => Enumerable.Repeat(metadata.Identifier.Package, 1);
		private static readonly Func<OracleProgramMetadata, IEnumerable<string>> NameSelector = metadata => Enumerable.Repeat(metadata.Identifier.Name, 1);

		private Func<OracleProgramMetadata, IEnumerable<string>> _selector;

		public FunctionMatchElement(string value) : base(value)
		{
		}

		protected override Func<OracleProgramMetadata, IEnumerable<string>> Selector
		{
			get { return _selector; }
		}

		public FunctionMatchElement AsResultValue()
		{
			IsResultValue = true;
			return this;
		}

		public FunctionMatchElement SelectOwner()
		{
			CheckSelectorNotAssigned();
			_selector = OwnerSelector;
			return this;
		}

		public FunctionMatchElement SelectPackage()
		{
			CheckSelectorNotAssigned();
			_selector = PackageSelector;
			return this;
		}

		public FunctionMatchElement SelectName()
		{
			CheckSelectorNotAssigned();
			_selector = NameSelector;
			return this;
		}

		private void CheckSelectorNotAssigned()
		{
			if (_selector != null)
			{
				throw new InvalidOperationException("Selector has been assigned already. ");
			}
		}

		public FunctionMatchElement SelectSynonymOwner()
		{
			CheckSelectorNotAssigned();
			_selector = SelectSynonymOwnerHandler;
			return this;
		}

		public FunctionMatchElement SelectSynonymPackage()
		{
			CheckSelectorNotAssigned();
			_selector = SelectSynonymPackageHandler;
			return this;
		}

		public FunctionMatchElement SelectSynonymName()
		{
			CheckSelectorNotAssigned();
			_selector = SelectSynonymIdentifierHandler;
			return this;
		}

		private static IEnumerable<string> SelectSynonymIdentifierHandler(OracleProgramMetadata metadata)
		{
			var synonyms = GetSynonymsFor(metadata);
			if (synonyms == null || metadata.Owner == null)
			{
				return null;
			}

			return metadata.Owner.Type == OracleSchemaObjectType.Function
				? synonyms.Select(s => s.Name)
				: Enumerable.Repeat(metadata.Identifier.Name, 1);
		}

		private static IEnumerable<string> SelectSynonymPackageHandler(OracleProgramMetadata metadata)
		{
			var synonyms = GetSynonymsFor(metadata);
			if (synonyms == null || metadata.Owner == null)
			{
				return null;
			}

			return metadata.Owner.Type == OracleSchemaObjectType.Function
				? null
				: synonyms.Select(s => s.Name);
		}

		private static IEnumerable<string> SelectSynonymOwnerHandler(OracleProgramMetadata metadata)
		{
			var synonyms = GetSynonymsFor(metadata);
			return synonyms == null ? null : synonyms.Select(s => s.Owner);
		}

		private static IEnumerable<OracleSynonym> GetSynonymsFor(OracleProgramMetadata metadata)
		{
			return metadata == null || metadata.Owner == null
				? null
				: metadata.Owner.Synonyms;
		}
	}
}
