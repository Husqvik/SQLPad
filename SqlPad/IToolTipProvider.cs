using System.Windows.Controls;

namespace SqlPad
{
	public interface IToolTipProvider
	{
		IToolTip GetToolTip(SqlDocumentStore sqlDocumentStore, int cursorPosition);
	}

	public interface IToolTip
	{
		UserControl Control { get; }
	}
}