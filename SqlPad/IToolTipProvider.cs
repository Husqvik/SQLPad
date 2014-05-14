using System.Windows.Controls;

namespace SqlPad
{
	public interface IToolTipProvider
	{
		IToolTip GetToolTip(IDatabaseModel databaseModel, SqlDocument sqlDocument, int cursorPosition);
	}

	public interface IToolTip
	{
		UserControl Control { get; }
	}
}