using System.Collections.Generic;
using System.Configuration;

namespace SqlPad
{
	public interface IDatabaseModel
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; }

		ICollection<string> Schemas { get; }

		IDictionary<OracleObjectIdentifier, IDatabaseObject> Objects { get; }

		IDictionary<OracleObjectIdentifier, IDatabaseObject> AllObjects { get; }

		void Refresh();
	}

	public interface IDatabaseObject
	{
		string Name { get; }

		string Type { get; }

		string Owner { get; }

		ICollection<IDatabaseObjectProperty> Properties { get; }
		ICollection<IColumn> Columns { get; }
	}

	public interface IColumn
	{
		string Name { get; }

		string Type { get; }
	}

	public interface IDatabaseObjectProperty
	{
		string Name { get; }

		object Value { get; }
	}
}
