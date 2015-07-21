using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SqlPad.Oracle
{
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
		internal void Initialize()
		{
			if (!String.IsNullOrEmpty(RegexValue))
			{
				RegexMatcher = new Regex(RegexValue, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
			}
		}

		internal Regex RegexMatcher { get; private set; }

		internal bool IsFixed => RegexMatcher == null;
	}

	[DebuggerDisplay("SqlGrammarRuleSequenceTerminal (Id={Id}, IsOptional={IsOptional})")]
	public partial class SqlGrammarRuleSequenceTerminal : ISqlGrammarRuleSequenceItem
	{
		public bool IsRequired => !isOptionalFieldSpecified || (isOptionalFieldSpecified && !IsOptional);

	    public NodeType Type => NodeType.Terminal;

	    public SqlGrammarRuleSequence ParentSequence { get; set; }

		public int SequenceIndex { get; set; }
		
		public SqlGrammarTerminal Terminal;
	}

	[DebuggerDisplay("SqlGrammarRuleSequenceNonTerminal (Id={Id}, IsOptional={IsOptional})")]
	public partial class SqlGrammarRuleSequenceNonTerminal : ISqlGrammarRuleSequenceItem
	{
		public bool IsRequired => !isOptionalFieldSpecified || (isOptionalFieldSpecified && !IsOptional);

	    public NodeType Type => NodeType.NonTerminal;

	    public SqlGrammarRuleSequence ParentSequence { get; set; }

		public int SequenceIndex { get; set; }
		
		public SqlGrammarRule TargetRule;
	}

	public interface ISqlGrammarRuleSequenceItem
	{
		NodeType Type { get; }

		string Id { get; }

		bool IsRequired { get; }

		SqlGrammarRuleSequence ParentSequence { get; }
		
		int SequenceIndex { get; }
	}
}
