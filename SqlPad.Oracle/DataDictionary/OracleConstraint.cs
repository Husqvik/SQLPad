using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle.DataDictionary
{
	public abstract class OracleConstraint : OracleObject
	{
		public abstract ConstraintType Type { get; }

		public OracleSchemaObject OwnerObject { get; set; }

		public IList<string> Columns { get; set; }

		public bool IsEnabled { get; set; }

		public bool IsDeferrable { get; set; }

		public bool IsValidated { get; set; }

		public bool IsRelied { get; set; }
	}

	[DebuggerDisplay("OraclePrimaryKeyConstraint (Name={FullyQualifiedName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OraclePrimaryKeyConstraint : OracleUniqueConstraint
	{
		public override ConstraintType Type => ConstraintType.PrimaryKey;
	}

	[DebuggerDisplay("OracleUniqueConstraint (Name={FullyQualifiedName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OracleUniqueConstraint : OracleConstraint
	{
		public override ConstraintType Type => ConstraintType.Unique;
	}

	[DebuggerDisplay("OracleCheckConstraint (Name={FullyQualifiedName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OracleCheckConstraint : OracleConstraint
	{
		public override ConstraintType Type => ConstraintType.Check;
	}

	[DebuggerDisplay("OracleForeignKeyConstraint (Name={FullyQualifiedName.Name}; IsEnabled={IsEnabled}; IsDeferrable={IsDeferrable}; IsValidated={IsValidated}; IsRelied={IsRelied})")]
	public class OracleReferenceConstraint : OracleConstraint
	{
		public OracleSchemaObject TargetObject { get; set; }

		public OracleUniqueConstraint ReferenceConstraint { get; set; }

		public DeleteRule DeleteRule { get; set; }

		public override ConstraintType Type => ConstraintType.ForeignKey;
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
