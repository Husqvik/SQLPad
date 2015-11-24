namespace SqlPad.Oracle.DebugTrace
{
	public class OracleTransientKernelProfile
	{
		public static readonly OracleTransientKernelProfileSortOption[] AllSortOption =
		{
			new OracleTransientKernelProfileSortOption("PRSCNT", "Number of times parsed"),
			new OracleTransientKernelProfileSortOption("PRSCPU", "CPU time spent parsing"),
			new OracleTransientKernelProfileSortOption("PRSELA", "Elapsed time spent parsing"),
			new OracleTransientKernelProfileSortOption("PRSDSK", "Number of physical reads from disk during parse"),
			new OracleTransientKernelProfileSortOption("PRSQRY", "Number of consistent mode block reads during parse"),
			new OracleTransientKernelProfileSortOption("PRSCU", "Number of current mode block reads during parse"),
			new OracleTransientKernelProfileSortOption("PRSMIS", "Number of library cache misses during parse"),
			new OracleTransientKernelProfileSortOption("EXECNT", "Number of executions"),
			new OracleTransientKernelProfileSortOption("EXECPU", "CPU time spent executing"),
			new OracleTransientKernelProfileSortOption("EXEELA", "Elapsed time spent executing"),
			new OracleTransientKernelProfileSortOption("EXEDSK", "Number of physical reads from disk during execute"),
			new OracleTransientKernelProfileSortOption("EXEQRY", "Number of consistent mode block reads during execute"),
			new OracleTransientKernelProfileSortOption("EXECU", "Number of current mode block reads during execute"),
			new OracleTransientKernelProfileSortOption("EXEROW", "Number of rows processed during execute"),
			new OracleTransientKernelProfileSortOption("EXEMIS", "Number of library cache misses during execute"),
			new OracleTransientKernelProfileSortOption("FCHCNT", "Number of fetches"),
			new OracleTransientKernelProfileSortOption("FCHCPU", "CPU time spent fetching"),
			new OracleTransientKernelProfileSortOption("FCHELA", "Elapsed time spent fetching"),
			new OracleTransientKernelProfileSortOption("FCHDSK", "Number of physical reads from disk during fetch"),
			new OracleTransientKernelProfileSortOption("FCHQRY", "Number of consistent mode block reads during fetch"),
			new OracleTransientKernelProfileSortOption("FCHCU", "Number of current mode block reads during fetch"),
			new OracleTransientKernelProfileSortOption("FCHROW", "Number of rows fetched"),
			new OracleTransientKernelProfileSortOption("USERID", "Userid of user that parsed the cursor")
		};
	}

	public class OracleTransientKernelProfileSortOption
	{
		public string ParameterValue { get; }

		public string Description { get; }
		
		public OracleTransientKernelProfileSortOption(string parameterValue, string description)
		{
			ParameterValue = parameterValue;
			Description = description;
		}
	}
}