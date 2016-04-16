using SqlPad.Commands;

namespace SqlPad.Oracle
{
	public class OracleCommandSettingsProviderFactory : ICommandSettingsProviderFactory
	{
		public ICommandSettingsProvider CreateCommandSettingsProvider(CommandSettingsModel settings)
		{
			return new EditDialog(settings);
		}
	}
}