using System.Collections.Generic;

namespace SqlPad
{
	public interface IValidationModel
	{
		IDictionary<StatementDescriptionNode, bool> NodeValidity { get; }
	}
}