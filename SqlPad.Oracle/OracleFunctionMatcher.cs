using System;

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

		public bool IsMatch(OracleFunctionMetadata functionMetadata, string currentSchema)
		{
			var result = _ownerMatch == null || (String.IsNullOrEmpty(_ownerMatch.Value) && functionMetadata.Identifier.Owner == currentSchema.ToQuotedIdentifier()) ||
			             _ownerMatch.IsMatch(functionMetadata);
			
			result &= _packageMatch == null || _packageMatch.IsMatch(functionMetadata);
			return result && (_identifierMatch == null || _identifierMatch.IsMatch(functionMetadata));
		}
	}

	internal class MatchElement<TElement>
	{
		public MatchElement(string value)
		{
			Value = value;
		}

		public string Value { get; private set; }

		public bool AllowPartialMatch { get; set; }

		public bool AllowStartWithMatch { get; set; }

		public bool DenyEqualMatch { get; set; }

		public string DeniedValue { get; set; }

		public Func<TElement, string> Selector { get; protected set; }

		public bool IsMatch(TElement element)
		{
			var elementValue = Selector(element);
			var valueMatches = elementValue == Value.ToQuotedIdentifier() ||
			                   (AllowStartWithMatch && elementValue.ToRawUpperInvariant().StartsWith(Value.ToRawUpperInvariant())) ||
			                   (AllowPartialMatch && CodeCompletionSearchHelper.IsMatch(elementValue, Value));

			return valueMatches && (String.IsNullOrEmpty(DeniedValue) || elementValue != DeniedValue.ToQuotedIdentifier());
		}
	}

	internal class FunctionMatchElement : MatchElement<OracleFunctionMetadata>
	{
		private static readonly Func<OracleFunctionMetadata, string> OwnerSelector = metadata => metadata.Identifier.Owner;
		private static readonly Func<OracleFunctionMetadata, string> PackageSelector = metadata => metadata.Identifier.Package;
		private static readonly Func<OracleFunctionMetadata, string> NameSelector = metadata => metadata.Identifier.Name;

		public FunctionMatchElement(string value) : base(value)
		{
		}

		public FunctionMatchElement SelectOwner()
		{
			CheckSelectorNotAssigned();
			Selector = OwnerSelector;
			return this;
		}

		public FunctionMatchElement SelectPackage()
		{
			CheckSelectorNotAssigned();
			Selector = PackageSelector;
			return this;
		}

		public FunctionMatchElement SelectName()
		{
			CheckSelectorNotAssigned();
			Selector = NameSelector;
			return this;
		}

		private void CheckSelectorNotAssigned()
		{
			if (Selector != null)
			{
				throw new InvalidOperationException("Selector has been assigned already. ");
			}
		}

		public FunctionMatchElement SelectSynonymOwner()
		{
			CheckSelectorNotAssigned();
			Selector = SelectSynonymOwnerHandler;
			return this;
		}

		public FunctionMatchElement SelectSynonymPackage()
		{
			CheckSelectorNotAssigned();
			Selector = SelectSynonymPackageHandler;
			return this;
		}

		public FunctionMatchElement SelectSynonymName()
		{
			CheckSelectorNotAssigned();
			Selector = SelectSynonymIdentifierHandler;
			return this;
		}

		private static string SelectSynonymIdentifierHandler(OracleFunctionMetadata metadata)
		{
			var synonym = GetSynonymFor(metadata);
			if (synonym == null)
			{
				return null;
			}

			return metadata.Owner.Type == OracleSchemaObjectType.Function
				? synonym.Name
				: metadata.Identifier.Name;
		}

		private static string SelectSynonymPackageHandler(OracleFunctionMetadata metadata)
		{
			var synonym = GetSynonymFor(metadata);
			if (synonym == null)
			{
				return null;
			}

			return metadata.Owner.Type == OracleSchemaObjectType.Function
				? null
				: synonym.Name;
		}

		private static string SelectSynonymOwnerHandler(OracleFunctionMetadata metadata)
		{
			var synonym = GetSynonymFor(metadata);
			return synonym == null ? null : synonym.Owner;
		}

		private static OracleSynonym GetSynonymFor(OracleFunctionMetadata metadata)
		{
			return metadata == null || metadata.Owner == null
				? null
				: metadata.Owner.Synonym;
		}
	}
}
