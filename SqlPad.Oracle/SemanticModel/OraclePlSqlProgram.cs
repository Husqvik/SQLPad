using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqlPad.Oracle.DataDictionary;

namespace SqlPad.Oracle.SemanticModel
{
	[DebuggerDisplay("OraclePlSqlProgram (Name={Name}; Type={Type}; ObjectOwner={ObjectIdentifier.Owner}; ObjectName={ObjectIdentifier.Name})")]
	public class OraclePlSqlProgram : OracleReferenceContainer
	{
		public OraclePlSqlProgram(string normalizedName, ProgramType programType, PlSqlProgramType plSqlProgramType, OraclePlSqlStatementSemanticModel semanticModel) : base(semanticModel)
		{
			Name = normalizedName;
			Type = plSqlProgramType;
			Metadata = new OracleProgramMetadata(programType, OracleProgramIdentifier.CreateFromValues(null, null, normalizedName), false, false, false, false, false, false, null, null, AuthId.Definer, OracleProgramMetadata.DisplayTypeNormal, false);
		}

		public PlSqlProgramType Type { get; }

		public StatementGrammarNode RootNode { get; set; }

		public OracleObjectIdentifier ObjectIdentifier { get; set; }

		public string Name { get; }

		public OracleProgramMetadata Metadata { get; }

		public IList<OracleStatementSemanticModel> SqlModels { get; } = new List<OracleStatementSemanticModel>();

		public IList<OraclePlSqlProgram> SubPrograms { get; } = new List<OraclePlSqlProgram>();

		public OraclePlSqlProgram Owner { get; set; }

		public IList<OraclePlSqlParameter> Parameters { get; } = new List<OraclePlSqlParameter>();

		public IList<OraclePlSqlType> Types { get; } = new List<OraclePlSqlType>();

		public IList<OraclePlSqlVariable> Variables { get; } = new List<OraclePlSqlVariable>();

		public IList<OraclePlSqlException> Exceptions { get; } = new List<OraclePlSqlException>();

		public IList<OraclePlSqlLabel> Labels { get; } = new List<OraclePlSqlLabel>();

		public OraclePlSqlParameter ReturnParameter { get; set; }

		public IEnumerable<OraclePlSqlVariable> AccessibleVariables
		{
			get
			{
				var variables = (IEnumerable<OraclePlSqlVariable>)Variables;
				if (Owner != null)
				{
					variables = variables.Concat(Owner.AccessibleVariables);
				}

				return variables;
			}
		}

		public IEnumerable<OraclePlSqlProgram> AllSubPrograms
		{
			get
			{
				foreach (var program in SubPrograms)
				{
					yield return program;

					if (program.SubPrograms.Count == 0)
					{
						continue;
					}

					foreach (var subProgram in program.AllSubPrograms)
					{
						yield return subProgram;
					}
				}
			}
		}

		public OraclePlSqlProgram Master
		{
			get
			{
				var program = this;
				while (program.Owner != null)
				{
					program = program.Owner;
				}

				return program;
			}
		}
	}

	public enum PlSqlProgramType
	{
		PlSqlBlock,
		StandaloneProgram,
		PackageProgram,
		NestedProgram
	}
}
