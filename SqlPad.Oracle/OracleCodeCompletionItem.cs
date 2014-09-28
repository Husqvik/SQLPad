using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleCodeCompletionItem (Name={Name}; Category={Category}; Priority={Priority})")]
	public class OracleCodeCompletionItem : ICodeCompletionItem
	{
		public string Category { get; set; }
		
		public string Name { get; set; }
		
		public StatementGrammarNode StatementNode { get; set; }

		public int Priority { get; set; }

		public int CategoryPriority { get; set; }
		
		public int InsertOffset { get; set; }
		
		public int CaretOffset { get; set; }

		public string Text { get; set; }
	}

	public static class OracleCodeCompletionCategory
	{
		public const string DatabaseSchema = "Database Schema";
		public const string SchemaObject = "Schema Object";
		public const string InlineView = "Inline View";
		public const string CommonTableExpression = "Common Table Expression";
		public const string Column = "Column";
		public const string PseudoColumn = "Pseudo column";
		public const string AllColumns = "All Columns";
		public const string JoinMethod = "Join Method";
		public const string PackageFunction = "Package function";
		public const string SchemaFunction = "Schema function";
		public const string Package = "Package";
		public const string DatabaseLink = "Database link";
		public const string FunctionParameter = "Function parameter";
		public const string BuiltInFunction = "Built-in function";
	}
}