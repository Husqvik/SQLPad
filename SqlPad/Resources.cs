using System.Windows;
using System.Windows.Media;

namespace SqlPad
{
	public class Resources
	{
		public static TextDecorationCollection WaveErrorUnderline { get; private set; }
		
		public static TextDecorationCollection WaveWarningUnderline { get; private set; }
		
		static Resources()
		{
			WaveErrorUnderline = new TextDecorationCollection { (TextDecoration)Application.Current.Resources["WaveErrorUnderline"] };
			WaveWarningUnderline = new TextDecorationCollection { (TextDecoration)Application.Current.Resources["WaveWarningUnderline"] };
		}
	}
}