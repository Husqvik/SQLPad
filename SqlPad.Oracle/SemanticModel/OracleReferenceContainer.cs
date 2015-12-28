using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SqlPad.Oracle.SemanticModel
{
	public class OracleReferenceContainer
	{
		public OracleReferenceContainer(OracleStatementSemanticModel semanticModel)
		{
			if (semanticModel == null)
			{
				throw new ArgumentNullException(nameof(semanticModel));
			}

			SemanticModel = semanticModel;
		}

		public OracleStatementSemanticModel SemanticModel { get; private set; }

		public ICollection<OracleTypeReference> TypeReferences { get; } = new List<OracleTypeReference>();

		public ICollection<OracleSequenceReference> SequenceReferences { get; } = new List<OracleSequenceReference>();

		public IList<OracleColumnReference> ColumnReferences { get; } = new List<OracleColumnReference>();

		public ICollection<OracleProgramReference> ProgramReferences { get; } = new List<OracleProgramReference>();

		public ICollection<OracleDataObjectReference> ObjectReferences { get; } = new List<OracleDataObjectReference>();

		public ICollection<OracleDataTypeReference> DataTypeReferences { get; } = new List<OracleDataTypeReference>();

		public ICollection<OraclePlSqlVariableReference> PlSqlVariableReferences { get; } = new List<OraclePlSqlVariableReference>();

		public IEnumerable<OracleReference> AllReferences
		{
			get
			{
				return ((IEnumerable<OracleReference>)TypeReferences)
					.Concat(SequenceReferences)
					.Concat(ColumnReferences)
					.Concat(ProgramReferences)
					.Concat(DataTypeReferences)
					.Concat(ObjectReferences)
					.Concat(PlSqlVariableReferences)
					.Concat(ObjectReferences.Where(r => r.PartitionReference != null).Select(r => r.PartitionReference));
			}
		}
	}

	[DebuggerDisplay("OracleMainObjectReferenceContainer (MainObjectReference={MainObjectReference})")]
	public class OracleMainObjectReferenceContainer : OracleReferenceContainer
	{
		private OracleDataObjectReference _mainObjectReference;


		public OracleMainObjectReferenceContainer(OracleStatementSemanticModel semanticModel)
			: base(semanticModel)
		{
		}

		public OracleDataObjectReference MainObjectReference
		{
			get { return _mainObjectReference; }
			set
			{
				if (_mainObjectReference != null)
				{
					throw new InvalidOperationException("MainObjectReference has been already set. ");
				}

				_mainObjectReference = value;
				ObjectReferences.Add(_mainObjectReference);
			}
		}
	}
}