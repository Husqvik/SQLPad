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

			TypeReferences = new List<OracleTypeReference>();
			ColumnReferences = new List<OracleColumnReference>();
			ProgramReferences = new List<OracleProgramReference>();
			SequenceReferences = new List<OracleSequenceReference>();
			ObjectReferences = new List<OracleDataObjectReference>();
			DataTypeReferences = new List<OracleDataTypeReference>();
		}

		public OracleStatementSemanticModel SemanticModel { get; private set; }

		public ICollection<OracleTypeReference> TypeReferences { get; private set; }

		public ICollection<OracleSequenceReference> SequenceReferences { get; private set; }

		public IList<OracleColumnReference> ColumnReferences { get; private set; }

		public ICollection<OracleProgramReference> ProgramReferences { get; private set; }

		public ICollection<OracleDataObjectReference> ObjectReferences { get; private set; }

		public ICollection<OracleDataTypeReference> DataTypeReferences { get; private set; }

		public IEnumerable<OracleReference> AllReferences
		{
			get
			{
				return ((IEnumerable<OracleReference>) TypeReferences)
					.Concat(SequenceReferences)
					.Concat(ColumnReferences)
					.Concat(ProgramReferences)
					.Concat(DataTypeReferences)
					.Concat(ObjectReferences)
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