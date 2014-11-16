using System;
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

		protected bool Equals(OracleCodeCompletionItem other)
		{
			return String.Equals(Category, other.Category) &&
			       String.Equals(Name, other.Name) &&
			       Equals(StatementNode, other.StatementNode) &&
			       Priority == other.Priority &&
			       CategoryPriority == other.CategoryPriority &&
				   InsertOffset == other.InsertOffset &&
				   CaretOffset == other.CaretOffset &&
			       String.Equals(Text, other.Text);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}

			if (ReferenceEquals(this, obj))
			{
				return true;
			}
			
			return obj.GetType() == GetType() && Equals((OracleCodeCompletionItem)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = (Category != null ? Category.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (StatementNode != null ? StatementNode.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ Priority;
				hashCode = (hashCode * 397) ^ CategoryPriority;
				hashCode = (hashCode * 397) ^ InsertOffset;
				hashCode = (hashCode * 397) ^ CaretOffset;
				hashCode = (hashCode * 397) ^ (Text != null ? Text.GetHashCode() : 0);
				return hashCode;
			}
		}
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
		public const string XmlTable = "XML Table";
		public const string JsonTable = "JSON table";
		public const string TableCollection = "Table collection";
	}
}