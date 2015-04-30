using SqlPad.Commands;

namespace SqlPad
{
	public interface IHelpProvider
	{
		void ShowHelp(CommandExecutionContext executionContext);
	}
}