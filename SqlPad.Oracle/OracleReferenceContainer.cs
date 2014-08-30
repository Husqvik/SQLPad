using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	public abstract class OracleReferenceContainer
	{
		protected OracleReferenceContainer()
		{
			TypeReferences = new List<OracleTypeReference>();
			ColumnReferences = new List<OracleColumnReference>();
			ProgramReferences = new List<OracleProgramReference>();
			SequenceReferences = new List<OracleSequenceReference>();
		}

		public ICollection<OracleTypeReference> TypeReferences { get; private set; }

		public ICollection<OracleSequenceReference> SequenceReferences { get; private set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }

		public ICollection<OracleProgramReference> ProgramReferences { get; private set; }

	}

	[DebuggerDisplay("OracleMainObjectReferenceContainer (MainObjectReference={MainObjectReference}; Columns={Columns.Count})")]
	public class OracleMainObjectReferenceContainer : OracleReferenceContainer
	{
		public OracleDataObjectReference MainObjectReference { get; set; }
	}
}