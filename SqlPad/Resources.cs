using System.Windows;
using System.Windows.Media;

namespace SqlPad
{
	public class Resources
	{
		public static TextDecorationCollection WaveErrorUnderline { get; private set; }
		
		public static TextDecorationCollection WaveWarningUnderline { get; private set; }
		
		public static TextDecorationCollection BoxedText { get; private set; }
		
		public static VisualBrush OutlineBoxBrush { get; private set; }

		static Resources()
		{
			WaveErrorUnderline = new TextDecorationCollection { (TextDecoration)Application.Current.Resources["WaveErrorUnderline"] };
			WaveWarningUnderline = new TextDecorationCollection { (TextDecoration)Application.Current.Resources["WaveWarningUnderline"] };
			BoxedText = new TextDecorationCollection { (TextDecoration)Application.Current.Resources["BoxedText"] };
			OutlineBoxBrush = (VisualBrush)Application.Current.Resources["OutlineBoxBrush"];
		}
	}
}