using System.Collections.Generic;
using System.Configuration;

namespace SqlPad
{
	public interface IDatabaseModel
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; }

		ICollection<string> Schemas { get; }

		IDictionary<IObjectIdentifier, IDatabaseObject> Objects { get; }

		IDictionary<IObjectIdentifier, IDatabaseObject> AllObjects { get; }

		void Refresh();
	}

	public interface IDatabaseObject
	{
		string Name { get; }

		string Type { get; }

		string Owner { get; }

		ICollection<IDatabaseObjectProperty> Properties { get; }
		
		IEnumerable<IColumn> Columns { get; }
	}

	public interface IColumn
	{
		string Name { get; }

		string FullTypeName { get; }
	}

	public interface IDatabaseObjectProperty
	{
		string Name { get; }

		object Value { get; }
	}
}
