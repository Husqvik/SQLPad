using System.Windows;

namespace SqlPad
{
	public class EditorInfoModel : ModelBase
	{
		private int _currentLine;
		private int _currentColumn;
		private int? _selectionLength;

		public int CurrentLine
		{
			get { return _currentLine; }
			set { UpdateValueAndRaisePropertyChanged(ref _currentLine, value); }
		}

		public int CurrentColumn
		{
			get { return _currentColumn; }
			set { UpdateValueAndRaisePropertyChanged(ref _currentColumn, value); }
		}

		public Visibility SelectionTextVisibility
		{
			get { return _selectionLength == null ? Visibility.Collapsed : Visibility.Visible; }
		}

		public int? SelectionLength
		{
			get { return _selectionLength; }
			set
			{
				if (UpdateValueAndRaisePropertyChanged(ref _selectionLength, value))
				{
					RaisePropertyChanged("SelectionTextVisibility");
				}
			}
		}
	}
}
