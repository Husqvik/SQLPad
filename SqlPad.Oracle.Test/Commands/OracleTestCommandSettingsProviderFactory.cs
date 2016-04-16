using SqlPad.Commands;

namespace SqlPad.Oracle.Test.Commands
{
	internal class OracleTestCommandSettingsProviderFactory : ICommandSettingsProviderFactory
	{
		public ICommandSettingsProvider CreateCommandSettingsProvider(CommandSettingsModel settings)
		{
			return new TestCommandSettings(settings);
		}
	}
}