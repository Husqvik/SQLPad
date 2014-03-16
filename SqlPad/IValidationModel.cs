using System.Collections.Generic;

namespace SqlPad
{
	public interface IValidationModel
	{
		IDictionary<StatementDescriptionNode, bool> TableNodeValidity { get; }

		IDictionary<StatementDescriptionNode, IColumnValidationData> ColumnNodeValidity { get; }
	}

	public interface IColumnValidationData
	{
		StatementDescriptionNode ColumnNode { get; set; }
		bool IsValid { get; set; }
		ICollection<string> TableNames { get; }
	}
}