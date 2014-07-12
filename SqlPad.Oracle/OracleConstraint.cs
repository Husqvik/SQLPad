using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	public abstract class OracleConstraint : OracleObject
	{
		public abstract ConstraintType Type { get; }

		public OracleSchemaObject Owner { get; set; }

		public IList<string> Columns { get; set; }

		public bool IsEnabled { get; set; }

		public bool IsDeferrable { get; set; }

		public bool IsValidated { get; set; }

		public bool IsRelied { get; set; }
	}

	[DebuggerDisplay("OraclePrimaryKeyConstraint (Name={FullyQualifiedObjectName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OraclePrimaryKeyConstraint : OracleUniqueConstraint
	{
		public override ConstraintType Type { get { return ConstraintType.PrimaryKey; } }
	}

	[DebuggerDisplay("OracleUniqueConstraint (Name={FullyQualifiedObjectName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OracleUniqueConstraint : OracleConstraint
	{
		public override ConstraintType Type { get { return ConstraintType.Unique; } }
	}

	[DebuggerDisplay("OracleCheckConstraint (Name={FullyQualifiedObjectName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OracleCheckConstraint : OracleConstraint
	{
		public override ConstraintType Type { get { return ConstraintType.Check; } }
	}

	[DebuggerDisplay("OracleForeignKeyConstraint (Name={FullyQualifiedObjectName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OracleForeignKeyConstraint : OracleConstraint
	{
		public OracleSchemaObject TargetObject { get; set; }

		public OracleUniqueConstraint ReferenceConstraint { get; set; }

		public DeleteRule DeleteRule { get; set; }

		public override ConstraintType Type { get { return ConstraintType.ForeignKey; } }
	}

	public enum ConstraintType
	{
		PrimaryKey,
		Unique,
		Check,
		ForeignKey,
	}

	public enum DeleteRule
	{
		None,
		SetNull,
		Cascade
	}
}
