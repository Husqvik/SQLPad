using System;
using System.Diagnostics;

namespace SqlPad.Oracle
{
	[DebuggerDisplay("OracleDatabaseLink (Name={FullyQualifiedName.Name}; Host={Host}; UserName={UserName}; Created={Created})")]
	public class OracleDatabaseLink : OracleObject
	{
		public string UserName { get; set; }
		
		public string Host { get; set; }
		
		public DateTime Created { get; set; }
	}
}