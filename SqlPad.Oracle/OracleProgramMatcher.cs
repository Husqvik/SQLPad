using System;
using System.Collections.Generic;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle
{
	internal class OracleProgramMatcher
	{
		private readonly ProgramMatchElement _ownerMatch;
		private readonly ProgramMatchElement _packageMatch;
		private readonly ProgramMatchElement _identifierMatch;

		public bool AllowPlSql { get; set; }

		public OracleProgramMatcher(ProgramMatchElement ownerMatch, ProgramMatchElement packageMatch, ProgramMatchElement identifierMatch)
		{
			_identifierMatch = identifierMatch;
			_packageMatch = packageMatch;
			_ownerMatch = ownerMatch;
		}

		public ProgramMatchResult GetMatchResult(OracleProgramMetadata programMetadata, string quotedCurrentSchema)
		{
			var programTypeSupported = AllowPlSql || programMetadata.Type != ProgramType.Procedure;

			var matchResult =
				new ProgramMatchResult
				{
					Metadata = programMetadata,
					IsMatched = programTypeSupported
				};

			if (!programTypeSupported)
			{
				return matchResult;
			}

			var isSchemaMatched = _ownerMatch == null || (_ownerMatch.Value != null && _ownerMatch.Value.Length == 0 && String.Equals(programMetadata.Identifier.Owner, quotedCurrentSchema)) ||
			                      _ownerMatch.IsMatch(programMetadata).Any();

			matchResult.IsMatched &= isSchemaMatched;

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

	internal struct ProgramMatchResult
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

		public string Value { get; }

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
			return elements?.Where(IsElementMatch) ?? EmptyArray;
		}

		private bool IsElementMatch(string elementValue)
		{
			var valueMatches = String.Equals(elementValue, _quotedValue) ||
			                   (AllowStartWithMatch && elementValue.ToRawUpperInvariant().StartsWith(_rawUpperInvariantValue)) ||
			                   (AllowPartialMatch && CodeCompletionSearchHelper.IsMatch(elementValue, Value));

			return valueMatches && (String.IsNullOrEmpty(DeniedValue) || !String.Equals(elementValue, _quotedDeniedValue));
		}
	}

	internal class ProgramMatchElement : MatchElement<OracleProgramMetadata>
	{
		private static readonly Func<OracleProgramMetadata, IEnumerable<string>> OwnerSelector = metadata => Enumerable.Repeat(metadata.Identifier.Owner, 1);
		private static readonly Func<OracleProgramMetadata, IEnumerable<string>> PackageSelector = metadata => Enumerable.Repeat(metadata.Identifier.Package, 1);
		private static readonly Func<OracleProgramMetadata, IEnumerable<string>> NameSelector = metadata => Enumerable.Repeat(metadata.Identifier.Name, 1);

		private Func<OracleProgramMetadata, IEnumerable<string>> _selector;

		public ProgramMatchElement(string value) : base(value)
		{
		}

		protected override Func<OracleProgramMetadata, IEnumerable<string>> Selector => _selector;

	    public ProgramMatchElement AsResultValue()
		{
			IsResultValue = true;
			return this;
		}

		public ProgramMatchElement SelectOwner()
		{
			CheckSelectorNotAssigned();
			_selector = OwnerSelector;
			return this;
		}

		public ProgramMatchElement SelectPackage()
		{
			CheckSelectorNotAssigned();
			_selector = PackageSelector;
			return this;
		}

		public ProgramMatchElement SelectName()
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

		public ProgramMatchElement SelectSynonymOwner()
		{
			CheckSelectorNotAssigned();
			_selector = SelectSynonymOwnerHandler;
			return this;
		}

		public ProgramMatchElement SelectSynonymPackage()
		{
			CheckSelectorNotAssigned();
			_selector = SelectSynonymPackageHandler;
			return this;
		}

		public ProgramMatchElement SelectSynonymName()
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
			return synonyms?.Select(s => s.Owner);
		}

		private static IEnumerable<OracleSynonym> GetSynonymsFor(OracleProgramMetadata metadata)
		{
			return metadata?.Owner?.Synonyms;
		}
	}
}
