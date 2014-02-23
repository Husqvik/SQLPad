using System.Collections.Generic;
using System.Configuration;
using System.Linq;

namespace SqlPad
{
	public class DatabaseModelFake : IDatabaseModel
	{
		private const string CurrentSchemaInternal = "HUSQVIK";
		private static readonly ConnectionStringSettings ConnectionStringInternal = new ConnectionStringSettings("ConnectionFake", "DATA SOURCE=HQ_PDB_TCP;PASSWORD=MMA_DEV;PERSIST SECURITY INFO=True;USER ID=HUSQVIK", "Oracle.DataAccess.Client");

		private static readonly string[] SchemasInternal = { "SYS", "SYSTEM", CurrentSchemaInternal, "PUBLIC" };

		private static readonly IDatabaseObject[] AllObjectsInternal =
		{
			new OracleDatabaseObject { Name = "DUAL", Owner = "SYS", Type = "TABLE" },
			new OracleDatabaseObject { Name = "V_$SESSION", Owner = "SYS", Type = "VIEW" },
			new OracleDatabaseObject { Name = "V$SESSION", Owner = "PUBLIC", Type = "SYNONYM" },
			new OracleDatabaseObject { Name = "DUAL", Owner = "PUBLIC", Type = "SYNONYM" },
			new OracleDatabaseObject { Name = "COUNTRY", Owner = CurrentSchemaInternal, Type = "TABLE" },
			new OracleDatabaseObject { Name = "ORDERS", Owner = CurrentSchemaInternal, Type = "TABLE" },
			new OracleDatabaseObject { Name = "VIEW_INSTANTSEARCH", Owner = CurrentSchemaInternal, Type = "VIEW" },
		};

		private static readonly IDatabaseObject[] ObjectsInternal = AllObjectsInternal.Where(o => o.Owner == "PUBLIC" || o.Owner == CurrentSchemaInternal).ToArray();
		
		#region Implementation of IDatabaseModel
		public ConnectionStringSettings ConnectionString { get { return ConnectionStringInternal; } }
		
		public string CurrentSchema { get { return CurrentSchemaInternal; } }
		
		public ICollection<string> Schemas { get { return SchemasInternal; } }
		
		public ICollection<IDatabaseObject> Objects { get { return ObjectsInternal; } }
		
		public ICollection<IDatabaseObject> AllObjects { get { return AllObjectsInternal; } }
		
		public void Refresh()
		{
		}
		#endregion
	}

	public class OracleDatabaseObject : IDatabaseObject
	{
		#region Implementation of IDatabaseObject
		public string Name { get; set; }
		public string Type { get; set; }
		public string Owner { get; set; }
		public ICollection<IDatabaseObjectProperty> Properties { get {return new IDatabaseObjectProperty[0];} }
		#endregion
	}

	public class OracleColumn : IDatabaseObjectProperty
	{
		#region Implementation of IDatabaseObjectProperty
		public string Name { get; set; }
		public object Value { get; set; }
		#endregion
	}
}