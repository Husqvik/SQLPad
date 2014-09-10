using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	public abstract class OracleReferenceContainer
	{
		protected OracleReferenceContainer(OracleStatementSemanticModel semanticModel)
		{
			if (semanticModel == null)
			{
				throw new ArgumentNullException("semanticModel");
			}

			SemanticModel = semanticModel;

			TypeReferences = new List<OracleTypeReference>();
			ColumnReferences = new List<OracleColumnReference>();
			ProgramReferences = new List<OracleProgramReference>();
			SequenceReferences = new List<OracleSequenceReference>();
			ObjectReferences = new List<OracleDataObjectReference>();
		}

		public OracleStatementSemanticModel SemanticModel { get; private set; }

		public ICollection<OracleTypeReference> TypeReferences { get; private set; }

		public ICollection<OracleSequenceReference> SequenceReferences { get; private set; }

		public ICollection<OracleColumnReference> ColumnReferences { get; private set; }

		public ICollection<OracleProgramReference> ProgramReferences { get; private set; }

		public ICollection<OracleDataObjectReference> ObjectReferences { get; private set; }
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