using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace SqlPad
{
	public class PageModel : ModelBase
	{
		private readonly IDatabaseModel _databaseModel;
		private readonly Action _reParseAction;
		private readonly ObservableCollection<object[]> _resultRowItems = new ObservableCollection<object[]>();
		private int _currentLine;
		private int _currentColumn;
		private int? _selectionLength;
		private Visibility _selectionTextVisibility = Visibility.Collapsed;

		public PageModel(IDatabaseModel databaseModel, Action reParseAction)
		{
			_reParseAction = reParseAction;
			_databaseModel = databaseModel;
		}

		public int CurrentLine
		{
			get { return _currentLine; }
			set
			{
				if (_currentLine == value)
					return;

				_currentLine = value;
				RaisePropertyChanged();
			}
		}

		public int CurrentColumn
		{
			get { return _currentColumn; }
			set
			{
				if (_currentColumn == value)
					return;

				_currentColumn = value;
				RaisePropertyChanged();
			}
		}

		public int? SelectionLength
		{
			get { return _selectionLength; }
			set
			{
				if (_selectionLength == value)
					return;

				_selectionLength = value;
				RaisePropertyChanged();

				SelectionTextVisibility = _selectionLength == null ? Visibility.Collapsed : Visibility.Visible;
			}
		}

		public Visibility SelectionTextVisibility
		{
			get { return _selectionTextVisibility; }
			set
			{
				if (_selectionTextVisibility == value)
					return;

				_selectionTextVisibility = value;
				RaisePropertyChanged();
			}
		}

		public ObservableCollection<object[]> ResultRowItems { get { return _resultRowItems; } }

		public string CurrentSchema
		{
			get { return _databaseModel.CurrentSchema; }
			set
			{
				_databaseModel.CurrentSchema = value;
				_reParseAction();
			}
		}
	}
}