namespace SqlPad.Commands
{
	public interface ICommandSettingsProvider
	{
		bool GetSettings();

		CommandSettingsModel Settings { get; }
	}

	public interface ICommandSettingsProviderFactory
	{
		ICommandSettingsProvider CreateCommandSettingsProvider(CommandSettingsModel settings);
	}
}