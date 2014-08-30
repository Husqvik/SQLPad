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

		public static void Initialize(ResourceDictionary resources)
		{
			WaveErrorUnderline = new TextDecorationCollection { (TextDecoration)resources["WaveErrorUnderline"] };
			WaveWarningUnderline = new TextDecorationCollection { (TextDecoration)resources["WaveWarningUnderline"] };
			BoxedText = new TextDecorationCollection { (TextDecoration)resources["BoxedText"] };
			OutlineBoxBrush = (VisualBrush)resources["OutlineBoxBrush"];
		}
	}
}