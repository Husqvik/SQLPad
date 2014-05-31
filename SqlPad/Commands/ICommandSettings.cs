namespace SqlPad.Commands
{
	public interface ICommandSettingsProvider
	{
		bool GetSettings();

		CommandSettingsModel Settings { get; }
	}
}