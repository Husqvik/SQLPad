using System;
using SqlPad.Commands;

namespace SqlPad.Oracle.Test.Commands
{
	internal class TestCommandSettings : ICommandSettingsProvider
	{
		private readonly bool _isValueValid;

		public TestCommandSettings(CommandSettingsModel settingsModel, bool isValueValid = true)
		{
			Settings = settingsModel;
			_isValueValid = isValueValid;
		}

		public EventHandler GetSettingsCalled;

		public bool GetSettings()
		{
			GetSettingsCalled?.Invoke(this, EventArgs.Empty);

			return _isValueValid;
		}

		public CommandSettingsModel Settings { get; }
	}
}