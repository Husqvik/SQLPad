using System;
using System.IO;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App
	{
		public static readonly string FolderNameCommonData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SQL Pad");
		public static readonly string FolderNameApplication = new Uri(Path.GetDirectoryName(typeof(App).Assembly.CodeBase)).LocalPath;

		public App()
		{
			if (!Directory.Exists(FolderNameCommonData))
			{
				Directory.CreateDirectory(FolderNameCommonData);
			}
		}
	}
}
