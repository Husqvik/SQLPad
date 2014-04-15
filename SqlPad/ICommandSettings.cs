namespace SqlPad
{
	public interface ICommandSettingsProvider
	{
		bool GetSettings();

		CommandSettingsModel Settings { get; }
	}
}