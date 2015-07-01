using System.Collections.Generic;

namespace SqlPad.Commands
{
	public interface ICommandFactory
	{
		ICollection<CommandExecutionHandler> CommandHandlers { get; }
	}
}
