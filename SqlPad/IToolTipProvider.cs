using System.Windows.Controls;

namespace SqlPad
{
	public interface IToolTipProvider
	{
		IToolTip GetToolTip(SqlDocumentRepository sqlDocumentRepository, int cursorPosition);
	}

	public interface IToolTip
	{
		UserControl Control { get; }
	}
}