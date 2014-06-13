using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	public abstract class OracleConstraint : OracleObject
	{
		public abstract ConstraintType Type { get; }

		public bool IsEnabled { get; set; }

		public bool IsDeferrable { get; set; }

		public bool IsValidated { get; set; }

		public bool IsRelied { get; set; }
	}

	public class OraclePrimaryKeyConstraint : OracleUniqueConstraint
	{
		public override ConstraintType Type { get { return ConstraintType.PrimaryKey; } }
	}

	public class OracleUniqueConstraint : OracleConstraint
	{
		public override ConstraintType Type { get { return ConstraintType.Unique; } }
	}

	public class OracleCheckConstraint : OracleConstraint
	{
		public override ConstraintType Type { get { return ConstraintType.Check; } }
	}

	[DebuggerDisplay("OracleForeignKeyConstraint (Name={FullyQualifiedName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OracleForeignKeyConstraint : OracleConstraint
	{
		public OracleSchemaObject TargetObject { get; set; }

		public OracleSchemaObject SourceObject { get; set; }

		public IList<string> SourceColumns { get; set; }

		public IList<string> TargetColumns { get; set; }

		public CascadeAction CascadeAction { get; set; }

		public override ConstraintType Type { get { return ConstraintType.ForeignKey; } }
	}

	public enum ConstraintType
	{
		PrimaryKey,
		Unique,
		Check,
		ForeignKey,
	}

	public enum CascadeAction
	{
		None,
		SetNull,
		Delete
	}
}
