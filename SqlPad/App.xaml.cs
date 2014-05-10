using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace SqlPad
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
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
