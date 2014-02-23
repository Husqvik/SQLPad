using System.Collections.Generic;
using System.Configuration;

namespace SqlPad
{
	public interface IDatabaseModel
	{
		ConnectionStringSettings ConnectionString { get; }

		string CurrentSchema { get; }

		ICollection<string> Schemas { get; }

		ICollection<IDatabaseObject> Objects { get; }

		ICollection<IDatabaseObject> AllObjects { get; }

		void Refresh();
	}

	public interface IDatabaseObject
	{
		string Name { get; }

		string Type { get; }

		string Owner { get; }

		ICollection<IDatabaseObjectProperty> Properties { get; }
	}

	public interface IDatabaseObjectProperty
	{
		string Name { get; }

		object Value { get; }
	}
}
