using System;

namespace SqlPad.Oracle.DataDictionary
{
	internal static class OracleObjectFactory
	{
		public static OracleSchemaObject CreateSchemaObjectMetadata(string objectType, string owner, string name, bool isValid, DateTime created, DateTime lastDdl, bool isTemporary)
		{
			var schemaObject = CreateObjectMetadata(objectType);
			schemaObject.FullyQualifiedName = OracleObjectIdentifier.Create(owner, name);
			schemaObject.IsValid = isValid;
			schemaObject.Created = created;
			schemaObject.LastDdl = lastDdl;
			schemaObject.IsTemporary = isTemporary;

			return schemaObject;
		}

		public static OracleConstraint CreateConstraint(string constraintType, string owner, string name, bool isEnabled, bool isValidated, bool isDeferrable, bool isRelied)
		{
			var constraint = CreateConstraint(constraintType);
			constraint.FullyQualifiedName = OracleObjectIdentifier.Create(owner, name);
			constraint.IsEnabled = isEnabled;
			constraint.IsValidated = isValidated;
			constraint.IsDeferrable = isDeferrable;
			constraint.IsRelied = isRelied;

			return constraint;
		}

		private static OracleConstraint CreateConstraint(string constraintType)
		{
			switch (constraintType)
			{
				case "P":
					return new OraclePrimaryKeyConstraint();
				case "U":
					return new OracleUniqueConstraint();
				case "R":
					return new OracleReferenceConstraint();
				case "C":
					return new OracleCheckConstraint();
				default:
					throw new InvalidOperationException($"Constraint type '{constraintType}' not supported. ");
			}
		}

		private static OracleSchemaObject CreateObjectMetadata(string objectType)
		{
			switch (objectType)
			{
				case OracleObjectType.Table:
					return new OracleTable();
				case OracleObjectType.View:
					return new OracleView();
				case OracleObjectType.Synonym:
					return new OracleSynonym();
				case OracleObjectType.Function:
					return new OracleFunction();
				case OracleObjectType.Procedure:
					return new OracleProcedure();
				case OracleObjectType.Sequence:
					return new OracleSequence();
				case OracleObjectType.Package:
					return new OraclePackage();
				default:
					throw new InvalidOperationException($"Object type '{objectType}' not supported. ");
			}
		}
	}
}
