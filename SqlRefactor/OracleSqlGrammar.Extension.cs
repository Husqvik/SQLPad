using System.Diagnostics;

namespace SqlRefactor
{
	public partial class SqlGrammar
	{
	}

	[DebuggerDisplay("SqlGrammarStartSymbol (Id={Id})")]
	public partial class SqlGrammarStartSymbol
	{

	}

	[DebuggerDisplay("SqlGrammarRuleSequence (Elements={Items.Length}, Comment={Comment})")]
	public partial class SqlGrammarRuleSequence
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