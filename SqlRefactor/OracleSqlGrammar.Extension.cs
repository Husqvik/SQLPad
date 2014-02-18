using System.Collections.Generic;
using System.Diagnostics;

namespace SqlRefactor
{
	public partial class SqlGrammar
	{
		//private bool _initialized;
		private HashSet<string> _startSymbols = new HashSet<string>();

		internal void Initialize()
		{
			//_initialized = true;
		}
	}

	[DebuggerDisplay("SqlGrammarStartSymbol (Id={Id})")]
	public partial class SqlGrammarStartSymbol
	{

	}

	public partial class SqlGrammarRule
	{
		
	}

	[DebuggerDisplay("SqlGrammarTerminal (Id={Id}, Value={Value}, RegexValue={RegexValue})")]
	public partial class SqlGrammarTerminal
	{
		
	}

	[DebuggerDisplay("SqlGrammarRuleSequenceTerminal (Id={Id})")]
	public partial class SqlGrammarRuleSequenceTerminal
	{
		internal bool IsRequired
		{
			get { return !isOptionalFieldSpecified || (isOptionalFieldSpecified && !IsOptional); }
		}
	}

	[DebuggerDisplay("SqlGrammarRuleSequenceNonTerminal (Id={Id}, IsOptional={IsOptional})")]
	public partial class SqlGrammarRuleSequenceNonTerminal
	{
		internal bool IsRequired
		{
			get { return !isOptionalFieldSpecified || (isOptionalFieldSpecified && !IsOptional); }
		}
	}
}