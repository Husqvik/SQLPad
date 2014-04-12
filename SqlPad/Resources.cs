using System.Windows;

namespace SqlPad
{
	public class Resources
	{
		public static TextDecorationCollection WaveErrorUnderline { get; private set; }
		public static TextDecorationCollection BoxedText { get; private set; }

		public static void Initialize(ResourceDictionary resources)
		{
			WaveErrorUnderline = new TextDecorationCollection {(TextDecoration)resources["WaveErrorUnderline"]};
			BoxedText = new TextDecorationCollection { (TextDecoration)resources["BoxedText"] };
		}
	}
}